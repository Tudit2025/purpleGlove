"""Build the paper-text reading pack from a Unity scene export (Python stdlib only)."""
from __future__ import annotations

import hashlib
import importlib.util
import json
from pathlib import Path
import re
import subprocess
from zipfile import ZipFile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "output/document-text"
DATA = json.loads((OUT / "scene-text.json").read_text(encoding="utf-8"))
PAPERS = {p["id"]: p for p in DATA["papers"]}
PREFIX = "Final disposition fields/"
TITLE = "紫手套—流程文书全文"
MD = ROOT / "doc" / (TITLE + ".md")
parts: list[str] = []
included: set[str] = set()
handled: set[tuple[str, str]] = set()


def add(text=""):
    parts.append(text)


def quote(text):
    add("\n".join("> " + line if line else ">" for line in text.splitlines()))


def heading(level, title):
    add("#" * level + " " + title)


def get(paper, path):
    return next(t["text"] for t in paper["ink"] if t["path"] == path)


def take(paper, path):
    handled.add((paper["id"], path))
    return get(paper, path)


def variant(kind, condition):
    return next(v for v in DATA["variants"] if v["kind"] == kind and v["condition"] == condition)


def dynamic(v, fields):
    quote("\n".join(get(v, key) for key in fields if get(v, key)))


def paper(pid, title, timing):
    p = PAPERS[pid]
    included.add(pid)
    heading(3, title)
    add("出现时机：" + timing)
    if pid == "act2-2":
        quote("\n\n".join(take(p, key) for key in ["Office", "Title", "Reference"]))
        for i in range(6):
            quote(take(p, f"Field {i}") + "：" + take(p, f"Value {i}"))
        quote("\n\n".join(take(p, key) for key in ["Custody note", "Footer"]))
        return
    if pid == "act2-3":
        quote(take(p, "Custody"))
        add("图形密文：纸面是 9 个猪圈符号，按 4＋5 分组；字母转写为 LEFT GLOVE。此处的字母转写为整理说明，原纸不直接印出英文字母。")
        quote(take(p, "Handwritten note"))
        return
    texts = []
    for t in p["ink"]:
        if t["path"].startswith((PREFIX, "Rack tab/")) or t["path"] in {"Recipient choices", "Source options"}:
            continue
        handled.add((pid, t["path"]))
        if t["text"]:
            texts.append(t["text"])
    quote("\n\n".join(texts))
    if pid == "act1-4":
        add("初次收到时，各填写栏为空；第五幕填写内容见“最终处置文书”。")
    if pid == "act3-3":
        add("初次收到时，还原文字、明文还原签名、来源确认签名均为空；解密和签名后按下列阶段更新。")


def plaintext(pid, title, timing):
    paper(pid, title, timing)


heading(1, TITLE)
add("按当前游戏流程整理，共 31 份／本文书与参考资料：20 份原始来件、6 份解密留档、2 本祷言集、1 本师父笔记、2 份结局回执。回执六之一另列出全部 8 种文字组合。")
add("文书正文、抬头、签名、页脚及编号均取自当前保存场景；解密明文与表单、回执的动态文字取自游戏实际逻辑。正文用引用块呈现，出现时机与空栏说明为整理标注。保留纸面的文字、标点与正文内换行，独立文字块按阅读顺序排列。封档号为 57，装订批号为 726。")
add("剧情日期：六月十五日晚至六月十六日清晨。核验截止、晨会及转押均为六月十六日六点；争议军情声称的越境日期为六月十八日。后者是待核实的文件主张，不代表法方实际下令。")
add("目录：第一幕来件 → 第二幕调档 → 第三幕军情核验 → 第四幕原件与密信 → 第五幕来信及最终处置 → 第六幕回执 → 两本祷言集 → 师父笔记 → 附记。")

heading(2, "一、第一幕：委托与师父留下的纸")
paper("act1-1", "01 公爵的核验委托", "第一幕首批来件。")
paper("act1-2", "02 师父封袋里的纸", "第一幕首批来件；附凯撒密文。")
plaintext("caesar", "03 凯撒密文明文留档", "正确还原师父封袋里的密文后生成。")
paper("act1-3", "04 行会会长的信", "第一幕首批来件。")
paper("act1-4", "05 行会档案转交申请表", "第一幕收到空表，后续保留为工作表单。")

heading(2, "二、第二幕：数字便条与第五十七号封档")
paper("act2-1", "06 印坊数字便条", "第二幕收到的初始线索，按页—行—字母查阅七瓣花版祷言集。")
plaintext("book", "07 书本密文明文留档", "正确还原数字便条后生成。")
paper("act2-2", "08 印务台账摘要", "申请调阅第五十七号封档后补送。")
paper("act2-3", "09 猪圈密文碎纸", "随第五十七号封档台账摘要一并送达。")
plaintext("pigpen", "10 猪圈密文明文留档", "正确还原碎纸图形密码后生成。")

