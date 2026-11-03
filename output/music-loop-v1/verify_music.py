from pathlib import Path
import hashlib
import json
import wave
import numpy as np

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]
data = json.loads((HERE / "manifest.json").read_text())
checks, results = 0, []


def check(condition, message):
    global checks
    assert condition, message
    checks += 1


def read(path):
    with wave.open(str(path), "rb") as clip:
        check((clip.getnchannels(), clip.getsampwidth(), clip.getframerate()) == (2, 2, 48000), str(path))
        return np.frombuffer(clip.readframes(clip.getnframes()), dtype="<i2").reshape(-1, 2).astype(float)/32767


check(len(data["tracks"]) == 5, "Missing track")
for track in data["tracks"]:
    clips = {}
    for name, clip in track["clips"].items():
        path = PROJECT / clip["path"]
        check(hashlib.sha256(path.read_bytes()).hexdigest() == clip["sha256"], "Audio changed: " + str(path))
        x = clips[name] = read(path)
        check(len(x) == clip["samples"], "Duration mismatch")
        check(np.max(np.abs(x)) < .9, "Clipping / insufficient headroom")
        check(np.isfinite(x).all(), "Non-finite sample")
    x = clips["loop"]
    jump = float(np.max(np.abs(x[0]-x[-1])))
    typical = float(np.quantile(np.abs(np.diff(x[:48000],axis=0)), .999))
    check(jump <= max(.003, typical*1.5), "Loop discontinuity: " + track["id"])
    # Half-second windows across each wrap: no silent padding or anomalous level drop.
    around = np.concatenate([x[-24000:], x[:24000]])
    windows = np.sqrt(np.mean(around.reshape(20,2400,2)**2,axis=(1,2)))
    check(windows.min() > 1e-4, "Silent loop edge")
    check(windows.max()/windows.min() < 8, "Excessive level discontinuity")
    if "intro" in clips:
        check(np.max(np.abs(clips["intro"][-1]-x[0])) <= max(.003, typical*1.5), "Intro join")
        check(np.max(np.abs(x[-1]-clips["outro"][0])) <= max(.003, typical*1.5), "Outro join")
        check(np.max(np.abs(clips["outro"][-1])) < 1/32767, "Outro does not finish at zero")
    check(track["license"] == "CC BY 4.0" and bool(track["page"]), "Missing source")
    results.append(dict(id=track["id"], loop_seconds=len(x)/48000, jump=jump, local_step_p999=typical,
                        rms_dbfs=float(20*np.log10(np.sqrt(np.mean(x*x)))), seam_window_ratio=float(windows.max()/windows.min())))
report = dict(passed=True, checks=checks, clips=sum(len(t["clips"]) for t in data["tracks"]), results=results,
              listening_review="Not performed by a human. See two-seam previews and source-time manifest.")
(HERE / "validation.json").write_text(json.dumps(report, indent=2)+"\n", encoding="utf-8")
print(json.dumps(report, indent=2))
