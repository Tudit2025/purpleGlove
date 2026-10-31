# 对话与测试控制台

主场景：`Assets/Art/DeskSceneV1/Scenes/PH_DeskRoom.unity`。进入 Play Mode 后按 **F1** 或 **反引号** 打开控制台，点击对话或封口袋收件按钮。也可以输入 `play arrival` 或 `deliver 1` 后回车。

## 播放与操作

- **空格、回车、鼠标左键**：打字中显示完整的一句；打字完成后进入下一句。最后一句之后结束事件。
- **F1 / 反引号**：打开或关闭控制台；控制台打开时暂停文字和发言音效。Esc 只关闭控制台。
- 对话开始时放回正在查看的物件，打开探视窗并锁定看门机位；WASD、鼠标视角偏移、物件点击和翻书都不能打断对话。
- 结束、测试中止或禁用对话组件后，恢复进入对话前的机位与探视窗开合状态；最后一次点击不会落到物件上。
- 平时不自动播放测试对话。控制台默认在编辑器和 Development Build 中可用；正式构建可在 `DialogueTestConsole.enableInReleaseBuild` 中显式开启。

屏幕始终只有一个台词面板：卢卡位于下三分之一，其他角色位于中三分之一。面板显示角色名、当前一句及操作提示。场景和眼睛保持像素采样，文字通过独立 UI 摄像机以屏幕分辨率绘制，保留中文笔画。整句先排版，再逐字显现，隐藏的后缀仍保留位置，避免换行跳动。中文、代理对和组合字符按完整文本元素推进，标点处略作停顿。

## 控制台命令

| 命令 | 功能 |
|---|---|
| `help` / `list` | 命令帮助 / 事件清单 |
| `play arrival` | 播放指定事件；当前对话未结束时拒绝重入 |
| `deliver 1`～`deliver 5` | 触发对应幕的封口袋收件；落桌后解锁并展示该幕文件 |
| `next` | 补全当前句，或前进一句 |
| `stop` | 中止当前对话或收件；收件中止不解锁文件，恢复进入事件前的机位与活门端点 |
| `speed 32` | 将打字速度设为每秒 32 个文本元素，范围 8–60 |
| `speed 0` | 恢复各角色默认速度 |
| `volume 0.75` | 发言音量 0–1；0 为静音，不影响环境雨声 |
| `clear` | 清空控制台记录 |

↑ / ↓ 可以浏览命令历史。控制台同时保留最近 50 条记录和 50 条命令。

## 封口袋收件事件

控制台的五个「第 N 幕 · 收件」按钮分别对应五幕；已收件按钮显示「已收」。这些入口直接测试收件，不要求先完成前一幕，也不会自动推进幕次。

1. 开始时放回正在检视的物件，锁定看门视角，停止鼠标视角偏移与 WASD／Esc 切换。活门若朝内，先空转到门外，再装入带蜡封的文书袋并转入房间。
2. 转动完成后，唯一可悬停和点击的场景物件是封口袋。悬停沿用桌面物件向镜头轻移的效果；探视窗、活门、书架、笔记本和工具盒均不可操作。
3. 左键点击封口袋后，袋子沿弧线飞向灰色文书桌垫，镜头随流程转回桌面。袋子落桌并短暂停留后消失，才解锁该幕原有文书并按既定位置展开；上一幕桌面文件收入书架。
4. 收件结束后恢复正常操作。已解锁的幕不能重复收件，可以从书架取回。F1 仍可打开测试控制台；控制台阻止袋子点击并暂停飞行动画，正在转动的活门会完成当前机械行程。关闭控制台的同一次点击／按键不会穿透到场景。

收件与对话互斥。转动、等待点击或飞行时用 `stop` 中止，或禁用收件组件，均会收回袋子、恢复原机位和活门端点，不解锁文件。没有自动触发测试收件。

