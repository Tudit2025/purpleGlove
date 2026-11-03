"""Export a review snapshot, never change Unity assets or overwrite an edited draft.

Python 3.11 standard library only. Run from the repository root:
  python output/performance-edit/build_review_pack.py --docs-dir doc/performance-edit --mobile-dir output/performance-edit/mobile
Choose new, empty destinations when exporting again after revisions.
"""
from __future__ import annotations

import argparse
import ast
import hashlib
import json
import re
from pathlib import Path
from xml.etree import ElementTree as ET
from zipfile import ZipFile, ZIP_DEFLATED

ROOT = Path(__file__).resolve().parents[2]
EVENT_ROOT = ROOT / "Assets/Data/Dialogue/Events"
SOURCE = ROOT / "doc/ph-revised.md"
TITLES = ["第零幕 · 门槛", "第一幕 · 留下原件", "第二幕 · 第五十七号封档", "第三幕 · 读得通的谎言",
          "第四幕 · 名字留在纸上", "第五幕 · 落笔与交卷", "第六幕 · 尾声"]
TIMES = ["六月十五日晚八点前", "六月十五日晚八点至晚十点", "六月十五日晚十点至十六日零点", "六月十六日零点至凌晨三点", "六月十六日凌晨三点至五点", "六月十六日清晨五点至交卷", "六月十六日清晨六点；重逢支线至六点半"]
FLOWS = ["PrologueSequence.cs", "FirstActFlow.cs", "SecondActFlow.cs、SecondActEnding.cs", "ThirdActFlow.cs",
         "FourthActOpening.cs、FourthActFlow.cs", "FifthActFlow.cs", "EpilogueFlow.cs"]

