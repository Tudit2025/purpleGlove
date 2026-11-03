# 循环音乐素材与验证

[使用说明](../../doc/ph-music-loops.md) · [试听页](audition/index.html) · [原始来源](sources.json) · [剪辑清单](manifest.json)

使用 Python 3、NumPy 和 PATH 中的 FFmpeg。仓库根目录执行：

```powershell
python -X utf8 output/music-loop-v1/fetch_sources.py
python -X utf8 output/music-loop-v1/build_loops.py
python -X utf8 output/music-loop-v1/install_music.py
python -X utf8 output/music-loop-v1/verify_music.py
python -X utf8 output/music-loop-v1/build_audition.py
```

原始 MP3 不纳入版本控制，可以依据下载链接重新获取；断点缓存位于 `Temp/music-download`。已下载文件不会重复获取。现有剪辑依据来源校验值复用；调整剪辑算法或循环选点时，需先从 manifest 移除需重做的条目。`install_music.py` 保留已经存在的配乐配置，不覆盖 Inspector 中的手工调整。

音频资产位于 `Assets/Audio/PH/Music`，游戏配置位于 `Assets/Resources/PHMusic/StoryMusic.asset`。Source MP3 → 48 kHz 立体声 → 稳态区间匹配 → 首尾交叉淡化 → 同曲各段采用统一固定增益 → PCM16。

循环结构：原曲区间 `[S,E+C)`，主体播放 `[S+C,E)`，尾部用升余弦权重将 `[E,E+C)` 交叉淡化至 `[S,S+C)`。最后一个样本后接回 `[S+C]`，故循环边界沿原曲连续。前奏结束于 `[S+C)`，结束段从 `[S+C]` 起，与任一循环边界相接，再交叉淡化进入原曲最后 16 秒并收尾。详细秒数见 manifest。

`MusicValidation.cs` 仅在 `Temp/music-validation` 隔离项目执行：同步主项目最新 Assets、Packages、ProjectSettings，将验证脚本复制到隔离项目 `Assets/Editor`，用 Unity 2022.3.62f2 批处理执行 `MusicValidation.Run`。它不保存主场景。结果位于 `unity-validation.txt`。

`validation.json` 和 `unity-validation.txt` 覆盖信号连续性与播放器行为，不表示完成了人工听审。`audition` 包含可独立播放的两次接缝样本，以及两个“前奏一次＋主体两遍＋结束一次”的示例。
