# 《紫手套》低多边形桌面场景 v1

以项目 `output/imagegen/ph-desk-v1.png` 为构图参考，将桌面方案搭建为真实 Unity 三维场景。适配 Unity 2022.3.62f2 / Built-in。所有网格为本项目程序构建，无外购模型依赖。

## 打开与查看

1. 打开 `Scenes/PH_DeskRoom.unity`，切到 Game 视图并选择 16:9。
2. 点击 Play：**A** 看左侧书架，**W** 看门口，**S / D** 回桌面；左下角仅保留小号黑边像素“WASD 切换视角”提示，不占用鼠标点击。**P** 切换像素采样。移动鼠标会让当前机位朝光标方向轻微转动，默认左右各最多 6°、上下各最多 4°，书架视角减半。相机位置固定，不提供自由行走。
3. 默认按 960×540 采样后放大。书本、笔记本、纸张与信封悬停时抽出，点击后拉到镜头前转正；**Esc / 右上角 X** 放回。检视期间锁定镜头。
4. 左侧书架从上到下对应第一幕至第五幕，侧面有一只小烛台，书架与卷宗不显示文字说明。幕结束后将该幕资料收成卷宗；点击卷宗取回原资料，同时收纳桌上另一幕的资料。参考书与笔记本常驻桌面。雨声与烛光在三个机位之间连续运行。
5. 本场景已设为 Build Settings 中启用的第 0 个场景，也是编辑器 Play 的默认入口。`DeskSceneStartup` 在脚本加载后恢复 Play 入口，不强行关闭当前正在编辑的场景。菜单 `Purple Glove > Scene > Set Desk Room as Default` 可重新设置。
6. **W** 看门后，点击门上方活动板打开眼部窄窗，再点翻起的板或窗下铜把手关闭。点击下方传物舱的圆环／底沿铜把手，托盘向左旋转半圈，把封蜡纸包从门外转入；再次点击向右转回。运动中重复点击不打断动作，机关不进入物件检视界面。

相机上的 `DeskScenePreview > Mouse Look` 可调整鼠标跟随：`Follow Mouse` 开关、`Horizontal Mouse Angle` / `Vertical Mouse Angle` 最大偏转角、`Mouse Dead Zone` 中心静止区（默认每个半屏的 5%）、`Mouse Response Curve` 响应曲线，以及 `Camera Follow Speed` 旋转跟随速度（越大越快，0 为立即到位，同时影响 WASD 机位切换）。曲线横轴为离开静止区后到画面边缘的归一化距离 0–1，纵轴为最大偏转角的比例 0–1，默认缓入缓出；自定义曲线输出会限制在 0–1 内。鼠标离开相机画面或窗口失焦时回正。

## 幕次归档接入与模拟

`07 Five act archive` 上的 `ActArchiveController` 管理五组资料。剧情在幕末调用 `EndAct(幕号)`（可绑定 UnityEvent），或调用 `CompleteCurrentAct()`；需要返回是否成功时用 `CompleteAct(幕号)`。只接受当前幕的一次完成，归档后自动展示下一幕的资料，第五幕结束后桌面阶段资料全部收起。`actCompleted` 事件提供完成的幕号。

`ShowActOnDesk(幕号)` 只展示已开始的幕，不改变剧情进度。新的来件在其预定桌面位置生成后，用 `RegisterDocument(幕号, SceneObject)` 加入该幕；保存和交换的是原物体，批注、输入内容与原桌面位置均保留。这里的归档状态存于本次运行中，尚未接入存档系统。

运行时选中控制器，在 Inspector 点击“模拟结束当前幕并归档”，或使用 `Purple Glove > Acts > Simulate End of Current Act`。编辑器模拟不改写场景初始进度。第二至五幕配置了密文／回件样张与空白工作纸，供交换流程预览；正式剧情来件、谜题完成条件与文书输入仍由后续剧情系统接入。

