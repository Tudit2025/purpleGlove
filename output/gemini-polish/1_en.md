# The Purple Glove · Text Localization · Part 1: Dialogue

## Notes for Localizers

This is the in-game dialogue for the narrative puzzle game *The Purple Glove*. The English localization maintains a restrained, clear, period-appropriate tone suited to an alternate-history Italian city-state setting, avoiding modern slang while keeping sentences concise for typewriter-style dialogue boxes.

---


## Act 0 · The Threshold

（背景，不改）时间：六月十五日晚八点前。

### 0-01 门外自述与入场

（背景，不改）触发：进入第零幕。演出：门外固定机位。卢卡独白，执事画外说话，不显示人物正脸。

#### prologue.L01｜卢卡

I am a student of Master Cryptographer Matteo Ricci.

#### prologue.L02｜卢卡

We serve the Printing Guild—translating books and handling confidential dispatches.

#### prologue.L03｜卢卡

Days ago, my master was summoned on business for the Duke of Milan. He has not returned, and his whereabouts remain unknown.

#### prologue.L04｜卢卡

Word on the street says he betrayed the Guild to forge documents for the Duke.

#### prologue.L05｜卢卡

Now, the Guild has received an urgent commission to verify a military order entangling several powers. Milan intends to use it to seize Venice's border granaries.

#### prologue.L06｜卢卡

Milan, France, and Venice jointly commissioned the Guild to confirm that the ciphertext matches the plaintext and that its provenance is sound. At six tomorrow morning—June sixteenth—our findings will serve as the basis for their vote.

#### prologue.L07｜卢卡

As the substitute "Master Cryptographer," I have been called in to handle this verification.

#### prologue.L08｜卢卡

I must uncover the truth and get my master out.

#### prologue.L09｜行会执事

Master Luca, the verification chamber is ready.

#### prologue.L10｜行会执事

Per Guild protocol, the door will be sealed once you enter. You may not leave until tomorrow's vote concludes.

#### prologue.L11｜卢卡

Understood. I am going in.

## Act 1 · Keep the Original

（背景，不改）时间：六月十五日晚八点至晚十点。

### 1-01 执事介绍首批来件

（背景，不改）触发：第一幕淡入完成，等待五秒；完整播放敲门声。演出：镜头转向门，观察窗打开，执事介绍两个包裹与转交申请表。

#### act1_arrival.L01｜行会执事

Luca, the Duke's verification commission has arrived, along with a letter Master Matteo... deposited before his departure.

#### act1_arrival.L02｜行会执事

The brown packet contains reference files prepared by the Guildmaster. He urged that you read them without fail.

#### act1_arrival.L03｜行会执事

The Guildmaster said we are to assist you in every way. Should you need me, simply ring the handbell on your desk.

#### act1_arrival.L04｜行会执事

Both items are logged. I will pass them through the hatch below.

### 1-02 看见三道裁口

（背景，不改）触发：首次检视师父密文纸。演出：保持纸张检视，卢卡回忆；本次游玩只播放一次。

#### memory_three_cuts.L01｜卢卡

My master's hand. Three deliberate notches along the edge... This must be a Caesar cipher.

### 1-03 执事介绍米兰代表

（背景，不改）触发：凯撒明文实际留存后等待五秒，再响敲门声。演出：等完成提示读完，转向门边，由执事介绍书记官。

#### act1_secretary_arrival.L01｜行会执事

Luca, the envoy from Milan—Secretary to Duke Conti—has arrived.

### 1-04 书记官第一次催促

（背景，不改）触发：执事介绍结束、观察窗关闭满两秒。演出：书记官先逐字说完开场，玩家推进后单独显示三项回应；选项上方不重复书记官台词。点击选择后只播放对应的书记官回答。

#### act1_secretary.L01｜公爵书记官

Master Luca, the documents for verification will arrive shortly. The matter is of grave importance; please ensure both the text and its provenance are found in order.

#### act1_secretary.ask_matteo｜选项

Where is Master Matteo now?

#### act1_secretary.no_guarantee｜选项

I cannot guarantee anything unseen.

#### act1_secretary.silence｜选项

