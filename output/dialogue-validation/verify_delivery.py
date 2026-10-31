"""Verify the installed files still match the Unity-tested copy and resolve their GUIDs."""
from pathlib import Path
import hashlib
import json
import re

root = Path(__file__).resolve().parents[2]
tested = root / "Temp/DialogueBuild"
guid_pattern = re.compile(r"guid: ([0-9a-f]{32})")
guid_map = {}
for meta in (root / "Assets").rglob("*.meta"):
    found = guid_pattern.search(meta.read_text(encoding="utf-8-sig"))
    if found:
        guid_map.setdefault(found.group(1), []).append(meta)

files = list((root / "Assets/Data/Dialogue").rglob("*.asset"))
files += list((root / "Assets/Art/Dialogue").rglob("*.png"))
files += list((root / "Assets/Art/Dialogue").glob("*.mat"))
files += list((root / "Assets/Art/Dialogue").glob("*.shader"))
files += list((root / "Assets/Scripts/Presentation").glob("Dialogue*.cs"))
files += [root / name for name in (
    "Assets/Scripts/Presentation/SceneInputGate.cs",
    "Assets/Scripts/Presentation/VisitorEyes.cs",
    "Assets/Scripts/Presentation/SceneObjectInteractor.cs",
    "Assets/Scripts/Presentation/DeskViewHud.cs",
    "Assets/Art/DeskSceneV1/Runtime/DeskScenePreview.cs",
    "Assets/Art/DeskSceneV1/Runtime/PixelSceneView.cs",
    "Assets/Art/DeskSceneV1/Editor/DeskDialogueBuilder.cs",
    "Assets/Art/DeskSceneV1/Scenes/PH_DeskRoom.unity",
)]

records = []
for path in files:
    relative = path.relative_to(root)
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    assert digest == hashlib.sha256((tested / relative).read_bytes()).hexdigest(), f"Changed since Unity verification: {relative}"
    meta = Path(str(path) + ".meta")
    assert meta.exists(), f"Missing meta: {relative}"
    own_guid = guid_pattern.search(meta.read_text(encoding="utf-8-sig")).group(1)
    assert len(guid_map[own_guid]) == 1, f"Duplicate GUID: {relative}"
    assert meta.read_bytes() == Path(str(tested / relative) + ".meta").read_bytes(), f"Meta differs from tested copy: {relative}"
    if path.suffix in {".asset", ".mat", ".unity"}:
        for guid in guid_pattern.findall(path.read_text(encoding="utf-8-sig")):
            if guid.startswith("00000000"):
                continue
            assert guid in guid_map and len(guid_map[guid]) == 1, f"Unresolved or duplicate reference {guid} in {relative}"
    records.append({"path": relative.as_posix(), "sha256": digest})

report = {"result": "PASS", "verified_files": len(records), "files": records}
(Path(__file__).parent / "delivery-check.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(f"PASS: {len(records)} installed files match Unity-tested copy; metadata and references resolve.")
