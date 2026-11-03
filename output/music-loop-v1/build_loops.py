"""Select harmonic matches, render cyclic PCM loops, ending sections and seam previews.

Requires NumPy and FFmpeg. Selection uses chroma/envelope similarity, not a claim
of human listening approval. All source times and signal checks are recorded.
"""
from pathlib import Path
import hashlib
import json
import subprocess
import uuid
import wave
import re
import numpy as np

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]
DEST = PROJECT / "Assets/Audio/PH/Music"
SR = 48000


def decode(path, rate=SR, channels=2):
    raw = subprocess.check_output(["ffmpeg", "-v", "error", "-i", str(path), "-ar", str(rate),
                                   "-ac", str(channels), "-f", "f32le", "pipe:1"])
    return np.frombuffer(raw, dtype="<f4").reshape(-1, channels).copy()


def guid(path):
    return uuid.uuid5(uuid.NAMESPACE_URL, "purpleglove/music/" + path).hex


def meta(path, kind="audio"):
    rel = path.relative_to(PROJECT).as_posix()
    target = Path(str(path) + ".meta")
    if target.exists():
        return
    header = "fileFormatVersion: 2\nguid: " + guid(rel) + "\n"
    if kind == "audio":
        template = (PROJECT / "Assets/Audio/PH/Ambience/rain_room_muffled_loop_10s.wav.meta").read_text()
        body = template[template.index("AudioImporter:"):]
    else:
        body = "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
    target.write_text(header + body, encoding="utf-8")


def write_wav(path, samples):
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as output:
        output.setnchannels(2)
        output.setsampwidth(2)
        output.setframerate(SR)
        output.writeframes(np.rint(np.clip(samples, -1, 1) * 32767).astype("<i2").tobytes())


def features(path):
    rate, step, size = 12000, 3000, 8192
    audio = decode(path, rate, 1)[:, 0]
    starts = np.arange(0, len(audio) - size, step)
    freq = np.fft.rfftfreq(size, 1 / rate)
    valid = (freq > 70) & (freq < 3500)
    notes = np.rint(69 + 12 * np.log2(np.maximum(freq, 1) / 440)).astype(int) % 12
    chroma, level, centroid = [], [], []
    window = np.hanning(size)
    for s in starts:
        segment = audio[s:s+size]
        spectrum = np.abs(np.fft.rfft(segment * window)) ** 0.7
        c = np.array([np.sum(spectrum[valid & (notes == note)]) for note in range(12)])
        chroma.append(c / max(np.linalg.norm(c), 1e-8))
        level.append(20 * np.log10(max(np.sqrt(np.mean(segment * segment)), 1e-8)))
        centroid.append(np.sum(freq * spectrum) / max(np.sum(spectrum), 1e-8))
    return np.asarray(chroma), np.asarray(level), np.asarray(centroid), len(audio) / rate


