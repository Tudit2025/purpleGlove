# 《紫手套》字体素材库 v1

收集日期：2026-10-31。9 个字族，12 个未经修改的静态 OTF/TTF，字体约 65 MiB。

先看 [字体对照总览](font-pairings.png) 或用浏览器打开 [可调字号样张](preview.html)。完整分配方案见 [ph-font-plan.md](../../doc/ph-font-plan.md)。

| 资料 | 英文 | 中文 |
|---|---|---|
| 正式公文 | EB Garamond | 思源宋体 |
| 旧书印本 | IM FELL English | 朱雀仿宋 |
| 私人手稿 | Courgette；长信备用 EB Garamond Italic | 霞鹜文楷 |
| 印坊台账 | EB Garamond | 朱雀仿宋 |
| 大字标题 | Cinzel | 思源宋体 SemiBold |
| 清晰转录与解谜辅助 | IBM Plex Mono | 思源黑体 |

- `fonts/`：可直接选取的字体，每个字族附自己的 `OFL.txt`。思源文件采用官方 CN 区域子集。
- `manifest.json`：来源链接、固定 revision、授权链接、文件大小和 SHA-256。
- `sources.lock.json`：下载时锁定的上游提交；发布版本记录在 manifest 中。
- `sources/`：每族来源记录，以及朱雀官方发布压缩包。
- `validation.json`：字体结构、版本、覆盖范围和实际样张检查记录。
- `script-characters.txt`：当前剧本完整去重字符；包含制作说明和标记，仅供建立 TMP 字集时筛选。
- `specimens.json`：样张文字与配对，用于复核。英文非密码文句为排版试译，不是最终英文剧本。
- `fetch_fonts.py`：复现下载；复用固定上游提交，不改写字体。
- `build_specimen.py`：检查字形、生成 PNG 和离线 HTML；需要 Python、Pillow、fontTools。

当前剧本 1001 个不同汉字在所选中文字体中全部覆盖。朱雀为 v0.212 技术预览版，建议配思源宋体回退。素材尚未导入场景或生成 TMP 字体资产，最终阅读效果需在 Unity 内核验。

所有字体按随附 SIL OFL 1.1 使用。商用游戏可使用、捆绑这些字体；随游戏分发字体时保留对应版权与完整授权。不要仅复制某一个字族的授权来代替全部字族。字体修改和衍生分发需另按 OFL 保留名称等条件处理。详见 [OFL FAQ](https://openfontlicense.org/ofl-faq/)。

复现命令（项目根目录）：

```powershell
python output/font-library-v1/fetch_fonts.py
python output/font-library-v1/build_specimen.py
```

本工作区的 fontTools 工具副本位于被忽略的 `Temp/ph-font-tools`，不属于字体发行物；其他机器可在自己的开发环境安装 Pillow、fontTools 后运行。
