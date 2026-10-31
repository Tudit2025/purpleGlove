"""Package Unity's rendered frames with the same foley clips at their animation cue times."""
from pathlib import Path
import subprocess
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "output/handbell-validation"
SR = 48000
mix = np.zeros(round(100 / 30 * SR), dtype=np.float32)

def add(path, time, gain):
    raw = subprocess.check_output(["ffmpeg", "-v", "error", "-i", str(ROOT / path), "-ar", str(SR), "-ac", "1", "-f", "f32le", "pipe:1"])
    x = np.frombuffer(raw, dtype="<f4")
    offset = round(time * SR)
    count = min(len(x), len(mix) - offset)
    mix[offset:offset + count] += x[:count] * gain

add("Assets/Audio/PH/Interactions/object_pickup_01.wav", 0, .4)
# The 30 FPS deterministic preview completes pickup at frame 16; strikes occur on alternating swing extrema.
for index, frame in enumerate([20, 27, 34, 41, 48, 55]):
    add(f"Assets/Audio/PH/Handbell/handbell_strike_{index % 3 + 1:02}.wav", frame / 30,
        .8 * .72 * (.83 if index % 2 else 1))
add("Assets/Audio/PH/Interactions/object_setdown_01.wav", 85 / 30, .4)
mix[-1200:] *= np.linspace(1, 0, 1200)
assert np.max(np.abs(mix)) < 1
subprocess.run(["ffmpeg", "-y", "-v", "error", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "pipe:0",
                "-c:a", "pcm_s24le", str(OUT / "ring-preview.wav")], input=mix.astype("<f4").tobytes(), check=True)
subprocess.run(["ffmpeg", "-y", "-v", "error", "-framerate", "30", "-i", str(OUT / "frames/ring-%03d.png"),
                "-i", str(OUT / "ring-preview.wav"), "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p",
                "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", "-shortest", str(OUT / "handbell-preview.mp4")], check=True)
print("Preview: Unity frames + cue-aligned foley mix; peak", float(np.max(np.abs(mix))))