heading(2, "三、第三幕：军情译录与核验")
paper("act3-1", "11 米兰与行会的译录委托", "第三幕正式核验封包。")
paper("act3-2", "12 师父译录的军情密文", "第三幕正式核验封包。")
paper("act3-6", "13 师父的工作纸", "第三幕正式核验封包；独立纸条，纸面仅用师父笔迹写关键词。")
paper("act3-5", "14 争议军情的法文原文", "第三幕正式核验封包。")
plaintext("vigenere", "15 军情密文明文留档", "正确还原维吉尼亚密文后生成。")
paper("act3-3", "16 军情核验表", "第三幕收到，明文还原与来源确认分别签署。")
heading(4, "解密完成、尚未签名")
v = variant("transcript", "restored")
quote("还原文字：" + get(v, "Restored transcript"))
add("明文还原签名栏仍为空。通用明文留存提示读完后，卢卡先自白：")
# Read the spoken reminder from its dialogue asset so revisions of the line reach this pack.
spec = importlib.util.spec_from_file_location("review_pack", ROOT / "output/performance-edit/build_review_pack.py")
review = importlib.util.module_from_spec(spec)
spec.loader.exec_module(review)
quote("\n\n".join(line["text"] for line in review.load_events()["act3_signature_hint"]["lines"]))
add("自白读完后才开放明文还原签名。纸面操作提示：")
quote(get(v, "Sign transcript prompt"))
heading(4, "完成明文还原签名")
v = variant("transcript", "signed")
quote("还原文字：" + get(v, "Restored transcript") + "\n明文还原签名：" + get(v, "Luca transcript signature"))
add("签署提示消失。来源确认的最终选择见第五幕。")
paper("act3-4", "17 夏尔的补充凭条", "第三幕夏尔会面后补送；属于具名预备说明。")

heading(2, "四、第四幕：换版原件与师父来信")
paper("act4-1", "18 商业文件委托签收页", "第四幕送达的第五十七号封档原件。")
paper("act4-2", "19 换版记录", "第四幕与商业文件委托签收页一并送核。")
paper("act4-3", "20 师父的仿射密码纸条", "第四幕修补封套内件；横向纸条上只有一行密文，词间为两个空格。")
paper("act4-5", "20a 师父的栅栏密码纸条", "第四幕修补封套内件；独立横向纸条上只有一行密文。")
plaintext("affine", "21 地点明文留档", "正确还原仿射密码纸条后生成。")
plaintext("rail", "22 师父自述明文留档", "正确还原栅栏密码纸条后生成；第五幕可决定保留或焚毁。")
paper("act4-4", "23 师父的明文书信", "第四幕，随两张密文纸条一并缝入封套；信上明写 A、B 数值。")

heading(2, "五、第五幕：三方来信与最终处置")
for n, pid in enumerate(["act5-1", "act5-2", "act5-3"], 24):
    paper(pid, f"{n:02d} " + get(PAPERS[pid], "Title"), "第五幕各方具名来信。")

heading(3, "最终处置文书 A：军情核验表的来源确认")
add("复用第 15 份文书。明文还原栏继续保留“法国将于六月十八日越境。”和 Luca 签名；以下是来源确认部分。")
fields = [PREFIX + name for name in ["Refuse guarantee", "Guarantee source", "Source evidence", "Source guarantee signature"]]
for guarantee, title in [(False, "选择不作来源担保"), (True, "选择担保来源可靠")]:
    heading(4, title)
    dynamic(variant("verification", f"guarantee={guarantee};revised=False"), fields)
    add("来源确认签名栏" + ("写入 Luca。" if guarantee else "为空。"))
heading(4, "更改过来源确认选择时")
add("最终选项与签名仍按当前选择显示，核验依据增加更正记录：")
dynamic(variant("verification", "guarantee=False;revised=True"), [PREFIX + "Source evidence"])

heading(3, "最终处置文书 B：行会档案转交申请表")
add("复用第 05 份文书。第五幕固定填写以下内容：")
p = PAPERS["act1-4"]
for label, field in [("档案编号：", "Archive number"), ("档案名称：", "Archive title"), ("转交清单及页数：", "Enclosures")]:
    quote(label + take(p, PREFIX + field))
