"""Package the delivered assets, guide, originals and build recipes."""
from hashlib import sha256
from pathlib import Path
import json
import zipfile

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parent.parent
DEST = ROOT / "ph-audio-library-v1.zip"


def main():
    report = json.loads((ROOT / "validation-report.json").read_text(encoding="utf-8"))
    if not report["passed"]:
        raise RuntimeError("Validation did not pass")
    files = [PROJECT / "doc/ph-audio-library.md", PROJECT / "Assets/Audio.meta"]
    files += sorted(p for p in (PROJECT / "Assets/Audio").rglob("*") if p.is_file())
    files += sorted(p for p in ROOT.rglob("*") if p.is_file()
                    and p.suffix not in (".zip", ".pyc") and p.name != "package-info.json")
    with zipfile.ZipFile(DEST, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for file in files:
            archive.write(file, file.relative_to(PROJECT).as_posix())
    with zipfile.ZipFile(DEST) as archive:
        bad = archive.testzip()
        if bad:
            raise RuntimeError("Invalid archive member: " + bad)
    info = {"file": DEST.name, "bytes": DEST.stat().st_size,
            "sha256": sha256(DEST.read_bytes()).hexdigest(), "entries": len(files),
            "asset_count": report["asset_count"], "zip_integrity_passed": True}
    (ROOT / "package-info.json").write_text(json.dumps(info, indent=2), encoding="utf-8")
    print(json.dumps(info, indent=2))


if __name__ == "__main__":
    main()
