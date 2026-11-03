"""Turn the polished Chinese and English packs into the game's text tables.

Inputs (this folder): the exported packs 1-3 (current game text), *_polished.md (revised
Chinese) and *_en.md (English). Outputs:
  zh-revision.json        old Chinese -> polished Chinese, applied to scene/prefab text in Unity
  dialogue-revision.json  polished dialogue by event, line and choice id
  Assets/Resources/PHLocalization/English.json   polished Chinese -> English, loaded at runtime
With --apply-code, Chinese literals in C# sources are revised in place.
Run from the repository root:  python -X utf8 output/gemini-polish/build_localization.py [--apply-code]
"""
from __future__ import annotations

import argparse
import importlib.util
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PACK = Path(__file__).parent
# The scene text the packs were exported from; output/document-text holds the current, polished scene.
SCENE_TEXT = json.loads((PACK / "scene-text-before-polish.json").read_text(encoding="utf-8"))
ENGLISH = ROOT / "Assets/Resources/PHLocalization/English.json"
CODE_ROOTS = ["Assets/Scripts/Presentation", "Assets/Art/DeskSceneV1/Runtime", "Assets/Art/DeskSceneV1/Editor"]
CJK = re.compile(r"[\u4e00-\u9fff]")
PREFIX = "Final disposition fields/"

spec = importlib.util.spec_from_file_location("review_pack", ROOT / "output/performance-edit/build_review_pack.py")
review_pack = importlib.util.module_from_spec(spec)
spec.loader.exec_module(review_pack)

# Act captions, ending names and the final card are composed in code from these pieces.
EXTRA_ENGLISH = {
    "第零幕": "Prologue", "第一幕": "Act I", "第二幕": "Act II", "第三幕": "Act III", "第四幕": "Act IV",
    "第五幕": "Act V", "第五幕 · 落笔与交卷": "Act V · Signing and Submission", "开门": "Open Door", "缺页": "The Missing Page", "真相易手": "Truth Changes Hands",
    "背书": "Endorsement", "终": "FIN",
}
EXTRA_ENGLISH.update({f"尾声 · {zh}": f"Epilogue · {EXTRA_ENGLISH[zh]}" for zh in ("开门", "缺页", "真相易手", "背书")})
# Scene text the packs missed: the Saint-Cyr key legend was hidden at export, and two retired
# option rows stay hidden but are translated so no Chinese remains in English mode.
EXTRA_ENGLISH.update({"当前密钥对齐上行 A": "Align current key letter with upper A",
                      "已证实  /  待核  /  存在冲突": "Verified  /  Pending  /  In Conflict",
                      "米兰  /  威尼斯  /  法国": "Milan  /  Venice  /  France"})
# One text object serves as both the ledger field label and the plate-change record title.
ENGLISH_OVERRIDES = {"换版记录": "Plate-Change Log"}


def unspace(text: str) -> str:
    """Letter-spaced capitals ("V E R I F Y") imitate the Chinese spaced titles but run several times wider
    than the Chinese they replace, so they shrink until unreadable; set them as ordinary words instead."""
    lines = []
    for line in text.split("\n"):
        # Whole-line spaced titles only; worked examples such as "Upper: H L O O L" keep their spacing.
        if re.fullmatch(r"[A-Z0-9&'· ]+", line) and re.search(r"(?:[A-Z0-9] ){2,}[A-Z0-9]", line):
            line = re.sub(r"(?<![A-Za-z])((?:[A-Z0-9&'] ){1,}[A-Z0-9&'])(?![A-Za-z])", lambda m: m[1].replace(" ", ""), line)
            line = re.sub(r" {2,}", " ", line)
        lines.append(line)
    return "\n".join(lines)


def wrap_guild_paragraph(paragraph: str) -> str:
    """Port of DeskSceneBuilder.WrapGuildParagraph: 26 characters, no punctuation at a line start."""
    lines, start = [], 0
    while start < len(paragraph):
        count = min(26, len(paragraph) - start)
        while count > 1 and start + count < len(paragraph) and paragraph[start + count] in "，。；：！？、）”’":
            count -= 1
        lines.append(paragraph[start:start + count])
        start += count
    return "\n".join(lines)


