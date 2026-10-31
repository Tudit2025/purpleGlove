# 对话与测试控制台

主场景：`Assets/Art/DeskSceneV1/Scenes/PH_DeskRoom.unity`。进入 Play Mode 后按 **F1** 或 **反引号** 打开控制台，点击对话或封口袋收件按钮。也可以输入 `play arrival` 或 `deliver 1` 后回车。

门边交谈与收件采用默认 0.85 秒的缓入缓出镜头过渡，从当前机位连续调整位置、朝向与视野；主摄像机 `DeskScenePreview.doorwayTransitionSeconds` 可调时长。交谈时先等镜头到位，再完整播放观察窗向上翻开的动画，开窗结束才显示第一句。读完最后一句后，先合上观察窗，再结束事件并让镜头返回；关窗期间保留门边机位与输入锁。观察窗开合沿用 `DoorMechanism.travelSeconds`（默认 0.55 秒）与机械音效。`stop` 会立即中止对话、释放输入，窗板从当前角度动画恢复，镜头平滑返回原机位。

## 第零幕与幕间转场

按 **F1** 点击“第零幕 · 门槛”，或输入 `prologue`（也可用 `play prologue`）。镜头切到门外短廊，以工作间门为主体，播放 [修订剧本](ph-revised.md) 中第零幕的 11 句台词：卢卡独白、画外执事交代、卢卡确认进入。使用原有角色文字颜色、逐字速度和发言音效，执事不显示人物图像，也不打开探视窗。空格／回车／左键逐句推进，最后一句“让我进去”之后再次推进即开门。

门扇绕原有铰链向室内打开，停留片刻后淡出；全黑时关闭门并切回桌面，淡显“第一幕”，停留，再隐藏幕名、淡入第一幕桌面。默认淡出 0.8 秒、纯黑停顿 0.3 秒、幕名淡入／淡出各 0.45 秒、幕名停留 1.6 秒、画面淡入 1 秒。雨声与场景环境音继续播放，开关门使用现有木门音效。

`transition 1` 至 `transition 5` 可单独预览同一套“第X幕”转场。转场本身不结算谜题或解锁文件；首次进入第一幕会接上下述执事交谈和送件流程，第一幕资料在玩家接收包裹、包裹落桌后解锁，也可用 `deliver 1` 单独测试收件。控制台打开或窗口失焦时暂停演出时钟，`stop` 可在独白、开门和黑场任意阶段中止，恢复此前镜头与门的状态。演出期间锁定场景点击、WASD、滚轮与解密输入，控制台始终可用于中止。

`PrologueSequence.BeginPrologue()` 可绑定剧情 UnityEvent；`ShowAct(int)` 为幕间转场入口，淡入完成后触发 `actEntered(int)`。`ActTransitionOverlay.Begin(int, Action, Action)` 可复用到后续幕次，第一项回调只在全黑时执行，第二项回调在淡入完成后执行。安装菜单为 `Purple Glove > Dialogue > Install Prologue and Act Transitions`，完整对话安装也会接入；重复安装保留已编辑的台词资产与演出参数。预览脚本与结果位于 `output/prologue-validation/`。

## 物品首次互动回忆

旧版与七瓣花版《祷文集》、师父的旧笔记、解密工具箱各有一句卢卡的回忆独白。第一次点击时沿用当前镜头，显示卢卡的对话面板；读完后自动继续查看物品或打开工具箱。悬停不触发，桌面与解密俯视下的工具箱入口共用一次记录。每件物品在本次游玩中只介绍一次，切幕后保留记录；重新进入 Play Mode 后重置，尚未接入跨运行存档。中途用控制台 `stop` 取消，下一次点击会重播。

