"""Create three dry handbell strikes from the retained Kenney CC0 recordings. No network."""
from pathlib import Path
import hashlib
import json
import subprocess
import uuid
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/Audio/PH/Handbell"
OUT.mkdir(parents=True, exist_ok=True)
SR = 48000
rows = []
for i, speed in enumerate([1.55, 1.53, 1.57]):
    # One resonant body across all strokes; subtle pitch changes avoid alternating unrelated bells.
    source = ROOT / "output/audio-library-v1/sources/kenney-impact/impactBell_heavy_000.ogg"
    # A smaller bell than the source: lift pitch, retain its inharmonic metallic body and dry decay.
    raw = subprocess.check_output(["ffmpeg", "-v", "error", "-i", str(source), "-ac", "1", "-ar", str(SR),
        "-af", f"asetrate={round(SR * speed)},aresample=48000,highpass=f=170,lowpass=f=9500", "-f", "f32le", "pipe:1"])
    x = np.frombuffer(raw, dtype="<f4").copy()
    active = np.flatnonzero(np.abs(x) > max(abs(x)) * .001)
    x = x[max(0, active[0] - 96):min(len(x), active[-1] + 1200)]
    x[:96] *= np.linspace(0, 1, 96)
    n = min(2400, len(x) // 4)
    x[-n:] *= np.linspace(1, 0, n)
    x *= (10 ** (-9 / 20)) / max(abs(x))
    target = OUT / f"handbell_strike_{i + 1:02}.wav"
    subprocess.run(["ffmpeg", "-y", "-v", "error", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "pipe:0",
                    "-c:a", "pcm_s24le", str(target)], input=x.astype("<f4").tobytes(), check=True)
    meta = Path(str(target) + ".meta")
    if not meta.exists():
        template = (ROOT / "Assets/Audio/PH/Interactions/object_pickup_01.wav.meta").read_text(encoding="utf-8")
        import re
        template = re.sub(r"guid: [a-f0-9]+", "guid: " + uuid.uuid5(uuid.NAMESPACE_URL, target.relative_to(ROOT).as_posix()).hex, template)
        meta.write_text(template, encoding="utf-8")
    rows.append({"file": target.relative_to(ROOT).as_posix(), "source": source.relative_to(ROOT).as_posix(),
        "source_sha256": hashlib.sha256(source.read_bytes()).hexdigest(), "license": "Kenney Impact Sounds / CC0",
        "speed": speed, "highpass_hz": 170, "lowpass_hz": 9500, "seconds": len(x) / SR,
        "peak_dbfs": round(float(20 * np.log10(max(abs(x)))), 3), "rms_dbfs": round(float(20 * np.log10(np.sqrt(np.mean(x*x)))), 3),
        "sample_rate": SR, "channels": 1, "bits": 24, "sha256": hashlib.sha256(target.read_bytes()).hexdigest()})
    assert np.isfinite(x).all() and max(abs(x)) < .36 and x[0] == 0 and x[-1] == 0
(ROOT / "output/handbell-validation/audio-manifest.json").write_text(json.dumps(rows, indent=2), encoding="utf-8")
(OUT / "SOURCES.txt").write_text("Handbell strikes: Kenney Impact Sounds (1.0), CC0.\n"
    "Retained source: output/audio-library-v1/sources/kenney-impact/impactBell_heavy_000.ogg\n"
    "Processing: mono, 1.55/1.53/1.57x pitch/speed, 170 Hz high-pass, 9.5 kHz low-pass, edge fades, -9 dBFS peak.\n"
    "48 kHz / 24-bit PCM. Rebuild: python output/handbell-validation/build_audio.py\n"
    "Pickup/setdown reuse PH/Interactions; their provenance remains in PH/SOURCES.txt.\n", encoding="utf-8")
(OUT / "Kenney-CC0.txt").write_bytes((ROOT / "output/audio-library-v1/sources/kenney-impact/License.txt").read_bytes())
print(json.dumps(rows, indent=2))