...

#### act1_secretary.L02｜公爵书记官

Master Matteo is presently assisting us with a matter of confidentiality.

#### act1_secretary.L03｜公爵书记官

Of course, of course. Proceed according to Guild rules.

#### act1_secretary.L04｜公爵书记官

It is most reassuring that you have no objections.

### 1-05 桌前总结

（背景，不改）触发：书记官交谈结束并回到桌面。演出：卢卡桌前独白。

#### act1_summary.L01｜卢卡

My master told me to "keep the original." Does he mean the military order that hasn't arrived yet?

#### act1_summary.L02｜卢卡

The dispatch isn't even here, yet Milan already sent someone to press me. They definitely have my master.

#### act1_summary.L03｜卢卡

"Seven-petal" must refer to the newly bound *Book of Prayers*. A custom edition—clearly meant to serve as a book cipher key.

## Act 2 · Case File Fifty-Seven

（背景，不改）时间：六月十五日晚十点至十六日零点。

### 2-01 贝阿特丽切送来旧便条

（背景，不改）触发：第二幕转场完成。演出：执事报身份，贝阿特丽切隔门说明。

#### beatrice.L01｜行会执事

Luca, Lady Beatrice, the representative from Venice, is here to see you. She brings a document.

#### beatrice.L02｜贝阿特丽切

So you are Master Luca. I have been looking into this affair for several days. One of your Guild colleagues slipped me a note.

#### beatrice.L03｜贝阿特丽切

He feared being dragged in as a witness, so he wrote it in cipher. You should be able to break it.

#### beatrice.L04｜贝阿特丽切

I have a fair idea of what it says, but given Venice's position, I cannot pursue this lead myself.

#### beatrice.L05｜卢卡

Pass the note through. I will verify it myself.

#### beatrice.L06｜行会执事

The note from the Venetian representative is logged. Sending it in now.

### 2-02 卢卡提示摇铃

（背景，不改）触发：书本密码明文实际留存，其他演出空闲。演出：桌前独白；共用完成提示按实际播放状态排队。

#### act2_ring_hint.L01｜卢卡

"Case File Fifty-Seven"... The plaintext is recorded. Time to ring the bell and have the Clerk fetch that file.

### 2-03 第一次调档

（背景，不改）触发：玩家摇铃。演出：镜头转向门，执事应铃。

#### act2_request.L01｜行会执事

Luca, how may I assist you?

#### act2_request.L02｜卢卡

Please retrieve Guild Sealed File Fifty-Seven. I need it for tonight's verification.

#### act2_request.L03｜行会执事

File Fifty-Seven... I believe that concerns a workflow dispute in the print shop. I will fetch it at once; please wait a moment.

### 2-04 第五十七号封档送达

（背景，不改）触发：调档等待结束。演出：执事隔门介绍来源与内容。

#### act2_archive_arrival.L01｜行会执事

Sealed File Fifty-Seven is here. It was deposited by Bartolo the typesetter, an old acquaintance of your master.

#### act2_archive_arrival.L02｜行会执事

It contains a ledger abstract and a trimmed paper scrap. Passing them in now.

### 2-05 怀疑与等待

（背景，不改）触发：猪圈明文实际留存，第二幕补充文件已接收。演出：镜头回桌面，卢卡总结。

#### act2_summary.L01｜卢卡

"Left glove"... Bartolo noted the mark of the man who ordered the plate changes. The Duke's commission mentioned that Carafa, who collects materials for Milan, wears a purple glove on his left hand.

#### act2_summary.L02｜卢卡

This Carafa is definitely mixed up in this. Chances are he has been operating around the Guild for quite some time...

#### act2_summary.L03｜卢卡

If I can retrieve that "original receipt page," I can prove how these two matters connect.

#### act2_summary.L04｜卢卡

If the plate-change record points to tonight's military order, Milan's scheme is clear: they forged the dispatch, then forced my master to sign as guarantor.

### 2-06 申请原始档案

（背景，不改）触发：玩家在桌前总结后再次摇响并放回手铃。演出：执事应铃；卢卡按封档附件列出的第17号委托、批号726申请原始档案。

