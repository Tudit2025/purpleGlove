"""Confirm the installed Assets match the Unity-validated copy and that every GUID still resolves.

Run from the repository root after LocalizationApply/LocalizationValidation:
  python -X utf8 output/gemini-polish/verify_delivery.py
"""
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TESTED = ROOT / "Temp/localization-sync"
GUID = re.compile(r"guid: ([0-9a-f]{32})")

changed = subprocess.run(["git", "status", "--porcelain", "--untracked-files=all", "--", "Assets"], cwd=ROOT,
                         capture_output=True, text=True, check=True).stdout.splitlines()
paths = sorted(line[3:].strip('"') for line in changed if not line.endswith("README.md"))
guids = {}
for meta in (ROOT / "Assets").rglob("*.meta"):
    match = GUID.search(meta.read_text(encoding="utf-8-sig"))
    if match:
        guids.setdefault(match[1], []).append(meta.relative_to(ROOT).as_posix())
duplicates = {g: p for g, p in guids.items() if len(p) > 1}
assert not duplicates, f"Duplicate GUIDs: {duplicates}"

records = []
for relative in paths:
    path = ROOT / relative
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    assert digest == hashlib.sha256((TESTED / relative).read_bytes()).hexdigest(), f"Differs from the tested copy: {relative}"
    if path.suffix in (".unity", ".asset", ".prefab"):
        for reference in GUID.findall(path.read_text(encoding="utf-8-sig")):
            assert reference.startswith("0000000000000000") or reference in guids, f"Unresolved GUID {reference} in {relative}"
    records.append({"path": relative, "sha256": digest})

report = {"result": "PASS", "verified_files": len(records), "files": records}
Path(__file__).with_name("delivery-check.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print(f"PASS: {len(records)} changed Assets files match the Unity-validated copy; GUIDs unique and resolved.")
