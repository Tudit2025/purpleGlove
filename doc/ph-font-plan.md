# 《紫手套》书面资料字体方案 v1

核对日期：2026-10-31（Asia/Hong_Kong）。依据：[修订剧本](ph-revised.md)与[桌面场景方案](ph-scene-desktop.md)。

已收集 **9 个字族、12 个静态 OTF/TTF 文件**，字体原文件合计约 **65 MiB**。均来自作者仓库、官方发布包或 Google Fonts 仓库，各字族附原始 OFL 1.1 授权。字体原件未修改；思源字体采用 Adobe 官方提供的 CN 区域子集。

- [离线可编辑样张](../output/font-library-v1/preview.html)：双击浏览器打开，可调字号、替换试排文句。
- [中英对照总览 PNG](../output/font-library-v1/font-pairings.png)：使用实际字体渲染，可直接审阅。
- [字体文件目录](../output/font-library-v1/fonts/)。
- [来源、版本与 SHA-256 清单](../output/font-library-v1/manifest.json)、[字形检查结果](../output/font-library-v1/validation.json)。

## 1. 选型结论

优先建立「正式文书、旧式印本、私人手稿」三种稳定视觉语言；大字标题与清晰阅读各加一组辅助字体。台账利用已有字体搭配和栏位区分，不为每份纸增加新字族。

这里的中文配对是本地化中的气质对应，不是把字体解释成同年代、同地区的真实配套字。剧本明确采用架空历史；英文各字体的历史来源也不完全相同。

| 用途与风格 | 英文 | 中文 | 使用理由与边界 |
|---|---|---|---|
| 正式文书：端正、克制、可反复核对 | **EB Garamond Regular / Medium** | **思源宋体 CN Regular** | 正式委托、核验结论、外交回件的主力。英文在较暗纸面优先 Medium；中文标题可用 SemiBold。 |
| 旧式印本：活字与书卷气 | **IM FELL English Regular** | **朱雀仿宋 Regular** | 《祷文集》与旧试印。FELL 的不规则边缘已提供旧印刷感，不再叠加伤害辨字的磨损。朱雀 v0.212 是技术预览版。 |
| 私人手稿：温度、书写感 | **Courgette Regular** | **霞鹜文楷 Regular** | 师父笔记、明文附记、个人批注。Courgette 用于短段；长信另备 EB Garamond Italic。密文和公式用清楚正体。 |
| 大字标题：庄重、具有仪式感 | **Cinzel Regular** | **思源宋体 CN SemiBold** | 行会通知抬头、规程封面、卷宗封面。仅用于短标题，不铺满整页正文。 |
| 清晰阅读：精确识别与逐字操作 | **IBM Plex Mono Regular** | **思源黑体 CN Regular** | 可选的清晰转录、解谜辅助网格、译文、输入与字幕。属于玩家阅读工具的现代字体，不解释成城邦时代的印刷样式。 |

**最小制作组合**：先用 EB Garamond＋思源宋体完成公文；用 Courgette＋霞鹜文楷完成私人内容；用 IM FELL English＋朱雀仿宋完成书页。Cinzel 与 IBM Plex Mono 在对应需求出现时加入。

## 2. 对应剧本中的具体物件