主摄像机上的 `SealedDeliveryController` 提供 `DeliverAct(int)`（UnityEvent）、`BeginDelivery(int)`（返回成功与否）、`Stop()`、`deliveryCompleted(int)` 与 `deliveryCancelled(int)`。`ActArchiveController.requireDelivery = true` 且 `previewAllActs = false` 时，包括第一幕在内的文件都等待收件；幕末归档本身不开放下一幕。直接收件不会改变 `CurrentAct` 或谜题状态。

`Purple Glove > Events > Install Sealed Deliveries in Desk Room` 安装或修复场景引用，完整场景构建也包含此步骤。封口袋复用已有活门纸包网格、蜡封材质和纸张／落桌音效，没有新增外部素材。验证脚本与运行截图见 `output/delivery-validation/`。

## 人物设置

所有配置均为 `Assets/Data/Dialogue/Speakers` 中的独立资产，可在 Inspector 中修改。颜色适配深色台词底板，并用角色名辅助辨识。

| 角色 / ID | 文字颜色 | 字/秒 | 音高倍率 | 低通 Hz | 最小敲击间隔 | 眼部设计 |
|---|---|---:|---:|---:|---:|---|
| 卢卡 `luca` | 暖纸白 `#F3DFAC` | 24 | 1.06 | 6200 | 75 ms | 第一人称主角，不在窗后显示 |
| 行会执事 `clerk` | 银灰 `#C8D5D9` | 22 | 0.96 | 4300 | 90 ms | 平直浓眉、榛色眼睛，沉稳疲惫 |
| 贝阿特丽切 `beatrice` | 灰绿 `#90D5B0` | 28 | 1.24 | 7600 | 65 ms | 锐眉、橄榄绿眼睛，警觉克制 |
| 孔蒂公爵 `duke` | 铜金 `#E4AF7F` | 17 | 0.69 | 2100 | 120 ms | 低灰眉、深眼窝，威严审视 |
| 夏尔 `charles` | 浅蓝 `#9CC9F0` | 23 | 1.13 | 8700 | 80 ms | 微挑眉、蓝眼与雀斑，谨慎观察 |
| 卡拉法 `carafa` | 淡紫 `#D2A4E6` | 21 | 0.86 | 3300 | 100 ms | 不对称细眉、琥珀眼与眼角纹 |
| 马泰奥 `matteo` | 灰紫 `#B9BFF0` | 16 | 0.77 | 2700 | 130 ms | 银眉、灰蓝眼与深纹，结局预留 |
| 巴尔托洛 `bartolo` | 麦金 `#E5C889` | 26 | 1.34 | 5100 | 70 ms | 浓眉、圆睁棕眼与墨迹，仅素材测试 |

每人轮换四个现有 `Assets/Audio/PH/Typewriter/Short` 样本，避免连续重复；音高、低通滤波、节奏与响度共同形成角色变体。每次敲击另有 ±2% 音高变化，三个声音槽允许短尾音重叠。空白和标点不触发敲击，补全台词不会补播音效，打开控制台和结束台词会停止音效。没有新增音频来源，授权沿用 [音效素材库](ph-audio-library.md)。

## 七组试演事件

| 事件 ID | 内容 | 句数 |
|---|---|---:|
| `arrival` | 行会执事说明封门、收件编号和调档规则 | 6 |
| `beatrice` | 贝阿特丽切引出第七号封档与隐去的来源 | 6 |
| `charles` | 执事介绍夏尔，讨论具名否认与证据复核 | 6 |
| `pressure` | 公爵以师父签名施压，卡拉法要求代领原件 | 8 |
| `archive` | 卢卡与执事核对共同封存和有效回执 | 6 |
| `matteo` | 获救之后师徒对话，结局预留测试 | 4 |
| `bartolo` | 印刷匠素材试音，明确标记为预留测试 | 4 |

这些事件用于验证表现和节奏，未写入谜题、文件交付、幕推进或结局状态。马泰奥不会因试播而被判定获救；巴尔托洛正篇“不露面”的设定保持在 [修订剧本](ph-revised.md) 中。

## 素材与事件接线