雨声在层级中的 `Rain ambience - indoor loop` 上配置。`SceneAmbience` 的 `Volume` 默认 0.8，控制雨声的独立音量；`Fade Seconds` 默认 1.25 秒；勾选 `Muted` 会平滑静音，取消后淡入。`AudioSource` 的音量由此组件管理，使用现有 10 秒双声道室内闷雨素材与 2D 播放，不随相机转向衰减。窗边雨与室内雨同源，因此只播放室内这一条。禁用组件或卸载场景时停止播放，重新启用时从静音淡入。

## 门上机关与门外走廊

`DoorLeaf` 保持原门的位置、左侧底部枢轴与外形尺寸。上方窄窗是实际贯通的 0.54 × 0.16m 开口，中心离地 1.66m；活动板沿上铰链向室内翻起 108°。门板与碰撞网格都绕开窗口和下方舱口，打开后可看见门外实体墙面。`Door exterior - dim corridor/Visitor eye line - 1.66m` 是来访者双眼对齐点，朝向室内；当前没有添加剧情人物。

下方传物舱采用圆托盘与中间隔板，绕竖轴在 0° / 180° 两端往返，承载物固定在 `TransferDrumPivot/CargoSocket - outside at rest` 上。初始关闭眼窗、纸包位于门外。纸包是独立演示道具，不计入任何一幕的证据或归档资料。

两个固定交互框均使用 `SceneObject`（悬停位移为 0）及 `DoorMechanism`，沿用相机拾取逻辑。`Activate()` 切换端点；`MoveTo(false/true)` 分别关闭／打开眼窗，或把货物转到门外／门内。`IsMoving` 防止重入，`reachedFirstStop` / `reachedSecondStop` 可绑定剧情交接事件。传物舱的 `TryLoadCargo(Transform)` 装入一个受脚本控制、无独立物理运动的小物体，`ReleaseCargo(Transform destination = null)` 在停止后移交原物体；两者保留物体身份，转动期间不允许装卸。请把货物尺寸控制在约 0.25 × 0.13m 的托盘半区内。当前未实现桌面物品拖入舱口或剧情自动收取。

门外补齐灰泥墙、木梁、石地板、顶板与壁烛，复用室内材质、米制 UV、像素采样及 Layer 8 光照。壁烛与门框微弱反光保持走廊昏暗。机关使用现有 `Trapdoor` 音效，随门的位置进行 3D 播放。

`Purple Glove > Scene > Add Visitation Door and Corridor` 可在已保存的桌面场景中更新门和门外走廊，保留桌面、书架、侧投递口和机位；完整构建也会生成这些结构。验收截图与记录位于项目根目录 `output/door-mechanism-validation/`。

## 资产内容

`Prefabs/` 内提供 31 个可复用资产：

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
- 本场景 `Materials/` 中保存上一批 `MaterialLibraryV1` 的风格化变体，沿用原始颜色贴图与金属度、光滑度参数。
- 物件采用深墨色反壳描边，默认宽度为 540p 下 1.2px，纸张为 0.75px；实际输出宽度随画面高度成比例变化。材质中可调整 `Ink outline` 和 `Outline pixels at 540p`，宽度设为 0 可关闭。网格 UV3 储存平滑外扩法线以减少裂缝，原始切面法线继续用于低多边形光照。文字、细缝线和火焰不加描边。
- 场景基础灯光与三色环境光均为初版的 **50%**；烛焰、窗光、门缝与光晕同步减弱，反射贴图在补齐后墙的房间中重新捕获。书架侧面烛台与桌面烛台共用光源参数。桌面烛光波动按减半后的基础强度计算。这里调整光照输入，不对最终画面强行乘黑色滤镜。
- 木纹在水平板上沿 X、立面沿 Y。需要其他纹理方向时，在建模工具中旋转对应 UV。
- 门扇根枢轴为左侧铰链底端；封存匣子节点 `LidPivot` 位于后缘；投递口 `HatchPivot` 位于上缘，便于后续开合动画。
- 拾取道具使用简单 BoxCollider；墙、地面和桌子使用静态 MeshCollider。没有 Rigidbody 或移动碰撞系统。
- 场景当前是第一幕视觉原型，密文准确显示 `NHHS WKH RULJLQDO`；笔记本仅为样例字母网格和排版。书页、蜡封与正式证据内容尚未按完整谜题实现。
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