台词保存在 `Assets/Data/Dialogue/Events/memory_prayer_old.asset`、`memory_prayer_flower.asset`、`memory_notebook.asset`、`memory_toolbox.asset`。其他采用 `SceneObjectInteractor` 点击入口的物件可添加 `FirstInteractionMemory`，指定主摄像机的 `DialogueController` 与回忆 `DialogueEvent`；自定义交互入口可调用 `TryBegin(继续交互的回调)`。菜单 `Purple Glove > Dialogue > Install First Interaction Memories` 安装这四项配置，完整场景构建与对话安装也会接入；重复安装保留已编辑的台词与自定义事件引用。验证脚本、242 项 Play Mode 检查结果及截图见 `output/object-memory-validation/`。

## 播放与操作

- **空格、回车、鼠标左键**：打字中显示完整的一句；打字完成后进入下一句。最后一句之后结束事件。
- **F1 / 反引号**：打开或关闭控制台；控制台打开时暂停文字和发言音效。Esc 只关闭控制台。
- 来访对话开始时放回正在查看的物件，打开探视窗并锁定看门机位；物品回忆保持原机位。WASD、鼠标视角偏移、物件点击和翻书都不能打断对话。
- 结束、测试中止或禁用对话组件后，恢复进入对话前的机位与探视窗开合状态；最后一次点击不会落到物件上。
- 平时不自动播放测试对话。控制台默认在编辑器和 Development Build 中可用；正式构建可在 `DialogueTestConsole.enableInReleaseBuild` 中显式开启。

屏幕始终只有一个台词面板：卢卡位于下三分之一，其他角色位于中三分之一。面板仅显示当前一句台词，不显示人物名称或交互提示；正文使用面板内的完整区域并保留边距，空格、回车和鼠标左键仍可补全或推进台词。场景和眼睛保持像素采样，文字通过独立 UI 摄像机以屏幕分辨率绘制，保留中文笔画。整句先排版，再逐字显现，隐藏的后缀仍保留位置，避免换行跳动。中文、代理对和组合字符按完整文本元素推进，标点处略作停顿。

## 明文完成与留存

留存字条只显示英文明文与中文译文，去掉“明文留存”、来源名称、幕次及“卢卡译录”等补充标签。

第一至第四幕，完整明文匹配该幕谜题答案后，等待最后一片字母木片归盘，再自动生成可检视的明文留存纸，加入当前桌面所展示幕次的文件。每幕第一次完成时，卢卡通过现有对话面板说“完成了。把这份明文留好。”，保持当前镜头。第二幕的书本／猪圈、第四幕的仿射／栅栏分别留存，完成提示每幕只播放一次；第五幕不生成明文文件。

答案匹配忽略大小写、空格、换行和标点，但必须包含完整正确字母；残句、错字、多余字母或数字均不保存。后续明文既可替换草稿，也可接在已保存的答案后继续写。每个谜题只生成一张文件；文件按标准词间空格排版，并附剧本中的中文译文，后续修改草稿不会覆盖已经留存的记录。切幕时分别保留各幕草稿，归档与取回旧幕保留原文件实例和内容，状态限本次游玩。第一幕已接入下述自动推进，其他幕次仍由各自的剧情流程控制。

明文书写字号由 `0.0013` 调至 `0.0021`，约增大 62%；按实际字宽自动换行，纸面显示最近三行，完整草稿保留。木片落字按字体行高和字形位置对齐。主摄像机 `PlaintextArchiveController` 管理判定与留存；`Purple Glove > Acts > Install Plaintext Archives` 可更新现有场景，完整工具区构建亦会接入。完成独白位于 `Assets/Data/Dialogue/Events/plaintext_complete.asset`，验证脚本与画面见 `output/plaintext-archive-validation/`。

专项验证通过 476 项检查，涵盖六段答案、第五幕排除、分幕草稿、归档取回、去重、落字时序与新字号边界；另通过 953 项键盘解码回归。验证采用 Unity Play Mode 中的键盘／指针及帧时序回放。

## 控制台命令

### 第一幕入场后的交付

序幕进入第一幕，或用 `transition 1` 完成第一幕转场后，`FirstActFlow` 等待五秒，再完整播放执事敲门音效。声音结束后进入 `act1_arrival` 门边对话，执事说明白色包裹中的公爵核验委托，以及棕色包裹中由行会交付的师父寄存文件。最后一句读完，观察窗完全关闭后，自动接入原有第一幕送件事件。玩家仍需点击接收棕、白包裹，等包裹落桌后才能阅读文件。