#### act2_original_request.L01｜行会执事

Yes, Luca?

#### act2_original_request.L02｜卢卡

Please retrieve the original archive referenced in Sealed File Fifty-Seven—Commission No. 17, Batch 726.

#### act2_original_request.L03｜行会执事

Understood. I will send someone to the vaults. Judging by the number, though, the originals are sealed in the inner repository—it will take some time.

## Act 3 · A Legible Lie

（背景，不改）时间：六月十六日零点至凌晨三点。

### 3-01 正式核验件到达

（背景，不改）触发：第三幕淡入完成。演出：敲门、开观察窗，执事报午夜、案号与批号。

#### act3_arrival.L01｜行会执事

Luca, the main files for tonight's commission—the military dispatch transcription—have arrived. They were indeed printed by our Guild.

#### act3_arrival.L02｜行会执事

There are five documents in the packet: the guild's transcription commission, bearing your master's own signature; his encrypted military transcript and working note; and a verification form.

#### act3_arrival.L03｜行会执事

Lastly, there is the French source dispatch signed by the French envoy. By the look of the paper, it came from our presses as well.

#### act3_arrival.L04｜卢卡

Understood. Please send them through.

### 3-01a 卢卡提示明文签名

（背景，不改）触发：第三幕正确明文实际留存，通用完成提示读完，且当前没有其他对话或物品检视。演出：保持当前镜头，播放卢卡自白。完整读完后才开放明文还原签名；本次游玩只完成一次，中途取消可恢复重播。

#### act3_signature_hint.L01｜卢卡

Now that the plaintext is confirmed, I should sign the Plaintext Restoration section on the Verification Form first.

### 3-02 夏尔介入

（背景，不改）触发：玩家完成明文还原签名，核验表收回。演出：门边交谈；为何不公开的追问已在当前固定对话中。

#### act3_charles.L01｜行会执事

Luca, Monsieur Charles, the representative from France, is here to see you.

#### act3_charles.L02｜夏尔

Good evening, Master Luca. If that cipher claims French troops will cross the border on June eighteenth, do not vouch for its provenance.

#### act3_charles.L03｜夏尔

Provide me with solid documentary proof, and France will do everything in its power to protect your master.

#### act3_charles.L04｜卢卡

Isn't that your signature on the French dispatch? If it is a forgery, why not denounce it openly?

#### act3_charles.L05｜夏尔

The signature is genuinely mine. However, I cannot let it be known that I privately signed *another* document with Milan—so I cannot disavow it without proof.

#### act3_charles.L06｜夏尔

I will put my promise in writing under my own hand. Please weigh my words carefully tonight.

#### act3_charles.L07｜行会执事

The supplementary note is logged under Commission No. 17, Batch 726. Once the window is shut, I will pass it through the hatch.

### 3-03 书记官要求归还原件

（背景，不改）触发：夏尔凭条实际接收后等待两秒。演出：执事介绍米兰代表，书记官隔门施压，卢卡拒绝提前交走原件。原始档案已在第二幕申请。

#### act3_secretary.L01｜行会执事

The representative from Milan has returned.

#### act3_secretary.L02｜公爵书记官

Master Luca, you must have completed the decryption by now. Since the text is in order, kindly return the accompanying originals to us.

#### act3_secretary.L03｜卢卡

Protocol requires that I retain all originals until provenance verification is complete.

#### act3_secretary.L04｜公爵书记官

...

#### act3_secretary.L05｜公爵书记官

Duke Conti awaits your conclusion. Kindly expedite your work, Master Luca.

### 3-04 卡拉法淡化换版

（背景，不改）触发：书记官交谈结束。演出：执事介绍，卡拉法在观察窗外说话。

#### act3_carafa.L01｜行会执事

Signor Carafa, representing Milan, is here.

#### act3_carafa.L02｜卡拉法

No need to make this so complicated, young man. Do not turn an old typesetting dispute into a diplomatic incident.

### 3-05 读得通的谎言

（背景，不改）触发：卡拉法交谈结束并回到桌面。演出：卢卡桌前独白。

#### act3_summary.L01｜卢卡

