"""Inspect collected font binaries; render an offline HTML and a PNG specimen.

Requires Pillow and fontTools. For this workspace, fontTools is also discovered
in Temp/ph-font-tools (an ignored, project-local tool directory).
No font binary is modified. Run after fetch_fonts.py.
"""
from pathlib import Path
import hashlib
import html
import json
import re
import sys

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[1]
sys.path.insert(0, str(PROJECT / "Temp/ph-font-tools"))
from fontTools.ttLib import TTFont
from PIL import Image, ImageDraw, ImageFont

FONTS = {
    "eb": "fonts/eb-garamond/EBGaramond-Regular.otf",
    "ebmedium": "fonts/eb-garamond/EBGaramond-Medium.otf",
    "ebitalic": "fonts/eb-garamond/EBGaramond-Italic.otf",
    "fell": "fonts/im-fell-english/IMFeENrm28P.ttf",
    "hand": "fonts/courgette/Courgette-Regular.ttf",
    "title": "fonts/cinzel/Cinzel-Regular.ttf",
    "mono": "fonts/ibm-plex-mono/IBMPlexMono-Regular.ttf",
    "song": "fonts/source-han-serif/SourceHanSerifCN-Regular.otf",
    "songbold": "fonts/source-han-serif/SourceHanSerifCN-SemiBold.otf",
    "fangsong": "fonts/zhuque-fangsong/ZhuqueFangsong-Regular.ttf",
    "kai": "fonts/lxgw-wenkai/LXGWWenKai-Regular.ttf",
    "sans": "fonts/source-han-sans/SourceHanSansCN-Regular.otf",
}

SAMPLES = [
    dict(title="01  正式文书 · 核验委托 / 外交回件", enfont="ebmedium", zhfont="song",
         enname="EB Garamond · Medium", zhname="思源宋体 · Regular",
         en="Keep the original.\nRecord the text and its source separately.\nReceipt does not imply agreement.",
         zh="保留原件。\n明文还原与来源确认，分别填写。\n收到材料，不等于同意参与。",
         note="克制、可信、可反复阅读。三方共用文书体系，以抬头、封件编号与具名回执区分。"),
    dict(title="02  旧式印本 · 两版《祷文集》", enfont="fell", zhfont="fangsong",
         enname="IM FELL English · Regular", zhname="朱雀仿宋 · Regular / v0.212 预览版",
         en="THE QUIET SERVANT CARRIED A LAMP\nHE LEFT THE HOUSE BEFORE THE SEVENTH BELL\nKEEP THE RECORD OF EVERY NAME",
         zh="安静的仆人提着一盏灯。\n他在第七次钟响前离开了屋子。\n留下每一个名字的记录。",
         note="英文采用附录 C 的七瓣花版三行原文；中文仅为样张译文，不参与逐字取码。"),
    dict(title="03  私人手稿 · 师父笔记 / 明文附记", enfont="hand", zhfont="kai",
         enname="Courgette · Regular", zhname="霞鹜文楷 · Regular",
         en="Do not erase my name.\nCheck what I say, too.\nKeep the original.",
         zh="不要抹去我的名字。\n我说的也请你查。\n保留原件。",
         note="让私人语气与公文分开。长信可改用 EB Garamond Italic；字母密文仍用清晰正体。"),
    dict(title="04  印坊记录 · 台账 / 签收页 / 换版存根", enfont="eb", zhfont="fangsong",
         enname="EB Garamond · Regular", zhname="朱雀仿宋 · Regular",
         en="BATCH 7—031     CASE 17\nOriginal: caravan transit papers.\nRevised: military transcript.",
         zh="装订批号：7—031　委托案号：17\n原定项目：商队通行文件。\n后改项目：军情译录件。",
         note="用紧凑栏位、对齐与手写补注形成账簿感；编号保持同一尺寸，不能做旧到难以辨认。"),
    dict(title="05  大字标题 · 通知抬头 / 规程封面", enfont="title", zhfont="songbold",
         enname="Cinzel · Regular", zhname="思源宋体 · SemiBold",
         en="DOCUMENT VERIFICATION\nKEEP THE ORIGINAL",
         zh="军情文书核验\n保留原件",
         note="庄重、具有仪式感；只用于大字抬头、封面与章节，不承担长正文。"),
    dict(title="06  清晰阅读 · 解谜辅助 / 输入 / 译文", enfont="mono", zhfont="sans",
         enname="IBM Plex Mono · Regular", zhname="思源黑体 · Regular",
         en="NHHS WKH RULJLQDO\nA=0  B=1  ...  Z=25\nI l 1 | O 0 | B 8 | S 5",
         zh="明文还原：保留原件。\n来源确认：待核。\n12-4-11 / 27-2-24 / 41-3-2",
         note="用于可选的清晰转录与解谜网格。纸面默认仍以正体衬线字或逐字格呈现密文。"),
]