def unwrap_letter(body: str) -> list[str]:
    """Salutation followed by the letter's paragraphs, with the wrapping removed."""
    return ["".join(block.split("\n")) for block in body.split("\n\n")]


def wrap_letter(paragraphs: list[str]) -> str:
    return "\n".join(wrap_guild_paragraph(line) for line in "\n\n".join(paragraphs).split("\n"))


# ---------------------------------------------------------------- pack parsing

def pack_body(path: Path) -> str:
    text = path.read_text(encoding="utf-8")
    text = text.split("\n---\n", 1)[1] if "\n---\n" in text else text
    return re.split(r"^## (?:润色说明|Translation Notes|Localization Notes)", text, flags=re.M)[0]


def pack_entries(path: Path) -> dict[str, str]:
    """'#### id｜…' entries of parts 1 and 3; suggestion lines are not game text."""
    result = {}
    for block in re.split(r"^(?=#{2,4} )", pack_body(path), flags=re.M):
        if not block.startswith("#### "):
            continue
        head, _, rest = block.partition("\n")
        key = re.split(r"[｜|（(]", head[5:], 1)[0].strip()
        lines = [l for l in rest.strip("\n").split("\n") if not l.startswith(("〔建议〕", "[Suggestion]"))]
        result[key] = "\n".join(lines).strip("\n")
    return result


def pack_blocks(path: Path) -> list[tuple[str, str]]:
    """Blockquotes of part 2 in reading order, with their heading path."""
    result, h3, h4, current = [], "", "", None
    for line in pack_body(path).split("\n"):
        if line.startswith(("## ", "### ", "#### ")):
            if line.startswith("## "):
                h3 = h4 = ""
            elif line.startswith("### "):
                h3, h4 = line, ""
            else:
                h4 = line
            current = None
        elif line.startswith(">"):
            if current is None:
                current = []
                result.append((h3 + " | " + h4, current))
            current.append(line[2:] if line.startswith("> ") else line[1:])
        else:
            current = None
    return [(key, "\n".join(lines)) for key, lines in result]


# ------------------------------------------------- document structure (mirror)