- 当前眼部素材为 `Assets/Art/Dialogue/Textures/visitor_*.png`：使用内置 **image_gen** 在原眼部横条上补全额头、面颊和头部外缘，保留透明背景。补图用于覆盖探视窗以外的区域，游戏中由门板遮挡，不展示整张脸。完整编辑提示词见 [image-prompts.json](../output/visitor-eyes-validation/image-prompts.json)；原始 `eyes_*.png` 横条仍保留。
- 贴图以 Point / Clamp / 无 Mipmap / 无压缩 / 保留 Alpha 导入。`VisitorEyes.shader` 按 96 × 144 网格采样，将原 192 × 288 网格的每 2×2 像素合成一个；使用环境光和场景烛光的漫反射，并接收门框阴影，没有自发光和高光。材质的 `Diffuse tint` 可整体调节亮度，`Silhouette alpha cutoff` 控制透明轮廓。
- 门后另有 `Visitor reflected corridor light`，用很弱的暖色点光近似走廊烛光的反射，使背对烛火的眉眼仍能辨认。仅访客出现时启用，结束或禁用访客组件时一起关闭。
- `Pixel visitor eyes` 位于已有的 `Visitor eye line - 1.66m` 锚点下，尺寸为 0.25 × 0.375 米，宽高均比原 0.20 × 0.30 米放大 25%，向走廊后退 0.40 米（世界 Z = 4.04）。上下图像边缘藏在门板后，透明侧边露出走廊，没有碰撞体。
- NPC 说话时换成其眼部图像；卢卡回应时保留当前访客；结束时隐藏。
- `Create > Purple Glove > Dialogue > Speaker / Event` 可以新建人物和事件。一个 `Line` 填一句话；事件含访客、敲门音、到场等待、台词顺序和测试标记。
- 将事件加入主摄像机 `DialogueController.events`，从现有 UnityEvent 调用 `PlayEvent(string id)`，或代码调用 `Play(DialogueEvent)`。`eventCompleted` 只在正常读完时触发，`eventCancelled` 用于中止。
- `Purple Glove > Dialogue > Install in Desk Room` 可重新安装引用；已有角色和事件资产不会被重置。完整场景构建器也包含安装步骤。
  重新安装时，仍使用内置旧眼部横条的角色会迁移到补全贴图；手动指定的其他人物贴图保持原有引用。

## 验证

验证脚本为 [DialogueValidation.cs](../output/dialogue-validation/DialogueValidation.cs)，运行于独立 Unity 项目副本。覆盖存盘重开、重复安装、中文字符、所有事件、机位与交互锁、对话中止和禁用清理、控制台设置、声音调度、文字分区和 Unicode 逐字边界。结果及实机渲染截图保存在 `output/dialogue-validation`。

眼部尺寸与光照调整另见 [VisitorEyesValidation.cs](../output/visitor-eyes-validation/VisitorEyesValidation.cs) 和 [验证结果](../output/visitor-eyes-validation/validation.txt)：七位访客均已实机渲染，验证透明贴图引用、门板遮住上下边缘、人物与反射光同步隐藏，并复查全部对话事件。`visitor-*.png` 是正常看门机位，`detail-*.png` 是同一站位放大后的检查图，不改变游戏机位。主工程素材及场景与测试副本的一致性记录在 [delivery-check.json](../output/visitor-eyes-validation/delivery-check.json)。

本次人脸放大 25%、人脸 2×2 像素合并及墙门像素细分的复验见 [验证结果](../output/visitor-pixel-validation/validation.txt) 与 [交付一致性](../output/visitor-pixel-validation/delivery-check.json)：Unity Play Mode 中 1230 项断言通过，覆盖七位访客、门板遮挡、墙门材质密度及所有对话事件。预览见 [正常看门机位](../output/visitor-pixel-validation/01-npc-middle.png) 和 [门窗细节](../output/visitor-pixel-validation/detail-clerk.png)。

音效已验证调度与角色参数，最终响度和听感仍需在实际输出设备上试听。
