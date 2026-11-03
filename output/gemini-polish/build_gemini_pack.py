"""Export the player-facing text as three Markdown packs for an external polishing pass.

Reads the current dialogue assets, runtime scripts, the scene text snapshot and the
verified document anthology; never changes Unity assets. Run from the repository root:
  python -X utf8 output/gemini-polish/build_gemini_pack.py
"""
from __future__ import annotations

import importlib.util
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).parent
SCRIPTS = ROOT / "Assets/Scripts/Presentation"
PERFORMANCE = ROOT / "doc/performance-edit"
ANTHOLOGY = ROOT / "doc/紫手套—流程文书全文.md"
SCENE_TEXT = ROOT / "output/document-text/scene-text.json"

spec = importlib.util.spec_from_file_location("review_pack", ROOT / "output/performance-edit/build_review_pack.py")
review_pack = importlib.util.module_from_spec(spec)
spec.loader.exec_module(review_pack)

CJK = re.compile(r"[一-鿿]")
LITERAL = re.compile(r'\$?"((?:\\.|[^"\\])*)"')

BACKGROUND = """这是中文叙事解谜游戏《紫手套》的游戏内文本，请在不改变剧情、线索和格式的前提下润色，让文字读起来更自然、更有质感。

故事背景：借用意大利城邦名称的架空历史。六月十五日夜，印务行会的核验人卢卡被封在三方共用的核验所里，要在次日清晨六点前核验一份声称“法国将越境”的军情密文。密文用的是他师父马泰奥设计的密码，而师父正被米兰方面扣押。米兰、威尼斯、法国的代表隔着门上的观察窗与卢卡交谈，文件经门上的传物舱送进来。卢卡始终不出房间，玩家也看不到任何人物的正脸。

文风：带时代感的书面中文，克制、清楚。不用现代网络用语，不堆砌辞藻。"""

COMMON_RULES = """1. 只改正文。所有标题行（以 # 开头的行）原样保留，不增删、不合并、不调换条目。
2. 以下内容不改：
   - 人名、地名、机构名：卢卡、马泰奥·里奇（马泰奥、里奇）、贝阿特丽切、夏尔、卡拉法、巴尔托洛、孔蒂公爵、米兰、威尼斯、法国、圣马丁修道院、印务行会、米兰内廷、七瓣花版。
   - 数字、日期、时刻和编号：第五十七号封档、第17号委托、批号726（七二六）、六月十五日、六月十六日、六月十八日、五点、六点、六点半等。
   - 英文、法文、拉丁字母、密文和明文答案（如 KEEP THE ORIGINAL）。
   - 占位符：花括号 {去向后果}，尖括号 ⟨数字⟩ 一类。
   - 按键与操作名：Esc、Enter、F1、← / →、滚轮。
3. 不改变事实与线索：每句话交代的信息（谁、做了什么、什么时候、哪份文件证明什么）保持不变。不补充原文没有的信息，不提前透露后文。
4. 篇幅与原文接近，单条增减不超过约两成。
5. 以「（背景，不改）」开头的段落是说明，原样保留。
6. 按输入的 Markdown 格式输出全文，未修改的条目也要原样输出，不要省略。如果你认为某处应删句、加句或调换顺序，不要直接改，在该条正文下另起一行写「〔建议〕……」。
7. 全部条目之后，用「## 润色说明」简要列出改动较大的条目和理由。"""

CHARACTERS = {
    "卢卡": "主角，第一人称。印务行会登记的核验人，马泰奥的学生。一心想证明师父清白，逐渐学会只信能复核的证据。",
    "行会执事": "门外负责登记、报时、传话和送件的人。客气，讲规程。",
    "公爵书记官": "替米兰孔蒂公爵出面。措辞体面，暗中施压。",
    "贝阿特丽切": "威尼斯代表，奉命调查军情。精明克制，也替威尼斯打算。",
    "夏尔": "法国密使，谨慎的外交官，愿意否认假军令，但顾虑自己与米兰的接触被曝光。",
    "卡拉法": "教皇使团的财务代理，左手常戴紫手套。经手过换版，想把事情说成普通的印务纠纷。",
    "马泰奥": "卢卡的师父，被扣押在圣马丁修道院。曾受胁迫在假命令上签名。",
    "孔蒂公爵": "米兰公爵，想借假军情接管威尼斯的边境粮站。",
    "巴尔托洛": "印刷匠，保留了原始签收页，正在躲藏。",
}


