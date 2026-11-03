# 《紫手套》低多边形桌面场景 v1

以项目 `output/imagegen/ph-desk-v1.png` 为构图参考，将桌面方案搭建为真实 Unity 三维场景。适配 Unity 2022.3.62f2 / Built-in。所有网格为本项目程序构建，无外购模型依赖。

## 打开与查看

主菜单为 `Scenes/PH_MainMenu.unity`，采用走廊侧墙与门的构图，主标题“紫手套”、副标题“明文的谎言”。打开该场景后点击 Play 即可预览；“开始游戏”淡出进入第零幕独白，再开门进入第一幕。菜单提供设置、退出及中英文切换；设置可调总音量、切换全屏及像素化，Esc 返回，语言和设置会保留。画面保留低分辨率场景采样、像素化标题、清晰菜单文字、雨声及微弱灯光变化，适配 16:9、4:3 和超宽窗口。`Purple Glove > Scene > Build Main Menu` 可从桌面场景的门廊重新生成主菜单；验证与实际截图见 `output/main-menu-validation/`。

对话正文、回应选项、主菜单主副标题及章节／结局标题使用 `PixelText` 字形像素采样，保留原有字族。1080p 下对话、副标题和章节标题使用 2 px 颗粒，主标题使用 3 px 颗粒；其他分辨率取整到实际屏幕像素。主菜单“设置 → 像素化”同时切换场景与上述文字，关闭后恢复平滑字体；文书、解密和普通菜单控件沿用清晰文字。运行时自动接入，无需重新生成场景。验证与截图见 `output/pixel-text-validation/`。

解密桌垫采用上方书写、下方操作的布局：明文纸位于上半部分，五种解密工具共用下半部分，工具标题位于两区之间。

主菜单与游戏镜头默认开启柔和暗角，四角逐渐变暗，中心亮度保持原样。在相机的 `PixelSceneView > Vignette` 中可开关效果，`Vignette Intensity` 调整强度（默认 0.45），`Vignette Smoothness` 调整渐变范围（默认 0.75）。暗角在像素放大后绘制，关闭像素采样或进入文书阅读／解密视角时仍生效；屏幕界面文字不受影响。

