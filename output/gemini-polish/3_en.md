# The Purple Glove · Text Localization · Part 3: UI & Object Inscriptions

## Notes for Localizers

This file contains UI prompts, workbench status hints, transition cards, and 3D object inscriptions. Strings are kept ultra-compact to fit narrow UI banners, book spines, and engraved wooden slots.

---


## 右上角「案头待办」

（背景，不改）随流程提示玩家下一步做什么：短的一条是任务名，完整的一句是操作说明。

#### DeskViewHud.Commands:46

Desk Tasks

#### DeskCommandHints:54a

Collect packet from hatch

#### DeskCommandHints:54b

Click packet to open on desk.

#### DeskCommandHints:62, DeskCommandHints:71, DeskCommandHints:78, DeskCommandHints:88, DeskCommandHints:99

Await Clerk's delivery

#### DeskCommandHints:63a

Decrypt Master's enclosed cipher

#### DeskCommandHints:63b

Set wheel per notes; write plaintext on slip.

#### DeskCommandHints:64

Plaintext filed; await visitor

#### DeskCommandHints:68

Ring bell to request sealed file

#### DeskCommandHints:70

Ring bell for original archive

#### DeskCommandHints:72a

Decrypt print shop number note

#### DeskCommandHints:72b

Check Prayer Book; write plaintext on slip.

#### DeskCommandHints:73

Await sealed file from Clerk

#### DeskCommandHints:74a

Decrypt Pigpen scrap in file

#### DeskCommandHints:74b

Use Pigpen key to transcribe scrap onto slip.

#### DeskCommandHints:75

Review clues in sealed file

#### DeskCommandHints:79a

Decrypt Master's military cipher

#### DeskCommandHints:79b

Check commission & notes; decrypt at workbench.

#### DeskCommandHints:81

Sign Plaintext Restoration

#### DeskCommandHints:82

Click "Plaintext Signature" on Verification Form.

#### DeskCommandHints:83

Esc to put down file; open Verification Form on rack.

#### DeskCommandHints:84

Open Verification Form on rack; click Plaintext Signature.

#### DeskCommandHints:85a

Form signed; await visitor

#### DeskCommandHints:85b

Review restored plaintext

#### DeskCommandHints:90a

Decrypt Master's two-column letter

#### DeskCommandHints:90b

Decrypt Affine & Rail Fence parts; record on slips.

#### DeskCommandHints:91a

Decrypt Affine column

#### DeskCommandHints:91b

Other part filed; decrypt Affine column onto slip.

#### DeskCommandHints:92a

Decrypt Rail Fence column

#### DeskCommandHints:92b

Other part filed; decrypt Rail Fence column onto slip.

#### DeskCommandHints:93

Both plaintexts filed

#### DeskCommandHints:98

Await Clerk to collect files

#### DeskCommandHints:114a

Retrieve current dossier

#### DeskCommandHints:114b

Esc to put down file; retrieve dossier from left shelf.

#### DeskCommandHints:115（⟨…⟩ 处填入卷宗名）

Click "⟨…⟩" on left shelf to return it to desk.

#### DeskCommandHints:116

Once complete, press Esc to set down quill and file.

#### DeskCommandHints:117（接在操作说明末尾，开头的空格保留）

 Press Esc after reading.

#### DeskCommandHints:121

Esc to put down file, then click handbell.

#### DeskCommandHints:122

Esc to put away tool, then click handbell on desk.

#### DeskCommandHints:123a

Return to main desk; click handbell for Clerk.

#### DeskCommandHints:123b

Click handbell on desk to summon Clerk.

#### DeskCommandHints:125

Esc to put down file and continue.

#### DeskCommandHints:126a

Esc to put away tool and continue.

#### DeskCommandHints:126b

Wait a moment; listen at the door.

#### DeskCommandHints:136a

Decide Provenance Guarantee

#### DeskCommandHints:136b

Select provenance option on form; Esc to put down.

#### DeskCommandHints:138a

Log Original Custody

#### DeskCommandHints:138b

Select recipient on Transfer Form; Esc to put down.

#### DeskCommandHints:140a

Decide fate of Master's Statement

#### DeskCommandHints:140b

Keep with dossier, or click burn prompt; Esc to put down.

#### DeskCommandHints:143a

Finalize files; ring bell to submit

#### DeskCommandHints:143b

Esc to put down file; ring handbell when ready.

#### DeskCommandHints:144