def header(title: str, part_rules: str, extra: str = "") -> str:
    parts = [f"# 《紫手套》文本润色 · {title}", "## 给润色者的说明", BACKGROUND, "### 规则", COMMON_RULES,
             "### 本部分的额外要求", part_rules]
    if extra:
        parts.append(extra)
    return "\n\n".join(parts) + "\n\n---\n"


def stages() -> list[dict]:
    """Act order, headings and staging notes from the maintained per-act scripts; text comes from the assets."""
    acts = []
    for path in sorted(PERFORMANCE.glob("0[0-7]-*.md")):
        source = path.read_text(encoding="utf-8")
        title = re.search(r"^# (.+)$", source, re.M)[1]
        time = re.search(r"^时间：(.+)$", source, re.M)
        sections = []
        for block in re.split(r"^(?=## )", source, flags=re.M):
            event = re.search(r"^事件编号：`([^`]+)`", block, re.M)
            if not event:
                continue
            trigger = re.search(r"^触发：(.+)$", block, re.M)
            staging = re.search(r"^演出：(.+)$", block, re.M)
            note = "".join(f"{label}：{m[1]}" for label, m in (("触发", trigger), ("演出", staging)) if m)
            sections.append({"heading": block.splitlines()[0][3:], "note": note, "event": event[1]})
        acts.append({"title": title, "time": time[1] if time else "", "sections": sections})
    return acts


def epilogue_consequences() -> dict[str, list[tuple[str, str]]]:
    """{去向后果} variants per ending, keyed the way EpilogueFlow.FillCustody chooses them."""
    source = (SCRIPTS / "EpilogueFlow.cs").read_text(encoding="utf-8")
    block = source.split("void FillCustody", 1)[1].split("sequence.lines", 1)[0]
    q = r'"(?:\\.|[^"\\])*"'
    names = {"Milan": "原件交米兰", "Venice": "原件交威尼斯", "France": "原件交法国"}
    endorsed, transferred = [], []
    for m in re.finditer(r"case FifthActFlow\.Custody\.(Milan|Venice|France):\s*consequence = fifth\.GuaranteesSource\s*\?\s*("
                         + q + r")\s*:\s*(" + q + ")", block):
        endorsed.append((f"epilogue.{m[1]}.endorsed", names[m[1]], json.loads(m[2])))
        transferred.append((f"epilogue.{m[1]}.transferred", names[m[1]], json.loads(m[3])))
    default = re.search(r"default:\s*consequence = (" + q + ")", block)
    endorsed.append(("epilogue.Guild.endorsed", "原件继续封存", json.loads(default[1])))
    if len(transferred) != 3:
        raise ValueError("EpilogueFlow consequence branches changed; update the exporter")
    return {"act6_endorsed": endorsed, "act6_transferred": transferred}