第一至第四幕的完整正确明文会自动留存为该幕文件，首次完成时卢卡说“完成了。把这份明文留好。”。第二、四幕各两段明文分别保存；重复输入不复制文件，第五幕不生成明文。每幕草稿独立保留，留存纸可随卷宗收纳、取回与检视；详见 [明文完成与留存](../../../doc/ph-dialogue-console.md#明文完成与留存)。明文书写字号增大约 62%，按字宽换行并显示最近三行，完整内容仍保留。

两本《祷文集》、师父笔记和工具箱在首次点击时各触发一句卢卡的回忆独白，镜头保持原位。按空格、回车或左键读完后，自动继续查看或开箱；每件物品在本次游玩中只介绍一次，中途取消则下次重播。台词与新增物品的接入方式见 [物品首次互动回忆](../../../doc/ph-dialogue-console.md#物品首次互动回忆)，检查结果及画面见 `output/object-memory-validation/`。

第一幕开场后递送棕、白两个叠放包裹，沿用一次点击接收整批的操作。落桌后展开两张纸：带冠徽卷草页眉、页脚的公爵委托，以及较小、老旧、带三个实体裁口的师父密文纸。委托说明卡拉法代领权限与军情今晚稍后送达，字体沿用预选公文组合；首次拿起旧纸时，卢卡回忆三个裁口代表凯撒位移三。可用 `Purple Glove > Acts > Install First Act Details` 单独更新，验证见 `output/first-act-validation/`。

第一幕解读并留存明文后，五秒响起敲门声，完成提示读完后进入书记官门边交谈。结束后回到桌面，播放卢卡的三句幕末总结，再进入第二幕转场、贝阿特丽切介绍与封口袋收件；点击封袋落桌后才展示第二幕材料。`FirstActFlow` 管理顺序与取消恢复，回看旧幕不重复触发。`Purple Glove > Acts > Install First Act Progression` 可单独安装；F1 章节面板可重开任意幕，窗口失焦／控制台暂停计时。详见 [第一幕自动推进](../../../doc/ph-dialogue-console.md#第一幕解读后的自动推进)，验证见 `output/first-act-flow-validation/`。

1. 打开 `Scenes/PH_DeskRoom.unity`，切到 Game 视图并选择 16:9。
2. 点击 Play：**W / A / S / D** 分别沿上／左／下／右方向切换相邻机位，与屏幕边缘箭头共用跳转关系；该方向没有相邻机位时不切换。在主桌面按 **A** 看左侧书架，按 **W** 或点击档案垫空白处进入档案俯视，按 **D** 进入右侧解密俯视；在柜子视角按 **D** 返回主桌面，在档案俯视按 **D** 进入解密俯视、按 **S** 返回主桌面。档案机位在垫子中心上方 1.45 米，解密机位在锚点上方 1.68 米，两者均为 90° 正俯视、视野角 48°，不随鼠标偏转；滚轮向前降低镜头、向后恢复全景，重新进入时重置高度。解密视角内 **WASD 不切换机位**，**Esc** 返回中桌面视角；其他机位可用 **Esc** 返回桌面。屏幕边缘箭头平时淡显、悬停高亮：桌面 **↑ 档案俯视 / ← 柜子 / → 解密俯视**，柜子 **→ 桌面**，解密 **← 档案俯视**，档案俯视 **→ 解密俯视 / ↓ 桌面**。解密视角左下角显示“Esc 返回中桌面视角”，其他机位保留“WASD 切换视角”。检视、书写、拖拽、工具转移、对话及收件时锁定镜头导航；箭头点击不会穿透到场景物件。切换位置、朝向与视野角时平滑过渡。主菜单“设置 → 像素化”控制场景像素采样，选择会保存并在菜单、游戏场景间沿用；D 解码机位默认使用屏幕原分辨率。普通桌面鼠标偏转默认左右各最多 6°、上下各最多 4°，书架减半。相机只在预设机位间切换，不提供自由行走。
3. 默认按 960×540 采样后放大。书本、笔记本、纸张与信封悬停时抽出，点击后拉到镜头前转正；**Esc / 右上角 X** 放回。检视期间锁定镜头；带文字的物品使用屏幕原分辨率渲染，保证中文与密码数字可读。
4. 左侧书架从上到下对应第一幕至第五幕，侧面有一只小烛台，书架与卷宗不显示文字说明。五幕文件等待封口袋收件后解锁：按 **F1** 点击对应幕收件按钮，或输入 `deliver 1`～`deliver 5`。镜头锁定看门，袋子经活门转入后可悬停、点击；点击后飞回桌面，落下消失并展开该幕证据文书，操作表收入桌面立夹。已收幕的卷宗可从书架取回，同时收起前一幕。第一幕公爵委托与会长来信并排、师父密文纸置于下方，新增档案转交申请表默认留在立夹；第三幕译录委托、带法方签名的法文原文与军情密文放在桌垫上，双签名栏核验表收入立夹。其余幕证据按桌垫自身的坐标与朝向排布，纸张四角均留在缝线内；桌面没有收起按钮。立夹中的操作表仅在点击交互时展开，退出归位，不参与幕间书架收纳。左侧师父笔记、普通版与七瓣花版《祷文集》（祷言集）为公共物品，不参与章节收纳。雨声与烛光连续运行。
5. Build Settings 以 `PH_MainMenu` 为第 0 个场景，`PH_DeskRoom` 为第 1 个场景，打包后从主菜单启动。编辑器打开主菜单后 Play 从主菜单开始；显式打开桌面场景仍可直接 Play 调试。`DeskSceneStartup` 不强行关闭当前编辑的场景，菜单 `Purple Glove > Scene > Configure Startup Scenes` 可恢复场景顺序和 Play 入口。
6. 门口视角由对话／收件事件触发，FOV 为 **28.5°**（原 38° 缩小 25%），不能用 WASD 或普通 `SetView` 自由进入。对话结束或收件中止后返回此前机位；收件成功后回到桌面展示文件。收件等待期间仅封口袋可交互，飞行期间锁住场景操作。门上窄窗与下方传物舱保留原有机关接口。
7. 师父笔记放在桌垫左侧，下沿朝向摄像机。点击拿起后，**点击屏幕左／右箭头、左／右书页**，或按 **A / D、左／右方向键**翻页；**Esc / X** 放回。翻页箭头沿用切换镜头的边缘位置、淡显与悬停高亮，首页隐藏左箭头、末页隐藏右箭头，翻动中箭头变暗且不接受连续翻页。左下角显示翻页／放回提示，右下角显示当前页码与总页数。共 7 个对页、14 页，包含三段旧记录、六种密码教案及完整猪圈图形表。翻页有实体纸张转动及现有翻页音效；首尾不循环，放回、再次拿起及切幕均保留本次运行中的页码。检视期间 A / D 只翻页，书本放回后恢复镜头导航；对话、控制台和其他模态操作期间暂停翻页，关闭它们的当帧输入也不会翻动书页。
8. 点击桌面木盒打开盒盖，五件工具按实际外形放大平铺：左侧上下放转盘和猪圈表，右侧并排放滑尺、仿射表和栅栏纸条，盒内尺寸约为旧版的 1.6～2.6 倍。直接点击工具或对应槽位取出，自动进入 D 机位；更换时先收回旧工具再取出新工具。点击工具上的皮革拉片或在空处右键归盒。盒盖、盒外空处、右键或 Esc 都能合盒。按住凯撒盘内盘沿圆周拖动，松手按字母刻度定位；圣西尔尺在下行窗口左右拖动，限制在 0–25 格。点击尺下方纸签填写关键词，点击左右黄铜销逐位对齐。猪圈表取出后直接对照实体图形，键盘输入明文，无需点击纸张或设置工具。仿射表为 26 列 × 2 行，点击表下方的 A/B 填参数；栅栏纸条为 41 列 × 2 行，取出后直接输入密文，每次键入或退格即按当前长度将前半排上行、后半排下行，奇数时上行多一个；按 Enter 后誊写明文，点击纸条继续修改密文。支持粘贴、Delete 清空和 Ctrl+A 替换，忽略空格标点，最多 82 个字母。明文纸不再提供点击后使用羽毛笔直接输入的入口，继续接收键盘解密落字和拖放木片；羽毛笔仍用于填写工具密钥及表格。仿射表上行固定 A–Z，填写有效 A/B 后自动生成下行密文；两行字母均不可手填或拖放修改。A 为 1–25 内与 26 互质的数，B 为 0–25；Tab 切换参数，Enter 从 A 转到 B、再结束填写。纸面按放大后的字宽换行，显示最近三行，完整草稿保留在 `Plaintext`。Esc／右键先落笔或取消拿取，再次 Esc 返回桌面。设置与草稿在本次运行内保留；来访、收件、控制台和窗口失焦会结束操作并归还手中木片。
9. 字母盘位于工具垫后方，默认采用 `QWERTYUIOP / ASDFGHJKL / ZXCVBNM` 三行错位布局，每格叠放三片独立木片，共 78 片。拖动顶部木片到表格、关键词纸签或明文纸，在纸面留下字母后归盘，可反复使用；拖到桌垫最前沿可摆放字母，右键木片归位，无效落点自动归盘。木片使用已有橡木、盘底使用胡桃木与皮革，沿用米制 UV、32 texels/m 像素采样、描边与场景光照。D 机位滚轮向前靠近工具查看细字，向后回到完整解密区；书写和拖动物件期间锁定滚轮，重新进入 D 机位恢复全景。

10. 取出并配置工具后，可直接敲实体键盘解密：凯撒盘至少转动过一次；圣西尔滑尺填好非空密钥；仿射表填好 A/B 后按 y=(Ax+B) mod 26 自动生成对应关系，上行为明文、下行为密文。按玩家当前配置生成对应关系，字母盘三层木片一起重排，小号按键标记留在原 QWERTY 位置；木片本身的字母身份保持不变。按 A–Z 输入密文，对应明文字母木片飞到明文纸落字后归盘，可连续输入及重复按键。圣西尔逐字循环密钥，空格、标点与换行不推进密钥，退格删除刚输入的字母时同步回退密钥位置。编辑密钥、仿射参数或栅栏密文时按 Enter 完成填写，再开始键盘解密或誊写明文；点击明文纸只结束工具信息编辑，不进入明文输入；配置有效时后续按键仍走解密。配置无效、工具归盒时恢复原字母盘。猪圈表取出即启用直接明文输入，字母盘保持原布局，按键字母原样落到明文纸，支持空格、标点、换行和退格；栅栏工具只即时排布输入的密文，玩家自行按列上下读出并誊写明文。解码键盘生效时 P 用于输入字母，像素化仅通过主菜单设置切换；Esc 及原有输入锁仍有效。`CipherWorkbenchKeyboard.cs` 管理映射、木片重排和落字动画，验证与截图见 `output/keyboard-decode-validation/`。仿射参数输入由 `CipherWorkbenchAffine.cs` 管理；`Purple Glove > Scene > Install Affine Parameters` 可单独更新已保存场景，验证及截图见 `output/affine-parameters/`。栅栏逐字排布由 `CipherWorkbenchRail.cs` 管理；`Purple Glove > Scene > Install Rail Cipher Input` 可单独更新场景，验证及截图见 `output/rail-live-input/`。

11. 章节测试：按 **F1** 或 **反引号** 打开面板，选择第零幕至第六幕，从该幕开场重新开始。每次跳转重载场景并准备此前的原件、明文与签名；目标幕仍须正常介绍、收件和解密。第六幕提供“开门／缺页／真相易手／背书”四个预设。可以在演出、收件或结局黑屏时再次跳转；Esc 关闭面板后继续演出。旧命令输入与独立事件试播已移除。详细规则见 [章节测试](../../../doc/ph-dialogue-console.md#跳转到各幕开始)，验证见 `output/chapter-jump-validation/`。
12. 语言切换：F1 面板顶部的“语言 / Language”可在中文与 English 之间即时切换，不重置进度；选择保存在 PlayerPrefs，跳转章节、重新进入 Play Mode 后仍然有效。中文始终是资源、场景和代码中的原文；`Localization`（`Assets/Scripts/Presentation/Localization.cs`）在显示时按中文原文查 `Resources/PHLocalization/English.json`，先整段、再逐行匹配。对白、选项、案头待办、视角提示、字母盘状态与幕名转场在显示时取英文；对白逐字速度在英文下加倍。纸面与物件上的 `TextMesh` 在载入场景时登记，切到英文后按原中文所占的宽度与行数等比缩小字号，切回中文时原样恢复；第五幕表单、核验表、回执等运行时写入的文字经 `Localization.SetText` 写入，笔记本由 `NotebookBook` 自行重排。编辑模式下始终显示中文，构建工具不会把英文写入场景。英文表与中文润色稿由 `output/gemini-polish/build_localization.py` 从润色包生成，英文验证与中英对照截图见 `output/gemini-polish/validation/`。

`Purple Glove > Scene > Install Cipher Toolbox` 可重复安装／更新解密区，保留文书、书架和房间。`CipherWorkbench` 与 `CipherWorkbenchInput` 管理实物射线拾取、工具取放和纸面输入；工具本身没有屏幕按钮、滑条、面板或输入框，屏幕边缘机位导航由 `DeskViewHud` 管理。`DeskCipherWorkbenchBuilder.cs`、`DeskCipherToolboxBuilder.cs`、`DeskLetterTrayBuilder.cs` 维护模型和布局。`Purple Glove > Scene > Arrange Cipher Toolbox Contents` 只更新盒内工具、分隔和点击区域，保留桌面其他物件的位置；完整安装也会使用此布局。盒内放大保持工具比例与材质纹理密度，取出后仍使用原有工作尺寸。布局截图和点击／取放验证见 `output/toolbox-layout-validation/`。`Prefabs/PF_LetterTray_QWERTY.prefab` 为整盘，`PF_LetterTile_A.prefab` 至 `PF_LetterTile_Z.prefab` 为各字母独立样例；每片都有 `CipherLetterTile.alphabetIndex`、`AnimationPivot`，盘中实例另有 `homeSocket`，便于后续动画绑定。`CipherWorkbench.letterSamples[0..25]` 始终按 A–Z 索引。独立样例不引用场景中的归位点；需要自动归盘时显式指定 `homeSocket`。验证脚本、结果与渲染截图见 `output/physical-cipher-validation/`；目前以 Play Mode 合成指针／按键验证，尚未进行系统鼠标和中文输入法手动测试。

相机上的 `DeskScenePreview > Mouse Look` 可调整鼠标跟随：`Follow Mouse` 开关、`Horizontal Mouse Angle` / `Vertical Mouse Angle` 最大偏转角、`Mouse Dead Zone` 中心静止区（默认每个半屏的 5%）、`Mouse Response Curve` 响应曲线，以及 `Camera Follow Speed` 旋转跟随速度（越大越快，0 为立即到位，同时影响 WASD 机位切换）。曲线横轴为离开静止区后到画面边缘的归一化距离 0–1，纵轴为最大偏转角的比例 0–1，默认缓入缓出；自定义曲线输出会限制在 0–1 内。鼠标离开相机画面或窗口失焦时回正。

`Table focus (W)` 配置桌垫引用、俯视中心及高度和视野角，默认偏移 `(0, 1.45, 0)`、视野角 `48°`；X/Z 是相对桌垫的俯视中心，Y 是全景高度。滚轮高度限制为全景的 60%～100%，与解密区一致。`Purple Glove > Scene > Configure Table Focus View` 可更新已保存场景的配置。`View.MatFocus` 保持枚举值 3，兼容已有场景。`DeskViewHud` 的 `Arrow Opacity` / `Arrow Hover Opacity` 调整箭头淡显及高亮透明度。现有 `DialogueController` 通过 `SetDialogueLock(true/false)` 进入／结束门口事件；入口会收回正在检视的物品，并恢复座位位置，避免从俯视高度看门。本次导航验证和截图位于 `output/camera-navigation-validation/`。

## 幕次归档接入与模拟

桌面烛台旁新增未点燃的备用白蜡堆，三根在下、一根在上。进入第一至第五幕分别显示 **4 / 3 / 2 / 1 / 0** 根，先用上层；第五幕烛台仍保持照明。沿用米制 UV、32 texels/m 像素采样、描边和场景光照，无拾取、额外灯光或数字标签。`ActCandlePile` 接收 `PrologueSequence` 黑场准备／进入幕次事件与 `ActArchiveController.actReceived` 收件事件；取消转场恢复原数量，重复收件、收纳及翻看旧卷宗不补回蜡烛。`Purple Glove > Scene > Install Act Candle Pile` 可更新已保存场景，完整场景构建也包含它。独立资产为 `Prefabs/PF_CandlePile_Unlit.prefab`，验证与截图见 `output/candle-pile-validation/`。

`07 Five act archive` 上的 `ActArchiveController` 管理五组资料。初始 `DeskAct = 0`，本场景开启 `requireDelivery`、关闭 `previewAllActs`，包括第一幕在内都需收件解锁。主摄像机 `SealedDeliveryController.DeliverAct(幕号)` 可绑定剧情 UnityEvent；袋子落桌后调用 `ReceiveAct` 开放并展示对应幕文件。剧情在幕末调用 `EndAct(幕号)` 或 `CompleteCurrentAct()`；需要返回是否成功时用 `CompleteAct(幕号)`。只接受当前幕的一次完成，归档不自动解锁下一幕。`actCompleted` 事件提供完成的幕号。

`ShowActOnDesk(幕号)` 只展示已解锁的幕，不改变剧情进度；`StoreDeskAct()` 收起当前资料。仅做文书预览时可开启 `previewAllActs`，此时无需收件。新的来件在其预定桌面位置生成后，用 `RegisterDocument(幕号, SceneObject)` 加入已解锁幕；保存和交换的是原物体，批注、输入内容与原桌面位置均保留。`sharedDocuments` 显式排除三件公共参考物，也拒绝重复归属。这里的归档与收件状态存于本次运行中，尚未接入存档系统。

运行时选中控制器，在 Inspector 点击“模拟结束当前幕并归档”，或使用 `Purple Glove > Acts > Simulate End of Current Act`。编辑器模拟不改写场景初始进度。五幕分别配置 4 / 3 / 5 / 4 / 3 张可检视文书，共19张，其中申请表与核验表跨幕收在立夹。第二幕两张调档材料、第三幕夏尔补充凭条延后交付；第一至第四幕解译后还可加入6张明文留存纸，自述明文在第四幕留存时立即进入立夹，成为第三份主动文件。所有文书按来源幕次登记，实际收纳位置由文书类别及剧情阶段决定。内容来自 `doc/ph-revised.md`。猪圈字符使用固定几何笔画，师父长密文保持全部82个字母。

`Purple Glove > Scene > Install Operational Document Rack` 可单独安装立夹规则。`DeskOperationalDocument` 管理申请表、核验表及师父自述明文；点击页签时原纸平铺至书写垫并对齐机位，Esc / X 退出后归位，控制台、收件或资料切换同样妥善收回。`FifthActDocument` 在第五幕提供来源担保、原件去向和自述焚毁的纸面操作。旧版七份表格的验证保留在 `output/operational-rack-validation/`，当前三份主动文件与完整交卷流程见 `output/final-acts-validation/`。

中文纸面按 `doc/ph-font-plan.md` 区分字体：公爵委托等正式公文使用思源宋体 CN；师父笔记、私人附语与卢卡明文留存纸的中文使用霞鹜文楷；旧印件、台账、目录及两版祷文集封面使用朱雀仿宋。坐标、独立密文与字母网格保留清晰字体。`Purple Glove > Scene > Apply Chinese Document Fonts` 可更新字体与封面，保留文书内容、幕次和桌面布局；完整构建及各文书重建入口也沿用这些字体。检查与实机渲染位于 `output/chinese-font-validation/`。

六份谜题原件的字母与数字密文统一使用 EB Garamond、96 点字形和 `0.002642202` 字符尺寸（原最大密文字号 × 120%），猪圈几何符号同步放大 20%。凯撒与维吉尼亚按纸面宽度换行，书本坐标每行三组；碎纸与数字便条相应扩宽、加高。栅栏原件为 1.44 × 0.06 米的单行长纸条，收到后横放桌面前沿，可检视、归位并随卷宗取回。解密镜头在原件可见时为其保留完整范围及底部导航空间，适配缩放和窄屏。`Purple Glove > Scene > Apply Unified Cipher Typography` 可更新现有场景，各幕重建也沿用相同排版；验证与截图见 `output/cipher-readability/`。

`Purple Glove > Scene > Build Act Documents` 重建文书并保存场景，保留房间、夜空、书架和公共参考物。纸面版式在 `DeskActDocumentBuilder.cs` 中维护；验证脚本、运行记录和阅读截图见 `output/act-documents-validation/`。这版用于文书摆放与阅读预览：尚未接入填写、剧情逐件投递、可比对的手写签样及回执结算，不提前填入解谜答案或生成成功回执。

`Purple Glove > Scene > Arrange Desk and Add Notebook Pages` 在现有场景内重排文书、移除旧收起按钮并设置笔记翻页。笔记内容与模型配置在 `DeskNotebookBuilder.cs`，翻页逻辑在 `NotebookBook.cs`，边缘箭头与提示由 `DeskViewHud.cs` 根据当前检视书本显示。旧记录和教案从开场即可全部翻阅，不随剧情自动改写；两版祷文集已制作第二幕使用的第 12、13 页。桌垫边界、笔记摆向、鼠标翻页、动画和保留页码的验证与截图见 `output/notebook-validation/`；翻页导航、输入阻挡及镜头箭头回归检查见 `output/book-navigation-validation/`。

雨声在层级中的 `Rain ambience - indoor loop` 上配置。`SceneAmbience` 的 `Volume` 默认 0.8，控制雨声的独立音量；`Fade Seconds` 默认 1.25 秒；勾选 `Muted` 会平滑静音，取消后淡入。`AudioSource` 的音量由此组件管理，使用现有 10 秒双声道室内闷雨素材与 2D 播放，不随相机转向衰减。窗边雨与室内雨同源，因此只播放室内这一条。禁用组件或卸载场景时停止播放，重新启用时从静音淡入。

## 窗外夜空

`08 Exterior night sky/NightSky_Sphere` 是包围房间的夜间天空球，包含冷蓝地平线、缓慢移动的薄云、稀疏星点和淡色弯月。天空沿用像素采样，窗格的原深蓝色遮挡面改为微弱冷色透明玻璃，木框与铅条保持原样。

天空材质 `Materials/M_Desk_NightSky.mat` 可调整天空与云层颜色、云量、移动速度、星光亮度以及月亮方向和大小。天空球不参与灯光、阴影或物件拾取，室内照明与雨声配置保持原值。`Purple Glove > Scene > Add Night Sky` 可更新已保存的桌面场景；完整构建也会生成天空。渲染截图与检查记录位于 `output/night-sky-validation/`。

## 门上机关与门外走廊

`DoorLeaf` 保持原门的位置、左侧底部枢轴与外形尺寸。上方窄窗是实际贯通的 0.54 × 0.16m 开口，中心离地 1.66m；活动板沿上铰链向室内翻起 108°。门板与碰撞网格都绕开窗口和下方舱口，打开后可看见门外实体墙面。`Door exterior - dim corridor/Visitor eye line - 1.66m` 是来访者双眼对齐点，朝向室内。对话访客使用约 0.20m 宽的透明头部贴图，后移到走廊内并接受场景光照；额头和面颊补图的上下边缘被门板遮住。章节测试和人物配置见 [对话与测试控制台](../../../doc/ph-dialogue-console.md)。

下方传物舱采用圆托盘与中间隔板，绕竖轴在 0° / 180° 两端往返，承载物固定在 `TransferDrumPivot/CargoSocket - outside at rest` 上。初始关闭眼窗，封口袋在收件事件开始时装入。`09 Sealed document delivery` 保存袋子与落桌锚点；纸包本身不计入证据或归档资料，收取后展示该幕原有文件。安装、章节测试及事件接线见 [对话与测试控制台](../../../doc/ph-dialogue-console.md)。

两个固定交互框均使用 `SceneObject`（悬停位移为 0）及 `DoorMechanism`，沿用相机拾取逻辑。`Activate()` 切换端点；`MoveTo(false/true)` 分别关闭／打开眼窗，或把货物转到门外／门内。`IsMoving` 防止重入，`reachedFirstStop` / `reachedSecondStop` 可绑定剧情交接事件。传物舱的 `TryLoadCargo(Transform)` 装入一个受脚本控制、无独立物理运动的小物体，`ReleaseCargo(Transform destination = null)` 在停止后移交原物体；两者保留物体身份，转动期间不允许装卸。请把货物尺寸控制在约 0.25 × 0.13m 的托盘半区内。当前未实现桌面物品拖入舱口或剧情自动收取。

门外补齐灰泥墙、木梁、石地板、顶板与壁烛，复用室内材质、米制 UV、像素采样及 Layer 8 光照。壁烛与门框微弱反光保持走廊昏暗。机关使用现有 `Trapdoor` 音效，随门的位置进行 3D 播放。

`Purple Glove > Scene > Add Visitation Door and Corridor` 可在已保存的桌面场景中更新门和门外走廊，保留桌面、书架、侧投递口和机位；完整构建也会生成这些结构。验收截图与记录位于项目根目录 `output/door-mechanism-validation/`。

## 资产内容

`Prefabs/` 内提供基础桌面资产，以及字母盘和 A–Z 独立木片样例：

- 木书桌、皮质书写垫。
- 炭黑／酒红七瓣花／苔绿三本合起的书、打开的笔记本。
- 密文信纸、空白答题纸、羊皮纸、打开的信封、红蜡封信封。
- 点燃的黄铜烛台及白蜡、按幕减少的四根备用白蜡堆、三支独立彩蜡。
- 黑玻璃墨水瓶、黄铜砂盒、羽毛笔、裁纸刀。
- 收件盘、带圆圈／叉／加号的三格信架、原件封存匣。
- 五层资料书架、可交互的幕次卷宗。
- 门扇、门框、投递口、窗框、灰泥墙板、木梁、石地板模块。

几何含倒角、切面法线、纸张折面与厚度、页层、木板缝、蜡滴、瓶口内壁、铁件与独立装饰。实际数量与三角形数以项目根目录 `output/desk-scene-v1/asset-report.txt` 为准。

`Meshes/*.asset` 是持久化的 Unity Mesh，可独立复用。`output/desk-scene-v1/obj/` 同时提供每个 Prefab 的 OBJ、MTL 与所用基础色贴图，可导入 Blender 等建模软件继续调整。OBJ 按右手坐标导出，米制；不包含 Unity 字体文字、灯光、脚本、碰撞、Shader 像素合并与描边或完整 PBR 响应，完整效果以 Prefab 为准。

## 桌面手摇铃

`Prefabs/PF_Handbell.prefab` 为可复用手摇铃：空心黄铜铃身、深色木柄和独立铃舌，约 16.6 × 28.5 × 16.6 cm。它位于笔记本后侧的空位，沿用桌面米制 UV、32 texels/m、像素材质、描边、灯光和悬停拾取。`Purple Glove > Scene > Install Desk Handbell` 可重复安装；保留场景实例的位置、剧情开关及事件绑定，完整场景构建也包含它。

- 默认点击：0.55 秒弧线拿起，卢卡原地自言自语“现在不需要用这个”；空格／回车／左键读完后，0.6 秒放回原位。每次不满足条件都会说，不作为一次性初见回忆。
- `DeskHandbell.SetRingAllowed(true)` 开启剧情条件（也可在 Inspector 勾选 `Ring Allowed`），之后点击拿起、摇动 1.4 秒，再放回；默认关闭，具体剧情节点由调用方决定。运行时 `TryInteract()` 可触发同一流程并返回是否成功启动，不绕过对话、收件、书写等输入锁。
- 摇动三次往返，六次铃舌敲击同步轮换三条铃声；`rang` 在首次实际发声时触发一次，`returnedToDesk` 在正常落桌后触发，适合分别接呼叫反馈和后续演出。需要一次性呼叫时勾选 `Consume Permission On Ring`。普通模式可随时撤销权限，首声前撤销会走默认独白，首声后撤销则停止后续敲击并放回。
- 动作期间锁住物品和视角操作；Esc 平滑放下，外部事件／控制台中断及组件禁用会立即归位并释放输入。失焦暂停动作和声音。`GripPivot` 控制手腕摇摆，`ClapperPivot` 控制铃舌；所有拿放均按本次交互的原始位置恢复，避免悬停导致累积漂移。
- 声音位于 `Assets/Audio/PH/Handbell`，由已留存的 Kenney CC0 金属铃录音制作；拿放复用 `Interactions/object_pickup_01.wav` 与 `object_setdown_01.wav`。独白复用卢卡现有逐字发声效果。`output/handbell-validation/build_audio.py` 可离线重制铃声，来源、处理参数与校验值在同目录清单中。

实际画面、[带声音的动画预览](../../../output/handbell-validation/handbell-preview.mp4)和验证记录位于 `output/handbell-validation/`：专项 195 项检查、剧情衔接回归 253 项检查通过。预览使用 Unity 实际渲染帧与按动作时点对齐的拟音；运行时声音由铃本体的 AudioSource 播放。动画由 `DeskHandbell` 的统一时间轴驱动，无额外 Animator 或物理刚体依赖。原始预览帧可由 `HandbellValidation.cs` 在隔离测试工程中重建，再运行 `build_preview.py` 合成视频。

## 尺度、材质与枢轴

- Unity 1 单位 = 1m。桌面高度 0.80m、宽 2.85m、深 1.80m。摆放用底部枢轴，Transform Scale 保持 1。
- 网格 UV 使用表面米制投影（1 UV = 1m）。材质采用 `PurpleGlove/Pixel Outlined`，`Texture pixel size multiplier = 4` 将原 128px 贴图按 4×4 格采样为 **32 texels/m，约 3.125cm/像素**。横纵颗粒各为初版的 4 倍，木纹等图案的整体尺寸保持原值。斜面 UV 基向量归一化；修改物体缩放后应同步校准 UV。
- 墙面、木梁和门的材质使用 `_Architecture` 后缀，`Texture pixel size multiplier = 2`，即 **64 texels/m，约 1.5625cm/像素**，把原来的一个像素拆成 2×2。桌面物件继续使用原材质；木纹和石纹的整体尺寸不变。
- 本场景 `Materials/` 中保存上一批 `MaterialLibraryV1` 的风格化变体，沿用原始颜色贴图与金属度、光滑度参数。
- 物件采用深墨色反壳描边，默认宽度为 540p 下 1.2px，纸张为 0.75px；实际输出宽度随画面高度成比例变化。材质中可调整 `Ink outline` 和 `Outline pixels at 540p`，宽度设为 0 可关闭。网格 UV3 储存平滑外扩法线以减少裂缝，原始切面法线继续用于低多边形光照。文字、细缝线和火焰不加描边。
- 场景基础灯光与三色环境光均为初版的 **50%**；烛焰、窗光、门缝与光晕同步减弱，反射贴图在补齐后墙的房间中重新捕获。书架侧面烛台与桌面烛台共用光源参数。桌面烛光波动按减半后的基础强度计算。这里调整光照输入，不对最终画面强行乘黑色滤镜。
- 木纹在水平板上沿 X、立面沿 Y。需要其他纹理方向时，在建模工具中旋转对应 UV。
- 门扇根枢轴为左侧铰链底端；封存匣子节点 `LidPivot` 位于后缘；投递口 `HatchPivot` 位于上缘，便于后续开合动画。
- 拾取道具使用简单 BoxCollider；墙、地面和桌子使用静态 MeshCollider。没有 Rigidbody 或移动碰撞系统。
- 场景包含五幕文书预览，密文按修订剧本保留，表格留待填写。公共笔记本可翻阅旧记录及六种教案；两版书已提供书本密码工具交互；可比对签样、蜡封与后续完整解谜流程仍待制作。
- 房间建筑使用 Layer 8 做场景内光照分离，暖光主要落在道具上、冷光维持背景可读性；当前工程无需修改层名称。合入其他已有项目时，应检查其 Layer 8 是否已有不同用途，并同步调整对应光源 Culling Mask。
- 书架和卷宗与桌面家具统一使用 Default 层，共用场景灯光，不使用专用补光。座位后墙复用其他墙面的灰泥与木梁材质，米制 UV、像素采样和描边规则相同。
- 中文 UI 使用随包分发的 `PHArchiveUI.ttf`，是已重命名的霞鹜文楷字形子集，授权见同目录 `PHArchiveUI-LICENSE.txt`。

## 文件、来源与复建

- `Scenes/PH_DeskRoom.unity`：组装后的场景。
- `output/desk-scene-v1/desktop.png`、`doorway.png`、`overview.png`：Unity 实际渲染截图。
- `output/desk-scene-v1/PurpleGlove_DeskScene_V1.unitypackage`：完整场景包，含依赖的材质贴图、两款字体、环境音与编辑器构建源码。
- 字体从现有素材库选用 EB Garamond Medium、Courgette、思源宋体 CN、霞鹜文楷与朱雀仿宋，未修改源文件；相应 OFL 授权随 `Fonts/` 分发。既有导出包为早期快照，新字体以当前场景及 `Fonts/` 目录为准。
- 环境音引用 `Assets/Audio/PH/Ambience/rain_room_muffled_loop_10s.wav`。来源与完整授权见原音效库 `Assets/Audio/PH` 附带文档和项目 `doc/ph-audio-library.md`；导出的场景包附所用音效的来源与授权文件。
- `Purple Glove > Scene > Build Desk Room V1` 可重新生成本网格库、Prefab、场景和导出文件，并更新启动配置。该操作覆盖本目录内的对应生成资产，请先把手工变体另存。构建工具不会在导入时自动运行。存在主菜单时保留主菜单、桌面场景的启动顺序，其余 Build Settings 条目排在二者之后。
- `Purple Glove > Scene > Refresh Room Surfaces and Lighting` 更新已保存场景的生成墙面、木梁、书架光照与反射环境，保留门窗、桌面道具及归档配置。

这是可运行、可继续编辑的场景与网格初版，已实现物件检视、三个固定视角及按幕归档交换。剧情、解谜、文书输入和跨运行存档仍需由后续游戏系统接入。

## 第二幕分批收件与调档

`Purple Glove > Acts > Install Second Act Progression` 更新第二幕的三件文书、祷文集工具及剧情接线。完整场景构建和相关文书重建也包含这套流程。

贝阿特丽切交谈后，第一封包只解锁书本密码便条。两本祷文集从检视物件变为破解工具：点击后自动进入破解区、打开第 12、13 页，便条移到旁边；书页只显示正文与页码，不印每页标题、装订日期或行号，按每页固定正文行序从一计数。直接键入明文，Esc 或离开机位、切换工具时恢复书与便条的原始位置。普通版坐标均有效，但得到不同字母；七瓣花版得到 `REQUEST CASE SEVEN`。全部字行和坐标由 `BookCipherText` 共用。

明文留存后，卢卡提示摇铃调档，读完才开放手摇铃。实际铃声触发一次调档请求，铃放回后开始门边谈话。关窗后等待六秒，再敲门说明档案来源；说明结束且关窗后送入第二包。第二包落桌才解锁整齐的公文表格台账和撕裂边缘的猪圈碎纸，原始签收页的调阅条件印在台账页脚。

`ActFiles.deferredDocuments` 保存稍后交付的文书，收纳与取回卷宗保留解锁边界；`SealedDeliveryController.BeginSupplement(2)` 复用传物舱和接收动画，不重复触发幕次收件或消耗备用蜡烛。明文记录的 `sourceDocument` 阻止未收件谜题提前留存。`SecondActFlow` 管理提示、摇铃、门边请求、等待、送达和中断恢复。流程组件的 `Stop()` / `Resume()` 支持第二幕，开控制台及失焦暂停计时。状态沿用现有架构，保存于本次运行中。

专项验证源码、结果及 Unity 渲染截图见 `output/second-act-validation/`：第二幕专项 298 项、第一幕至第二幕衔接回归 267 项通过。

第二幕猪圈密码 `LEFT GLOVE` 完成落字并留存后，`SecondActFlow` 自动平滑回到桌面，播放 `act2_summary`：卢卡怀疑替米兰代领材料的卡拉法参与了两次换版，但仍需等待原件查明具体经手行为。独白结束后淡出、显示第三幕幕名、淡入；黑场收纳第二幕文件和两张明文，并把备用蜡烛减至两根。淡入结束提交第二幕完成，再经 `act3_arrival` 介绍和封包接收解锁第三幕文件。

`Purple Glove > Acts > Install Second Act Ending` 可单独更新这段演出，保留文书布局及祷文集内容。中断黑场会恢复原卷宗与蜡烛，旧幕重读不会重复推进。`Stop()` / `Resume()` 覆盖独白、转场和第三幕收件。验证及实际渲染见 `output/second-act-ending-validation/`。

第三幕明文留存后，点击立夹的“核验签字”页签，将表展开至书写垫，再点击“明文还原签名”栏，表格归位后才进入夏尔门边对话；翻看旧幕资料时也可签署。关窗后单独接收夏尔补充凭条，归入第三幕；文件齐全后卢卡提示摇铃，实际摇响并放回后调档，再依次进入书记官、卡拉法门边对话和桌前幕末独白。随后将第三幕证据归档到书架，已签核验表继续留在立夹，换烛并转入第四幕。来源担保留到第五幕决定。`ThirdActFlow` 支持 `Stop()` / `Resume()`，重复阅读不再次推进；`Purple Glove > Acts > Install Third Act Progression` 可单独安装。第四幕由开幕控制器继续衔接。流程最初验证见 `output/third-act-flow-validation/`，立夹变更后的完整回归见上述新验证目录。

第四幕淡入后，`FourthActOpening` 自动播放 `act4_arrival` 门边介绍，说明行会和修道院两份封件的来源与内容。关窗后送入灰白、棕色两个并排且分别封缄的封包，点击接收落桌后展开商业文件委托签收页、换版记录、师父明文书信和两张独立横向密文纸条。两张纸条仅写单行密文，无标题、签名、编号或说明，仿射词间使用两个空格；信上明写 `A = 5    B = 8`。信件去掉标题、底部说明及“我原以为”至“伪案拆穿”一句。首次拿起仿射纸条播放 `memory_affine_strip`，保持检视并提示密码类型及信中密钥；本次游玩只播放一次。两段密文内容与答案不变，分别绑定自己的来源纸条。`Stop()` / `Resume()` 支持介绍及收件中断恢复；`Purple Glove > Acts > Install Fourth Act Opening` 可单独安装。纸条与拾取独白验证、渲染见 `output/fourth-act-strips/`，原开场验证见 `output/fourth-act-opening-validation/`。

第四幕两份明文均实际留存后，`FourthActFlow` 自动接桌前独白，读完后直接转入第五幕，已移除现状核实界面及其确认步骤。自述明文在第四幕完成留存时已立即进入立夹。`FifthActFlow` 接执事介绍与三封代表来信；玩家操作立夹中的核验表、转交申请和自述明文，随时摇铃交卷。关窗后原文件飞入传物舱、转门转出，实际交接完成才淡出并显示“第六幕 · 尾声”，淡入后接上结局演出。明文签名跨幕保留，焚毁或已转出的文件不会从旧卷宗重新出现。

`Purple Glove > Acts > Install Fourth and Fifth Act Progression` 安装上述流程并绑定尾声。第五幕此前的[验证结果](../../../output/final-acts-validation/validation.txt)与实际渲染保留在 `output/final-acts-validation/`；当前完整尾声验证见下文。跨运行存档尚未接入。

## 第六幕尾声

`Purple Glove > Acts > Install Sixth Act Epilogue` 可单独安装四个结局，保存的主场景已接入。`EpilogueFlow` 根据第五幕锁定的结果播放“开门”“缺页”“真相易手”“背书”，只有前两者区分自述是否焚毁。第六幕沿用原房间，五层卷宗柜保持五层，尾声回执单独落在书写垫上。

六点经探视口对话宣读结果，关窗后通过传物舱自动送入新回执；“开门／缺页”再经六点半时间卡、执事报明、关窗送入接回凭据，才轮到师父来到探视口。所有台词完成且探视口完全关闭后，前两种结局开启主门并同步淡出，后两种关门淡出，最终停在结局名与“终”。整个尾声保持输入锁，逐句对话可正常推进，F1 可选择任意幕重新开始，也可直接进入四种尾声预设。

专项 Play Mode 验证覆盖16种组合、第四幕至第五幕原有交互、第五幕交卷至第六幕转场、真实回执投递、探视口与主门时序、最后开门和淡出同步、暂停与中断恢复。源码、报告与 Unity 实际渲染见 [epilogue-validation](../../../output/epilogue-validation/validation.txt)。