custodies = [("Guild", "继续封存"), ("Milan", "交米兰"), ("Venice", "交威尼斯"), ("France", "交法国")]
for custody, label in custodies:
    heading(4, label)
    dynamic(variant("transfer", custody), [PREFIX + "Custody " + str(i) for i in range(4)] + [PREFIX + "Disposition"])
    signature = get(variant("transfer", custody), PREFIX + "Application signature")
    add("申请人签名栏为空。" if not signature else "申请人签名：")
    if signature:
        quote(signature)
add("行会核准及交接登记栏没有另行生成文字；实际手续以第六幕回执记载为准。")

heading(3, "最终处置文书 C：师父自述明文留档")
add("复用第 21 份文书，正文不变。第五幕纸面下方增加：")
dynamic(variant("confession", "act5"), [PREFIX + "Default disposition", PREFIX + "Burn plaintext"])
add("若焚毁，纸面被移除，文件架位置显示：")
destroyed = next(t["text"] for t in DATA["otherText"] if t["path"].endswith("/Destroyed plaintext marker"))
quote(destroyed)

heading(2, "六、第六幕：结果回执")
heading(3, "27 行会 · 处理回执（回执六之一）")
included.add("report")
add("出现时机：第六幕清晨六点的结果通报后送入。是否担保来源 × 原件四种去向，共有下列 8 种完整正文；是否烧毁自述不改变此回执的文字。")
for guarantee in [False, True]:
    for custody, label in custodies:
        heading(4, ("担保来源可靠" if guarantee else "不作来源担保") + "／" + label)
        quote(get(PAPERS["report"], "Title") + "\n\n" + variant("report", custody + f";guarantee={guarantee}")["body"] + "\n\n" + get(PAPERS["report"], "Folio"))
handled.update(("report", t["path"]) for t in PAPERS["report"]["ink"])
paper("reunion", "28 证人接回 · 交接凭据（回执六之二）", "仅“开门”“缺页”结局送达：未作来源担保且原件继续由行会封存；清晨六点半接回师父。")

heading(2, "七、共用参考资料：两本祷言集")
add("这两本书不随各幕卷宗收起。游戏可展开的文字为第 12、13 页，每页四行；下方“第几行”为查询标注。行内字母上方的小号数字按字母顺序从 1 开始，不计空格，每行重新编号。")
for number, pid, edition in [(29, "prayer-old", "旧版"), (30, "prayer-flower", "七瓣花版")]:
    p = PAPERS[pid]
    included.add(pid)
    heading(3, f"{number} 祷言集 · {edition}")
    add("封面文字：")
    quote(take(p, "Prayer title") + "\n" + take(p, "Prayer edition"))
    for page in [12, 13]:
        heading(4, f"第 {page} 页")
        for line in range(1, 5):
            add(f"第 {line} 行：")
            quote(take(p, f"Cipher spread 12-13/Page {page} line {line}"))
        add("页码：" + take(p, f"Cipher spread 12-13/Folio {page - 12}"))

heading(2, "八、共用参考资料：马泰奥的旧笔记")
included.add("notebook")
heading(3, "31 师父笔记（全部 14 页）")
add("书中共 7 个对开。以下按实际页码列出全部正文；猪圈密码页的图形对照另作文字说明。")
for spread_index, spread in enumerate(PAPERS["notebook"]["spreads"]):
    for side_index, side in enumerate(["left", "right"]):
        page = spread_index * 2 + side_index + 1
        heading(4, f"第 {page:02d} 页")
        quote(spread[side])
        if page in [7, 8]:
            letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"[:18] if page == 7 else "ABCDEFGHIJKLMNOPQRSTUVWXYZ"[18:]
            add("图形对照旁的字母标签：")
            quote(" ".join(letters))
add("猪圈对照图说明：A—I 依次对应九宫格各格的边框片段，J—R 使用相同边框并加点；S—V 的尖角依次朝下、右、左、上，W—Z 使用相同尖角并加点。图形由场景几何绘制，以上说明不属于笔记原文。")

heading(2, "附记：文书标签、隐藏旧字段与核对范围")
heading(3, "文件架标签")
for pid in ["act1-4", "act3-3", "rail"]:
    quote(take(PAPERS[pid], "Rack tab/Tab label"))
heading(3, "场景中保留但当前隐藏的旧字段")
add("以下文字对象在当前流程中隐藏，整理时单列存档，不混入玩家实际阅读的正文：")
add("档案转交申请表的旧受领方选项：")
quote(take(PAPERS["act1-4"], "Recipient choices"))
add("军情核验表的旧来源状态选项：")
quote(take(PAPERS["act3-3"], "Source options"))
heading(3, "来源与范围")
add("以 PH_DeskRoom 当前保存场景为准，遍历五幕原始来件、解密留档、共用参考书和结局回执。表单及回执分支通过实际脚本在隔离项目中计算；未重建或改写主项目场景。第零幕没有独立可读文书；对白中提及、尚未做成可读纸面的其他材料不额外补写。密码工具刻字、卷宗状态和普通交互界面不计为文书正文。")
add("本合订本只提取现有内容，不校改原文。完整来源对象与文字节点见[场景文字导出数据](../output/document-text/scene-text.json)；重新整理时可使用 output/document-text 下的导出器与生成脚本。")