| 物件 | 幕次 | 主体字样 | 细节安排 |
|---|---|---|---|
| 行会通知、任务便笺、规程、代表登记册、空白核验表与检索单 | 序幕／第一幕起 | EB Garamond＋思源宋体 | 通知和规程封面可用 Cinzel＋宋体 SemiBold；填写内容用手稿组。表格标签与玩家填写的字分开。 |
| 公爵核验委托、正式军情件、委托封面、证人安排表 | 第一、三幕 | EB Garamond＋思源宋体 | 抬头、案号、装订批号的层级稳定；明文还原与来源确认保留两个明确栏位。 |
| 普通版与七瓣花版《祷文集》 | 第一幕可见，第二幕使用 | IM FELL English＋朱雀仿宋 | 两版使用同一字族；区别来自版次、装订日期、七瓣花标记及确定正文。不能只靠换字体告诉玩家哪本正确。 |
| 师父旧笔记：过往记录、反思、六种教案 | 第一幕起可翻阅 | Courgette＋霞鹜文楷 | 教案标题和个人记述可手写；字母表、数字与演算格切回正体。长英文段落可用 EB Garamond Italic。 |
| 数字便条、台账摘要、猪圈碎纸 | 第二幕 | 坐标用 EB Garamond；台账公文抬头和栏名用思源宋体，记录值用朱雀仿宋；便条及碎纸附语用霞鹜文楷 | 台账用整齐表格及统一字号；原始签收页调阅条件并入页脚，不再另出封存目录。 |
| 原始签收页、换版指令、试印存根、出库记录 | 第四幕 | EB Garamond＋朱雀仿宋；正式出库说明用宋体组 | 同一批号、同一案号保持相同数字样式；保管见证人与经手人签名分区。 |
| 师父双栏密信及明文附记 | 第四幕 | 密文用 EB Garamond 正体；附记用 Courgette＋霞鹜文楷 | 双栏密文保留独立布局；不将长串大写字母改成装饰性连笔。 |
| 三方正式来件、参与确认、暂停转押回执、共同封存清单 | 第五幕 | EB Garamond＋思源宋体 | 用正文明确区别「收到」「同意参与」「暂停已生效」。字体、颜色或纸张出现本身不能代替承诺内容。 |
| 玩家工作纸、译文、最终核验母稿 | 全程／第四、五幕 | 公文与手稿组按内容分层 | 最终母稿采用公文组；逐字输入与清晰阅读可用辅助组。 |

三方共用基本公文字体，通过抬头、署名、封件编号、纸张与印记区分。避免给某一阵营专门设置「邪恶」字体，让视觉提前泄露案件结论。

## 3. 解谜与可读性约束

### 密文、坐标和公式

- 凯撒、维吉尼亚、仿射、栅栏密文统一以清楚的大写 A–Z 显示。默认纸面可用 EB Garamond Medium；需要对齐时以布局中的固定字符格定位，不把比例字体误称为等宽字体。
- 辅助转录使用 IBM Plex Mono；重点观察 `I l 1`、`O 0`、`B 8`、`S 5` 和数字坐标的连接符。重要数字不使用花体或旧式下沉数字。
- 密码字串关闭会替换或合并字形的连字、花饰、历史字形替换，不启用长 s。遇到排版平台不支持这些开关时，以逐字符显示与实际渲染结果验证。
- 书本密码取字以剧本附录 C 的固定印刷行、页号与字母序列为准。放大、字号设置、字体回退或切换语言不得重新自动折行、改变取字坐标。
- 两版书的对应取字位置都要存在，差别来自剧本给定的正文与排版修订。可读模式若换为 EB Garamond，应同时处理两版并保留固定行。
- 中文是明文或书页的译文。书本密码仍对英文行计数，不能将中文译文当成同一组逐字符密文的输入。

### 猪圈密码与签名

猪圈密码按剧本附录 D 制作独立图形／精灵，固定开口方向、角度与点的位置。不要直接依赖某字体中的 `└· □ ⊏ 〉` 组合；通用符号字体的替换可能改变谜题。此次不将任何字族视为猪圈密码素材。

马泰奥与卡拉法的签名属于可比对物证。量产字体能定基调，但同一字体不能证明同一人书写。后续需为每个签名制作固定图稿，保留回钩、断笔、倾角等可辨特征；同一人的对照签样保持这些特征，并提供清楚的印刷姓名辅助阅读。这里没有代替美术制作签名证据。

### 屏幕阅读

以下尺寸是 **1920×1080 最终显示画面、纸张拉近后** 的试排起点，需按实际屏幕与字体字面调整：

| 内容 | 建议起点 | 调整原则 |
|---|---|---|
| 英文正文 | 28–34 px，行距约 1.4–1.55 | 旧印本和手写体优先放大；长段落缩短行长。 |
| 中文正文 | 30–36 px，行距约 1.5–1.7 | 朱雀细笔画在暗处易损，先增字号、提高纸面照明。 |
| 密文、坐标、关键编号 | 32–40 px | 保留明确字符间距，关键数字无需靠猜。 |
| 封面与抬头 | 42–60 px | Cinzel 和宋体 SemiBold 在这类短内容中使用。 |

场景方案中的 640×360 内部画面放大到 1080p 只是风格实验。若文字也先降到 640×360，最终 30 px 的字实际只有约 10 个内部像素，无法据此保证复杂汉字清晰。阅读状态宜让纸面文字使用较高分辨率的渲染路径，或进一步拉近纸张，同时保持既定机位与实物操作。