def write_json(name, data):
    (ROOT / name).write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def inspect_fonts():
    manifest = json.loads((ROOT / "manifest.json").read_text(encoding="utf-8"))
    script = (PROJECT / "doc/ph-revised.md").read_text(encoding="utf-8")
    han = set(re.findall(r"[\u3400-\u9fff]", script))
    latin = set("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789")
    # Script prose punctuation; pigpen symbols are intentionally separate artwork.
    punctuation = set("，。！？：；、‘’“”（）《》【】—…·／－〈〉")
    report = {"scope": "Full revised script Han characters; Latin letters and digits; Chinese prose punctuation. Does not assert in-engine readability.",
              "script_han_count": len(han), "fonts": [], "samples": []}
    cmaps = {}
    for family in manifest["families"]:
        license_path = ROOT / family["license_path"]
        assert hashlib.sha256(license_path.read_bytes()).hexdigest() == family["license_sha256"]
        for entry in family["files"]:
            path = ROOT / entry["path"]
            assert hashlib.sha256(path.read_bytes()).hexdigest() == entry["sha256"], path
            with TTFont(path) as font:
                cmap = font.getBestCmap()
                cmaps[entry["path"]] = cmap
                missing_han = "".join(sorted(c for c in han if ord(c) not in cmap))
                missing_punct = "".join(sorted(c for c in punctuation if ord(c) not in cmap))
                missing_latin = "".join(sorted(c for c in latin if ord(c) not in cmap))
                info = dict(path=entry["path"], family=font["name"].getDebugName(1),
                            style=font["name"].getDebugName(2), version=font["name"].getDebugName(5),
                            glyph_count=len(cmap), static="fvar" not in font,
                            missing_latin_digits=missing_latin,
                            fsType=font["OS/2"].fsType)
                if family["language"].startswith("zh"):
                    info.update(missing_script_han=missing_han, missing_prose_punctuation=missing_punct)
                assert info["static"] and not missing_latin, info
                # FreeType must actually be able to open and rasterize each file.
                raster = ImageFont.truetype(str(path), 32)
                assert raster.getmask("Abc 0123").getbbox()
                report["fonts"].append(info)
    for sample in SAMPLES:
        for language, key in (("en", "enfont"), ("zh", "zhfont")):
            cmap = cmaps[FONTS[sample[key]]]
            missing = "".join(sorted({c for c in sample[language] if not c.isspace() and ord(c) not in cmap}))
            assert not missing, (sample["title"], language, missing)
            report["samples"].append(dict(title=sample["title"], language=language, missing=missing))
    (ROOT / "script-characters.txt").write_text("".join(sorted(set(script) - set("\r\n\t"))), encoding="utf-8")
    write_json("validation.json", report)
    return report


def render_png():
    width, height = 1920, 2370
    im = Image.new("RGB", (width, height), "#1d2221")
    d = ImageDraw.Draw(im)

    def font(key, size):
        return ImageFont.truetype(str(ROOT / FONTS[key]), size)

    def text(x, y, value, key="sans", size=25, color="#292820"):
        f = font(key, size)
        d.text((x, y), value, font=f, fill=color, anchor="lt", spacing=13)

    text(70, 42, "紫手套  /  书面资料字体样张", "songbold", 48, "#f2e8d5")
    text(72, 115, "5 组风格 · 9 个字族 · 中英对应 · 原始字体实排", size=27, color="#c6b99f")
    text(72, 160, "字体只传达材料类型与书写语气；版次、签名和来源仍由物证核验。", size=24, color="#c6b99f")
    for idx, sample in enumerate(SAMPLES):
        top = 228 + idx * 337
        d.rounded_rectangle((58, top, 1862, top + 316), radius=10, fill="#eee3cc")
        d.rectangle((58, top + 47, 1862, top + 49), fill="#c6b896")
        d.line((960, top + 66, 960, top + 259), fill="#c6b896", width=1)
        text(87, top + 12, sample["title"], size=24)
        text(89, top + 65, sample["enname"], size=20, color="#706044")
        text(997, top + 65, sample["zhname"], size=20, color="#706044")
        en_size = 28 if idx == 1 else 38
        for lang, x, size, key in [("en", 90, en_size, sample["enfont"]), ("zh", 998, 35, sample["zhfont"])]:
            f = font(key, size)
            for j, line in enumerate(sample[lang].splitlines()):
                assert d.textlength(line, font=f) < 835, (idx, line)
                text(x, top + 112 + j * 49, line, key, size)
        text(89, top + 278, sample["note"], size=21, color="#675b48")
    text(70, 2270, "样张用途：风格与字形对比。英文非密码文句为排版试译，中文祷文为示意译文。", size=23, color="#c6b99f")
    text(70, 2312, "实际尺寸请打开 preview.html 调整字号；最终仍需在游戏机位、光照与像素采样下复核。", size=23, color="#c6b99f")
    im.save(ROOT / "font-pairings.png")


