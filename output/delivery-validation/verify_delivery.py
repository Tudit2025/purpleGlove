"""Confirm the installed scene/scripts match the Unity-tested copy and resolve asset GUIDs."""
from pathlib import Path
import hashlib
import json
import re

root = Path(__file__).resolve().parents[2]
tested = root / "Temp/DeskSceneV1Build"
paths = [
    "Assets/Scripts/Presentation/SealedDeliveryController.cs",
    "Assets/Scripts/Presentation/SealedPacketAction.cs",
    "Assets/Scripts/Presentation/SceneInputGate.cs",
    "Assets/Scripts/Presentation/SceneObjectInteractor.cs",
    "Assets/Scripts/Presentation/ActArchiveController.cs",
    "Assets/Scripts/Presentation/DialogueController.cs",
    "Assets/Scripts/Presentation/DialogueTestConsole.cs",
    "Assets/Scripts/Presentation/CipherWorkbench.cs",
    "Assets/Art/DeskSceneV1/Runtime/DeskScenePreview.cs",
    "Assets/Art/DeskSceneV1/Editor/DeskDeliveryBuilder.cs",
    "Assets/Art/DeskSceneV1/Editor/DeskRoomBuilder.cs",
    "Assets/Art/DeskSceneV1/Editor/DeskDoorBuilder.cs",
    "Assets/Art/DeskSceneV1/Editor/DeskActDocumentBuilder.cs",
    "Assets/Art/DeskSceneV1/Scenes/PH_DeskRoom.unity",
]
pattern = re.compile(r"guid: ([0-9a-f]{32})")
guids = {}
for meta in (root / "Assets").rglob("*.meta"):
    match = pattern.search(meta.read_text(encoding="utf-8-sig"))
    if match:
        guids.setdefault(match.group(1), []).append(str(meta.relative_to(root)))

records = []
for relative in paths:
    path = root / relative
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    assert digest == hashlib.sha256((tested / relative).read_bytes()).hexdigest(), relative
    meta = Path(str(path) + ".meta")
    assert meta.read_bytes() == Path(str(tested / relative) + ".meta").read_bytes(), str(meta)
    guid = pattern.search(meta.read_text(encoding="utf-8-sig")).group(1)
    assert len(guids[guid]) == 1, f"Duplicate GUID: {relative}"
    if path.suffix == ".unity":
        for reference in pattern.findall(path.read_text(encoding="utf-8-sig")):
            if not reference.startswith("00000000"):
                assert reference in guids and len(guids[reference]) == 1, reference
    records.append({"path": relative, "sha256": digest})

report = {"result": "PASS", "verified_files": len(records), "files": records}
Path(__file__).with_name("delivery-check.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print(f"PASS: {len(records)} installed files match the Unity-tested copy; metadata and scene GUIDs resolve.")
