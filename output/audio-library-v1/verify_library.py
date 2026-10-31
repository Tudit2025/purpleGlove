"""Check delivered WAVs, provenance, loop joins, metadata and dialogue overlap."""
from collections import Counter
from hashlib import sha256
from pathlib import Path
import json
import re
import wave
import numpy as np

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parent.parent
SR = 48000


def read_wav(path):
    with wave.open(str(path), "rb") as audio:
        params = audio.getparams()
        raw = np.frombuffer(audio.readframes(params.nframes), dtype=np.uint8).reshape(-1, 3).astype(np.int32)
    values = raw[:, 0] | (raw[:, 1] << 8) | (raw[:, 2] << 16)
    values = np.where(values >= 8388608, values - 16777216, values)
    return values.astype(np.float64).reshape(-1, params.nchannels) / 8388608, params


def main():
    manifest = json.loads((ROOT / "manifest.json").read_text(encoding="utf-8"))
    failures, results, bank = [], [], []
    references = set()
    for item in manifest["assets"]:
        path = PROJECT / item["file"]
        x, params = read_wav(path)
        peak = float(np.max(np.abs(x)))
        rms = float(np.sqrt(np.mean(x*x)))
        issues = []
        if params.framerate != SR or params.sampwidth != 3:
            issues.append("Unexpected WAV encoding")
        if params.nchannels != (2 if item["loop"] else 1):
            issues.append("Unexpected channel count")
        if peak > 10 ** (-3 / 20) or rms < 1e-5:
            issues.append("Clipping risk or near-silent asset")
        if abs(len(x) / SR - item["duration_s"]) > .00011:
            issues.append("Duration mismatch")
        if sha256(path.read_bytes()).hexdigest() != item["sha256"]:
            issues.append("Hash mismatch")
        if not path.with_name(path.name + ".meta").exists():
            issues.append("Missing Unity importer")
        seam = None
        if item["loop"]:
            jump = float(np.max(np.abs(x[0] - x[-1])))
            normal_delta = float(np.sqrt(np.mean(np.diff(x, axis=0)**2)))
            seam = {"wrap_delta": round(jump, 8), "wrap_over_typical_sample_delta": round(jump / normal_delta, 3)}
            if jump > normal_delta * 5:
                issues.append("Loop join discontinuity")
        elif max(float(np.max(np.abs(x[0]))), float(np.max(np.abs(x[-1])))) > 1e-6:
            issues.append("Non-zero one-shot edge")
        for ref in item["sources"]:
            references.add(ref)
            if not (ROOT / "sources" / ref).is_file():
                issues.append("Missing source: " + ref)
        if item["category"] == "Typewriter/Short":
            if not .09 <= len(x) / SR <= .15:
                issues.append("Dialogue key outside intended 90-150 ms range")
            bank.append(x)
        results.append({"id": item["id"], "peak_dbfs": round(20 * np.log10(peak), 2),
                        "rms_dbfs": round(20 * np.log10(rms), 2), "loop_join": seam, "issues": issues})
        failures.extend(f"{item['id']}: {issue}" for issue in issues)

    # Check source integrity against the download-time manifest, not against a
    # newly calculated value stored next to the same final clip.
    for source_manifest in sorted((ROOT / "sources").glob("*/source.json")):
        data = json.loads(source_manifest.read_text(encoding="utf-8"))
        for entry in data["files"]:
            ref = f"{data['pack']}/{entry['filename']}"
            if ref in references:
                path = source_manifest.parent / entry["filename"]
                if sha256(path.read_bytes()).hexdigest() != entry["sha256"]:
                    failures.append("Original source hash mismatch: " + ref)

    guids = [re.search(r"^guid: (\w+)$", p.read_text(encoding="utf-8"), re.M).group(1)
             for p in (PROJECT / "Assets/Audio").rglob("*.meta")]
    if len(guids) != len(set(guids)):
        failures.append("Duplicate Unity GUID")
    expected = {item["file"] for item in manifest["assets"]}
    actual = {p.relative_to(PROJECT).as_posix() for p in (PROJECT / "Assets/Audio/PH").rglob("*.wav")}
    if expected != actual:
        failures.append("Manifest and delivery file set differ")

    rng = np.random.default_rng(3102026)
    overlap = []
    for spacing in [.060, .095]:
        mixed = np.zeros((SR * 12, 1))
        position, previous = 0, -1
        while position < len(mixed) - SR:
            index = int(rng.choice([i for i in range(len(bank)) if i != previous]))
            clip = bank[index]
            mixed[position:position + len(clip)] += clip
            position += round(spacing * float(rng.uniform(.9, 1.1)) * SR)
            previous = index
        peak = float(np.max(np.abs(mixed)))
        overlap.append({"spacing_s": spacing, "peak_dbfs": round(20 * np.log10(peak), 2)})
        if peak > 10 ** (-3 / 20):
            failures.append("Dialogue overlap exceeds -3 dBFS")

    preview, _ = read_wav(ROOT / "audition.wav")
    if np.max(np.abs(preview)) > 10 ** (-3 / 20):
        failures.append("Audition clips")
    report = {"passed": not failures, "asset_count": len(results),
              "categories": dict(Counter(item["category"] for item in manifest["assets"])),
              "original_source_files_used": len(references), "dialogue_overlap": overlap,
              "failures": failures, "checks": results,
              "scope": "File/DSP validation only; subjective listening and Unity scene mixing are not verified."}
    (ROOT / "validation-report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({k: v for k, v in report.items() if k != "checks"}, ensure_ascii=True, indent=2))
    raise SystemExit(0 if not failures else 1)


if __name__ == "__main__":
    main()