def document_blocks() -> list[tuple[str, list[tuple[str, str]]]]:
    """Rebuild every anthology quote from scene ink, keeping which text object each part came from.

    Mirrors output/document-text/build_document_text.py; parts are ("ink", text) or ("lit", text)."""
    papers = {p["id"]: p for p in SCENE_TEXT["papers"]}
    seq: list[tuple[str, list[tuple[str, str]]]] = []

    def get(paper, path):
        return next(t["text"] for t in paper["ink"] if t["path"] == path)

    def variant(kind, condition):
        return next(v for v in SCENE_TEXT["variants"] if v["kind"] == kind and v["condition"] == condition)

    def joined(texts, sep):
        parts = []
        for i, text in enumerate(texts):
            if i:
                parts.append(("lit", sep))
            parts.append(("ink", text))
        return parts

    def quote(tag, parts):
        seq.append((tag, parts))

    def paper(pid):
        p = papers[pid]
        if pid == "act2-2":
            quote("static", joined([get(p, k) for k in ("Office", "Title", "Reference")], "\n\n"))
            for i in range(6):
                quote("inline", [("ink", get(p, f"Field {i}")), ("lit", "："), ("ink", get(p, f"Value {i}"))])
            quote("static", joined([get(p, "Custody note"), get(p, "Footer")], "\n\n"))
            return
        if pid == "act2-3":
            quote("static", [("ink", get(p, "Custody"))])
            quote("static", [("ink", get(p, "Handwritten note"))])
            return
        texts = [t["text"] for t in p["ink"] if t["text"] and not t["path"].startswith((PREFIX, "Rack tab/"))
                 and t["path"] not in {"Recipient choices", "Source options"}]
        quote("static", joined(texts, "\n\n"))

    def dynamic(v, fields):
        quote("dynamic", joined([get(v, k) for k in fields if get(v, k)], "\n"))

    for pid in ["act1-1", "act1-2", "caesar", "act1-3", "act1-4", "act2-1", "book", "act2-2", "act2-3", "pigpen",
                "act3-1", "act3-2", "act3-5", "vigenere", "act3-3"]:
        paper(pid)
    v = variant("transcript", "restored")
    quote("label", [("lit", "还原文字："), ("ink", get(v, "Restored transcript"))])
    quote("skip", [])  # Luca's spoken reminder, already part of the dialogue pack.
    quote("static", [("ink", get(v, "Sign transcript prompt"))])
    quote("skip", [])  # Transcript + signature, both covered above.
    for pid in ["act3-4", "act4-1", "act4-2", "act4-3", "affine", "rail", "act4-4", "act5-1", "act5-2", "act5-3"]:
        paper(pid)
    fields = [PREFIX + n for n in ("Refuse guarantee", "Guarantee source", "Source evidence", "Source guarantee signature")]
    for guarantee in (False, True):
        dynamic(variant("verification", f"guarantee={guarantee};revised=False"), fields)
    dynamic(variant("verification", "guarantee=False;revised=True"), [PREFIX + "Source evidence"])
    p = papers["act1-4"]
    for label, field in (("档案编号：", "Archive number"), ("档案名称：", "Archive title"), ("转交清单及页数：", "Enclosures")):
        quote("label", [("lit", label), ("ink", get(p, PREFIX + field))])
    for custody in ("Guild", "Milan", "Venice", "France"):
        v = variant("transfer", custody)
        dynamic(v, [PREFIX + "Custody " + str(i) for i in range(4)] + [PREFIX + "Disposition"])
        if get(v, PREFIX + "Application signature"):
            quote("static", [("ink", get(v, PREFIX + "Application signature"))])
    dynamic(variant("confession", "act5"), [PREFIX + "Default disposition", PREFIX + "Burn plaintext"])
    destroyed = next(t["text"] for t in SCENE_TEXT["otherText"] if t["path"].endswith("/Destroyed plaintext marker"))
    quote("static", [("ink", destroyed)])
    report = papers["report"]
    for guarantee in (False, True):
        for custody in ("Guild", "Milan", "Venice", "France"):
            body = variant("report", custody + f";guarantee={guarantee}")["body"]
            quote("dynamic", [("ink", get(report, "Title")), ("lit", "\n\n"), ("ink", body), ("lit", "\n\n"),
                              ("ink", get(report, "Folio"))])
    paper("reunion")
    for pid in ("prayer-old", "prayer-flower"):
        p = papers[pid]
        quote("static", [("ink", get(p, "Prayer title")), ("lit", "\n"), ("ink", get(p, "Prayer edition"))])
        for page in (12, 13):
            for line in range(1, 5):
                quote("static", [("ink", get(p, f"Cipher spread 12-13/Page {page} line {line}"))])
    for spread_index, spread in enumerate(papers["notebook"]["spreads"]):
        for side_index, side in enumerate(("left", "right")):
            quote("static", [("ink", spread[side])])
            if spread_index * 2 + side_index + 1 in (7, 8):
                quote("skip", [])  # Letter labels beside the pigpen chart.
    for pid in ("act1-4", "act3-3", "rail"):
        quote("static", [("ink", get(papers[pid], "Rack tab/Tab label"))])
    return seq


def split_label(line: str) -> tuple[str, str]:
    match = re.match(r"^(.*?(?:：|: |:))\s*(.*)$", line)
    if not match:
        raise ValueError(f"No label separator in {line!r}")
    return match[1], match[2]


