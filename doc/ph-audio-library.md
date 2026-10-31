# 《紫手套》音效素材库 v1

已准备 **90 条 WAV 音效**，位于 [`Assets/Audio/PH`](../Assets/Audio/PH)。适配雨夜、烛光、旧木桌与纸质文书的场景：以短、干、偏暗的实物拟音为主，保留纸张和布料的细节。没有烘焙房间混响，方便后续按桌面、门口和室外分别混音。

先听 [62 秒分类试听](../output/audio-library-v1/audition.wav)，时间标记见 [试听索引](../output/audio-library-v1/audition-index.csv)。全部素材可通过 [播放列表](../output/audio-library-v1/all-sounds.m3u8) 逐条播放；[中文清单](../output/audio-library-v1/catalog.csv) 可用 Excel 打开。

## 素材内容

| 目录 | 数量 | 内容 |
|---|---:|---|
| `Wood` | 12 | 木材互撞：轻、中、重各 4 条 |
| `PaperBook` | 15 | 翻页 3、慢翻页 3、书本落桌 3、开合书 2、拿放信纸 4 |
| `Ambience` | 3 | 屋外雨 40 秒、窗边雨 10 秒、室内隔窗闷雨 10 秒；均已制作循环 |
| `Doors` | 9 | 木门开 2、关 3、重关 1，以及 3 种敲门节奏 |
| `Trapdoor` | 10 | 活板门／投递口开合各 3，独立铰链吱响 3，小锁扣 1 |
| `Bag` | 11 | 重物布袋提起、放下、落地各 3；拖动、翻找各 1 |
| `Interactions` | 14 | 木抽屉开合、投递信件、封存匣锁合、盖章、拆封、拿放小物件、确认与受阻反馈 |
| `Typewriter/Clean` | 8 | 保留机械回弹尾音的打字机单键素材 |
| `Typewriter/Short` | 8 | 同一批打字机录音裁出的约 110–140 ms 短敲击，供后续对话效果加工 |

90 条包含派生变体与复合拟音，使用了 56 个原始音频文件。慢翻页与普通翻页、打字机 Clean 与 Short 分别同源；活板门、布袋和部分操作声由不同素材叠加制作。每条成品的来源、处理参数、音量、时长和校验值均在 [manifest.json](../output/audio-library-v1/manifest.json) 中。

## 优先使用

| 场景动作 | 推荐文件 |
|---|---|
| 坐在桌前的主要环境底声 | `Ambience/rain_room_muffled_loop_10s.wav` |
| 第零幕门外短廊 | `Ambience/rain_outside_loop_40s.wav`，按廊道遮蔽程度调整音量 |
| 窗边或需要增强雨感时 | `Ambience/rain_window_loop_10s.wav` |
| 正常翻书 | `PaperBook/page_turn_01..03.wav` 随机轮换 |
| 提起／放回信件 | `PaperBook/paper_pickup_01..02.wav`、`paper_setdown_01..02.wav` |
| 门外来人 | `Doors/door_knock_pattern_01..03.wav`，一次触发完整节奏 |
| 投递口开合 | `Trapdoor/trapdoor_open_01..03.wav`、`trapdoor_close_01..03.wav` |
| 来信到桌面收发盘 | `Interactions/letter_deliver_01..02.wav` |
| 布袋有重量地落在桌面 | `Bag/bag_heavy_setdown_01..03.wav` |
| 后续制作逐字对话效果 | 从 `Typewriter/Clean` 加工，或以 `Typewriter/Short` 作为短音起点 |

与现有脚本一致：第一幕封门后，正门开关声只应出现在允许开门的剧情节点。普通文书交接使用侧面投递口的活板门声。布袋按用户要求作为备用道具素材准备。

## 音色与播放建议

- 同类交互优先随机轮换素材，并避免连续重复同一条。轻碰撞、纸张、布料可以加约 ±3% 的音高与少量音量变化。
- 环境雨声已比门、木碰撞更轻。对话出现时，可再将雨声压低约 3–5 dB；这些是初始混音建议，需要结合实际输出音量试听。
- 窗边雨与室内闷雨来自同一条录音，适合按机位切换或交叉淡化。长期叠加会强化相关频段，不建议默认同时播放。
- 雨声 WAV 的循环接缝已经处理。运行时设置 `AudioSource.loop = true`；启停时另做约 0.5–1.5 秒音量淡入淡出，不在循环文件首尾添加静音。
- 活板门关闭的主要落板声位于约 0.25 秒。动画事件应与落板接触对齐；需要不同动作速度时可使用独立的 `hinge_creak` 和 `trapdoor_latch` 层。
- 不必因像素画面而全局压成低采样率或重度 bitcrusher。当前素材通过适量降速和滤波取得旧木、厚纸的暗色质感；更强的失真可留给特定剧情或角色。

