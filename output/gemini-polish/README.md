# 文本润色与英文本地化

2026-11-03：中文润色稿已写回游戏，英文版作为游戏内本地化接入。Play Mode 中按 F1，在面板顶部的“语言 / Language”切换。

2026-11-03 更新：中文对白润色已撤回，包括对白、独白、对话选项及结局动态台词，恢复到 `62d7ba35` 的文字。纸面文书和界面润色保留。生成器以 `1-台词.md` 为中文对白来源，`1_polished.md` 仅作历史对照，不再回填；英文稿 `1_en.md` 保留，并映射到恢复后的中文。原 `apply-report.txt`、`delivery-check.json` 和 `validation/` 记录的是润色提交时的验证结果。

撤回后的验证见 [对白恢复核对](../dialogue-rollback-validation/restoration.txt) 与 [Unity Play Mode 报告](../dialogue-rollback-validation/validation.txt)：37 份对白资源与原提交一致，144 条对白相关条目已恢复，6934 项本地化检查通过。

## 文件

| 文件 | 内容 |
|---|---|
| `1-台词.md`、`2-纸面文书.md`、`3-界面与物件文字.md` | 润色前从游戏导出的原文包（`build_gemini_pack.py` 生成），作为比对基准 |
| `1_polished.md` | 已撤回的对白润色稿，仅供历史对照 |
| `2_polished.md`、`3_polished.md` | 仍在使用的文书与界面中文润色稿 |
| `*_en.md` | 英文稿 |
| `scene-text-before-polish.json` | 导出原文包时的场景文字快照，用于把纸面文字逐段对回场景对象 |
| `build_localization.py` | 生成下面三项数据，并改写 C# 中的中文字面量、更新 `doc/performance-edit` 逐幕稿 |
| `zh-revision.json` / `dialogue-revision.json` | 原文 → 润色稿（场景与预制体按文字，对话资源按事件、句号与选项编号） |
| `Assets/Resources/PHLocalization/English.json` | 当前中文（原版对白、润色后的文书与界面）→ 英文，游戏运行时读取 |
| `LocalizationApply.cs` | 在隔离项目中写回润色稿；变长的纸面文字按原区域缩小字号。结果见 `apply-report.txt` |
| `LocalizationValidation.cs` | Play Mode 验证与中英对照截图，结果见 `validation/` |
| `verify_delivery.py` | 核对主项目文件与验证副本一致、GUID 可解析，结果见 `delivery-check.json` |

## 修改英文后重新生成

1. 编辑 `*_en.md`，保留所有标题行与编号。
2. 在仓库根目录运行 `python -X utf8 output/gemini-polish/build_localization.py`。脚本会检查所有显示中的中文都有英文，缺失时报错。
3. 英文只在运行时查表，不需要重建场景。较大改动后，在 `Temp/localization-sync` 隔离项目中运行 `LocalizationValidation.Run` 复查排版。

中文仍是所有资源、场景与代码中的原文。新增或改动中文时，需要在英文稿中补上对应条目。

## 已知限制

- 纸面英文按原中文所占宽度与行数等比缩小；居中的单行标题可以扩展到纸面可用宽度。少数长栏目名（如台账格内的条目）缩到原字号的 0.4–0.5 倍，检视时可读，但比中文小。
- 结局名（开门 / 缺页 / 真相易手 / 背书）的英文 Open Door、The Missing Page、Truth Changes Hands、Endorsement，以及第零幕 “Prologue”、滑尺标签 “Align current key letter with upper A”，不在英文稿中，由 `build_localization.py` 补充。
- 英文对白逐字速度为中文设定的两倍。
- 重新运行 `build_gemini_pack.py` 会用当前游戏文本覆盖 1–3 号原文包；这些原文包是本次润色的比对基准，请勿覆盖。