def align(parts: list[tuple[str, str]], tag: str, new: str) -> list[str]:
    """Text of each ink part inside a revised quote whose separators are whole lines."""
    inks = [text for kind, text in parts if kind == "ink"]
    if tag == "inline":
        label, value = split_label(new)
        return [label.rstrip("：: ").strip(), value]
    if tag == "label":
        return [split_label(new)[1]]
    spans, line = [], 0
    for kind, text in parts:
        if kind == "ink":
            spans.append((line, line + text.count("\n")))
        line += text.count("\n")
    total = line + 1
    lines = new.split("\n")
    if len(lines) == total:
        return ["\n".join(lines[a:b + 1]) for a, b in spans]
    # Rewrapped prose: the longest part absorbs the difference; the rest align from either end.
    grow = max(range(len(inks)), key=lambda i: spans[i][1] - spans[i][0])
    delta = len(lines) - total
    result = []
    for i, (a, b) in enumerate(spans):
        if i < grow:
            result.append("\n".join(lines[a:b + 1]))
        elif i == grow:
            result.append("\n".join(lines[a:b + 1 + delta]))
        else:
            result.append("\n".join(lines[a + delta:b + 1 + delta]))
    return result


# ------------------------------------------------------------------ tables

class Table:
    def __init__(self, name):
        self.name, self.whole, self.lines, self.conflicts = name, {}, {}, []

    def add(self, source: str, target: str, lines: bool = False):
        store = self.lines if lines else self.whole
        if not source or not CJK.search(re.sub(r"\{[^}]*\}", "", source)):
            return
        if source in store and store[source] != target:
            self.conflicts.append((source, store[source], target))
            return
        store[source] = target

    def translate(self, text: str) -> str:
        if text in self.whole:
            return self.whole[text]
        if "\n" not in text:
            return self.lines.get(text, text)
        return "\n".join(self.whole.get(line, self.lines.get(line, line)) for line in text.split("\n"))

    def entries(self) -> list[dict]:
        merged = dict(self.lines)
        merged.update(self.whole)
        return [{"zh": k, "text": v} for k, v in sorted(merged.items())]


def template(text: str) -> str:
    count = iter(range(10))
    return re.sub(r"⟨[^⟩]*⟩", lambda _: "{" + str(next(count)) + "}", text)


