# 《紫手套》低多边形桌面场景 v1

以项目 `output/imagegen/ph-desk-v1.png` 为构图参考，将桌面方案搭建为真实 Unity 三维场景。适配 Unity 2022.3.62f2 / Built-in。所有网格为本项目程序构建，无外购模型依赖。

## 打开与查看

1. 打开 `Scenes/PH_DeskRoom.unity`，切到 Game 视图并选择 16:9。
2. 点击 Play：**A** 看左侧书架，**W** 聚焦文书桌垫，**D** 正俯视右侧解密区，**S / Esc** 回普通桌面。W 机位约 73° 向下看；D 机位在解密区锚点上方 1.68 米、90° 向下看、视野角 48°，同时容纳工具垫、字母盘与工具盒，不随鼠标偏转。切换位置、朝向与视野角时平滑过渡。解密区隐藏视角提示及全部额外屏幕 UI；其他机位保留小号“WASD 切换视角”提示。**P** 切换像素采样；D 解码机位默认使用屏幕原分辨率。鼠标轻微偏转默认左右各最多 6°、上下各最多 4°，书架减半，W 机位减至 20%。相机只在预设机位间切换，不提供自由行走。
3. 默认按 960×540 采样后放大。书本、笔记本、纸张与信封悬停时抽出，点击后拉到镜头前转正；**Esc / 右上角 X** 放回。检视期间锁定镜头；带文字的物品使用屏幕原分辨率渲染，保证中文与密码数字可读。
4. 左侧书架从上到下对应第一幕至第五幕，侧面有一只小烛台，书架与卷宗不显示文字说明。五幕文件等待封口袋收件后解锁：按 **F1** 点击对应幕收件按钮，或输入 `deliver 1`～`deliver 5`。镜头锁定看门，袋子经活门转入后可悬停、点击；点击后飞回桌面，落下消失并展开该幕文书。已收幕的卷宗可从书架取回，同时收起前一幕。文书按桌垫自身的坐标与朝向排成三列，纸张四角均留在缝线内；桌面没有收起按钮。左侧师父笔记、普通版与七瓣花版《祷文集》（祷言集）为公共物品，不参与章节收纳。雨声与烛光连续运行。
5. 本场景已设为 Build Settings 中启用的第 0 个场景，也是编辑器 Play 的默认入口。`DeskSceneStartup` 在脚本加载后恢复 Play 入口，不强行关闭当前正在编辑的场景。菜单 `Purple Glove > Scene > Set Desk Room as Default` 可重新设置。
6. 门口视角由对话／收件事件触发，FOV 为 **28.5°**（原 38° 缩小 25%），不能用 WASD 或普通 `SetView` 自由进入。对话结束或收件中止后返回此前机位；收件成功后回到桌面展示文件。收件等待期间仅封口袋可交互，飞行期间锁住场景操作。门上窄窗与下方传物舱保留原有机关接口。
7. 师父笔记放在桌垫左侧，下沿朝向摄像机。点击拿起后，**点击右页／左页**或按**右／左方向键**翻页；**Esc / X** 放回。共 7 个对页、14 页，包含三段旧记录、六种密码教案及完整猪圈图形表。翻页有实体纸张转动及现有翻页音效；首尾不循环，翻动中重复点击不打断动画。放回、再次拿起及切幕均保留本次运行中的页码。
8. 点击桌面木盒打开盒盖，直接点击五个实物槽位取出工具，自动进入 D 机位；更换时先收回旧工具再取出新工具。点击工具上的皮革拉片或在空处右键归盒。盒盖、盒外空处、右键或 Esc 都能合盒。按住凯撒盘内盘沿圆周拖动，松手按字母刻度定位；圣西尔尺在下行窗口左右拖动，限制在 0–25 格。点击尺下方纸签填写关键词，点击左右黄铜销逐位对齐。猪圈表直接阅读实体图形。仿射表为 26 列 × 2 行，栅栏纸条为 41 列 × 2 行；点击格子输入 A–Z，支持自动前进、方向键、Tab、粘贴和擦除。点击明文纸直接书写，羽毛笔移动到当前书写位置，纸面显示最近六行，完整草稿保留在 `Plaintext`。Esc／右键先落笔或取消拿取，再次 Esc 返回桌面。设置与草稿在本次运行内保留；来访、收件、控制台和窗口失焦会结束操作并归还手中木片。
9. 字母盘位于工具垫后方，采用 `QWERTYUIOP / ASDFGHJKL / ZXCVBNM` 三行错位布局，每格叠放三片独立木片，共 78 片。拖动顶部木片到表格、关键词纸签或明文纸，在纸面留下字母后归盘，可反复使用；拖到桌垫最前沿可摆放字母，右键木片归位，无效落点自动归盘。木片使用已有橡木、盘底使用胡桃木与皮革，沿用米制 UV、32 texels/m 像素采样、描边与场景光照。D 机位滚轮向前靠近工具查看细字，向后回到完整解密区；书写和拖动物件期间锁定滚轮，重新进入 D 机位恢复全景。