def dialogue_pack() -> tuple[str, int]:
    events = review_pack.load_events()
    runtime = review_pack.runtime_choices()
    consequences = epilogue_consequences()
    acts = stages()
    used = {s["event"] for act in acts for s in act["sections"]}
    current = (set(events) - set(review_pack.LEGACY)) | set(runtime)
    if used != current:
        raise ValueError(f"Per-act scripts and dialogue assets disagree: {sorted(used ^ current)}")

    speakers = sorted({line["speaker"] for e in used for line in (events.get(e) or runtime[e])["lines"]},
                      key=list(CHARACTERS).index)
    cast = "\n".join(f"- {name}：{CHARACTERS[name]}" for name in speakers)
    rules = """- 台词在对话框里逐字打出，宜用短句。卢卡以外的人都在门外隔窗说话。
- 「选项」条目是玩家点击的按钮文字，保持简短，能一眼看懂。
- 人物口吻要分得开，参考下面的角色表。"""
    out = [header("第 1 部分：台词", rules, "### 角色表（背景，不改）\n\n" + cast)]
    count = 0
    for act in acts:
        out.append(f"## {act['title']}" + (f"\n\n（背景，不改）时间：{act['time']}" if act["time"] else ""))
        for section in act["sections"]:
            event = events.get(section["event"]) or runtime[section["event"]]
            out.append(f"### {section['heading']}\n\n（背景，不改）{section['note']}")
            for n, line in enumerate(event["lines"], 1):
                out.append(f"#### {event['id']}.L{n:02}｜{line['speaker']}\n\n{line['text']}")
                count += 1
                for choice in line["choices"]:
                    out.append(f"#### {event['id']}.{choice['id']}｜选项\n\n{choice['text']}")
                    count += 1
            for key, label, text in consequences.get(event["id"], []):
                out.append(f"#### {key}｜替换 {{去向后果}}（{label}）\n\n{text}")
                count += 1
    return "\n\n".join(out) + "\n", count


def document_pack() -> tuple[str, int]:
    source = ANTHOLOGY.read_text(encoding="utf-8")
    body = source.split("\n", 1)[1]
    # Hidden legacy fields are archived in the anthology but never shown to the player; the scope note is export metadata.
    body = re.sub(r"^### (场景中保留但当前隐藏的旧字段|来源与范围)\n.*?(?=^#{1,3} |\Z)", "", body, flags=re.M | re.S).rstrip()
    body = body.replace("## 附记：文书标签、隐藏旧字段与核对范围", "## 附记：文书架标签")
    rules = """- 引用块（以「>」开头的行）是纸面上的字，需要润色；引用块之外的文字（出现时机、空栏说明等）是整理说明，原样保留。
- 纸面已经排版：保持每份文书的行数不变，每行字数不超过原行。标题中字与字之间的空格（如「核 验 委 托」）是排版效果，保留。
- 表单的栏目名、签名栏、页码、编号格式保留。笔记教案和祷言集页面里的例题、字母、算式不改。
- 以下文字由程序填入，这里不改（需要改时在第 3 部分改）：「继续封存」「交米兰」「交威尼斯」「交法国」，以及选项前的 ○ ●。
- 不同文书的语气要分开：公文、私信、账簿、教案笔记各有各的写法。"""
    count = sum(1 for line in body.splitlines() if line.startswith(">") and CJK.search(line))
    return header("第 2 部分：纸面文书", rules) + "\n" + body + "\n", count


def code_entries(path: Path, lines: set[int] | None = None) -> list[tuple[str, str]]:
    """Chinese literals from a script; concatenated pieces become one template with ⟨…⟩ for code values."""
    entries = []
    for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        if lines is not None and number not in lines:
            continue
        stripped = line.strip()
        if stripped.startswith(("//", "[")) or "Debug." in line:
            continue
        matches = list(LITERAL.finditer(line))
        groups, current = [], []
        for i, m in enumerate(matches):
            current.append(m)
            following = line[m.end():matches[i + 1].start()] if i + 1 < len(matches) else ""
            if not re.fullmatch(r"\s*\+(?:[^?:,;\"]*\+)?\s*", following):
                groups.append(current)
                current = []
        texts = []
        for group in groups:
            text = ""
            for i, m in enumerate(group):
                piece = m[1].encode().decode("unicode_escape").encode("latin-1").decode("utf-8")
                if m[0].startswith("$"):
                    piece = re.sub(r"\{[^}]*\}", "⟨数字⟩", piece)
                text += piece
                if i + 1 < len(group) and line[m.end():group[i + 1].start()].strip() != "+":
                    text += "⟨…⟩"
            # Leading/trailing concatenation with code values, such as "第" + numeral + "幕".
            start, end = group[0], group[-1]
            if re.search(r"\+\s*$", line[:start.start()]) and not re.search(r"[=(]\s*\+?\s*$", line[:start.start()]):
                text = "⟨…⟩" + text
            if re.match(r"\s*\+\s*[A-Za-z(]", line[end.end():]):
                text += "⟨…⟩"
            if CJK.search(text):
                texts.append(text)
        for i, text in enumerate(texts):
            suffix = "abcdefgh"[i] if len(texts) > 1 else ""
            entries.append((f"{path.stem}:{number}{suffix}", text))
    return entries