旧化优先放在纸边、污迹、折痕和非文字区域；不得遮挡取字点、批号、签名特征和猪圈圆点。两页比对时也要分别保证字号，不因横向摆放把正文压到难读。

## 4. Unity 2022.3 / TextMeshPro 使用建议

本项目为 Unity 2022.3.62f2，已配置 TextMeshPro 3.0.7。桌面场景目前通过动态 `Font`、`TextMesh` 与 `WorldDocumentText` 显示纸面文字，尚未迁移到 TMP。2026-11-01 已将特色中文字体应用到实际场景及重建脚本：

- **霞鹜文楷**：师父笔记及翻动书页、第一幕旧纸上的中文附语、旧编码工作单、私人便条、师父明文附记，以及卢卡明文留存纸的中文标题、译注与落款。
- **朱雀仿宋**：两版祷文集封面、印务台账、封档目录、原始签收页、旧试印与换版指令；数字便条仅标题使用仿宋，取字坐标保持清晰正体。
- **思源宋体 CN**：公爵委托等正式公文。独立的英文明文、凯撒密文与签名继续使用既定的 EB Garamond／Courgette；双栏密信中的连续密文、数字网格不改为花体。

两套新增中文字体以原始字体文件和 OFL 授权随 `Assets/Art/DeskSceneV1/Fonts/` 分发，并配置思源宋体作为缺字回退。现有笔记页仍保留原有分行，换字后按纸面边界适配；两版祷文集内页尚待制作。可使用 `Purple Glove > Scene > Apply Chinese Document Fonts` 更新已保存场景。引擎检查脚本、记录与检视截图见 `output/chinese-font-validation/`。

以下是未来迁移到 TMP 时的配置建议：

1. 从素材库选取需要的静态 OTF/TTF 及同目录 `OFL.txt`，一并放进项目字体目录。当前文件均为原生静态字体，无需转换可变字重。
2. 为每个实际使用的字面创建 TMP Font Asset。Regular、Medium、SemiBold、Italic 使用收集到的对应文件，避免依赖软件伪粗体／伪斜体；中文不作人为斜切来模拟手写。
3. 正文和密码分别建资产或排版预设。已知剧本文字可用 [script-characters.txt](../output/font-library-v1/script-characters.txt) 作为字符来源的起点；该文件包含制作说明和 Markdown 标记，是保守全集，不是最终发行子集。猪圈等图形应从字体需求中独立处理。英文及数字网格单独维护 A–Z、a–z、0–9、标点与公式符号。
4. 中文图集先按实际字集生成，检查缺字和图集容量。需要动态补字时，使用 Dynamic 与适当的 Multi Atlas 设置，并保留所需源字体；图集大小和内存以实机测量决定。不要把几万汉字无差别塞进一张固定图集。
5. 中文正文的回退建议为「朱雀仿宋／霞鹜文楷 → 思源宋体 CN」，清晰阅读与字幕用思源黑体 CN。思源 CN 为简中地区字形，不能直接当作已完成繁中地区规范适配。
6. 中英文样式最好显式分段指定。若只设一个中文字体为主字体，它自带的拉丁字母可能先被命中，英文就不会使用本方案指定的字族。只配置 fallback 并不等同于完成中英配对。

Dynamic、Multi Atlas、Fallback Font Asset Table 与 Character File 的建议核对了项目内 TMP 3.0.7 编辑器实现；具体配置仍需在项目引擎中完成。

## 5. 交付与核验结果

| 字族 | 收集字面 | 文件版本 | 主用范围 |
|---|---|---|---|
| EB Garamond | Regular、Medium、Italic | 1.002／Italic 1.001 | 公文、台账、长信备用 |
| IM FELL English | Regular | 3.00 | 旧书印本 |
| Courgette | Regular | 1.002 | 短手稿 |
| Cinzel | Regular | 2.000 | 大字标题 |
| IBM Plex Mono | Regular | 2.3 | 清晰转录、解谜网格 |
| 思源宋体 CN | Regular、SemiBold | 2.003 | 中文公文、抬头、回退 |
| 朱雀仿宋 | Regular | 0.212，技术预览 | 中文旧书、账簿 |
| 霞鹜文楷 | Regular | 1.530 | 中文手稿 |
| 思源黑体 CN | Regular | 2.005 | 清晰阅读、字幕、输入 |