def render_html():
    faces = "\n".join(f"@font-face{{font-family:'{key}';src:url('{path}');font-weight:400;font-style:normal;font-display:block}}" for key, path in FONTS.items())
    cards = []
    for sample in SAMPLES:
        pair = []
        for lang, key, name in (("en", "enfont", "enname"), ("zh", "zhfont", "zhname")):
            pair.append(f'<section><p class="fontname">{html.escape(sample[name])}</p><div lang="{lang}" class="sample" data-face="{sample[key]}" style="font-family:{sample[key]},song" contenteditable="true" spellcheck="false" aria-label="{html.escape(sample[name])} 可编辑样张">{html.escape(sample[lang])}</div></section>')
        cards.append(f'<article><h2>{html.escape(sample["title"])}</h2><div class="pair">{"".join(pair)}</div><p class="note">{html.escape(sample["note"])}</p></article>')
    page = '''<!doctype html>
<html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>紫手套 · 字体对照样张</title><style>
__FACES__
*{box-sizing:border-box}body{margin:0;background:#1d2221;color:#f2e8d5;font-family:sans,sans-serif;font-synthesis:none}
main{max-width:1640px;margin:auto;padding:42px 30px}h1{font-family:songbold;font-weight:400;font-size:42px;margin:0 0 16px}
.intro{line-height:1.8;color:#c6b99f} .controls{display:flex;gap:24px;align-items:center;flex-wrap:wrap;position:sticky;top:0;z-index:2;background:#1d2221;padding:16px 0;border-bottom:1px solid #56584e}
input,button{font:inherit}button{background:#eee3cc;border:0;border-radius:4px;padding:8px 14px;cursor:pointer}input{vertical-align:middle}output{display:inline-block;min-width:60px}
article{background:#eee3cc;color:#292820;border-radius:7px;margin-top:24px;overflow:hidden}h2{font-size:20px;font-weight:400;border-bottom:1px solid #c6b896;margin:0;padding:16px 24px}
.pair{display:grid;grid-template-columns:1fr 1fr}.pair section{padding:18px 24px;min-width:0}.pair section+section{border-left:1px solid #c6b896}.fontname{font-size:15px;color:#706044;margin:0 0 18px}
.sample{font-size:var(--size,32px);line-height:1.6;white-space:pre;overflow-x:auto;min-height:180px;font-variant-ligatures:none;font-feature-settings:'liga' 0,'clig' 0,'dlig' 0,'hlig' 0;outline-offset:4px}
.note{font-size:15px;line-height:1.7;color:#675b48;margin:0;padding:14px 24px;border-top:1px solid #d2c5a9}a{color:#d9bf8a}
#status{font-size:14px;color:#c6b99f}footer{margin-top:28px;line-height:1.8;color:#c6b99f;font-size:15px}
@media(max-width:850px){main{padding:24px 14px}.pair{grid-template-columns:1fr}.pair section+section{border-left:0;border-top:1px solid #c6b896}h1{font-size:30px}}
@media print{body{background:white;color:black}.controls{display:none}article{break-inside:avoid}.sample{font-size:22px}main{padding:0}}
</style><main><h1>紫手套 / 书面资料字体样张</h1>
<p class="intro">五组字体搭配，覆盖六类资料用途。点击任一样张可输入自己的文句；拉动字号比较清晰度。<br>本页直接读取同目录内字体，可离线打开。英文非密码文句为排版试译，中文祷文仅为示意译文。</p>
<div class="controls"><label>字号 <input id="size" type="range" min="18" max="48" value="32"> <output id="value">32 px</output></label><button id="reset">恢复原文</button><span id="status">正在检查本地字体…</span></div>
__CARDS__
<footer>书本取字行固定且不自动换行；窄屏可横向滚动。中文译文不参与英文取码。<br>本页是排版样张，未模拟 Unity 的光照、距离或 640×360 内部分辨率。<br>字体来源、授权与校验记录：<a href="manifest.json">manifest.json</a> · <a href="validation.json">validation.json</a> · <a href="../../doc/ph-font-plan.md">选型方案</a></footer></main>
<script>
const samples=[...document.querySelectorAll('.sample')], originals=samples.map(x=>x.textContent);
const slider=document.getElementById('size'), value=document.getElementById('value');
slider.addEventListener('input',()=>{document.documentElement.style.setProperty('--size',slider.value+'px');value.textContent=slider.value+' px'});
document.getElementById('reset').addEventListener('click',()=>samples.forEach((el,i)=>{el.textContent=originals[i]}));
const faces=[...new Set(samples.map(el=>el.dataset.face))];
Promise.all(faces.map(async face=>{try{const loaded=await document.fonts.load('32px "'+face+'"');return loaded.length>0}catch{return false}})).then(result=>{
const failed=faces.filter((_,i)=>!result[i]); document.getElementById('status').textContent=failed.length?'字体加载失败：'+failed.join('、'):'本地字体已加载 · '+faces.length+' 个样张字面';
});
</script></html>'''
    (ROOT / "preview.html").write_text(page.replace("__FACES__", faces).replace("__CARDS__", "\n".join(cards)), encoding="utf-8")


if __name__ == "__main__":
    report = inspect_fonts()
    render_png()
    render_html()
    write_json("specimens.json", SAMPLES)
    for font in report["fonts"]:
        print(font["path"], "missing Han:", font.get("missing_script_han", "N/A"), "missing punctuation:", font.get("missing_prose_punctuation", "N/A"))
    print("Validated", len(report["fonts"]), "static fonts and", report["script_han_count"], "script Han characters.")