content = "\n\n".join(p for p in parts if p) + "\n"
MD.write_text(content, encoding="utf-8")

# Reuse the repository's plain-text/Word formatter without running its dialogue exporter.
(OUT / (TITLE + ".txt")).write_text(review.make_notes(content), encoding="utf-8-sig")
review.make_docx([re.sub(r"^#### ", "### ", content, flags=re.M)], OUT / (TITLE + ".docx"))

# Coverage checks are based on exported strings, including branch variants and all notebook pages.
normalize = lambda text: re.sub(r"\s+", "", text)
normalized = normalize(re.sub(r"^> ?", "", content, flags=re.M))
assert included == set(PAPERS), sorted(set(PAPERS) - included)
checks = 1
for pid, p in PAPERS.items():
    if pid == "notebook":
        for spread in p["spreads"]:
            for side in ["left", "right"]:
                assert normalize(spread[side]) in normalized, (pid, side)
                checks += 1
        continue  # Rendered folios, temporary turning faces and alphabet cells are presentation duplicates.
    for t in p["ink"]:
        if t["text"]:
            assert normalize(t["text"]) in normalized, (pid, t["path"], t["text"])
            checks += 1
for v in DATA["variants"]:
    if v["kind"] == "report":
        assert normalize(v["body"]) in normalized, v["condition"]
        checks += 1
    else:
        for t in v["ink"]:
            if t["text"]:
                assert normalize(t["text"]) in normalized, (v["kind"], v["condition"], t["path"])
                checks += 1
assert len(PAPERS) == 32
assert len([v for v in DATA["variants"] if v["kind"] == "report"]) == 8
assert len(PAPERS["notebook"]["spreads"]) == 7
assert not re.search(r"第七号封档|7—031|七—〇三一", content)
checks += 4
with ZipFile(OUT / (TITLE + ".docx")) as package:
    document = ET.fromstring(package.read("word/document.xml"))
    word_text = normalize("".join(document.itertext()))
txt_text = normalize((OUT / (TITLE + ".txt")).read_text(encoding="utf-8-sig"))
expected = [t["text"] for p in PAPERS.values() if p["id"] != "notebook" for t in p["ink"] if t["text"]]
expected += [spread[side] for spread in PAPERS["notebook"]["spreads"] for side in ["left", "right"]]
expected += [v["body"] for v in DATA["variants"] if v["kind"] == "report"]
expected += [t["text"] for v in DATA["variants"] if v["kind"] != "report" for t in v["ink"] if t["text"]]
for source_text in expected:
    for target in [word_text, txt_text]:
        assert normalize(source_text) in target, source_text
        checks += 1

sources = [DATA["scene"], "Assets/Scripts/Presentation/ThirdActVerification.cs", "Assets/Scripts/Presentation/FifthActDocument.cs",
           "Assets/Scripts/Presentation/FifthActFlow.cs", "Assets/Scripts/Presentation/EpilogueFlow.cs",
           "Assets/Scripts/Presentation/PlaintextArchiveController.cs", "Assets/Art/DeskSceneV1/Editor/DeskNotebookBuilder.cs",
           "Assets/Art/DeskSceneV1/Editor/DeskSecondActBuilder.cs", "output/document-text/scene-text.json"]
manifest = {
    "source_commit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
    "papers": len(PAPERS), "originals": 21, "plaintext_records": 6, "reference_books": 3, "receipt_objects": 2,
    "report_variants": 8, "runtime_snapshots": len(DATA["variants"]), "coverage_checks": checks,
    "sources": {p: hashlib.sha256((ROOT / p).read_bytes()).hexdigest() for p in sources},
    "artifacts": [str(MD.relative_to(ROOT)).replace("\\", "/"), f"output/document-text/{TITLE}.txt", f"output/document-text/{TITLE}.docx"],
}
(OUT / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
(OUT / "validation.txt").write_text(f"PASS: {checks} text coverage checks; 32 documents/books; 20 runtime snapshots; 8 report variants; 14 notebook pages.\n", encoding="utf-8")
print(json.dumps({"checks": checks, "papers": len(PAPERS), "characters": len(content), "artifacts": manifest["artifacts"]}, ensure_ascii=False, indent=2))