def build():
    revision, english = Table("revision"), Table("english")
    dialogue: list[dict] = []
    events = review_pack.load_events()

    # Part 1: dialogue lines, choices, runtime prompts and epilogue substitutions.
    old, new, eng = (pack_entries(PACK / name) for name in ("1-台词.md", "1_polished.md", "1_en.md"))
    for key in old:
        revision.add(old[key], new[key])
        english.add(new[key], eng[key])
        event_id, _, item = key.partition(".")
        if event_id not in events:
            continue  # Runtime prompts and substitutions live in code.
        # Assets hold either the exported text (before the sync) or the polished text (after it).
        match = re.fullmatch(r"L(\d+)", item)
        if match:
            index = int(match[1]) - 1
            if events[event_id]["lines"][index]["text"] not in (old[key], new[key]):
                raise ValueError(f"Pack differs from asset: {key}")
            dialogue.append({"id": event_id, "line": index, "choice": "", "old": old[key], "text": new[key]})
        else:
            index = next((i for i, l in enumerate(events[event_id]["lines"])
                          for c in l["choices"] if c["id"] == item and c["text"] in (old[key], new[key])), None)
            if index is None:
                raise ValueError(f"Pack choice differs from asset: {key}")
            dialogue.append({"id": event_id, "line": index, "choice": item, "old": old[key], "text": new[key]})

    # Part 3: interface strings; ⟨…⟩ marks code values and becomes a format argument.
    old, new, eng = (pack_entries(PACK / name) for name in ("3-界面与物件文字.md", "3_polished.md", "3_en.md"))
    for key in old:
        if key.startswith(("ActTransitionOverlay:57", "EpilogueFlow:223")):
            continue  # Composed from EXTRA_ENGLISH pieces.
        revision.add(template(old[key]), template(new[key]))
        english.add(template(new[key]), template(eng[key]))

    # Part 2: every paper text object, plus line entries for the code-composed forms and receipts.
    blocks = document_blocks()
    sources = [pack_blocks(PACK / name) for name in ("2-纸面文书.md", "2_polished.md", "2_en.md")]
    if not len(blocks) == len(sources[0]) == len(sources[1]) == len(sources[2]):
        raise ValueError(f"Document block counts differ: {len(blocks)} {[len(s) for s in sources]}")
    for (tag, parts), (key, o), (_, p), (_, e) in zip(blocks, *sources):
        if tag == "skip":
            continue
        rebuilt = "".join(text for _, text in parts)
        if rebuilt != o:
            raise ValueError(f"Scene text does not rebuild {key}:\n{rebuilt!r}\n{o!r}")
        inks = [text for kind, text in parts if kind == "ink"]
        for zh, po, en in zip(inks, align(parts, tag, p), align(parts, tag, e)):
            revision.add(zh, po)
            english.add(po, en)
        if tag == "dynamic":
            for zh, po in zip(o.split("\n"), p.split("\n")):
                revision.add(zh, po, lines=True)
            if len(p.split("\n")) == len(e.split("\n")):
                for po, en in zip(p.split("\n"), e.split("\n")):
                    english.add(po, en, lines=True)

    # The guild letter is authored as paragraphs and wrapped by the builder; keep scene and builder in step.
    letter = next(t["text"] for p in SCENE_TEXT["papers"] if p["id"] == "act1-3" for t in p["ink"] if t["path"] == "Body")
    old_paragraphs = unwrap_letter(letter)
    if wrap_letter(old_paragraphs) != letter:
        raise ValueError("WrapGuildParagraph port does not reproduce the saved guild letter")
    gemini_wrapped = revision.whole[letter]
    new_paragraphs = unwrap_letter(gemini_wrapped)
    if len(new_paragraphs) != len(old_paragraphs):
        raise ValueError("Polished guild letter changed its paragraph count")
    rewrapped = wrap_letter(new_paragraphs)
    revision.whole[letter] = rewrapped
    english.whole[rewrapped] = english.whole.pop(gemini_wrapped)
    for i, (before, after) in enumerate(zip(old_paragraphs[1:], new_paragraphs[1:])):
        tail = "\n\n" if i < len(old_paragraphs) - 2 else ""
        revision.add(before + tail, after + tail)

    for zh, en in EXTRA_ENGLISH.items():
        english.add(zh, en)
    for store in (english.whole, english.lines):
        for key, value in store.items():
            if unspace(value) != value:
                print(f"  unspaced: {value!r} -> {unspace(value)!r}")
                store[key] = unspace(value)
    english.whole.update(ENGLISH_OVERRIDES)
    english.conflicts = [c for c in english.conflicts if c[0] not in ENGLISH_OVERRIDES]
    return revision, english, dialogue


# ------------------------------------------------------------ C# literals

LITERAL = re.compile(r'(?<![@\w])(\$?)"((?:\\.|[^"\\\n])*)"')


def decode(body: str) -> str:
    return re.sub(r'\\(u[0-9a-fA-F]{4}|.)', lambda m: chr(int(m[1][1:], 16)) if m[1][0] == "u" and len(m[1]) == 5
                  else {"n": "\n", "t": "\t", "r": "\r", "0": "\0"}.get(m[1], m[1]), body)


def encode(text: str) -> str:
    return text.replace("\\", "\\\\").replace('"', '\\"').replace("\n", "\\n").replace("\t", "\\t")


def revise_code(revision: Table, apply: bool) -> tuple[int, set[str]]:
    """Revise literals in place; texts polished two different ways are left for manual review."""
    ambiguous = {source for source, _, _ in revision.conflicts}
    changed, used = 0, set()
    for folder in CODE_ROOTS:
        for path in sorted((ROOT / folder).glob("*.cs")):
            with open(path, encoding="utf-8", newline="") as handle:  # keep the checkout's CRLF endings
                source = handle.read()

            def replace(match):
                nonlocal changed
                prefix, body = match[1], match[2]
                text = decode(body)
                if not CJK.search(text) or (prefix and "{" in text):
                    return match[0]
                if text in ambiguous:
                    print(f"  ambiguous literal kept: {path.relative_to(ROOT).as_posix()}: {text}")
                    return match[0]
                updated = revision.translate(text)
                if updated == text:
                    return match[0]
                used.add(text)
                used.update(text.split("\n"))
                changed += 1
                return prefix + '"' + encode(updated) + '"'

            revised = LITERAL.sub(replace, source)
            if apply and revised != source:
                path.write_text(revised, encoding="utf-8", newline="")
    return changed, used


