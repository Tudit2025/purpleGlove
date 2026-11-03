# 《紫手套》文本润色 · 第 3 部分：界面与物件上的文字

## 给润色者的说明

这是中文叙事解谜游戏《紫手套》的游戏内文本，请在不改变剧情、线索和格式的前提下润色，让文字读起来更自然、更有质感。

故事背景：借用意大利城邦名称的架空历史。六月十五日夜，印务行会的核验人卢卡被封在三方共用的核验所里，要在次日清晨六点前核验一份声称“法国将越境”的军情密文。密文用的是他师父马泰奥设计的密码，而师父正被米兰方面扣押。米兰、威尼斯、法国的代表隔着门上的观察窗与卢卡交谈，文件经门上的传物舱送进来。卢卡始终不出房间，玩家也看不到任何人物的正脸。

文风：带时代感的书面中文，克制、清楚。不用现代网络用语，不堆砌辞藻。

### 规则

1. 只改正文。所有标题行（以 # 开头的行）原样保留，不增删、不合并、不调换条目。
2. 以下内容不改：
   - 人名、地名、机构名：卢卡、马泰奥·里奇（马泰奥、里奇）、贝阿特丽切、夏尔、卡拉法、巴尔托洛、孔蒂公爵、米兰、威尼斯、法国、圣马丁修道院、印务行会、米兰内廷、七瓣花版。
   - 数字、日期、时刻和编号：第五十七号封档、第17号委托、批号726（七二六）、六月十五日、六月十六日、六月十八日、五点、六点、六点半等。
   - 英文、法文、拉丁字母、密文和明文答案（如 KEEP THE ORIGINAL）。
   - 占位符：花括号 {去向后果}，尖括号 ⟨数字⟩ 一类。
   - 按键与操作名：Esc、Enter、F1、← / →、滚轮。
3. 不改变事实与线索：每句话交代的信息（谁、做了什么、什么时候、哪份文件证明什么）保持不变。不补充原文没有的信息，不提前透露后文。
4. 篇幅与原文接近，单条增减不超过约两成。
5. 以「（背景，不改）」开头的段落是说明，原样保留。
6. 按输入的 Markdown 格式输出全文，未修改的条目也要原样输出，不要省略。如果你认为某处应删句、加句或调换顺序，不要直接改，在该条正文下另起一行写「〔建议〕……」。
7. 全部条目之后，用「## 润色说明」简要列出改动较大的条目和理由。

### 本部分的额外要求

- 屏幕和物件上的空间有限，字数不能多于原文。
- 「·」「→」「/」「「」」等分隔符和括号保留。
- 一个标题下列了多个编号，表示同一句文字在多处使用，改一次即可。
- 「⟨…⟩」「⟨数字⟩」是程序填入的内容，保留在原位置。「尾声 · ⟨…⟩」处填入的结局名（开门、缺页、真相易手、背书）也被程序当作判断条件，不改。

---


## 右上角「案头待办」

（背景，不改）随流程提示玩家下一步做什么：短的一条是任务名，完整的一句是操作说明。

#### DeskViewHud.Commands:46

案头待办

#### DeskCommandHints:54a

收取传物舱中的封套

#### DeskCommandHints:54b

点击封套，移至案头拆阅。

#### DeskCommandHints:62, DeskCommandHints:71, DeskCommandHints:78, DeskCommandHints:88, DeskCommandHints:99

候执事送入文书

#### DeskCommandHints:63a

破译师父封袋里的密文

#### DeskCommandHints:63b

依笔记校准转盘，将译文誊于明文纸上。

#### DeskCommandHints:64

译文已留档，候门外来人

#### DeskCommandHints:68

摇铃，请执事调取封档

#### DeskCommandHints:70

摇铃，请执事调取原始档案

#### DeskCommandHints:72a

破译印坊数字便条

#### DeskCommandHints:72b

对照祷文集查字，在明文纸上记下译文。

#### DeskCommandHints:73

候执事调来封档

#### DeskCommandHints:74a

破译封档内的猪圈密文

#### DeskCommandHints:74b

参看猪圈密码表，将碎纸符号还原至明文纸。

#### DeskCommandHints:75

核对封档中留下的线索