# An event occurs only once in this editorial index, even when several endings share it.
# Stage directions summarize the current flow code; text is read from the actual assets.
STAGES = [
    [
        ("门外自述与入场", "进入第零幕。", "门外固定机位。卢卡独白，执事画外说话，不显示人物正脸。", "prologue",
         "最后一句推进后主门打开，播放入场转场；第一幕在桌面开始。当前用逐句推进完成入场，没有额外的“进入工作间”选择按钮。"),
    ],
    [
        ("执事介绍首批来件", "第一幕淡入完成，等待五秒；完整播放敲门声。", "镜头转向门，观察窗打开，执事介绍两个包裹与转交申请表。", "act1_arrival",
         "观察窗完全关闭后，传物舱送来棕白包裹。点击接收：公爵委托、会长信、师父密文纸；档案转交申请收入立夹。"),
        ("看见三道裁口", "首次检视师父密文纸。", "保持纸张检视，卢卡回忆；本次游玩只播放一次。", "memory_three_cuts",
         "玩家解凯撒密码，留存 KEEP THE ORIGINAL。首次留存会播放共用提示 plaintext_complete，见共用独白稿。"),
        ("执事介绍米兰代表", "凯撒明文实际留存后等待五秒，再响敲门声。", "等完成提示读完，转向门边，由执事介绍书记官。", "act1_secretary_arrival",
         "观察窗完全关闭后等待两秒，再开启书记官对话。"),
        ("书记官第一次催促", "执事介绍结束、观察窗关闭满两秒。", "书记官先逐字说完开场，玩家推进后单独显示三项回应；选项上方不重复书记官台词。点击选择后只播放对应的书记官回答。", "act1_secretary",
         "对应回答结束后关窗，镜头平滑回到桌面；不串播另外两条回答。"),
        ("桌前总结", "书记官交谈结束并回到桌面。", "卢卡桌前独白。", "act1_summary",
         "淡出、第二幕幕名、淡入。第一幕证据归柜，备用蜡烛余三根，第二幕开始介绍来件。"),
    ],
    [
        ("贝阿特丽切送来旧便条", "第二幕转场完成。", "执事报身份，贝阿特丽切隔门说明。", "beatrice",
         "关窗后传物舱送件；点击接收才取得书本密码便条。用七瓣花版解出并留存 REQUEST CASE FIFTY SEVEN。"),
        ("卢卡提示摇铃", "书本密码明文实际留存，其他演出空闲。", "桌前独白；共用完成提示按实际播放状态排队。", "act2_ring_hint",
         "提示结束后才开放摇铃。玩家实际摇响、放回手铃。"),
        ("第一次调档", "玩家摇铃。", "镜头转向门，执事应铃。", "act2_request",
         "关窗后等六秒，代表执事去调档；可查看已有资料。之后敲门送档。"),
        ("第五十七号封档送达", "调档等待结束。", "执事隔门介绍来源与内容。", "act2_archive_arrival",
         "观察窗完全关闭后送入档袋，点击接收才解锁台账摘要与猪圈碎纸。解出并留存 LEFT GLOVE。"),
        ("怀疑与等待", "猪圈明文实际留存，第二幕补充文件已接收。", "镜头回桌面，卢卡总结。", "act2_summary",
         "独白结束后开放手铃，等待玩家再次摇铃申请原始档案。"),
        ("申请原始档案", "玩家在桌前总结后再次摇响并放回手铃。", "执事应铃；卢卡按封档附件列出的第17号委托、批号726申请原始档案。", "act2_original_request",
         "第二幕证据及两份明文归柜；换烛后备用蜡烛余两根，转入第三幕。"),
    ],
    [
        ("正式核验件到达", "第三幕淡入完成。", "敲门、开观察窗，执事报午夜、案号与批号。", "act3_arrival",
         "关窗后收件：译录委托、军情密文、法文原文、核验表。核验表收入立夹。解出并留存 FRANCE WILL CROSS ON JUNE EIGHTEEN，再打开核验表，签署明文还原。"),
        ("夏尔介入", "玩家完成明文还原签名，核验表收回。", "门边交谈；为何不公开的追问已在当前固定对话中。", "act3_charles",
         "关窗后单独投递夏尔的补充凭条；点击接收后才取得这张纸。"),
        ("书记官要求归还原件", "夏尔凭条实际接收后等待两秒。", "执事介绍米兰代表，书记官隔门施压，卢卡拒绝提前交走原件。原始档案已在第二幕申请。", "act3_secretary",
         "关窗，再进入卡拉法来访。"),
        ("卡拉法淡化换版", "书记官交谈结束。", "执事介绍，卡拉法在观察窗外说话。", "act3_carafa",
         "关窗，镜头平滑回到桌面。"),
        ("读得通的谎言", "卡拉法交谈结束并回到桌面。", "卢卡桌前独白。", "act3_summary",
         "第三幕证据归柜，已签核验表与转交申请仍在立夹；备用蜡烛余一根，转入第四幕。"),
    ],
    [
        ("两份独立封件", "第四幕淡入完成。", "执事报凌晨三点，分别说明行会库与修道院的送件来路。", "act4_arrival",
         "关窗后收两包四张纸：商业文件委托签收页、换版记录、师父双栏密信、师父明文书信。分别解仿射与栅栏密码，顺序不限。"),
        ("救他与替他辩白", "地点与自述两份明文都已落字、实际留存。", "镜头从破解区回桌面。自述明文已收入立夹。", "act4_summary",
         "独白后直接转第五幕。原始文书和地点明文归柜，三份主动文件留在立夹；备用蜡烛用尽，烛台上的蜡烛仍燃烧。"),
    ],
    [
        ("代表来信与默认安排", "第五幕开始。", "执事在观察窗外报五点，介绍三封信与三项决定。", "act5_arrival",
         "关窗后收三封代表来信。玩家可在立夹中决定来源担保、原件去向、自述保留或焚毁；没有阅读倒计时。"),
        ("选择来源担保时", "来源担保由否改为是。", "保留当前核验表阅读位置，卢卡提醒；交卷前仍可更改。", "act5_source_warning",
         "提醒结束回到文件操作。再次从否改为是时可再次触发。"),
        ("焚毁自述的确认", "打开自述明文，点击焚毁操作。", "同时显示两个可点击选项，不逐字显示、不播放打字声。", "act5_burn_choice",
         "选择 burn 才移纸焚烧并记为已毁；选择 keep 返回阅读。只烧这张明文，密信原件仍在。"),
        ("摇铃交卷的确认", "收到第五幕三封信后拿起手铃。", "同时显示两个可点击选项；此时还没有实际摇响。", "act5_ring_choice",
         "选择 ring 才实际摇响并放回，锁定当前安排；选择 wait 放回手铃继续处理文件。"),
        ("执事接件", "玩家确认摇铃，手铃动作完成。", "镜头转向门边，执事取件并说明仍不能离开；三项选择按纸面记录办理。", "act5_handoff",
         "关窗后把核验表、转交申请、仍保留的自述及换版原件送入转舱。文件真正转出后才淡出，进入第六幕。"),
    ],
    [
        ("开门／缺页共用：六点回报", "不担保来源，原件继续由行会封存。", "第六幕淡入；敲门、打开探视口，主门仍关着。", "act6_saved_report",
         "关探视口，自动送入处理回执，落桌后转舱关闭。显示半小时后／清晨六点半短卡。"),
        ("开门／缺页共用：六点半接回", "六点半转场完成。", "执事从探视口报告接回凭据；师父还没有开始说话。", "act6_reunion_arrival",
         "关探视口，自动送入新的接回凭据，落桌后再请师父到探视口。按自述状态选择下列一段重逢。"),
        ("结局一：开门", "不担保来源、原件继续封存、自述保留。", "师父和卢卡隔着探视口重逢。", "act6_open",
         "最后一句结束，探视口完全关闭，镜头停在门上约半秒；主门开启与约1.6秒淡出同步，全黑显示结局名与终。"),
        ("结局二：缺页", "不担保来源、原件继续封存、自述焚毁。", "与开门共用前两段回报和收件，只替换这一段重逢。", "act6_missing",
         "探视口完全关闭，主门开启与淡出同步；全黑显示缺页与终。开门后不再有对白。"),
        ("真相易手：六点回报", "不担保来源，原件交米兰、威尼斯或法国。", "执事从探视口说明共同审查与暂停转押。", "act6_transfer_report",
         "关探视口，自动送入处理回执与交接登记合页；落桌后再开启探视口，接真相易手结尾。"),
        ("结局三：真相易手", "真相易手的处理回执已落桌。", "执事按原件去向说明后果。自述是否焚毁不改变这段尾声。", "act6_transferred",
         "探视口关闭，主门保持关闭并淡出，显示真相易手与终。师父仍留候核验。"),
        ("背书：六点回报", "签了来源担保；原件去向、自述状态任意。", "执事从探视口报告已经转押；不能写成师父已死亡。", "act6_endorsed_report",
         "关探视口，自动送入转押与原件交接登记合页；落桌后再开启探视口。"),
        ("结局四：背书", "背书的处理回执已落桌。", "执事按原件去向说明后果。自述是否焚毁不改变这段尾声。", "act6_endorsed",
         "探视口关闭，主门保持关闭并淡出，显示背书与终。"),
    ],
]