`Purple Glove > Scene > Install Cipher Toolbox` 可重复安装／更新解密区，保留文书、书架和房间。`CipherWorkbench` 与 `CipherWorkbenchInput` 管理实物射线拾取、工具取放和纸面输入；没有屏幕按钮、滑条、面板或输入框。`DeskCipherWorkbenchBuilder.cs`、`DeskLetterTrayBuilder.cs` 维护模型和布局。`Prefabs/PF_LetterTray_QWERTY.prefab` 为整盘，`PF_LetterTile_A.prefab` 至 `PF_LetterTile_Z.prefab` 为各字母独立样例；每片都有 `CipherLetterTile.alphabetIndex`、`AnimationPivot`，盘中实例另有 `homeSocket`，便于后续动画绑定。`CipherWorkbench.letterSamples[0..25]` 始终按 A–Z 索引。独立样例不引用场景中的归位点；需要自动归盘时显式指定 `homeSocket`。验证脚本、结果与渲染截图见 `output/physical-cipher-validation/`；目前以 Play Mode 合成指针／按键验证，尚未进行系统鼠标和中文输入法手动测试。

相机上的 `DeskScenePreview > Mouse Look` 可调整鼠标跟随：`Follow Mouse` 开关、`Horizontal Mouse Angle` / `Vertical Mouse Angle` 最大偏转角、`Mouse Dead Zone` 中心静止区（默认每个半屏的 5%）、`Mouse Response Curve` 响应曲线，以及 `Camera Follow Speed` 旋转跟随速度（越大越快，0 为立即到位，同时影响 WASD 机位切换）。曲线横轴为离开静止区后到画面边缘的归一化距离 0–1，纵轴为最大偏转角的比例 0–1，默认缓入缓出；自定义曲线输出会限制在 0–1 内。鼠标离开相机画面或窗口失焦时回正。

`Table focus (W)` 配置桌垫引用、相对机位偏移和视野角，默认偏移 `(0, 1.45, -0.45)`、视野角 `48°`。`Purple Glove > Scene > Configure Table Focus View` 可更新已保存场景的配置。`View.MatFocus` 追加在原有枚举末尾，保持旧场景的枚举值。现有 `DialogueController` 通过 `SetDialogueLock(true/false)` 进入／结束门口事件；入口会收回正在检视的物品，并恢复座位位置，避免从俯视高度看门。验证和截图位于 `output/table-focus-validation/`。

## 幕次归档接入与模拟

`07 Five act archive` 上的 `ActArchiveController` 管理五组资料。初始 `DeskAct = 0`，本场景开启 `requireDelivery`、关闭 `previewAllActs`，包括第一幕在内都需收件解锁。主摄像机 `SealedDeliveryController.DeliverAct(幕号)` 可绑定剧情 UnityEvent；袋子落桌后调用 `ReceiveAct` 开放并展示对应幕文件。剧情在幕末调用 `EndAct(幕号)` 或 `CompleteCurrentAct()`；需要返回是否成功时用 `CompleteAct(幕号)`。只接受当前幕的一次完成，归档不自动解锁下一幕。`actCompleted` 事件提供完成的幕号。

`ShowActOnDesk(幕号)` 只展示已解锁的幕，不改变剧情进度；`StoreDeskAct()` 收起当前资料。仅做文书预览时可开启 `previewAllActs`，此时无需收件。新的来件在其预定桌面位置生成后，用 `RegisterDocument(幕号, SceneObject)` 加入已解锁幕；保存和交换的是原物体，批注、输入内容与原桌面位置均保留。`sharedDocuments` 显式排除三件公共参考物，也拒绝重复归属。这里的归档与收件状态存于本次运行中，尚未接入存档系统。

