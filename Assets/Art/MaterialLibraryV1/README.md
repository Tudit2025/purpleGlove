# 《紫手套》第一批像素材质

适配当前 Unity 2022.3.62f2、Built-in Render Pipeline。共 22 张独立 128×128 RGB PNG 与 22 个 Standard 材质。直接把 `Materials/M_*.mat` 拖到模型的 Mesh Renderer 上；打开 `Scenes/MaterialLibraryPreview.unity` 可在统一光照下比较材质响应。

## 统一规格

- 128×128 原生像素，每张最多 12 个颜色，全部为无文字、无装饰图案的基础色贴图。
- Point / Repeat / sRGB / 无压缩 / 无 Mipmap / Aniso 0。生成原图经面积降采样、窄边界衔接和确定性色板整理，成品不是仅在大图中模拟像素外观。
- 四边可重复；最外侧对应像素严格一致。墙石纹理仍需结合模型尺寸检查重复图案。
- 黑玻璃使用不透明深色底色、Metallic 0、Smoothness 0.78；黑色满墨瓶的反光由光源产生。该材质不提供透射、折射或瓶内液体。
- 蜡、皮、木、纸均为非金属。黄铜和老铁各有 Metallic / Smoothness 参数；锈铁的参数是整张表面的折中值，不含逐像素金属度遮罩。
- 这一轮不提供法线、AO、位移或烘焙高光。低多边形轮廓和真实场景灯光负责体积，避免高频凹凸噪声。

## 像素密度与 UV

统一目标 **128 texels/m**，即约 **7.8125 mm/像素**。一张 128px 贴图对应 1m×1m 表面。仅把所有贴图做成相同尺寸，不能自动让大小不同的物体拥有相同像素密度。

推荐在建模软件中把 UV 展开为 **1 UV 单位 = 1 米表面距离**，材质 Tiling 保持 `(1,1)`。表面超过 1m 时让 UV 越过 0–1，Repeat 会自动铺贴；小物件只采样贴图的一部分，不要把整张图塞进每个小面。

| 平面实际尺寸 | 占用 UV 范围 / 等价 Tiling | 表面像素数 |
| --- | --- | --- |
| 2m × 1m 桌面 | 2 × 1 | 256 × 128 |
| 0.24m × 0.32m 书皮 | 0.24 × 0.32 | 约 31 × 41 |
| 0.21m × 0.30m 信纸 | 0.21 × 0.30 | 约 27 × 38 |
| 0.08m × 0.08m 小物件面 | 0.08 × 0.08 | 约 10 × 10 |

若模型每面使用完整 0–1 UV，按其实际尺寸设置材质 Tiling；形状不同的面应优先在模型中修正 UV。复制材质后调整，不要修改被其他物体共用的材质。圆柱侧面 U 的跨度取周长、V 取高度；木纹沿纹理 V 轴，横向木纹通过旋转 UV 实现。

预览球的赤道密度约为 128 texels/m，球极点的经纬 UV 会压缩；预览用于对比材质响应，不作为生产模型的 UV 模板。模型缩放后也需检查密度。若后续要改为 256 texels/m，应整场一致地改为 1 UV 单位对应 0.5m。

## 资源索引

| 文件标识（T_ 贴图 / M_ 材质） | 中文 | 建议用途 |
| --- | --- | --- |
| Glass_InkBlack | 黑色墨水玻璃 | 满墨的黑色墨水瓶 |
| Metal_AgedBrass | 旧黄铜 | 烛台、砂盒、小五金 |
| Wax_Ivory | 象牙白蜡 | 刻度蜡烛、备用白蜡 |
| Wax_Oxblood | 暗红蜡 | 红蜡烛、红蜡封 |
| Wax_Ochre | 赭金蜡 | 金赭蜡烛、蜡封 |
| Wax_Indigo | 靛蓝蜡 | 蓝蜡烛、蜡封 |
| Leather_Umber | 深棕旧皮革 | 师父笔记本、书写垫 |
| Leather_Oxblood | 酒红皮革 | 七瓣花版书皮的基础材质 |
| Leather_Moss | 苔绿皮革 | 规程册、登记册 |
| Leather_Charcoal | 炭黑皮革 | 深色封面、装订皮 |
| Wood_DarkWalnut | 深胡桃木 | 桌面、封存匣 |
| Wood_HoneyOak | 蜜棕橡木 | 收发盘、信架 |
| Wood_AgedPine | 旧松木 | 抽屉、架板 |
| Wood_Weathered | 灰褐风化木 | 门板、木框 |
| Paper_RagIvory | 象牙手工纸 | 正式来信、空白答题纸 |
| Paper_AgedCream | 旧米黄纸 | 旧笔记、旧回执 |
| Parchment_Vellum | 浅色犊皮纸 | 较精细的正式文书 |
| Parchment_Aged | 陈旧羊皮纸 | 陈年文书、封存记录 |
| Wall_LimePlaster | 旧石灰墙 | 工作间灰泥墙面 |
| Wall_GreyStone | 灰石砌墙 | 门边与走廊石墙 |
| Iron_Blackened | 发黑锻铁 | 门铰、匣子包角、铁件 |
| Iron_Rusted | 锈蚀老铁 | 旧门五金、闲置铁件 |

纸张、羊皮纸只提供基底。正文、密码、版次、蜡封图案、书皮花印和刻度另作清晰的文字或图案层，不要写入 128px 底图。纸张几何厚度、撕边和折痕也由模型处理。薄纸如果要从两面阅读，使用有正反面的薄网格；Standard 默认剔除背面。

## 预览与源文件

项目根目录 `output/material-library-v1/` 包含：

- `contact-sheet.png`：成品基础色总览，最近邻 2 倍放大。
- `repeat-sheet.png`：每款成品的 2×2 重复铺贴总览。
- `unity-material-preview.png`：独立预览场景的真实 Unity 渲染。
- `PurpleGlove_Materials_V1.unitypackage`：包含贴图、材质、预览网格、场景及本说明，可导入其他 Built-in 项目。
- `sources/`：内置 image_gen 为每个材质独立生成的原始 PNG。
- `generation-prompts.json`：完整提示词集、颜色与材质参数。
- `validation.txt`：生成与 Unity 导入检查记录。

源图和工具保留在当前仓库。可使用 `Purple Glove > Materials > Rebuild V1 Library` 从源图重建本批资源（会覆盖本库中对应生成资源的调整；先保存手工变体）。该菜单工具不包含在可移植 unitypackage 内。

当作最终游戏场景使用时，还需要在模型上检查 UV、烛光下的文字可读性及固定机位下的远处纹理闪烁。默认关闭 Mipmap 是为保持近景像素；若远墙出现闪烁，可单独开启 Mipmap 做实际机位对照。该库不更改项目画面分辨率、已有场景或全局渲染设置。