The transcription and encryption are indeed my master's work. Did no one warn him that the French source sheet itself was suspect?

#### act3_summary.L02｜卢卡

Or am I mistaken—was the plate change just an innocent print shop error? I won't know for certain until the original archive arrives from the vault.

#### act3_summary.L03｜卢卡

Milan's people are growing impatient. Who knows what else they might try before dawn.

## Act 4 · A Name Left on Paper

（背景，不改）时间：六月十六日凌晨三点至五点。

### 4-01 两份独立封件

（背景，不改）触发：第四幕淡入完成。演出：执事报凌晨三点，分别说明行会库与修道院的送件来路。

#### act4_arrival.L01｜行会执事

Luca, two separate packets have just arrived.

#### act4_arrival.L02｜行会执事

The grey-white sleeve holds the original vault archive you requested for Commission No. 17, Batch 726.

#### act4_arrival.L03｜行会执事

It appears to contain drafts of a trade transit agreement.

#### act4_arrival.L04｜行会执事

The brown packet came from the monastery north of the city. A monk brought it in person, insisted it be handed to you, and left immediately.

#### act4_arrival.L05｜行会执事

I have logged the brown packet as "personal correspondence."

#### act4_arrival.L06｜卢卡

Thank you. Please send them both in.

### 4-02 救他与替他辩白

（背景，不改）触发：地点与自述两份明文都已落字、实际留存。演出：镜头从破解区回桌面。自述明文已收入立夹。

#### act4_summary.L01｜卢卡

"North Gate, Saint Martin"... That must be where they are holding him.

#### act4_summary.L02｜卢卡

He admits he knew the order was false, yet signed his name to save Bartolo's family.

#### act4_summary.L03｜卢卡

The forged military order reused Charles's genuine signature from the trade transit agreement. No wonder the French dared not speak out openly.

#### act4_summary.L04｜卢卡

My master's confession and the plate-change originals are the decisive proof. Now, the choice of how to conclude this falls to me.

## Act 5 · Signing and Submission

（背景，不改）时间：六月十六日清晨五点至交卷。

### 5-01 代表来信与默认安排

（背景，不改）触发：第五幕开始。演出：执事在观察窗外报五点，介绍三封信与三项决定。

#### act5_arrival.L01｜行会执事

Luca, the representatives of Milan, France, and Venice have each sent in a formal letter.

#### act5_arrival.L02｜行会执事

It is five o'clock. The morning session is approaching, and I must soon collect your verification report.

#### act5_arrival.L03｜行会执事

The Guildmaster anticipated you might need to transfer certain files. If so, simply fill out the Archive Transfer Request Form.

#### act5_arrival.L04｜行会执事

Once your decisions are finalized, ring the handbell and I will take the dossier to the council.

### 5-02 选择来源担保时

（背景，不改）触发：来源担保由否改为是。演出：保留当前核验表阅读位置，卢卡提醒；交卷前仍可更改。

#### act5_source_warning.L01｜卢卡

This military order is clearly a spliced forgery, and the cipher is a smokescreen... Should I really vouch for Milan's provenance?

### 5-03 焚毁自述的确认

（背景，不改）触发：打开自述明文，点击焚毁操作。演出：同时显示两个可点击选项，不逐字显示、不播放打字声。

#### act5_burn_choice.L01｜卢卡

My master's statement is vital evidence, yet submitting it will formally incriminate him and place him in grave danger.

#### act5_burn_choice.burn｜选项

Hold it to the candle and burn the statement.

#### act5_burn_choice.keep｜选项

Keep it and submit it with the verification files.

### 5-04 摇铃交卷的确认

（背景，不改）触发：收到第五幕三封信后拿起手铃。演出：同时显示两个可点击选项；此时还没有实际摇响。

#### act5_ring_choice.L01｜卢卡

Ring the bell now and have the Clerk collect the files as currently arranged?

#### act5_ring_choice.ring｜选项

Ring the bell

#### act5_ring_choice.wait｜选项

Not yet

### 5-05 执事接件