运行时选中控制器，在 Inspector 点击“模拟结束当前幕并归档”，或使用 `Purple Glove > Acts > Simulate End of Current Act`。编辑器模拟不改写场景初始进度。五幕分别配置 6 / 7 / 8 / 9 / 8 张可检视文书，共 38 张；内容来自 `doc/ph-revised.md`，包括原始记录、完整密文和未填写的核验／回件表。猪圈字符使用固定几何笔画，师父长密文保持全部 82 个字母。中文纸面采用思源宋体 CN，字体授权随包附带。

`Purple Glove > Scene > Build Act Documents` 重建文书并保存场景，保留房间、夜空、书架和公共参考物。纸面版式在 `DeskActDocumentBuilder.cs` 中维护；验证脚本、运行记录和阅读截图见 `output/act-documents-validation/`。这版用于文书摆放与阅读预览：尚未接入填写、剧情逐件投递、可比对的手写签样及回执结算，不提前填入解谜答案或生成成功回执。

`Purple Glove > Scene > Arrange Desk and Add Notebook Pages` 在现有场景内重排文书、移除旧收起按钮并设置笔记翻页。笔记内容与模型配置在 `DeskNotebookBuilder.cs`，运行逻辑在 `NotebookBook.cs`。旧记录和教案从开场即可全部翻阅，不随剧情自动改写；两版祷文集内页仍待制作。桌垫边界、笔记摆向、鼠标翻页、动画和保留页码的验证与截图见 `output/notebook-validation/`。

雨声在层级中的 `Rain ambience - indoor loop` 上配置。`SceneAmbience` 的 `Volume` 默认 0.8，控制雨声的独立音量；`Fade Seconds` 默认 1.25 秒；勾选 `Muted` 会平滑静音，取消后淡入。`AudioSource` 的音量由此组件管理，使用现有 10 秒双声道室内闷雨素材与 2D 播放，不随相机转向衰减。窗边雨与室内雨同源，因此只播放室内这一条。禁用组件或卸载场景时停止播放，重新启用时从静音淡入。

## 窗外夜空

`08 Exterior night sky/NightSky_Sphere` 是包围房间的夜间天空球，包含冷蓝地平线、缓慢移动的薄云、稀疏星点和淡色弯月。天空沿用像素采样，窗格的原深蓝色遮挡面改为微弱冷色透明玻璃，木框与铅条保持原样。

天空材质 `Materials/M_Desk_NightSky.mat` 可调整天空与云层颜色、云量、移动速度、星光亮度以及月亮方向和大小。天空球不参与灯光、阴影或物件拾取，室内照明与雨声配置保持原值。`Purple Glove > Scene > Add Night Sky` 可更新已保存的桌面场景；完整构建也会生成天空。渲染截图与检查记录位于 `output/night-sky-validation/`。

## 门上机关与门外走廊

`DoorLeaf` 保持原门的位置、左侧底部枢轴与外形尺寸。上方窄窗是实际贯通的 0.54 × 0.16m 开口，中心离地 1.66m；活动板沿上铰链向室内翻起 108°。门板与碰撞网格都绕开窗口和下方舱口，打开后可看见门外实体墙面。`Door exterior - dim corridor/Visitor eye line - 1.66m` 是来访者双眼对齐点，朝向室内。对话访客使用约 0.20m 宽的透明头部贴图，后移到走廊内并接受场景光照；额头和面颊补图的上下边缘被门板遮住。试播和人物配置见 [对话与测试控制台](../../../doc/ph-dialogue-console.md)。

下方传物舱采用圆托盘与中间隔板，绕竖轴在 0° / 180° 两端往返，承载物固定在 `TransferDrumPivot/CargoSocket - outside at rest` 上。初始关闭眼窗，封口袋在收件事件开始时装入。`09 Sealed document delivery` 保存袋子与落桌锚点；纸包本身不计入证据或归档资料，收取后展示该幕原有文件。安装、控制台命令及事件接线见 [对话与测试控制台](../../../doc/ph-dialogue-console.md)。

两个固定交互框均使用 `SceneObject`（悬停位移为 0）及 `DoorMechanism`，沿用相机拾取逻辑。`Activate()` 切换端点；`MoveTo(false/true)` 分别关闭／打开眼窗，或把货物转到门外／门内。`IsMoving` 防止重入，`reachedFirstStop` / `reachedSecondStop` 可绑定剧情交接事件。传物舱的 `TryLoadCargo(Transform)` 装入一个受脚本控制、无独立物理运动的小物体，`ReleaseCargo(Transform destination = null)` 在停止后移交原物体；两者保留物体身份，转动期间不允许装卸。请把货物尺寸控制在约 0.25 × 0.13m 的托盘半区内。当前未实现桌面物品拖入舱口或剧情自动收取。