COMMON = [
    ("首次看旧版祷文集", "memory_prayer_old"), ("首次看七瓣花版祷文集", "memory_prayer_flower"),
    ("首次看师父旧笔记", "memory_notebook"), ("首次用解密工具箱", "memory_toolbox"),
    ("本幕第一次留存明文", "plaintext_complete"), ("当前不能摇铃", "handbell_not_needed"),
]
LEGACY = ["arrival", "charles", "pressure", "archive", "matteo", "bartolo", "act3_ring_hint", "act3_request"]


def scalar(raw: str) -> str:
    raw = raw.strip()
    if raw.startswith('"'):
        value = ast.literal_eval(raw)
        if not isinstance(value, str):
            raise ValueError(raw)
        return value
    return raw


def field(text: str, key: str) -> str:
    match = re.search(rf"^  {key}: ?(.*)$", text, re.M)
    if not match:
        raise ValueError(f"Missing field: {key}")
    return scalar(match[1])


def load_events() -> dict:
    speakers = {}
    for path in (ROOT / "Assets/Data/Dialogue/Speakers").glob("*.asset"):
        guid = re.search(r"^guid: (\w+)$", Path(str(path) + ".meta").read_text(), re.M)[1]
        speakers[guid] = field(path.read_text(encoding="utf-8"), "displayName")
    events = {}
    for path in sorted(EVENT_ROOT.glob("*.asset")):
        text = path.read_text(encoding="utf-8")
        lines = []
        blocks = re.split(r"(?=^  - speaker:)", text.split("  lines:\n", 1)[1], flags=re.M)
        for block in blocks:
            if not block.strip():
                continue
            speaker = re.search(r"^  - speaker:.*guid: (\w+)", block, re.M)
            match = re.search(r"^    text: (.+)$", block, re.M)
            if not speaker or not match:
                raise ValueError(f"Unsupported dialogue serialization: {path}")
            choices = []
            for choice in re.finditer(r"^    - id: (.+)\n      text: (.+)\n      nextLine: (-?\d+)$", block, re.M):
                choices.append({"id": scalar(choice[1]), "text": scalar(choice[2]), "nextLine": int(choice[3])})
            if len(choices) != len(re.findall(r"^    - id:", block, re.M)):
                raise ValueError(f"Unparsed choices in {path}")
            lines.append({"speaker": speakers[speaker[1]], "text": scalar(match[1]), "choices": choices,
                          "endEvent": bool(re.search(r"^    endEvent: 1$", block, re.M))})
        if len(lines) != len(re.findall(r"^    text:", text, re.M)):
            raise ValueError(f"Unparsed lines in {path}")
        event = {"id": field(text, "id"), "title": field(text, "title"), "context": field(text, "context"),
                 "testOnly": field(text, "testOnly") == "1", "source": path.relative_to(ROOT).as_posix(), "lines": lines,
                 "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
        if event["id"] in events:
            raise ValueError("Duplicate event ID")
        events[event["id"]] = event
    return events


def runtime_choices() -> dict:
    path = ROOT / "Assets/Scripts/Presentation/FifthActFlow.cs"
    source = path.read_text(encoding="utf-8")
    quoted = r'"(?:\\.|[^"\\])*"'
    pattern = r'Choice\(\s*(' + quoted + r')\s*,\s*(' + quoted + r')\s*,\s*(' + quoted + r')\s*,\s*(' + quoted + r')\s*,\s*(' + quoted + r')\s*,\s*(' + quoted + r')\s*\)'
    result = {}
    for match in re.finditer(pattern, source):
        event_id, prompt, yes_id, yes, no_id, no = map(ast.literal_eval, match.groups())
        result[event_id] = {"id": event_id, "title": prompt, "source": path.relative_to(ROOT).as_posix(), "runtime": True,
                            "lines": [{"speaker": "卢卡", "text": prompt, "choices": [
                                {"id": yes_id, "text": yes, "nextLine": -1}, {"id": no_id, "text": no, "nextLine": -1}]}]}
    if set(result) != {"act5_ring_choice", "act5_burn_choice"}:
        raise ValueError("Runtime choice extraction incomplete")
    return result


def event_text(event: dict) -> str:
    output = [f"事件编号：`{event['id']}`", f"来源：`{event['source']}`", ""]
    for index, line in enumerate(event["lines"], 1):
        output += [f"**{line['speaker']}｜{event['id']}.L{index:02}**", "", line["text"], ""]
        if line.get("endEvent"):
            output += ["本句结束后：结束本次对话，不继续播放其他分支回答。"]
        for choice in line["choices"]:
            destination = "结束本次选择，按选项执行" if choice["nextLine"] == -1 else f"转到第 {choice['nextLine'] + 1} 句"
            output += [f"**选项｜{event['id']}.{choice['id']}**", "", choice["text"], "", f"选择后：{destination}。", ""]
    return "\n\n".join(part for part in output if part).strip()


def fifth_branches() -> str:
    path = ROOT / "Assets/Scripts/Presentation/FifthActFlow.cs"
    source = path.read_text(encoding="utf-8")
    output = ["## 交卷台词的替换文字", "修改这里对应的分支文字时，保留台词模板中的花括号。原件四个值是继续封存、交米兰、交威尼斯、交法国。"]
    q = r'"(?:\\.|[^"\\])*"'
    for m in re.finditer(r'\.Replace\(("\{[^}]+\}")\s*,\s*(GuaranteesSource|ConfessionBurned)\s*\?\s*(' + q + r')\s*:\s*(' + q + r')\)', source):
        token, variable, yes, no = m.groups()
        label = "担保来源" if variable == "GuaranteesSource" else "自述已焚毁"
        output += [f"### {ast.literal_eval(token)}", f"**{label}时**", ast.literal_eval(yes), f"**{label}为否时**", ast.literal_eval(no)]
    if sum(s.startswith("### ") for s in output) != 3:
        raise ValueError("Missing handoff substitutions")
    output += ["### {原件}", "继续封存 / 交米兰 / 交威尼斯 / 交法国", "修改备注："]
    return "\n\n".join(output)


def sixth_branches() -> str:
    source = (ROOT / "Assets/Scripts/Presentation/EpilogueFlow.cs").read_text(encoding="utf-8")
    block = source.split("void FillCustody", 1)[1].split("string ReportText", 1)[0]
    q = r'"(?:\\.|[^"\\])*"'
    output = ["## 原件去向造成的完整台词差异", "`{原件}`替换为：继续封存、交米兰、交威尼斯、交法国。下列文字替换`{去向后果}`；每次只使用符合当前去向和结局的一段。"]
    names = {"Milan": "米兰", "Venice": "威尼斯", "France": "法国"}
    matches = list(re.finditer(r'case FifthActFlow.Custody.(Milan|Venice|France):\s*consequence = fifth.GuaranteesSource\s*\?\s*(' + q + r')\s*:\s*(' + q + r')', block))
    if len(matches) != 3:
        raise ValueError("Missing epilogue branches")
    for match in matches:
        output += [f"### 交{names[match[1]]}", "**真相易手**", ast.literal_eval(match[3]), "**背书**", ast.literal_eval(match[2])]
    default = re.search(r'default:\s*consequence = (' + q + r')', block)
    output += ["### 继续封存 · 背书", ast.literal_eval(default[1]), "修改备注："]
    return "\n\n".join(output)


def section(title: str) -> str:
    source = SOURCE.read_text(encoding="utf-8")
    match = re.search(r"^### " + re.escape(title) + r"\n(.*?)(?=^#{1,3} |\Z)", source, re.M | re.S)
    if not match:
        raise ValueError(f"Missing script section {title}")
    # Convert source tables to vertical records so phone readers need no horizontal scrolling.
    lines = match[1].strip().splitlines()
    output = []
    i = 0
    while i < len(lines):
        if lines[i].startswith("|"):
            table = []
            while i < len(lines) and lines[i].startswith("|"):
                table.append([c.strip() for c in lines[i].strip("|").split("|")])
                i += 1
            for row in table[2:]:
                output += ["", "；".join(f"{heading}：{cell}" for heading, cell in zip(table[0], row)), ""]
        else:
            output.append(lines[i])
            i += 1
    return "\n".join(output)


def make_docs(events: dict) -> dict[str, str]:
    docs = {}
    for act, stages in enumerate(STAGES):
        parts = [f"# {TITLES[act]}", f"时间：{TIMES[act]}。", "版本：2026-11-02 返回改稿已接入后的游戏文本。直接改台词正文即可；角色与定位编号请保留。修改备注可填写节奏、动作、删句或增句要求。",
                 f"演出顺序依据：`Assets/Scripts/Presentation/{FLOWS[act]}`。台词来源逐段标明。", "## 本幕演出顺序"]
        parts += ["\n".join(f"{n}. {stage[0]}" for n, stage in enumerate(stages, 1))]
        if act == 6:
            parts += ["这里列出互斥结局，游玩时不会从头到尾连播。",
                      "开门：共用六点回报 → 六点半接回 → 开门重逢。\n\n缺页：共用六点回报 → 六点半接回 → 缺页重逢。\n\n真相易手：对应六点回报 → 真相易手结尾。\n\n背书：对应六点回报 → 背书结尾。"]
        for n, (title, trigger, staging, event_id, after) in enumerate(stages, 1):
            parts += [f"## {act}-{n:02} {title}", f"触发：{trigger}", f"演出：{staging}", event_text(events[event_id]), f"结束后：{after}", "修改备注：", "---"]
        if act == 6:
            parts.append(sixth_branches())
        parts += ["## 本幕整体调整", "想保留的句子：", "想删减或重写的段落：", "节奏、停顿、镜头或动作："]
        docs[f"{act:02}-{TITLES[act].replace(' · ', '-')}.md"] = "\n\n".join(parts) + "\n"

    parts = ["# 共用独白与物品回忆", "这些事件不固定在幕末触发。物品回忆可在玩家首次操作时插入；三道裁口的回忆已放在第一幕，不重复抄录。",
             "明文完成提示在第一至第四幕各自首次成功留存后最多播放一次，不是每道密码都播放。触发其他独白时会等待对话空闲。"]
    for title, event_id in COMMON:
        parts += [f"## {title}", event_text(events[event_id]), "修改备注：", "---"]
    docs["07-共用独白与物品回忆.md"] = "\n\n".join(parts) + "\n"

    parts = ["# 待接入对白与旧试演稿", "本文件用于保留可调整素材。下面第一部分来自剧本文档，当前流程未提供对应选择或触发；第二部分是仍保留的旧试演资源，不应当作当前主线已播放内容。",
             "## 剧本保留，当前未接入的内容",
             "### 第一幕：书记官的另外两种回应", "来源：doc/ph-revised.md／解读后的门边交谈。当前主线固定播放“这是两件事”。",
             "**卢卡｜draft.act1.ask_location**", "师父被关在哪里？", "**公爵书记官｜draft.act1.location_reply**", "他在一处安静的地方等结果。",
             "**卢卡｜draft.act1.silent**", "保持沉默。（选项／动作，非朗读台词。）", "修改备注：",
             "### 第二幕：贝阿特丽切的隐瞒", "来源：doc/ph-revised.md／贝阿特丽切的隐瞒。剧本计划在破译完成前追问被遮住的姓名，当前 SecondActFlow 未接入这段。",
             "**贝阿特丽切｜draft.act2.hidden_name.L01**", "是我涂的。", "**卢卡｜draft.act2.hidden_name.L02**", "你怕我知道？",
             "**贝阿特丽切｜draft.act2.hidden_name.L03**", "我怕委员会先把他带走，再问他是不是有用的证人。他的家人刚出去。",
             "**卢卡｜draft.act2.hidden_name.L04**", "你也不想让我绕过你找到他。", "演出：贝阿特丽切沉默片刻。",
             "**贝阿特丽切｜draft.act2.hidden_name.L05**", "是。", "修改备注：",
             "### 第三幕：初步来源判断的提醒", "来源：doc/ph-revised.md／第一项正式判断。剧本中勾选已证实时的提示，当前 ThirdActVerification 只实现明文签名。",
             "**卢卡｜draft.act3.source_note**", "我只是用对了钥匙，还没有找到签发这道命令的人。", "修改备注：",
             "### 第三幕：书记官交谈的另外两种回应", "来源：doc/ph-revised.md／公爵的要求。当前主线固定播放保留原件的回应。",
             "**卢卡｜draft.act3.ask_location**", "我要先知道老师在哪。", "后续：书记官给出模糊承诺，不交地点。原稿没有写出具体回应台词。",
             "**卢卡｜draft.act3.follow_rules**", "我会按规程交卷。", "说明：不等于同意交出原件。", "修改备注：",
             "## 其他需留意的稿件差异",
             "序幕：原稿写单独的“进入工作间”确认；当前是推进最后一句后直接开门入场。原稿通知、折痕、手指等画面细节不应视为已经逐项实现。",
             "第一幕：原稿封门时读出“米兰、威尼斯、法国”，未在当前 DialogueEvent 台词中。档案转交申请说明在当前 act1_arrival 第三句，已纳入主线稿。",
             "第三幕：夏尔为何不公开的追问，当前已并入固定对话；不再列成玩家可选分支。维吉尼亚关键词提示的纸面载体在原稿中标为暂缓制作。",
             "第四幕：原稿独白首句为“圣马丁，北门。师父就在那边等着六点的转押。”；当前资源为“圣马丁，北门。师父的信说，六点就会从那里转押。”。主线稿保留资源版本，避免提前认定有外部证据。",
             "## 保留在项目内的旧试演资源", "这些资源尚在 DialogueController 的事件资源集合或安装器中，但当前各幕流程没有自动播放它们；matteo 与 bartolo 另标 testOnly。角色名可能与现在的主线出场安排不同。"]
    for event_id in LEGACY:
        parts += [f"### {events[event_id]['title']}", f"资源 testOnly：{'是' if events[event_id]['testOnly'] else '否'}。分类依据为当前流程引用，不能仅用 testOnly 判断是否主线。", event_text(events[event_id]), "修改备注："]
    docs["08-待接入对白与旧试演稿.md"] = "\n\n".join(parts) + "\n"

    parts = ["# 纸面文字与演出设计参考", "本文件从 doc/ph-revised.md 抽取书信、谜题文字及演出设计，方便同批改稿。它保留的是剧本文档版本，未逐字核对场景中的全部 TextMesh、书页与纸张资源；不要与前七份已核对的游戏对白混用。密码正文、答案或取字坐标改动后，需要另行同步谜题数据。",
             "本文件的表格已转为纵向段落，便于手机修改。纸面文字不是门边朗读对白。"]
    selections = {
        0: ["画面与声音", "入场时保留的任务便笺", "本幕的信息边界"],
        1: ["第一批收件", "行会会长的信", "谜题一：凯撒密码"],
        2: ["谜题二：书本密码", "玩家主动调档", "谜题三：猪圈密码"],
        3: ["正式核验件到达", "谜题四：维吉尼亚密码", "夏尔第一次直接介入"],
        4: ["商业文件委托签收页与换版记录", "谜题五：仿射密码", "谜题六：栅栏密码", "师父的明文书信", "师父的话如何复核"],
        5: ["三位代表的要求与条件", "三份主动文件", "主动文件一：军情核验表", "主动文件二：行会档案转交申请", "主动文件三：师父自述明文"],
        6: ["共同演出规则", "状态定义"],
    }
    for act, titles in selections.items():
        parts.append(f"## {TITLES[act]}")
        for title in titles:
            parts += [f"### {title}（剧本参考）", section(title), "修改备注："]
    docs["09-纸面文字与演出设计参考.md"] = "\n\n".join(parts) + "\n"
    return docs


W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"
ET.register_namespace("w", W)


def wtag(name: str) -> str:
    return f"{{{W}}}{name}"


def child(parent, name: str, attrs: dict | None = None):
    return ET.SubElement(parent, wtag(name), {wtag(k): str(v) for k, v in (attrs or {}).items()})


def plain(text: str) -> str:
    return re.sub(r"\[([^\]]+)\]\([^)]*\)", r"\1", text).replace("**", "").replace("`", "")


def make_notes(content: str) -> str:
    lines = []
    for line in content.splitlines():
        if line.startswith(("来源：`", "演出顺序依据：")):
            continue
        if line == "---":
            lines.append("")
            continue
        line = re.sub(r"^#{1,6} ", "", line)
        line = re.sub(r"^> ?", "", line)
        lines.append(plain(line))
    return re.sub(r"\n{3,}", "\n\n", "\n".join(lines)).strip() + "\n"


def make_docx(chapters: list[str], path: Path):
    document = ET.Element(wtag("document"))
    body = child(document, "body")
    for ci, chapter in enumerate(chapters):
        if ci:
            p = child(body, "p")
            child(child(p, "r"), "br", {"type": "page"})
        for raw in chapter.splitlines():
            if not raw.strip() or raw == "---":
                continue
            heading = re.match(r"^(#{1,3}) (.+)$", raw)
            style = f"Heading{len(heading[1])}" if heading else "Normal"
            text = heading[2] if heading else raw
            if text.startswith(">"):
                text = text.removeprefix("> ").removeprefix(">")
            bold = raw.startswith("**") and raw.endswith("**")
            p = child(body, "p")
            props = child(p, "pPr")
            child(props, "pStyle", {"val": style})
            if bold:
                child(props, "keepNext")
            run = child(p, "r")
            if bold:
                child(child(run, "rPr"), "b")
            content = child(run, "t")
            content.set("{http://www.w3.org/XML/1998/namespace}space", "preserve")
            content.text = plain(text.strip())
    section_props = child(body, "sectPr")
    child(section_props, "pgSz", {"w": 8391, "h": 11906})  # A5, one column, comfortable on phones.
    child(section_props, "pgMar", {"top": 650, "right": 620, "bottom": 650, "left": 620, "header": 300, "footer": 300, "gutter": 0})
    styles = ET.Element(wtag("styles"))
    for style_id, size, color, level in [("Normal", 23, "242424", None), ("Heading1", 34, "542958", 0),
                                          ("Heading2", 28, "542958", 1), ("Heading3", 25, "3C3C3C", 2)]:
        style = child(styles, "style", {"type": "paragraph", "styleId": style_id})
        child(style, "name", {"val": style_id})
        if level is not None:
            child(style, "basedOn", {"val": "Normal"})
            child(style, "next", {"val": "Normal"})
        props = child(style, "pPr")
        child(props, "spacing", {"after": 125, "before": 160 if level is not None else 0, "line": 340, "lineRule": "auto"})
        child(props, "widowControl")
        if level is not None:
            child(props, "keepNext")
            child(props, "outlineLvl", {"val": level})
        rp = child(style, "rPr")
        child(rp, "rFonts", {"ascii": "Calibri", "hAnsi": "Calibri", "eastAsia": "Microsoft YaHei"})
        child(rp, "sz", {"val": size})
        child(rp, "color", {"val": color})
        child(rp, "lang", {"val": "zh-CN", "eastAsia": "zh-CN"})
        if level is not None:
            child(rp, "b")
    settings = ET.Element(wtag("settings"))
    child(settings, "zoom", {"percent": 110})
    content_types = '''<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/><Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/><Override PartName="/word/settings.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml"/></Types>'''
    rels = '''<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>'''
    docrels = '''<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/settings" Target="settings.xml"/></Relationships>'''
    with ZipFile(path, "w", ZIP_DEFLATED) as archive:
        archive.writestr("[Content_Types].xml", content_types)
        archive.writestr("_rels/.rels", rels)
        archive.writestr("word/_rels/document.xml.rels", docrels)
        for name, element in [("document", document), ("styles", styles), ("settings", settings)]:
            archive.writestr(f"word/{name}.xml", ET.tostring(element, encoding="utf-8", xml_declaration=True))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--docs-dir", type=Path, required=True)
    parser.add_argument("--mobile-dir", type=Path, required=True)
    args = parser.parse_args()
    for destination in (args.docs_dir, args.mobile_dir):
        if destination.exists() and any(destination.iterdir()):
            parser.error(f"Destination must be empty; preserve edited drafts: {destination}")
    events = load_events()
    assets_count = len(events)
    asset_lines = sum(len(e["lines"]) for e in events.values())
    runtime = runtime_choices()
    events.update(runtime)
    indexed = [s[3] for stages in STAGES for s in stages] + [x[1] for x in COMMON] + LEGACY
    if len(indexed) != len(set(indexed)) or set(indexed) != set(events):
        raise ValueError(f"Coverage mismatch: {set(events) ^ set(indexed)}")
    docs = make_docs(events)
    main_ids = [s[3] for stages in STAGES for s in stages if not events[s[3]].get("runtime")]
    main_lines = sum(len(events[key]["lines"]) for key in main_ids)
    common_lines = sum(len(events[key]["lines"]) for _, key in COMMON)
    legacy_lines = sum(len(events[key]["lines"]) for key in LEGACY)
    intro = f'''# 《紫手套》演出与台词改稿包

整理日期：2026-11-02。范围：序幕、五幕正篇、第六幕四种结局、共用独白、旧试演与剧本补充。

## 怎么改

以逐幕文档为主：点击下方对应幕，复制到手机笔记中即可改稿。[手机笔记纯文本包](../../output/performance-edit/mobile/紫手套-手机笔记逐幕稿.zip)内每幕一份 TXT，已去掉 Markdown 标记和源码路径，保留演出提示、角色名和台词编号。

另附[演出与台词 Word 改稿](../../output/performance-edit/mobile/紫手套-演出与台词-手机改稿.docx)，可用 WPS 或 Word 编辑、加批注。[完整改稿包](../../output/performance-edit/mobile/紫手套-逐幕改稿包.zip)包含逐幕 TXT、Word 和 Markdown。纸面文字与演出设计参考另有单独文件，未并入台词 Word 合订本。

每句台词独立成段，上方有角色和定位编号，例如 act3_charles.L03。直接改正文，尽量保留编号；需要增删、调顺序或改演出时写在该段“修改备注”中。新增句子可写“在 L03 后新增”，无需重新编号整幕。

选择手机笔记、TXT、Word 或 Markdown 中的一份作为你的改稿主文件即可，各版本不会自动同步。修改后把改稿交回，可按编号逐项更新游戏资源与相关生成代码。当前底稿不会自动写回游戏。

## 收录情况

当前项目共 {assets_count} 个对话资源、{asset_lines} 句资源台词，已全部收录且未改写原句。按当前流程分类：各幕主线 {main_lines} 句，共用独白 {common_lines} 句，旧试演 {legacy_lines} 句。另收录运行时创建的 2 个确认提示及 4 个选项，并列出交卷和尾声的动态替换文字。

主线台词以 Assets/Data/Dialogue/Events 为准；触发与演出顺序按当前流程脚本整理。剧本文档中尚未接入的选择、旧试演和纸面参考分别标明，避免误当作当前游戏内容。

## 文件目录

'''
    intro += "\n".join(f"- [{name[:-3]}]({name})" for name in docs)
    intro += '''

## 共用演出约定

第一幕封门后，门边对白经观察窗／探视口完成，外来文件经传物舱交接。对话结束并关窗后才收件。桌前独白保持桌面或当前检视机位。

门外说话对象更换时，先完全关闭观察窗，再更换头像、重新开窗，开到位后才显示下一句。卢卡与同一访客的来回对话保持连续。

第六幕只有“开门”和“缺页”在最后一句结束、探视口完全关闭后开启主门，并同步淡出；开门后不再追加对白。“真相易手”和“背书”保持主门关闭。

对白和选项统一使用印品铸宋体，对话框无左侧彩色竖条。对方附带选择的台词先正常逐字播放，玩家推进后单独显示选项，不再显示对方台词。第五幕卢卡自己的焚毁、摇铃确认仍同时显示提示与选项，必须点击选择。改短句、长句或标点可能改变打字节奏；需要特别的停顿可写备注。

## 后续回填说明

台词编号中的 L01 对应该事件的第一句（Unity lines[0]）。事件资源、运行时确认选项与动态分支的来源路径已逐段标注。纸面参考来自原剧本，尚未逐字核对场景中的纸张文字。

本目录是独立改稿副本，原剧本和游戏内容仍保留。重新导出请使用新目录，生成工具会拒绝覆盖非空目录，避免覆盖你的改稿。
'''
    docs = {"README.md": intro, **docs}
    for destination in (args.docs_dir, args.mobile_dir):
        destination.mkdir(parents=True, exist_ok=True)
    notes_dir = args.mobile_dir / "notes"
    notes_dir.mkdir()
    for name, content in docs.items():
        (args.docs_dir / name).write_text(content, encoding="utf-8", newline="\n")
        make_docx([content], args.mobile_dir / (name[:-3] + ".docx"))
        (notes_dir / (name[:-3] + ".txt")).write_text(make_notes(content), encoding="utf-8-sig", newline="\n")
    # Keep the main phone manuscript focused on performance and dialogue. The long
    # paper reference is a separate Word file in the bundle.
    make_docx([content for name, content in docs.items() if not name.startswith("09-")], args.mobile_dir / "紫手套-演出与台词-手机改稿.docx")
    with ZipFile(args.mobile_dir / "紫手套-手机笔记逐幕稿.zip", "w", ZIP_DEFLATED) as archive:
        for path in sorted(notes_dir.glob("*.txt")):
            archive.write(path, path.name)
    with ZipFile(args.mobile_dir / "紫手套-逐幕改稿包.zip", "w", ZIP_DEFLATED) as archive:
        for path in sorted(args.mobile_dir.glob("*.docx")):
            archive.write(path, "Word/" + path.name)
        for path in sorted(args.docs_dir.glob("*.md")):
            archive.write(path, "Markdown/" + path.name)
        for path in sorted(notes_dir.glob("*.txt")):
            archive.write(path, "手机笔记/" + path.name)
    snapshot = {"date": "2026-11-02", "events": events, "assetEvents": assets_count, "assetLines": asset_lines,
                "mainLines": main_lines, "commonLines": common_lines, "legacyLines": legacy_lines,
                "runtimePrompts": 2, "runtimeOptions": 4}
    (args.mobile_dir / "source-snapshot.json").write_text(json.dumps(snapshot, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({key: value for key, value in snapshot.items() if key != "events"}, ensure_ascii=False))


if __name__ == "__main__":
    main()