计时从转场淡入完成开始；控制台或窗口失焦时暂停。`stop` / `continue` 支持中止和继续本流程；本次运行中重复预览第一幕或回看旧幕不重复交付。`play act1_arrival` 可单独试播对话，`deliver 1` 仍可直接测试送件。台词位于 `Assets/Data/Dialogue/Events/act1_arrival.asset`；`openingDelay` 默认为五秒。

### 第一幕解读后的自动推进

第一幕正确明文实际留存、最后一片字母木片归盘后，`PlaintextArchiveController.plaintextPreserved(1)` 启动 `FirstActFlow`。五秒后门口响起敲门声；若“完成了。把这份明文留好。”尚未读完，保留当前文字，待读完后再进入 `act1_secretary` 门边交谈，不重复敲门。当前演出采用剧本的“这是两件事”回应。交谈结束后关闭探视窗，镜头在 0.75 秒内回到桌面主机位，再以现有面板播放 `act1_summary` 的三句卢卡自言自语。

总结读完后播放第二幕转场。黑场中收起第一幕资料、备用蜡烛变为三根；淡入完成时正式归档第一幕，再播放既有 `beatrice` 介绍并启动第二幕封口袋收件。第二幕文书仍须点击封袋、落桌后才解锁，第一幕原件及明文保持同一份物体，随时可从书架取回。重复输入、修改草稿、回看旧幕均不重新触发这条流程。

控制台或窗口失焦暂停倒计时与演出。`stop` 中止自动流程并恢复当前机位／输入；转场尚未完成时会恢复第一幕材料和四根备用蜡烛，收件中止不解锁第二幕文书。`continue` 从被中止的阶段继续，已完成的归档不重复执行。状态限本次运行。`Purple Glove > Acts > Install First Act Progression` 可单独接入，完整场景及明文留存安装也会接入；重复安装保留已编辑台词、事件引用和时长。台词在 `act1_secretary.asset`、`act1_summary.asset`，书记官沿用现有书记员眼部和打字音素材，独立的 `ducal_secretary.asset` 可替换外观与声音。验证记录与截图位于 `output/first-act-flow-validation/`。

### 命令表

| 命令 | 功能 |
|---|---|
| `help` / `list` | 命令帮助 / 事件清单 |
| `prologue` / `play prologue` | 第零幕门外独白、开门、黑屏切至第一幕桌面 |
| `transition 1`～`transition 5` | 预览指定幕名的淡出、黑场、幕名、淡入 |
| `play arrival` | 播放指定事件；当前对话未结束时拒绝重入 |
| `deliver 1`～`deliver 5` | 触发对应幕的封口袋收件；落桌后解锁并展示该幕文件 |
| `next` | 补全当前句，或前进一句 |
| `stop` | 中止当前对话或收件；收件中止不解锁文件，恢复进入事件前的机位与活门端点 |
| `continue` | 从被中止的阶段继续第一幕至第二幕的自动流程 |
| `speed 32` | 将打字速度设为每秒 32 个文本元素，范围 8–60 |
| `speed 0` | 恢复各角色默认速度 |
| `volume 0.75` | 发言音量 0–1；0 为静音，不影响环境雨声 |
| `clear` | 清空控制台记录 |

↑ / ↓ 可以浏览命令历史。控制台同时保留最近 50 条记录和 50 条命令。

## 封口袋收件事件

第一幕 `deliver 1` 使用棕、白两个叠放包裹，点击任一可见包裹接收整批；悬停、飞向桌垫、落桌后解锁的流程相同。只展开公爵核验委托和师父旧密文纸两张文本。委托包含卡拉法代领范围及军情今晚稍后送达的说明，使用思源宋体与 EB Garamond，页眉、页脚带冠徽卷草。旧纸较小，有三个实体裁口，保留 `NHHS WKH RULJLQDO` 与“七瓣花，新版”提示；第一次拿起时卢卡回忆凯撒位移三，纸张持续保持检视，读完可继续阅读。第二至第五幕仍为单包裹。