（背景，不改）触发：玩家确认摇铃，手铃动作完成。演出：镜头转向门边，执事取件并说明仍不能离开；三项选择按纸面记录办理。

#### act5_handoff.L01｜行会执事

Luca, I am here to collect the verification dossier.

#### act5_handoff.L02｜行会执事

Per protocol, you must remain inside until the morning session concludes. I will return with word later today.

## Act 6 · Epilogue

（背景，不改）时间：六月十六日清晨六点；重逢支线至六点半。

### 6-01 开门／缺页共用：六点回报

（背景，不改）触发：不担保来源，原件继续由行会封存。演出：第六幕淡入；敲门、打开探视口，主门仍关着。

#### act6_saved_report.L01｜行会执事

Luca, I have returned.

#### act6_saved_report.L02｜行会执事

Upon reading your report, Venice and France demanded an immediate halt to the granary vote. Carafa has been detained pending investigation.

#### act6_saved_report.L03｜行会执事

Word of the plate switch is out. Milan refused public inspection, but the originals remain safely sealed with the Guild.

#### act6_saved_report.L04｜行会执事

Milan finally conceded that Master Matteo is being held at the North Gate of Saint Martin Monastery.

#### act6_saved_report.L05｜卢卡

So they called off his transfer?

#### act6_saved_report.L06｜行会执事

They did. I also brought the council receipts—passing them in now.

### 6-02 开门／缺页共用：六点半接回

（背景，不改）触发：六点半转场完成。演出：执事从探视口报告接回凭据；师父还没有开始说话。

#### act6_reunion_arrival.L01｜行会执事

Luca, witnesses from all three parties have jointly escorted Master Matteo Ricci back.

#### act6_reunion_arrival.L02｜卢卡

He is right outside?

#### act6_reunion_arrival.L03｜行会执事

Yes. I will bring him to the window.

### 6-03 结局一：开门

（背景，不改）触发：不担保来源、原件继续封存、自述保留。演出：师父和卢卡隔着探视口重逢。

#### act6_open.L01｜马泰奥

Luca.

#### act6_open.L02｜卢卡

Master, I turned in your written statement.

#### act6_open.L03｜马泰奥

As you should have.

#### act6_open.L04｜卢卡

They will likely subject you to many more questions.

#### act6_open.L05｜马泰奥

I know. Thank you, Luca.

#### act6_open.L06｜马泰奥

We still have a long road ahead of us...

### 6-04 结局二：缺页

（背景，不改）触发：不担保来源、原件继续封存、自述焚毁。演出：与开门共用前两段回报和收件，只替换这一段重逢。

#### act6_missing.L01｜马泰奥

Luca, did you submit the confession I enclosed for you?

#### act6_missing.L02｜卢卡

In the end, I burned it.

#### act6_missing.L03｜马泰奥

Why?

#### act6_missing.L04｜卢卡

If that page had been seen, they would never have let you go, and the Guild would have beencompromised as well.

#### act6_missing.L05｜马泰奥

...

#### act6_missing.L06｜马泰奥

I understand. Whatever remains, I will answer for in person.

#### act6_missing.L07｜行会执事

The formalities are complete, Luca. I am unsealing the door for you now.

### 6-05 真相易手：六点回报

（背景，不改）触发：不担保来源，原件交米兰、威尼斯或法国。演出：执事从探视口说明共同审查与暂停转押。

#### act6_transfer_report.L01｜行会执事

Luca, I have returned.

#### act6_transfer_report.L02｜行会执事

Upon reading your report, Venice and France demanded an immediate halt to the granary vote. Carafa has been detained pending investigation.

#### act6_transfer_report.L03｜行会执事

Word of the plate switch is out, and the original files have been transferred according to your request.

#### act6_transfer_report.L04｜行会执事

I have brought the council receipts—passing them in now.

### 6-06 结局三：真相易手

（背景，不改）触发：真相易手的处理回执已落桌。演出：执事按原件去向说明后果。自述是否焚毁不改变这段尾声。

#### act6_transferred.L01｜行会执事

{去向后果}

#### act6_transferred.L02｜卢卡

What will happen to my master now?

#### act6_transferred.L03｜行会执事

