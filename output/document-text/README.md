# 流程文书导出

面向阅读和修改的文件：

- [Markdown 合订本](../../doc/紫手套—流程文书全文.md)
- [Word 合订本](紫手套—流程文书全文.docx)
- [纯文本合订本](紫手套—流程文书全文.txt)

收录 30 份／本文书与参考资料，包括 19 份原始来件、6 份解密留档、3 本参考书、2 份回执。另整理核验表和转交表的填写状态、焚毁提示及处理回执的全部 8 种正文。

`scene-text.json` 为当前场景的文字快照，同时保留场景中其他文字节点和可检查物件清单供核对。明文记录按解密完成后的文字导出。`manifest.json` 记录来源提交、源文件校验值和数量；`validation.txt` 记录三种阅读格式的逐项文字覆盖检查结果。

更新步骤：

1. 将主项目最新的 `Assets`、`Packages`、`ProjectSettings` 同步到 `Temp/performance-sync-validation` 隔离项目，保留其 Library 缓存。
2. 将 `DocumentTextExport.cs` 复制到隔离项目的 `Assets/Editor`，用 Unity 2022.3.62f2 的批处理模式执行 `DocumentTextExport.Run`。导出器只允许在该隔离项目运行，不保存场景，也不调用任何场景重建菜单。
3. 在仓库根目录执行 `python -X utf8 output/document-text/build_document_text.py`，重新生成三种格式及核对结果。生成脚本复用 `output/performance-edit/build_review_pack.py` 的 Word/TXT 排版函数。

文书拓扑或新增纸面变化后，应同步调整生成脚本的文书清单与出现时机；脚本会检查是否覆盖导出的全部文书、正文、笔记页及动态文字。