RUNTIME = ROOT / "Assets/Scripts/Presentation"
# Developer-only or non-display Chinese: the F1 console, validation messages, punctuation sets and logic keys.
NOT_DISPLAYED = {"DialogueTestConsole.cs", "DialogueEvent.cs", "DialogueController.cs", "StoryMusicDirector.cs",
                 "CipherWorkbench.cs", "Localization.cs"}
# Literal pieces that are only ever shown inside a composed string checked below.
FRAGMENTS = {"零", "一", "二", "三", "四", "五", "第", "幕", "\n\n换版原件：", "。\n交接手续办结，副本留档。", "当前安排：",
             "第17号委托 · 回执六之一\n清晨六点 / 行会登记送达\n\n", "\n经更正，以当前选择为准。", "尾声 · ", "\n终",
             "不作来源担保", "担保来源可靠", "开门", "缺页", "真相易手", "背书"}


def check_display(english: Table) -> list[str]:
    """Every displayed Chinese literal, and every composed string, must resolve to text without Chinese."""
    problems = []

    def need(text, where):
        shown = english.translate(text)
        if CJK.search(re.sub(r"\{\d\}", "", shown)):
            problems.append(f"{where}: {text!r} -> {shown!r}")

    # FifthActFlow still substitutes these tokens into the handoff event, but no current line contains them.
    tokens = {"{来源}", "{自述}", "{手续}"}
    lines = [l["text"] for e in review_pack.load_events().values() for l in e["lines"]]
    if any(t in line for t in tokens for line in lines):
        raise ValueError("A dialogue line now uses a FifthActFlow handoff token; add its substitutions to the packs")
    flow = (RUNTIME / "FifthActFlow.cs").read_text(encoding="utf-8")
    block = flow.split('.Replace("{来源}"', 1)[1].split("}).ToArray()", 1)[0] if '.Replace("{来源}"' in flow else ""
    unused = {decode(m[2]) for m in LITERAL.finditer(block)}
    for path in sorted(RUNTIME.glob("*.cs")):
        if path.name in NOT_DISPLAYED:
            continue
        for match in LITERAL.finditer(path.read_text(encoding="utf-8")):
            text = decode(match[2])
            if (not CJK.search(text) or text in FRAGMENTS or text in unused or (match[1] and "{" in text)
                    or re.fullmatch(r"\{[^}]+\}", text) or text.startswith(" (")):
                continue  # Placeholder tokens and runtime object names are never shown.
            need(text, path.name)
    custody = ["继续封存", "交米兰", "交威尼斯", "交法国"]
    for mark in ("○ ", "● "):
        for label in ["不作来源担保", "担保来源可靠"] + custody:
            need(mark + label, "FifthActDocument choice")
    for label in custody:
        need("当前安排：" + label, "FifthActDocument status")
    need("换版记录：法使尚未核签。" + "\n经更正，以当前选择为准。", "FifthActDocument revised")
    epilogue = (RUNTIME / "EpilogueFlow.cs").read_text(encoding="utf-8")
    results = [decode(m) for m in re.findall(r'\? "((?:\\.|[^"\\])*\\n(?:\\.|[^"\\])*)"\s*:\s*"', epilogue)]
    results += [decode(m) for m in re.findall(r'^\s*: "((?:\\.|[^"\\])*\\n(?:\\.|[^"\\])*)";', epilogue, re.M)]
    if len(results) != 2:
        raise ValueError("EpilogueFlow.ReportText changed; update the composed receipt check")
    for result in results:
        for label in custody:
            need("第17号委托 · 回执六之一\n清晨六点 / 行会登记送达\n\n" + result + "\n\n换版原件：" + label + "。\n交接手续办结，副本留档。",
                 "EpilogueFlow receipt")
    for act in ("第零幕", "第一幕", "第二幕", "第三幕", "第四幕", "第五幕", "第五幕 · 落笔与交卷"):
        need(act, "act caption / dossier title")
        need(english.translate("到左侧书架点击「{0}」，将文书放回案头。").replace("{0}", english.translate(act)), "shelf hint")
    for ending in ("开门", "缺页", "真相易手", "背书"):
        need("尾声 · " + ending + "\n终", "final card")
    return problems