#### DeskCommandHints:79a

破译师父译录的军情密文

#### DeskCommandHints:79b

对照委托与师父笔记，在译码台还原并记下明文。

#### DeskCommandHints:81

签署明文还原

#### DeskCommandHints:82

点击核验表上的「明文还原」签名栏。

#### DeskCommandHints:83

Esc 放回手中文书，再打开文书架上的军情核验表。

#### DeskCommandHints:84

打开文书架上的军情核验表，点击明文还原签名栏。

#### DeskCommandHints:85a

核验已签，候门外来人

#### DeskCommandHints:85b

核对已还原的明文

#### DeskCommandHints:90a

破译师父双栏密信

#### DeskCommandHints:90b

分别还原仿射与栅栏两段密文，将译文记在明文纸上。

#### DeskCommandHints:91a

破译双栏密信中的仿射密文

#### DeskCommandHints:91b

另一段译文已留档，继续还原仿射密文并记下明文。

#### DeskCommandHints:92a

破译双栏密信中的栅栏密文

#### DeskCommandHints:92b

另一段译文已留档，继续还原栅栏密文并记下明文。

#### DeskCommandHints:93

两段译文已留档

#### DeskCommandHints:98

候执事收取文书

#### DeskCommandHints:114a

取回当前案卷

#### DeskCommandHints:114b

Esc 放回手中文书，再到左侧书架取回当前案卷。

#### DeskCommandHints:115（⟨…⟩ 处填入卷宗名）

到左侧书架点击「⟨…⟩」，将文书放回案头。

#### DeskCommandHints:116

记下完整译文后，按 Esc 放下羽笔，留存译文。

#### DeskCommandHints:117（接在操作说明末尾，开头的空格保留）

 阅毕按 Esc 放回。

#### DeskCommandHints:121

Esc 放回手中文书，再点击桌上的手铃。

#### DeskCommandHints:122

Esc 收起手边工具，再回桌面中央点击手铃。

#### DeskCommandHints:123a

返回桌面中央，点击手铃唤来执事。

#### DeskCommandHints:123b

点击桌上的手铃唤来执事。

#### DeskCommandHints:125

Esc 放回手中文书，继续办理。

#### DeskCommandHints:126a

Esc 收起手边的工具，继续办理。

#### DeskCommandHints:126b

稍候片刻，留意门外动静。

#### DeskCommandHints:136a

核定来源担保

#### DeskCommandHints:136b

在核验表上择定是否担保来源；Esc 放回文书。

#### DeskCommandHints:138a

登记原件去向

#### DeskCommandHints:138b

在转交申请表上择定受领方；Esc 放回文书。

#### DeskCommandHints:140a

斟酌自述明文的处置

#### DeskCommandHints:140b

可留存随卷递交，或点击纸上焚毁项；Esc 放回文书。

#### DeskCommandHints:143a

核定文书，摇铃交卷

#### DeskCommandHints:143b

Esc 放回手中文书；定妥后点击手铃，请执事收卷。

#### DeskCommandHints:144

自述明文已毁。核对来源担保与原件去向，定妥后摇铃请执事收卷。

#### DeskCommandHints:145

在文书架核对来源担保、原件去向与自述处置；定妥后摇铃请执事收卷。

## 视角与翻书提示

（背景，不改）屏幕角落的操作提示。

#### DeskViewHud:137

滚轮 缩放视角

#### DeskViewHud:140

Esc 返回案头主视角

#### DeskViewHud:147

切换视角

#### DeskViewHud:157

翻页 · ← / → · Esc 放回书册

#### DeskViewHud:158a

翻页中…

#### DeskViewHud:158b

第 ⟨数字⟩–⟨数字⟩ 页 / 共 ⟨数字⟩ 页

## 译码台字母盘上的状态提示

（背景，不改）刻在字母盘边上，随当前工具变化。空间很窄。

#### CipherWorkbenchKeyboard:135

书本 · 页—行—字母，直接键入明文 · Esc 放回

#### CipherWorkbenchKeyboard:136

Enter 完成填写 · 返回字母盘

#### CipherWorkbenchKeyboard:137a

凯撒 · 键入密文 → 明文

#### CipherWorkbenchKeyboard:137b

