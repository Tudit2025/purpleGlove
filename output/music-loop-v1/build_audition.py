"""Local audition page, ending-sequence previews and a Chinese edit index."""
from pathlib import Path
import html
import json
import subprocess
import wave
import numpy as np

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]
tracks = json.loads((HERE / "manifest.json").read_text())["tracks"]
roles = {"phoenix": "第一、二幕调查；第四幕自述揭晓前", "anabasis": "第三幕军情核验与来访压力",
         "unraveling": "第四幕自述揭晓后与第五幕选择", "hiraeth": "开门／缺页", "undertow": "真相易手／背书"}
cards = []
for track in tracks:
    ident = track["id"]
    ending = "intro" in track["clips"]
    if ending:
        blocks = {}
        for part, clip in track["clips"].items():
            with wave.open(str(PROJECT / clip["path"]), "rb") as source:
                blocks[part] = np.frombuffer(source.readframes(source.getnframes()), dtype="<i2").reshape(-1, 2)
        sequence = np.concatenate([blocks["intro"], blocks["loop"], blocks["loop"], blocks["outro"]])
        subprocess.run(["ffmpeg", "-y", "-v", "error", "-f", "s16le", "-ar", "48000", "-ac", "2", "-i", "pipe:0",
                        "-c:a", "libmp3lame", "-b:a", "192k", str(HERE / "audition" / (ident+"_ending_sequence.mp3"))],
                       input=sequence.tobytes(), check=True)
    clip = track["clips"]["loop"]
    seams = track["audition_seams_seconds"]
    safe = html.escape(track["title"])
    cards.append(f'''<article><h2>{safe}</h2><p>{roles[ident]} · 循环 {clip['seconds']:g} 秒</p>
<label>游戏用循环主体（重复播放）<audio controls loop preload="metadata" src="../../../{clip['path']}"></audio></label>
<label>接缝试听（包含两次接缝）<audio id="{ident}" controls preload="metadata" src="{ident}_two_seams.mp3"></audio></label>
<p>接缝位于 {seams[0]:g} 秒与 {seams[1]:g} 秒。
<button onclick="hear('{ident}',9)">从第一处接缝前 3 秒听起</button>
<button onclick="hear('{ident}',{seams[1]-3})">从第二处接缝前 3 秒听起</button></p>
{('<label>完整结构演示：前奏一次 → 主体两遍 → 结束段<audio controls preload="metadata" src="'+ident+'_ending_sequence.mp3"></audio></label>') if ending else ''}
<p><a href="{track['page']}" target="_blank" rel="noopener">作者原曲页面</a></p></article>''')
page = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>紫手套 · 循环配乐试听</title><style>
body{max-width:860px;margin:40px auto;padding:0 20px;background:#161418;color:#e5dccb;font:16px/1.7 system-ui,sans-serif}
h1,h2{font-weight:600}article{background:#242029;border:1px solid #463b49;border-radius:12px;margin:24px 0;padding:20px 24px}
label{display:block;margin:15px 0}audio{display:block;width:100%;margin-top:8px}button{padding:8px 12px;margin:5px;border:1px solid #8e768e;border-radius:6px;background:#45364c;color:#fff;cursor:pointer}
a{color:#d2b0df}small{color:#bbafbd}</style><h1>紫手套 · 循环配乐试听</h1>
<p>五首免费配乐的游戏剪辑版。可以直接听循环接缝，也可以试听两首尾声曲目的完整结构。</p>
<p>文件已通过接缝、响度与 Unity 播放检查；乐句衔接和情绪是否合适，仍需实际试听确认。这里使用音频文件本身的音量，游戏中另有低音量播放和对话压低。</p>
''' + "\n".join(cards) + '''<small>Music by Scott Buckley — CC BY 4.0 · www.scottbuckley.com.au<br>
本游戏版本经截取、循环交叉淡化及响度调整。<a href="https://creativecommons.org/licenses/by/4.0/">许可</a></small>
<script>const players=[...document.querySelectorAll('audio')];players.forEach(a=>a.addEventListener('play',()=>players.forEach(b=>{if(a!==b)b.pause()})));
function hear(id,time){const a=document.getElementById(id);a.currentTime=time;a.play()}</script></html>'''
(HERE / "audition/index.html").write_text(page, encoding="utf-8")

rows = ["| 曲目 | 用途 | 前奏 | 循环主体 | 结束段 |", "|---|---|---:|---:|---:|"]
for t in tracks:
    durations = [f"{t['clips'][p]['seconds']:g} 秒" if p in t['clips'] else "—" for p in ["intro", "loop", "outro"]]
    rows.append(f"| {t['title']} | {roles[t['id']]} | " + " | ".join(durations) + " |")
doc = '''# 《紫手套》循环配乐接入

已用五首免费曲目完成首版，共 9 个立体声 PCM WAV。全部原曲来自 Scott Buckley 官方下载，使用 CC BY 4.0，成品经过截取、循环交叉淡化和固定增益调整。Candlelit Monastery 未加入。

[打开本地试听页](../output/music-loop-v1/audition/index.html)可试听五首循环及两首尾声结构；也可使用[接缝试听播放列表](../output/music-loop-v1/audition/all-loops.m3u8)。

## 音乐与剧情

''' + "\n".join(rows) + '''

第零幕门外保留环境声，进入第一幕后音乐淡入。音乐跟随当前剧情进度，打开旧卷宗或切换桌面／门口视角不会倒退音乐。第四幕只有自述明文真正留存后才进入 Unraveling。第四幕结束到第五幕沿用同一首，不重新起播。

第六幕按最终结局进入 Hiraeth 或 Undertow：前奏只播一次，主体持续循环。结束淡出开始后，在下一个循环边界接入结束段；结束画面显示期间可继续收束，最多等待一个循环周期，不阻塞剧情或开门动画。若剧情在前奏结束前已结束，则直接接结束段。结束段只播放一次。

## 游戏内播放

音乐系统只在 `PH_DeskRoom` 自动建立，退出或重新加载场景时销毁旧声源。无需重建主场景，也没有改写现有剧情流程。

- 配置：[StoryMusic.asset](../Assets/Resources/PHMusic/StoryMusic.asset)。可在 Unity Inspector 调整曲目、音乐音量和淡化时长。
- 默认音乐音量 0.4；曲目切换淡化 4 秒；对话时乘 0.65、文件交接时乘 0.5，平滑恢复。雨声和操作声保持独立。
- 打开调试控制台、窗口失焦或应用暂停时，音乐记住播放位置；恢复后重新安排剩余前奏、循环及待播结束段。静音不会清空进度。
- 循环片段使用 `AudioSource.loop`；前奏和结束段使用 DSP 时钟排程。按样本数计算时长，避免逐帧重新播放造成空隙。
- 资源为 48 kHz、16-bit、双声道 PCM WAV；Unity 使用 Streaming + PCM。原曲来源为 MP3，转成 WAV 是为了编辑和明确循环长度，不代表恢复无损音质。

两路音乐交叉淡化，每路包含前奏、循环、结束段三个声源。尾声的音乐署名位于黑幕与结局标题之上的独立 Canvas，完整来源说明还会随游戏打包。

## 核对与试听

音频已通过 72 项文件与信号检查：9 个片段格式、时长、校验值、余量、五处循环接缝、两组前奏／结束段连接，以及循环附近的音量变化。Unity 实际运行验证包含五种配乐状态、16 种结局处置组合、回看旧卷宗、静音、交接压低、暂停恢复、真实 DSP 循环／结束和场景重新加载。

循环区间依据音高分布与响度包络相似度选取，保留 2–4 秒交叉淡化。接缝检查通过不等于完成主观听审；当前版本仍需按试听页确认乐句重叠、旋律重复感和游戏实际混音。每首原曲截取位置、处理参数与试听接缝时间见[编辑清单](../output/music-loop-v1/manifest.json)。

- [音频检查结果](../output/music-loop-v1/validation.json)
- [Unity 检查结果](../output/music-loop-v1/unity-validation.txt)
- [源码及复现说明](../output/music-loop-v1/README.md)

## 署名与来源

''' + "\n".join(f"- [{t['title']}]({t['page']}) — Scott Buckley。" for t in tracks) + '''

作者[使用说明](https://www.scottbuckley.com.au/library/using-this-music/)允许署名使用和改编；[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)要求保留署名、许可链接并注明修改。游戏结束画面与 [MUSIC_CREDITS.txt](../Assets/StreamingAssets/MUSIC_CREDITS.txt) 已加入这些信息。来源原文件的下载链接及 SHA-256 见 [sources.json](../output/music-loop-v1/sources.json)。
'''
(PROJECT / "doc/ph-music-loops.md").write_text(doc, encoding="utf-8")
print("Created audition/index.html, ending previews and doc/ph-music-loops.md")