def update_performance_docs():
    """Bring the per-act scripts in doc/performance-edit up to the polished dialogue."""
    old, new = pack_entries(PACK / "1-台词.md"), pack_entries(PACK / "1_polished.md")
    found = set()
    for path in sorted((ROOT / "doc/performance-edit").glob("0[0-7]-*.md")):
        with open(path, encoding="utf-8", newline="") as handle:
            text = handle.read()
        eol = "\r\n" if "\r\n" in text else "\n"
        text = text.replace("\r\n", "\n")

        def replace(match):
            key = match["id"]
            if key not in new:
                return match[0]
            if match["text"] != old[key]:
                raise ValueError(f"{path.name}: {key} differs from the exported pack")
            found.add(key)
            return match["head"] + new[key]

        text = re.sub(r"(?P<head>\*\*[^｜\n*]+｜(?P<id>[\w.]+)\*\*\n\n)(?P<text>[^\n]+)", replace, text)
        for custody, heading in (("Milan", "### 交米兰"), ("Venice", "### 交威尼斯"), ("France", "### 交法国"), ("Guild", "### 继续封存 · 背书")):
            for label, kind in (("**真相易手**\n\n", "transferred"), ("**背书**\n\n", "endorsed")):
                key = f"epilogue.{custody}.{kind}"
                prefix = heading + "\n\n" if custody == "Guild" else label
                if key in old and prefix + old[key] in text:
                    text = text.replace(prefix + old[key], prefix + new[key], 1)
                    found.add(key)
        text = text.replace("版本：2026-11-02 返回改稿已接入后的游戏文本。", "版本：2026-11-03 润色稿已接入后的游戏文本。")
        with open(path, "w", encoding="utf-8", newline="") as handle:
            handle.write(text.replace("\n", eol))
    missing = sorted(set(old) - found)
    if missing:
        raise ValueError(f"Per-act scripts lack polished entries: {missing}")
    print(f"performance-edit: {len(found)} entries updated")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply-code", action="store_true")
    parser.add_argument("--update-docs", action="store_true")
    args = parser.parse_args()
    if args.update_docs:
        update_performance_docs()
        return
    revision, english, dialogue = build()
    problems = check_display(english)
    if problems:
        raise ValueError("Displayed text without English:\n" + "\n".join(problems))
    for table in (revision, english):
        if table.conflicts:
            print(f"{table.name}: {len(table.conflicts)} conflicting entries")
            for source, first, second in table.conflicts:
                print(f"  {source!r}\n    kept {first!r}\n    drop {second!r}")
    untranslated = [e for e in english.entries() if CJK.search(e["text"])]
    if untranslated:
        raise ValueError(f"English table still contains Chinese: {untranslated[:5]}")

    (PACK / "zh-revision.json").write_text(json.dumps({"entries": revision.entries()}, ensure_ascii=False, indent=1) + "\n",
                                           encoding="utf-8")
    (PACK / "dialogue-revision.json").write_text(json.dumps({"entries": dialogue}, ensure_ascii=False, indent=1) + "\n",
                                                 encoding="utf-8")
    ENGLISH.parent.mkdir(parents=True, exist_ok=True)
    ENGLISH.write_text(json.dumps({"entries": english.entries()}, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    changed, used = revise_code(revision, args.apply_code)
    pending = [k for k, v in {**revision.lines, **revision.whole}.items() if k != v and k not in used]
    print(f"revision: {len(revision.whole)} whole + {len(revision.lines)} line entries; "
          f"english: {len(english.whole)} whole + {len(english.lines)} line entries")
    print(f"code literals {'revised' if args.apply_code else 'to revise'}: {changed}")
    print(f"changed texts not found in code (scene, prefab or asset data): {len(pending)}")


if __name__ == "__main__":
    sys.exit(main())