门外补齐灰泥墙、木梁、石地板、顶板与壁烛，复用室内材质、米制 UV、像素采样及 Layer 8 光照。壁烛与门框微弱反光保持走廊昏暗。机关使用现有 `Trapdoor` 音效，随门的位置进行 3D 播放。

`Purple Glove > Scene > Add Visitation Door and Corridor` 可在已保存的桌面场景中更新门和门外走廊，保留桌面、书架、侧投递口和机位；完整构建也会生成这些结构。验收截图与记录位于项目根目录 `output/door-mechanism-validation/`。

## 资产内容

`Prefabs/` 内提供基础桌面资产，以及字母盘和 A–Z 独立木片样例：

- 木书桌、皮质书写垫。
- 炭黑／酒红七瓣花／苔绿三本合起的书、打开的笔记本。
- 密文信纸、空白答题纸、羊皮纸、打开的信封、红蜡封信封。
- 点燃的黄铜烛台及白蜡、三支独立彩蜡。
- 黑玻璃墨水瓶、黄铜砂盒、羽毛笔、裁纸刀。
- 收件盘、带圆圈／叉／加号的三格信架、原件封存匣。
- 五层资料书架、可交互的幕次卷宗。
- 门扇、门框、投递口、窗框、灰泥墙板、木梁、石地板模块。

几何含倒角、切面法线、纸张折面与厚度、页层、木板缝、蜡滴、瓶口内壁、铁件与独立装饰。实际数量与三角形数以项目根目录 `output/desk-scene-v1/asset-report.txt` 为准。

`Meshes/*.asset` 是持久化的 Unity Mesh，可独立复用。`output/desk-scene-v1/obj/` 同时提供每个 Prefab 的 OBJ、MTL 与所用基础色贴图，可导入 Blender 等建模软件继续调整。OBJ 按右手坐标导出，米制；不包含 Unity 字体文字、灯光、脚本、碰撞、Shader 像素合并与描边或完整 PBR 响应，完整效果以 Prefab 为准。

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
- 场景包含五幕文书预览，密文按修订剧本保留，表格留待填写。公共笔记本可翻阅旧记录及六种教案；两版书内页、可比对签样、蜡封与完整解谜流程尚待制作。
- 房间建筑使用 Layer 8 做场景内光照分离，暖光主要落在道具上、冷光维持背景可读性；当前工程无需修改层名称。合入其他已有项目时，应检查其 Layer 8 是否已有不同用途，并同步调整对应光源 Culling Mask。
- 书架和卷宗与桌面家具统一使用 Default 层，共用场景灯光，不使用专用补光。座位后墙复用其他墙面的灰泥与木梁材质，米制 UV、像素采样和描边规则相同。
- 中文 UI 使用随包分发的 `PHArchiveUI.ttf`，是已重命名的霞鹜文楷字形子集，授权见同目录 `PHArchiveUI-LICENSE.txt`。

## 文件、来源与复建

- `Scenes/PH_DeskRoom.unity`：组装后的场景。
- `output/desk-scene-v1/desktop.png`、`doorway.png`、`overview.png`：Unity 实际渲染截图。
- `output/desk-scene-v1/PurpleGlove_DeskScene_V1.unitypackage`：完整场景包，含依赖的材质贴图、两款字体、环境音与编辑器构建源码。
- 字体从现有素材库选用 EB Garamond Medium 与 Courgette，未修改源文件；相应 OFL 授权随 `Fonts/` 分发。
- 环境音引用 `Assets/Audio/PH/Ambience/rain_room_muffled_loop_10s.wav`。来源与完整授权见原音效库 `Assets/Audio/PH` 附带文档和项目 `doc/ph-audio-library.md`；导出的场景包附所用音效的来源与授权文件。
- `Purple Glove > Scene > Build Desk Room V1` 可重新生成本网格库、Prefab、场景和导出文件，并设为默认启动场景。该操作覆盖本目录内的对应生成资产，请先把手工变体另存。构建工具不会在导入时自动运行。其余 Build Settings 场景条目保留在本场景之后。
- `Purple Glove > Scene > Refresh Room Surfaces and Lighting` 更新已保存场景的生成墙面、木梁、书架光照与反射环境，保留门窗、桌面道具及归档配置。

这是可运行、可继续编辑的场景与网格初版，已实现物件检视、三个固定视角及按幕归档交换。剧情、解谜、文书输入和跨运行存档仍需由后续游戏系统接入。