Master Matteo must remain in custody pending inquiry. And since the originals have changed hands, any future inspection will require the holder's consent.

#### act6_transferred.L04｜行会执事

The Guild will keep these receipts on file. Should there be further word, we will notify you.

#### epilogue.Milan.transferred｜替换 {去向后果}（原件交米兰）

Per your request, the plate-change originals have been signed for by Milan.

#### epilogue.Venice.transferred｜替换 {去向后果}（原件交威尼斯）

The plate-change originals have been delivered to the Venetian Committee. Lady Beatrice took Master Matteo into her custody; we will have to negotiate with Venice to see him now.

#### epilogue.France.transferred｜替换 {去向后果}（原件交法国）

The plate-change originals have been signed for by Monsieur Charles. France is currently focused on addressing the misused signature, so it will be some time before Master Matteo is returned.

### 6-07 背书：六点回报

（背景，不改）触发：签了来源担保；原件去向、自述状态任意。演出：执事从探视口报告已经转押；不能写成师父已死亡。

#### act6_endorsed_report.L01｜行会执事

Luca, the Milanese have taken Master Matteo away.

#### act6_endorsed_report.L02｜卢卡

What? Could you not stop them?

#### act6_endorsed_report.L03｜行会执事

You vouched for the provenance of the military order. That gave Milan the upper hand at this morning's council.

#### act6_endorsed_report.L04｜卢卡

Where have they taken him?

#### act6_endorsed_report.L05｜行会执事

We do not know yet. The Guild is making inquiries.

#### act6_endorsed_report.L06｜行会执事

Here are the council receipts for your review.

### 6-08 结局四：背书

（背景，不改）触发：背书的处理回执已落桌。演出：执事按原件去向说明后果。自述是否焚毁不改变这段尾声。

#### act6_endorsed.L01｜行会执事

{去向后果}

#### act6_endorsed.L02｜卢卡

I should never have vouched for Milan... With that signature, we have lost all leverage.

#### act6_endorsed.L03｜卢卡

My master signed his name under duress and invited disaster; in the end, I repeated the very same mistake.

#### act6_endorsed.L04｜行会执事

The Guild will continue tracing his whereabouts. I will inform you if anything comes to light.

#### epilogue.Milan.endorsed｜替换 {去向后果}（原件交米兰）

The Duke reclaimed the originals and secured your provenance guarantee. I fear they will hold Master Matteo until their ambitions are fully realized.

#### epilogue.Venice.endorsed｜替换 {去向后果}（原件交威尼斯）

The Venetian Committee received the plate-change originals, but because of your provenance guarantee, they were left on the defensive all morning.

#### epilogue.France.endorsed｜替换 {去向后果}（原件交法国）

Monsieur Charles signed for the originals but declined to make them public. Instead, he demands to know why you vouched for a dispatch never countersigned by France.

#### epilogue.Guild.endorsed｜替换 {去向后果}（原件继续封存）

The originals remain sealed with the Guild for the three parties to inspect, yet because of that guarantee, our entire night's work has come to naught...

## 共用独白与物品回忆

### 首次看旧版祷文集

（背景，不改）

#### memory_prayer_old.L01｜卢卡

This common old edition of the *Book of Prayers* was my reference text when I first studied cryptography. Many book ciphers rely on it.

### 首次看七瓣花版祷文集

（背景，不改）

#### memory_prayer_flower.L01｜卢卡

This Seven-Petal edition of the *Book of Prayers* was custom-commissioned by my master and used internally within the Guild.

### 首次看师父旧笔记

（背景，不改）

#### memory_notebook.L01｜卢卡

My master's old notebook. It contains his lesson plans on cryptography.

### 首次用解密工具箱

（背景，不改）

#### memory_toolbox.L01｜卢卡

My decryption toolbox. For standard ciphers, these instruments spare me a great deal of manual calculation.

### 本幕第一次留存明文

（背景，不改）

#### plaintext_complete.L01｜卢卡

It is done. I should file this plaintext away safely.

### 当前不能摇铃

（背景，不改）

#### handbell_not_needed.L01｜卢卡

There is no need to ring the bell right now.