菜单 `Purple Glove > Acts > Install First Act Details` 更新第一幕文书与包裹；完整文书构建及收件安装也包含对应配置。师父回忆台词在 `Assets/Data/Dialogue/Events/memory_three_cuts.asset`，`FirstInteractionMemory.duringInspection` 控制拿起后再介绍。检查脚本与画面位于 `output/first-act-validation/`：265 项不同断言通过，覆盖五幕收件、字体与文字边界、三个几何缺口、检视回忆的取消／重播；原有四件物品回忆的 242 项回归检查亦通过。

控制台的五个「第 N 幕 · 收件」按钮分别对应五幕；已收件按钮显示「已收」。这些入口直接测试收件，不要求先完成前一幕，也不会自动推进幕次。

1. 开始时放回正在检视的物件，锁定看门视角，停止鼠标视角偏移与 WASD／Esc 切换。活门若朝内，先空转到门外，再装入带蜡封的文书袋并转入房间。
2. 转动完成后，唯一可悬停和点击的场景物件是封口袋。悬停沿用桌面物件向镜头轻移的效果；探视窗、活门、书架、笔记本和工具盒均不可操作。
3. 左键点击封口袋后，袋子沿弧线飞向灰色文书桌垫，镜头随流程转回桌面。袋子落桌并短暂停留后消失，才解锁该幕原有文书并按既定位置展开；上一幕桌面文件收入书架。
4. 收件结束后恢复正常操作。已解锁的幕不能重复收件，可以从书架取回。F1 仍可打开测试控制台；控制台阻止袋子点击并暂停飞行动画，正在转动的活门会完成当前机械行程。关闭控制台的同一次点击／按键不会穿透到场景。

收件与对话互斥。转动、等待点击或飞行时用 `stop` 中止，或禁用收件组件，均会收回袋子、恢复原机位和活门端点，不解锁文件。没有自动触发测试收件。

主摄像机上的 `SealedDeliveryController` 提供 `DeliverAct(int)`（UnityEvent）、`BeginDelivery(int)`（返回成功与否）、`Stop()`、`deliveryCompleted(int)` 与 `deliveryCancelled(int)`。`ActArchiveController.requireDelivery = true` 且 `previewAllActs = false` 时，包括第一幕在内的文件都等待收件；幕末归档本身不开放下一幕。直接收件不会改变 `CurrentAct` 或谜题状态。

`Purple Glove > Events > Install Sealed Deliveries in Desk Room` 安装或修复场景引用，完整场景构建也包含此步骤。封口袋复用已有活门纸包网格、蜡封材质和纸张／落桌音效，没有新增外部素材。验证脚本与运行截图见 `output/delivery-validation/`。

## 人物设置

所有配置均为 `Assets/Data/Dialogue/Speakers` 中的独立资产，可在 Inspector 中修改。颜色适配深色台词底板，通过文字颜色、面板位置及发言音效辅助辨识角色。

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
| `beatrice` | 贝阿特丽切交付印坊便条，保留待解的封档编号 | 5 |
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

### 第二幕调档流程

使用 `Purple Glove > Acts > Install Second Act Progression` 安装。第一幕结束后自动接第二幕开场；贝阿特丽切不提前说出封档编号。第二幕首包只有数字便条，普通版及七瓣花版祷文集均可作为工具在破解区打开第 12、13 页。

正确明文 `REQUEST CASE SEVEN` 留存后，事件 `act2_ring_hint` 提示摇铃；提示读完才允许实际摇响。铃放回桌面后进入 `act2_request` 门边请求，关窗后等待六秒，再播放 `act2_archive_arrival`，随后送入台账摘要和猪圈碎纸。`stop` / `continue` 可中止及恢复提示、请求、等待、送档说明与收件；只播放测试对话不会绕过收件解锁。