def select_loop(path, ident):
    c, level, centroid, duration = features(path)
    # Endings get short initial statements. Other tracks can select deeper excerpts.
    ending = ident in ("hiraeth", "undertow")
    overlap = 2.0 if ident == "phoenix" else 4.0
    frames = round(overlap * 4)
    first = range(32, min(round((28 if ending else 95) * 4), len(c) - 400))
    candidates = []
    target = 64 if ident == "phoenix" else 56
    for s in first:
        for e in range(s + 44 * 4, min(s + 88 * 4, len(c) - frames - 48)):
            a, b = c[s:s+frames], c[e:e+frames]
            harmonic = float(np.mean(np.sum(a * b, axis=1)))
            envelope = float(np.mean(np.abs(level[s:s+frames] - level[e:e+frames])))
            color = float(np.mean(np.abs(np.log(np.maximum(centroid[s:s+frames], 1) / np.maximum(centroid[e:e+frames], 1)))))
            quiet_penalty = max(0, -36 - min(level[s:s+frames].mean(), level[e:e+frames].mean()))
            score = (1-harmonic)*12 + envelope*.07 + color*.2 + abs((e-s)/4-target)*.002 + quiet_penalty*.05
            candidates.append((score, s, e, harmonic, envelope))
    if not candidates:
        raise ValueError("No sufficiently long loop region: " + ident)
    candidates.sort()
    score, s, e, harmonic, envelope = candidates[0]
    return dict(source_start=s/4, source_end=e/4, overlap=overlap, duration=(e-s)/4,
                source_duration=duration, harmonic_similarity=harmonic, overlap_level_difference_db=envelope,
                selection="chroma/envelope match; subjective audition pending",
                alternatives=[dict(start=t[1]/4, end=t[2]/4, score=t[0]) for t in candidates[::max(1,len(candidates)//50)][:5]])


def render(track):
    ident = track["id"]
    path = HERE / "sources" / track["file"]
    plan = select_loop(path, ident)
    source = decode(path)
    s, e, cross = [round(plan[k]*SR) for k in ("source_start", "source_end", "overlap")]
    # A full cycle begins after the overlap. Its end is the original preceding sample.
    w = (.5 - .5*np.cos(np.linspace(0, np.pi, cross)))[:,None]
    bridge = source[e:e+cross]*(1-w) + source[s:s+cross]*w
    loop = np.concatenate([source[s+cross:e], bridge])
    # One gain for all parts of a track; preserve dynamics without a limiter.
    rms = float(np.sqrt(np.mean(loop**2)))
    gain = min(10**(-23/20)/max(rms,1e-8), 10**(-3/20)/max(float(np.max(np.abs(source))), float(np.max(np.abs(loop))), 1e-8))
    clips = {"loop": loop * gain}
    if ident in ("hiraeth", "undertow"):
        intro = source[:s+cross].copy()
        # Exit at the cycle boundary through the same next sample, then dissolve into
        # the original final 16 seconds, retaining its composed release and reverb.
        tail = source[-16*SR:].copy()
        lead = source[s+cross:s+cross+10*SR].copy()
        n = 6*SR
        wout = (.5-.5*np.cos(np.linspace(0,np.pi,n)))[:,None]
        outro = np.concatenate([lead[:-n], lead[-n:]*(1-wout)+tail[:n]*wout, tail[n:]])
        intro[:SR//2] *= np.linspace(0,1,SR//2)[:,None]
        outro[-2*SR:] *= np.linspace(1,0,2*SR)[:,None]
        clips.update(intro=intro*gain, outro=outro*gain)
    result = dict(track, edit=plan, gain_db=float(20*np.log10(gain)), clips={})
    for part, samples in clips.items():
        output = DEST / (ident + "_" + part + ".wav")
        write_wav(output, samples)
        meta(output)
        peak = float(np.max(np.abs(samples)))
        result["clips"][part] = dict(path=output.relative_to(PROJECT).as_posix(), samples=len(samples),
                                     seconds=len(samples)/SR, peak_dbfs=float(20*np.log10(max(peak,1e-8))),
                                     sha256=hashlib.sha256(output.read_bytes()).hexdigest(), guid=re.search(r"guid: (\w+)", Path(str(output)+".meta").read_text())[1])
    # Preview two wraps, with enough context to judge the whole crossfade.
    n = 12*SR
    preview = np.concatenate([clips["loop"][-n:], clips["loop"], clips["loop"][:n]])
    preview_path = HERE / "audition" / (ident + "_two_seams.mp3")
    preview_path.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run(["ffmpeg", "-y", "-v", "error", "-f", "f32le", "-ar", str(SR), "-ac", "2", "-i", "pipe:0",
                    "-c:a", "libmp3lame", "-b:a", "192k", str(preview_path)], input=preview.astype("<f4").tobytes(), check=True)
    result["audition_seams_seconds"] = [12, 12+len(loop)/SR]
    result["audition"] = preview_path.relative_to(PROJECT).as_posix()
    result["seam"] = dict(sample_jump=float(np.max(np.abs(clips["loop"][0]-clips["loop"][-1]))),
                          local_step_p99=float(np.quantile(np.abs(np.diff(clips["loop"][:SR],axis=0)),.99)))
    print(ident, json.dumps(plan), flush=True)
    return result


if __name__ == "__main__":
    DEST.mkdir(parents=True, exist_ok=True)
    meta(DEST, "folder")
    from fetch_sources import TRACKS, fetch
    tracks = json.loads((HERE / "sources.json").read_text()) if (HERE / "sources.json").exists() else [
        fetch(t) for t in TRACKS if (HERE / "sources" / (t[0]+".mp3")).exists() and (HERE / "sources" / (t[0]+".html")).exists()]
    old = json.loads((HERE / "manifest.json").read_text())["tracks"] if (HERE / "manifest.json").exists() else []
    rendered = []
    for track in tracks:
        cached = next((t for t in old if t["id"] == track["id"] and t["sha256"] == track["sha256"]), None)
        rendered.append(cached or render(track))
        (HERE / "manifest.json").write_text(json.dumps(dict(sample_rate=SR, format="stereo PCM16", tracks=rendered), indent=2)+"\n", encoding="utf-8")
    (HERE / "audition/all-loops.m3u8").write_text("#EXTM3U\n" + "\n".join(t["id"]+"_two_seams.mp3" for t in tracks)+"\n", encoding="utf-8")