拨动转盘后可键入密文

#### CipherWorkbenchKeyboard:138

猪圈 · 对照图形，直接键入明文

#### CipherWorkbenchKeyboard:140a（⟨…⟩/⟨…⟩ 是当前用到第几个密钥字母）

密钥 ⟨…⟩/⟨…⟩ · 键入密文 → 明文

#### CipherWorkbenchKeyboard:140b

填写密钥后可键入密文

#### CipherWorkbenchKeyboard:141

仿射 · 对照表为空，直接键入明文

#### CipherWorkbenchKeyboard:142a

仿射 · 键入密文 → 明文

#### CipherWorkbenchKeyboard:142b

填满两行，每行 A–Z 不重复

#### CipherWorkbenchKeyboard:143

栅栏 · 交错拼读两行，直接键入明文

## 书架卷宗的状态标签

（背景，不改）书架每层卷宗上的小签。

#### ActArchiveController:261a

未启封

#### ActArchiveController:261b

案头

#### ActArchiveController:261c

已归档

#### ActArchiveController:261d

暂存

## 原件去向标签

（背景，不改）第五幕转交申请表的选项，也会出现在表上的「当前安排」和第六幕的回执里。

#### FifthActFlow:142a

交米兰

#### FifthActFlow:142b

交威尼斯

#### FifthActFlow:143a

交法国

#### FifthActFlow:143b

继续封存

## 转场字幕

（背景，不改）幕间黑场上的字。

#### ActTransitionOverlay:55

第六幕 · 尾声

#### ActTransitionOverlay:57a, ActTransitionOverlay:57b（⟨…⟩ 处填入幕数）

第⟨…⟩幕

#### EpilogueFlow:146

半小时后
清晨六点半

#### EpilogueFlow:223（⟨…⟩ 处填入结局名）

尾声 · ⟨…⟩
终

## 场景物件上的刻字与标签

（背景，不改）刻在工具、木盘和纸签上的字，空间固定。

#### scene:Stored Caesar/Outer legend

密文 · 外环

#### scene:Stored Caesar/Inner legend

明文 · 内环

#### scene:Cipher tray/Slot 0

转盘

#### scene:Stored Pigpen/Title

猪圈密码表 · 辨明圆点与朝向

#### scene:Cipher tray/Slot 1

猪圈

#### scene:Stored SaintCyr/Title

维吉尼亚密码 · 圣西尔滑尺

#### scene:Stored SaintCyr/Plain legend

明文

#### scene:Stored SaintCyr/Cipher legend

密文

#### scene:Cipher tray/Slot 2

滑尺

#### scene:Stored Affine/Title

仿射密码 · 映射关系

#### scene:Stored Affine/Instructions

上行明文 · 下行密文    y = (a × x + b) mod 26

#### scene:Cipher tray/Slot 3

仿射

#### scene:Stored RailFence/Title

栅栏密码 · 双行纸条

#### scene:Stored RailFence/Instructions

前半填上行，后半填下行；按列上下交错拼读。

#### scene:Cipher tray/Slot 4

栅栏

#### scene:DecodingMat/Mat label

破译工具

#### scene:Plaintext writing slip/Writing heading

明文誊写处

#### scene:LetterTray_QWERTY/Tray inscription

字 母 盘

## 润色说明

1. **统一全局道具与交互名称**：
   - 将 `DeskCommandHints:54a` 中的“传递筒”统一为背景设定与台词中使用的“传物舱”。
   - 将 `DeskCommandHints:72b` 中的“祈祷书”统一为游戏内实际书名“祷文集”。
   - 将略显生硬的工程术语“中桌面”（`DeskCommandHints:122/123a`、`DeskViewHud:140`）润色为更自然的“桌面中央”与“案头主视角”，且严控字数不超长。
2. **提升界面与刻字的古典质感**：
   - 书架卷宗状态标签中，将现代感较强的“未开始”“桌面”（`ActArchiveController:261a/b`）调整为更贴合古籍案卷管理的“未启封”“案头”。
   - 将操作提示中的“呼叫执事”改为“唤来执事”，“阅读后”精简为“阅毕”，“明文书写处”润色为“明文誊写处”，在严守字数上限的前提下增强时代氛围。