def ui_pack() -> tuple[str, int]:
    scene = json.loads(SCENE_TEXT.read_text(encoding="utf-8"))
    labels = {}
    for item in scene["otherText"]:
        if item.get("active", True) and CJK.search(item["text"]):
            labels.setdefault(item["text"], []).append("scene:" + "/".join(item["path"].split("/")[-2:]))
    groups = [
        ("右上角「案头待办」", "随流程提示玩家下一步做什么：短的一条是任务名，完整的一句是操作说明。",
         code_entries(SCRIPTS / "DeskViewHud.Commands.cs", {46}) + code_entries(SCRIPTS / "DeskCommandHints.cs")),
        ("视角与翻书提示", "屏幕角落的操作提示。",
         code_entries(SCRIPTS / "DeskViewHud.cs", {137, 140, 147, 157, 158})),
        ("译码台字母盘上的状态提示", "刻在字母盘边上，随当前工具变化。空间很窄。",
         code_entries(SCRIPTS / "CipherWorkbenchKeyboard.cs", set(range(135, 144)))),
        ("书架卷宗的状态标签", "书架每层卷宗上的小签。",
         code_entries(SCRIPTS / "ActArchiveController.cs", {261})),
        ("原件去向标签", "第五幕转交申请表的选项，也会出现在表上的「当前安排」和第六幕的回执里。",
         code_entries(SCRIPTS / "FifthActFlow.cs", {142, 143})),
        ("转场字幕", "幕间黑场上的字。",
         code_entries(SCRIPTS / "ActTransitionOverlay.cs", {55, 57}) + code_entries(SCRIPTS / "EpilogueFlow.cs", {146, 223})),
        ("场景物件上的刻字与标签", "刻在工具、木盘和纸签上的字，空间固定。",
         [(", ".join(dict.fromkeys(keys)), text) for text, keys in labels.items()]),
    ]
    notes = {"DeskCommandHints:115": "⟨…⟩ 处填入卷宗名", "DeskCommandHints:117": "接在操作说明末尾，开头的空格保留",
             "CipherWorkbenchKeyboard:140a":"⟨…⟩/⟨…⟩ 是当前用到第几个密钥字母", "ActTransitionOverlay:57": "⟨…⟩ 处填入幕数",
             "EpilogueFlow:223": "⟨…⟩ 处填入结局名"}
    rules = """- 屏幕和物件上的空间有限，字数不能多于原文。
- 「·」「→」「/」「「」」等分隔符和括号保留。
- 一个标题下列了多个编号，表示同一句文字在多处使用，改一次即可。
- 「⟨…⟩」「⟨数字⟩」是程序填入的内容，保留在原位置。「尾声 · ⟨…⟩」处填入的结局名（开门、缺页、真相易手、背书）也被程序当作判断条件，不改。"""
    out = [header("第 3 部分：界面与物件上的文字", rules)]
    count = 0
    for title, note, entries in groups:
        out.append(f"## {title}\n\n（背景，不改）{note}")
        merged: dict[str, list[str]] = {}
        for key, text in entries:
            merged.setdefault(text, []).append(key)
        for text, keys in merged.items():
            note = next((n for k, n in notes.items() if any(re.fullmatch(re.escape(k) + "[a-h]?", key) for key in keys)), "")
            out.append(f"#### {', '.join(keys)}" + (f"（{note}）" if note else "") + f"\n\n{text}")
            count += 1
    return "\n\n".join(out) + "\n", count


def main():
    packs = [("1-台词.md", dialogue_pack()), ("2-纸面文书.md", document_pack()), ("3-界面与物件文字.md", ui_pack())]
    for name, (content, count) in packs:
        (OUT / name).write_text(content, encoding="utf-8")
        print(f"{name}: {count} entries, {len(content)} characters")


if __name__ == "__main__":
    main()