Statement burned. Check provenance & transfer, then ring bell.

#### DeskCommandHints:145

Check provenance, transfer & statement on rack; ring bell when ready.

## 视角与翻书提示

（背景，不改）屏幕角落的操作提示。

#### DeskViewHud:137

Scroll Zoom View

#### DeskViewHud:140

Esc Return to Main Desk

#### DeskViewHud:147

Switch View

#### DeskViewHud:157

Turn Page · ← / → · Esc Put Down Book

#### DeskViewHud:158a

Turning page…

#### DeskViewHud:158b

Pages ⟨数字⟩–⟨数字⟩ / ⟨数字⟩

## 译码台字母盘上的状态提示

（背景，不改）刻在字母盘边上，随当前工具变化。空间很窄。

#### CipherWorkbenchKeyboard:135

Book · Page–Line–Letter, type plaintext · Esc Back

#### CipherWorkbenchKeyboard:136

Enter Confirm · Return to Letter Tray

#### CipherWorkbenchKeyboard:137a

Caesar · Type Cipher → Plaintext

#### CipherWorkbenchKeyboard:137b

Rotate wheel before typing ciphertext

#### CipherWorkbenchKeyboard:138

Pigpen · Match symbol, type plaintext

#### CipherWorkbenchKeyboard:140a（⟨…⟩/⟨…⟩ 是当前用到第几个密钥字母）

Key ⟨…⟩/⟨…⟩ · Type Cipher → Plaintext

#### CipherWorkbenchKeyboard:140b

Enter keyword before typing ciphertext

#### CipherWorkbenchKeyboard:141

Affine · Table empty, type plaintext

#### CipherWorkbenchKeyboard:142a

Affine · Type Cipher → Plaintext

#### CipherWorkbenchKeyboard:142b

Fill both rows, A–Z without repeats

#### CipherWorkbenchKeyboard:143

Rail Fence · Read alternating, type plaintext

## 书架卷宗的状态标签

（背景，不改）书架每层卷宗上的小签。

#### ActArchiveController:261a

Unopened

#### ActArchiveController:261b

On Desk

#### ActArchiveController:261c

Archived

#### ActArchiveController:261d

Stashed

## 原件去向标签

（背景，不改）第五幕转交申请表的选项，也会出现在表上的「当前安排」和第六幕的回执里。

#### FifthActFlow:142a

Deliver to Milan

#### FifthActFlow:142b

Deliver to Venice

#### FifthActFlow:143a

Deliver to France

#### FifthActFlow:143b

Retain in Guild Seal

## 转场字幕

（背景，不改）幕间黑场上的字。

#### ActTransitionOverlay:55

Act VI · Epilogue

#### ActTransitionOverlay:57a, ActTransitionOverlay:57b（⟨…⟩ 处填入幕数）

Act ⟨…⟩

#### EpilogueFlow:146

Half an Hour Later
6:30 AM

#### EpilogueFlow:223（⟨…⟩ 处填入结局名）

Epilogue · ⟨…⟩
FIN

## 场景物件上的刻字与标签

（背景，不改）刻在工具、木盘和纸签上的字，空间固定。

#### scene:Stored Caesar/Outer legend

Cipher · Outer

#### scene:Stored Caesar/Inner legend

Plain · Inner

#### scene:Cipher tray/Slot 0

Wheel

#### scene:Stored Pigpen/Title

Pigpen Key · Note Dots & Openings

#### scene:Cipher tray/Slot 1

Pigpen

#### scene:Stored SaintCyr/Title

Vigenère · Saint-Cyr Slide

#### scene:Stored SaintCyr/Plain legend

Plain

#### scene:Stored SaintCyr/Cipher legend

Cipher

#### scene:Cipher tray/Slot 2

Slide

#### scene:Stored Affine/Title

Affine Cipher · Mapping Table

#### scene:Stored Affine/Instructions

Upper: Plain · Lower: Cipher    y = (a × x + b) mod 26

#### scene:Cipher tray/Slot 3

Affine

#### scene:Stored RailFence/Title

Rail Fence · Two-Row Strips

#### scene:Stored RailFence/Instructions

First half upper, second half lower; read alternating by column.

#### scene:Cipher tray/Slot 4

Rails

#### scene:DecodingMat/Mat label

Cipher Tools

#### scene:Plaintext writing slip/Writing heading

Plaintext Slip

#### scene:LetterTray_QWERTY/Tray inscription

L E T T E R   T R A Y