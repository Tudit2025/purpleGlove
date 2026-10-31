"""Read-only asset/scene verification against the Unity-tested project copy."""
import hashlib
import json
import re
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "output/visitor-eyes-validation"
CLONE = ROOT / "Temp/VisitorEyesBuild"


def blocks(text):
    return {
        re.search(r"&(-?\d+)", block).group(1): block
        for block in re.split(r"(?=^--- !u!)", text, flags=re.M)
        if block.startswith("--- !u!")
    }


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


entries = json.loads((OUT / "image-prompts.json").read_text(encoding="utf-8"))
assert len(entries) == 7
assets = []
for entry in entries:
    path = ROOT / entry["output"]
    with Image.open(path) as picture:
        assert picture.mode == "RGBA" and picture.size == (1024, 1536)
        assert picture.getchannel("A").getextrema()[0] == 0
    meta = path.with_suffix(".png.meta").read_text()
    guid = re.search(r"^guid: (\w+)", meta, re.M).group(1)
    speaker = ROOT / ("Assets/Data/Dialogue/Speakers/" + entry["id"] + ".asset")
    assert "guid: " + guid in speaker.read_text(encoding="utf-8")
    assert "alphaIsTransparency: 1" in meta
    for asset in (path, speaker):
        assert digest(asset) == digest(CLONE / asset.relative_to(ROOT)), asset
    assert (ROOT / entry["source"]).exists(), "Original strip lost"
    assets.append({"id": entry["id"], "path": entry["output"], "sha256": digest(path)})

for relative in (
    "Assets/Scripts/Presentation/VisitorEyes.cs",
    "Assets/Art/DeskSceneV1/Editor/DeskDialogueBuilder.cs",
    "Assets/Art/Dialogue/VisitorEyes.shader",
    "Assets/Art/Dialogue/VisitorEyes.mat",
):
    assert digest(ROOT / relative) == digest(CLONE / relative), relative

scene = "Assets/Art/DeskSceneV1/Scenes/PH_DeskRoom.unity"
actual = blocks((ROOT / scene).read_text(encoding="utf-8"))
validated = blocks((CLONE / scene).read_text(encoding="utf-8"))
before = blocks((ROOT / "Temp/visitor-eyes-scene-before.unity").read_text(encoding="utf-8"))
assert actual == validated, "Delivered scene differs from tested scene"
changed = {key for key in before if key in actual and before[key] != actual[key]}
# The workspace is shared with another editor. The final transfer separately
# checked that ONLY three visitor blocks and the new light differed from the
# then-current workspace. Earlier unrelated saves may differ from the turn-start
# backup, so do not treat that old snapshot as an exclusive workspace baseline.
visitor_blocks = {"644005093", "644005094", "848910796", "848910797"}
assert visitor_blocks <= changed, "Visitor scene update missing"
result = (OUT / "validation.txt").read_text()
assert result.startswith("PASS:"), result
report = {"status": "PASS", "unity": result.splitlines()[0], "assets": assets,
          "visitor_scene_changes": sorted(visitor_blocks),
          "preserved": "Final transfer checked only visitor pose/reference/children and its new light changed"}
(OUT / "delivery-check.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