已检查所有 12 个文件的字体结构、静态字面、版本、SHA-256、基本拉丁字母与数字覆盖，并用 FreeType/Pillow 实际打开渲染。所选的全部中文字面均覆盖修订剧本中的 **1001 个不同汉字**，本次检查的中文正文标点也无缺字；六组中英样张的用字无缺字。这个结果不代表任意未来译文、繁中转换或玩家输入都已覆盖。

PNG 已作视觉检查；HTML 使用本地字体、包含加载状态提示，提供字号调整和可编辑文句。样张用于评估字体，不模拟实际游戏光照、透视、距离与低分辨率渲染；最终可读性仍须在引擎里验收。

## 6. 官方来源与授权

| 字族 | 官方来源／收集来源 | 随库授权 |
|---|---|---|
| EB Garamond | [作者静态字体仓库](https://github.com/octaviopardo/EBGaramond12) | [OFL](../output/font-library-v1/fonts/eb-garamond/OFL.txt) |
| IM FELL English | [Google Fonts](https://github.com/google/fonts/tree/main/ofl/imfellenglish) | [OFL](../output/font-library-v1/fonts/im-fell-english/OFL.txt) |
| Courgette | [Google Fonts](https://github.com/google/fonts/tree/main/ofl/courgette) | [OFL](../output/font-library-v1/fonts/courgette/OFL.txt) |
| Cinzel | [NDISCOVER](https://github.com/NDISCOVER/Cinzel) | [OFL](../output/font-library-v1/fonts/cinzel/OFL.txt) |
| IBM Plex Mono | [IBM 项目](https://github.com/IBM/plex)、[Google Fonts 文件](https://github.com/google/fonts/tree/main/ofl/ibmplexmono) | [OFL](../output/font-library-v1/fonts/ibm-plex-mono/OFL.txt) |
| 思源宋体 CN | [Adobe](https://github.com/adobe-fonts/source-han-serif) | [OFL](../output/font-library-v1/fonts/source-han-serif/OFL.txt) |
| 朱雀仿宋 | [璇玑造字](https://github.com/TrionesType/zhuque)、[v0.212 发布包](https://github.com/TrionesType/zhuque/releases/tag/v0.212) | [OFL](../output/font-library-v1/fonts/zhuque-fangsong/OFL.txt) |
| 霞鹜文楷 | [作者仓库](https://github.com/lxgw/LxgwWenKai)、[v1.530](https://github.com/lxgw/LxgwWenKai/releases/tag/v1.530) | [OFL](../output/font-library-v1/fonts/lxgw-wenkai/OFL.txt) |
| 思源黑体 CN | [Adobe](https://github.com/adobe-fonts/source-han-sans) | [OFL](../output/font-library-v1/fonts/source-han-sans/OFL.txt) |

这些文件按 SIL OFL 1.1 分发，可用于商业游戏并随游戏捆绑；分发字体时保留各自版权声明与授权全文。不要单独销售字体文件。若以后实际修改字库或再分发衍生字体，需遵守 OFL 及对应保留名称要求；本次没有修改字体。参见 [OFL 官方常见问题](https://openfontlicense.org/ofl-faq/)。

风格依据：EB Garamond 是十六世纪人文主义字形的复刻方向，适合本作的正式文字基调；IM FELL 的来源更晚，此处只借其旧活字气质；Courgette 具有较低笔画反差的手写斜体造型。朱雀仿宋来自近现代中文活字传统，用作中文阅读层的风格对应。上述配对是本项目的设计判断，不声称历史上确实存在这种跨语种搭配。参见 [EB Garamond 说明](https://raw.githubusercontent.com/google/fonts/main/ofl/ebgaramond/DESCRIPTION.en_us.html)、[FELL 说明](https://raw.githubusercontent.com/google/fonts/main/ofl/imfellenglish/DESCRIPTION.en_us.html)、[Courgette 说明](https://raw.githubusercontent.com/google/fonts/main/ofl/courgette/DESCRIPTION.en_us.html)、[朱雀项目说明](https://github.com/TrionesType/zhuque)。
