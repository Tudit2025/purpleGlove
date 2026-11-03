# 《紫手套》循环配乐接入

已用五首免费曲目完成首版，共 9 个立体声 PCM WAV。全部原曲来自 Scott Buckley 官方下载，使用 CC BY 4.0，成品经过截取、循环交叉淡化和固定增益调整。Candlelit Monastery 未加入。

[打开本地试听页](../output/music-loop-v1/audition/index.html)可试听五首循环及两首尾声结构；也可使用[接缝试听播放列表](../output/music-loop-v1/audition/all-loops.m3u8)。

## 音乐与剧情

| 曲目 | 用途 | 前奏 | 循环主体 | 结束段 |
|---|---|---:|---:|---:|
| Phoenix (2026) | 第一、二幕调查；第四幕自述揭晓前 | — | 84.25 秒 | — |
| The Weight of Air - Anabasis I | 第三幕军情核验与来访压力 | — | 48.5 秒 | — |
| Unraveling | 第四幕自述揭晓后与第五幕选择 | — | 54.5 秒 | — |
| Hiraeth | 开门／缺页 | 16.5 秒 | 48 秒 | 20 秒 |
| Undertow | 真相易手／背书 | 31.75 秒 | 48 秒 | 20 秒 |

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

- [Phoenix (2026)](https://www.scottbuckley.com.au/library/phoenix-2026/) — Scott Buckley。
- [The Weight of Air - Anabasis I](https://www.scottbuckley.com.au/library/anabasis-i/) — Scott Buckley。
- [Unraveling](https://www.scottbuckley.com.au/library/unraveling/) — Scott Buckley。
- [Hiraeth](https://www.scottbuckley.com.au/library/hiraeth/) — Scott Buckley。
- [Undertow](https://www.scottbuckley.com.au/library/undertow/) — Scott Buckley。

作者[使用说明](https://www.scottbuckley.com.au/library/using-this-music/)允许署名使用和改编；[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)要求保留署名、许可链接并注明修改。游戏结束画面与 [MUSIC_CREDITS.txt](../Assets/StreamingAssets/MUSIC_CREDITS.txt) 已加入这些信息。来源原文件的下载链接及 SHA-256 见 [sources.json](../output/music-loop-v1/sources.json)。