## 打字机与后续对话

原始 8 条录音完整保存在 [`sources/typewriter`](../output/audio-library-v1/sources/typewriter)，包括原采样率与声道。源录音为 22.05 kHz；导出成 48 kHz 便于统一工程规格，并不增加原录音的频率细节。

`Clean` 做了单声道化、轻度高低通、音量和边缘整理，保留按键与回弹；`Short` 只取约 110–140 ms 的主要敲击。两套都没有添加角色音高、机械变声或混响。后续仍可制作不同人物的语气和音色。

对话触发建议从以下设置起步：

- 使用 Short，约每 70–110 ms 最多触发一次，不要求每个字符都出声。
- 空格、标点、富文本标签不触发；跳过或瞬间补全文字时不要补播全部按键。
- 至少轮换 4–8 个样本，避免连续重复；音高随机幅度先控制在 ±2%。
- 可以保留 2–3 个播放声部，让短尾音自然重叠。低沉、沙哑等人物处理在后续对话系统中实现。

试听最后包含 Clean 轮播和 Short 连续播放示例，后者只演示节奏与重叠，并非最终对话效果。

## Unity 导入

文件统一为 **48 kHz / 24-bit PCM WAV**；动作音为单声道，雨声为双声道。随附 `.meta` 已配置：短音 `Decompress On Load + PCM`，雨声 `Streaming + PCM`，保留采样率和成品音量，不额外归一化。PCM 保留当前短音瞬态和循环接缝；之后可按目标平台评估压缩。

桌面交互可以先用 2D 或少量空间混合，门和投递口按物件位置使用 3D；雨声与对话建议各有独立的混音分组。导入选项参考 [Unity Audio Clip 文档](https://docs.unity.cn/2022.2/Documentation/Manual/class-AudioClip.html)。

主场景 `Assets/Art/DeskSceneV1/Scenes/PH_DeskRoom.unity` 已接入室内隔窗雨声：`Rain ambience - indoor loop` 上的 `SceneAmbience` 管理 1.25 秒开场淡入、循环和静音淡出，独立音量默认 0.8。使用 2D 双声道播放，桌面／门口机位切换不会打断或衰减雨声；未同时叠加同源窗边雨。场景生成脚本也保留此配置。其他动作音与对话音仍待对应交互、动画和剧情系统接入；实际响度与主观听感仍需在设备上试听。

## 来源与复现

所选来源均标明 CC0；Rain on Window 同时提供多种授权，本包选用作者明确提供的 CC0。

| 作者 | 素材来源 | 本包用途 |
|---|---|---|
| Kenney | [RPG Audio](https://kenney.nl/assets/rpg-audio) | 书纸、布料、门、铰链、锁扣、小物件 |
| Kenney | [Impact Sounds](https://kenney.nl/assets/impact-sounds) | 木材碰撞、软质重击 |
| Cassie-OrbitGames | [Typewriter sounds](https://opengameart.org/content/typewriter-sounds) | 8 个打字机录音 |
| alxl | [Rain on Window Loop](https://opengameart.org/content/rain-on-window-loop) | 窗边与室内雨声 |
| Ove Melaa | [Rain — 2 Versions](https://opengameart.org/content/rain-ambient-not-loopable-2-versions-available) | 屋外长雨声，经剪辑制成循环 |

[来源说明](../Assets/Audio/PH/SOURCES.txt)、原始音频、作者包内授权文本、来源页面快照和下载校验记录均已保留。获取日期为 2026-10-31。参考 [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)。

复现需要 Python、NumPy 和 PATH 中的 FFmpeg。已有 sources 时不需要重新下载：

```powershell
python output/audio-library-v1/build_library.py
python output/audio-library-v1/verify_library.py
```

`fetch_sources.py` 只用于重新获取公开来源。重建会覆盖本素材库中对应的成品文件与导入设置；手工调整前请另外保存版本。

## 检查结果

[检测报告](../output/audio-library-v1/validation-report.json) 已通过：90 条文件可解码，规格与清单一致；无削波、近静音文件或缺失来源，单次音效首尾为零，Unity GUID 无重复，3 条雨声通过接缝幅度检查。打字机短音以 60 ms、95 ms 间隔随机重叠的峰值均低于 -15 dBFS。

这些是文件与信号检查，不等同于人工听审或 Unity 场景混音验收。
