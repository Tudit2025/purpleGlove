"""Build PH's dry, dark foley palette from the retained CC0 sources.

Requirements: Python 3.11+, NumPy, FFmpeg on PATH. No network is used.
Run from any directory: python output/audio-library-v1/build_library.py
"""
from dataclasses import dataclass
from functools import lru_cache
from hashlib import sha256
from pathlib import Path
import csv
import json
import subprocess
import uuid
import wave

import numpy as np

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parent.parent
DEST = PROJECT / "Assets/Audio/PH"
SR = 48000
CATALOG = []
RENDERED = {}


@dataclass
class Clip:
    samples: np.ndarray
    sources: list
    edits: list


def db(value):
    return 10 ** (value / 20)


def peak_normalize(x, target=0):
    return x * db(target) / max(float(np.max(np.abs(x))), 1e-9)


def fade(x, attack=.002, release=.025):
    x = x.copy()
    a = min(round(attack * SR), len(x) // 2)
    r = min(round(release * SR), len(x) // 2)
    if a:
        x[:a] *= np.linspace(0, 1, a)[:, None]
    if r:
        x[-r:] *= np.linspace(1, 0, r)[:, None]
    return x


def trim(x, threshold=-45, pad=.006):
    active = np.flatnonzero(np.max(np.abs(x), axis=1) > np.max(np.abs(x)) * db(threshold))
    if not len(active):
        raise ValueError("Silent source")
    start = max(0, active[0] - round(pad * SR))
    end = min(len(x), active[-1] + round(.035 * SR))
    return x[start:end]


@lru_cache(maxsize=160)
def source(pack, filename, speed=1.0, lowpass=6000, highpass=65, stereo=False):
    path = ROOT / "sources" / pack / filename
    filters = f"asetrate={round(SR * speed)},aresample={SR},highpass=f={highpass},lowpass=f={lowpass}"
    # Decode first at 48 kHz before speed processing; source sample rates differ.
    raw = subprocess.check_output([
        "ffmpeg", "-v", "error", "-i", str(path), "-ac", "2" if stereo else "1",
        "-ar", str(SR), "-f", "f32le", "pipe:1"])
    cooked = subprocess.run([
        "ffmpeg", "-v", "error", "-f", "f32le", "-ar", str(SR),
        "-ac", "2" if stereo else "1", "-i", "pipe:0", "-af", filters,
        "-f", "f32le", "pipe:1"], input=raw, stdout=subprocess.PIPE, check=True).stdout
    x = np.frombuffer(cooked, dtype="<f4").reshape(-1, 2 if stereo else 1).copy()
    if not stereo:
        x = fade(trim(x))
        x = peak_normalize(x)
    return Clip(x, [f"{pack}/{filename}"], [{"source": f"{pack}/{filename}",
        "speed": speed, "highpass_hz": highpass, "lowpass_hz": lowpass,
        "channels": 2 if stereo else 1}])


def rpg(name, speed=1.0, lp=6000):
    return source("kenney-rpg", name + ".ogg", speed, lp)


def impact(name, speed=1.0, lp=5500):
    return source("kenney-impact", name + ".ogg", speed, lp)


def excerpt(clip, start, duration):
    x = clip.samples[round(start * SR):round((start + duration) * SR)].copy()
    return Clip(fade(x), clip.sources, clip.edits + [{"excerpt_start_s": start, "duration_s": duration}])


def mix(*layers):
    length = max(round(t * SR) + len(c.samples) for t, c, gain in layers)
    channels = layers[0][1].samples.shape[1]
    x = np.zeros((length, channels), dtype=np.float32)
    sources, edits = [], []
    for t, clip, gain in layers:
        offset = round(t * SR)
        x[offset:offset + len(clip.samples)] += clip.samples * gain
        sources.extend(clip.sources)
        edits.append({"offset_s": t, "gain": gain, "edits": clip.edits})
    return Clip(fade(x), sorted(set(sources)), edits)


def loop_crossfade(clip, duration, overlap, start=0):
    # Output begins in the continuous middle. Its tail blends into the source
    # immediately before that middle, so the loop wrap is naturally continuous.
    n, cross, first = round(duration * SR), round(overlap * SR), round(start * SR)
    x = clip.samples[first:first + n + cross].copy()
    if len(x) != n + cross:
        raise ValueError("Source too short for loop")
    body = x[cross:n]
    theta = np.linspace(0, np.pi / 2, cross)[:, None]
    bridge = x[n:n + cross] * np.cos(theta) + x[:cross] * np.sin(theta)
    result = np.concatenate([body, bridge])
    return Clip(result, clip.sources, clip.edits + [{"loop_start_s": start,
        "loop_duration_s": duration, "equal_power_crossfade_s": overlap}])


def wav24(path, x):
    path.parent.mkdir(parents=True, exist_ok=True)
    values = np.rint(np.clip(x, -1, 1) * 8388607).astype(np.int32).reshape(-1)
    packed = np.column_stack([values & 255, (values >> 8) & 255, (values >> 16) & 255]).astype(np.uint8)
    with wave.open(str(path), "wb") as handle:
        handle.setnchannels(x.shape[1])
        handle.setsampwidth(3)
        handle.setframerate(SR)
        handle.writeframes(packed.tobytes())


def guid_for(path):
    return uuid.uuid5(uuid.NAMESPACE_URL, "ph-audio-v1/" + path.relative_to(PROJECT).as_posix()).hex


def import_meta(path, loop=False):
    path.parent.mkdir(parents=True, exist_ok=True)
    meta = path.with_name(path.name + ".meta")
    # Keep any GUID already assigned by Unity or a previous build.
    guid = guid_for(path)
    if meta.exists():
        import re
        match = re.search(r"^guid: ([0-9a-f]+)", meta.read_text(), re.M)
        if match:
            guid = match.group(1)
    meta.write_text(f"""fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: {2 if loop else 0}
    sampleRateSetting: 0
    sampleRateOverride: 48000
    compressionFormat: 0
    quality: 1
    conversionMode: 0
    preloadAudioData: {0 if loop else 1}
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 0
  ambisonic: 0
  loadInBackground: {1 if loop else 0}
  m_Use3D: 1
  userData:
  assetBundleName:
  assetBundleVariant:
""", encoding="utf-8")


def save(category, name, label, clip, peak=-8, loop=False, rms=None, note=""):
    x = clip.samples.copy()
    x -= np.mean(x, axis=0, keepdims=True)
    if loop:
        if rms is not None:
            x *= db(rms) / max(float(np.sqrt(np.mean(x * x))), 1e-9)
        if np.max(np.abs(x)) > db(peak):
            x *= db(peak) / np.max(np.abs(x))
    else:
        x = peak_normalize(fade(x), peak)
    path = DEST / category / (name + ".wav")
    import_meta(path, loop)
    wav24(path, x)
    row = {"id": name, "category": category, "label_zh": label,
           "file": path.relative_to(PROJECT).as_posix(), "duration_s": round(len(x) / SR, 4),
           "sample_rate": SR, "bit_depth": 24, "channels": x.shape[1], "loop": loop,
           "peak_dbfs": round(20 * np.log10(max(float(np.max(np.abs(x))), 1e-9)), 2),
           "rms_dbfs": round(20 * np.log10(max(float(np.sqrt(np.mean(x * x))), 1e-9)), 2),
           "sources": clip.sources, "processing": clip.edits, "note_zh": note,
           "sha256": sha256(path.read_bytes()).hexdigest()}
    CATALOG.append(row)
    RENDERED[name] = x


def build():
    # Wooden objects: four independent source takes at each weight.
    for weight, chinese, level, rate in [("light", "轻", -12, .97), ("medium", "中", -8, .93), ("heavy", "重", -5, .87)]:
        for i in range(4):
            save("Wood", f"wood_collision_{weight}_{i+1:02}", f"木材碰撞／{chinese} {i+1}",
                 impact(f"impactWood_{weight}_{i:03}", rate, 4900), level)

    for i in range(3):
        save("PaperBook", f"page_turn_{i+1:02}", f"翻书页 {i+1}", rpg(f"bookFlip{i+1}", .94, 7200), -13)
        save("PaperBook", f"page_turn_slow_{i+1:02}", f"慢翻厚书页 {i+1}", rpg(f"bookFlip{i+1}", .78, 5300), -14,
             note="由对应翻页录音降速制作；不是独立录音。")
        save("PaperBook", f"book_setdown_{i+1:02}", f"书本放上木桌 {i+1}", rpg(f"bookPlace{i+1}", .92, 4900), -10)
    save("PaperBook", "book_open_01", "打开旧书", mix((0, rpg("bookOpen", .85), .7), (.06, rpg("bookFlip3", .9), .25)), -12)
    save("PaperBook", "book_close_01", "合上旧书", rpg("bookClose", .83, 4500), -10)
    for i in range(2):
        save("PaperBook", f"paper_pickup_{i+1:02}", f"拿起信纸 {i+1}", rpg(f"bookFlip{i+2}", 1.1, 7500), -16)
        save("PaperBook", f"paper_setdown_{i+1:02}", f"放下信纸 {i+1}",
             mix((0, rpg(f"bookFlip{i+2}", .91), .75), (.1, rpg(f"bookPlace{i+1}", 1.12), .12)), -16)

    outside = source("rain-outside", "rain_long.ogg", 1, 7200, 100, True)
    # Choose a stable 42 s excerpt, avoiding the original fades and large bursts.
    second_rms = np.array([np.sqrt(np.mean(outside.samples[i*SR:(i+1)*SR] ** 2))
                           for i in range(len(outside.samples) // SR)])
    start = min(range(20, len(second_rms) - 62),
                key=lambda k: float(np.std(second_rms[k:k+42]) / max(np.mean(second_rms[k:k+42]), 1e-9)))
    save("Ambience", "rain_outside_loop_40s", "屋外雨声／40 秒循环", loop_crossfade(outside, 40, 2, start),
         -12, True, -26, "取自原始非循环长录音；选取稳定片段并用 2 秒交叉淡化制作循环。")
    for name, label, cutoff, level in [("window", "窗边雨声", 5600, -29), ("room_muffled", "室内隔窗雨声", 1900, -34)]:
        window = source("rain-window", "rain_on_window_loop.wav", 1, cutoff, 110, True)
        save("Ambience", f"rain_{name}_loop_10s", label + "／10 秒循环", loop_crossfade(window, 10, .8),
             -14, True, level, "两种窗雨共用同一录音；按机位切换或交叉淡化，避免长时间同相叠加。")

    for i in range(2):
        save("Doors", f"wood_door_open_{i+1:02}", f"木门打开 {i+1}", rpg(f"doorOpen_{i+1}", .91, 4800), -8)
    for i in range(3):
        save("Doors", f"wood_door_close_{i+1:02}", f"木门关闭 {i+1}", rpg(f"doorClose_{i+1}", .9, 4500), -7)
    save("Doors", "wood_door_close_heavy_01", "木门重关", mix((0, rpg("doorClose_4", .8, 4000), .75),
         (.07, impact("impactWood_heavy_003", .81, 3000), .35)), -5)
    for i, times in enumerate([(0, .3), (0, .22, .48), (0, .18, .36, .83)]):
        save("Doors", f"door_knock_pattern_{i+1:02}", ["两下克制敲门", "三下常规敲门", "急促敲门"][i],
             mix(*[(t, impact(f"impactWood_medium_{j%4:03}", .86, 2500), 1 - .1 * (j % 2)) for j, t in enumerate(times)]), -10)

    for i in range(3):
        save("Trapdoor", f"trapdoor_open_{i+1:02}", f"活板门／投递口打开 {i+1}",
             mix((0, rpg("metalLatch", .9, 4700), .28), (.10, rpg(f"creak{i+1}", .85, 4300), .6),
                 (.38, impact(f"impactPlank_medium_{i:03}", .9), .25)), -9,
             note="复合拟音：锁扣＋木铰链吱响＋板材限位轻撞。")
        save("Trapdoor", f"trapdoor_close_{i+1:02}", f"活板门／投递口合上 {i+1}",
             mix((0, rpg(f"creak{i+1}", 1.1, 3900), .2), (.25, impact(f"impactPlank_medium_{i:03}", .84), .9),
                 (.35, rpg("metalLatch", 1, 4500), .22)), -7,
             note="复合拟音；主要板材落点在 0.25 秒，动画据此对齐。")
        save("Trapdoor", f"hinge_creak_{i+1:02}", f"木铰链吱响层 {i+1}", rpg(f"creak{i+1}", .82, 4500), -15)
    save("Trapdoor", "trapdoor_latch_01", "活板门小锁扣", rpg("metalLatch", .89, 4200), -13)

    for i in range(3):
        cloth = rpg(f"cloth{i+1}", .82, 4200)
        soft = impact(f"impactSoft_heavy_{i:03}", .76, 2800)
        save("Bag", f"bag_heavy_pickup_{i+1:02}", f"提起装重物的布袋 {i+1}",
             mix((0, rpg("dropLeather", .95, 3200), .12), (.04, cloth, .8)), -13,
             note="复合拟音；布料摩擦为主，软质低频为辅。")
        save("Bag", f"bag_heavy_setdown_{i+1:02}", f"重物布袋放在木桌 {i+1}",
             mix((0, cloth, .25), (.15, soft, .85), (.17, impact(f"impactWood_medium_{i:03}", .82, 2800), .18)), -8)
        save("Bag", f"bag_heavy_drop_{i+1:02}", f"重物布袋落地 {i+1}",
             mix((0, soft, 1), (.025, cloth, .3), (.10, rpg("dropLeather", .8, 3200), .24)), -6)
    save("Bag", "bag_drag_01", "拖动布袋", mix((0, rpg("clothBelt", .7, 3700), .8),
         (.45, rpg("clothBelt2", .72, 3500), .65), (.95, rpg("cloth4", .8, 3900), .4)), -13)
    save("Bag", "bag_rummage_01", "翻找布袋", mix((0, rpg("cloth2", .9), .7),
         (.24, rpg("cloth4", .86), .55), (.52, rpg("handleSmallLeather", .95), .3)), -14)

    for i in range(2):
        save("Interactions", f"drawer_open_{i+1:02}", f"浅木抽屉拉开 {i+1}",
             mix((0, impact(f"impactWood_light_{i:03}", .95), .25), (.05, rpg(f"creak{i+1}", .9, 3500), .8)), -12,
             note="由木碰撞与吱响组合，适合旧木抽屉。")
        save("Interactions", f"drawer_close_{i+1:02}", f"浅木抽屉推回 {i+1}",
             mix((0, rpg(f"creak{i+1}", 1.15, 3500), .4), (.30, impact(f"impactWood_medium_{i:03}", .9), .7)), -10)
        save("Interactions", f"letter_deliver_{i+1:02}", f"信件落入收发盘 {i+1}",
             mix((0, rpg(f"bookFlip{i+1}", 1.12), .6), (.20, rpg(f"bookPlace{i+1}", 1.1), .22),
                 (.22, impact(f"impactWood_light_{i:03}", 1.05), .1)), -13)
    save("Interactions", "lock_open_01", "封存匣开锁", mix((0, rpg("metalClick", .88), .5), (.15, rpg("metalLatch", .9), .8)), -12)
    save("Interactions", "lock_close_01", "封存匣锁合", mix((0, rpg("metalLatch", .88), .8), (.12, rpg("metalClick", .94), .35)), -12)
    save("Interactions", "seal_stamp_01", "封印压下", mix((0, impact("impactWood_medium_001", .82, 4200), .8),
         (.04, rpg("bookClose", 1.1), .2)), -12, note="复合拟音，用于盖章动作，不是热蜡滴落录音。")
    save("Interactions", "seal_break_01", "拆开封口", mix((0, rpg("bookFlip3", .75), .7),
         (.05, impact("impactWood_light_004", 1.4, 6800), .22)), -15, note="纸张和短促脆响组合。")
    save("Interactions", "object_pickup_01", "拿取小物件", rpg("handleSmallLeather", 1.05, 4800), -16)
    save("Interactions", "object_setdown_01", "小物件放回木桌", impact("impactWood_light_004", 1.08, 4700), -15)
    save("Interactions", "interact_confirm_01", "操作确认／木质轻点", impact("impactWood_light_002", 1.2, 3800), -18)
    save("Interactions", "interact_blocked_01", "操作受阻／锁扣轻响", mix((0, rpg("metalClick", .82, 2600), .5),
         (.095, impact("impactWood_light_001", .85, 3100), .35)), -17)

    for i in range(8):
        key = source("typewriter", f"typewriter{i+1}.wav", 1, 8500, 130)
        save("Typewriter/Clean", f"typewriter_key_clean_{i+1:02}", f"打字机完整单键素材 {i+1}", key, -10,
             note="保留机械回弹尾音；仅单声道化、轻度滤波和边缘整理。未添加角色音高、失真或混响；原录音另存。")
        # Isolate the strongest strike; a 12 ms lead preserves its onset.
        maximum = int(np.argmax(np.abs(key.samples[:, 0])))
        start = max(0, maximum / SR - .012)
        short = excerpt(key, start, [.12, .13, .14, .12, .13, .11, .12, .14][i])
        save("Typewriter/Short", f"typewriter_key_short_{i+1:02}", f"打字机短敲击 {i+1}", short, -16,
             note="供对话系统后续加工的短样本；与同编号 Clean 同源，不是新的独立录音。")


def catalogs_and_preview():
    DEST.mkdir(parents=True, exist_ok=True)
    (ROOT / "manifest.json").write_text(json.dumps({"version": 1, "date": "2026-10-31", "sample_rate": SR,
        "source_license": "CC0-1.0", "assets": CATALOG}, ensure_ascii=False, indent=2), encoding="utf-8")
    keys = ["id", "category", "label_zh", "file", "duration_s", "channels", "loop", "peak_dbfs", "rms_dbfs", "sources", "note_zh"]
    with (ROOT / "catalog.csv").open("w", encoding="utf-8-sig", newline="") as out:
        writer = csv.DictWriter(out, fieldnames=keys, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(CATALOG)
    playlist = ["#EXTM3U"]
    for row in CATALOG:
        playlist += [f"#EXTINF:{row['duration_s']},{row['label_zh']} | {row['id']}", "../../" + row["file"]]
    (ROOT / "all-sounds.m3u8").write_text("\n".join(playlist) + "\n", encoding="utf-8")
    selected = ["wood_collision_light_01", "wood_collision_medium_01", "wood_collision_heavy_01",
        "page_turn_01", "page_turn_02", "page_turn_slow_01", "book_open_01", "book_close_01",
        "wood_door_open_01", "wood_door_close_01", "door_knock_pattern_02", "trapdoor_open_01",
        "trapdoor_close_01", "bag_heavy_pickup_01", "bag_heavy_setdown_01", "bag_heavy_drop_01",
        "bag_drag_01", "drawer_open_01", "drawer_close_01", "letter_deliver_01", "lock_open_01",
        "seal_stamp_01", "seal_break_01", "rain_outside_loop_40s", "rain_window_loop_10s", "rain_room_muffled_loop_10s"]
    segments, cues, position = [], [], 0
    for name in selected:
        x = RENDERED[name]
        if "loop" in name:
            x = fade(x[:6 * SR], .25, .4)
        if x.shape[1] == 1:
            x = np.repeat(x, 2, axis=1)
        cues.append({"start_s": round(position / SR, 3), "id": name,
                     "label_zh": next(a["label_zh"] for a in CATALOG if a["id"] == name)})
        segments += [x, np.zeros((round(.6 * SR), 2), np.float32)]
        position += len(x) + round(.6 * SR)
    for bank, interval in [("clean", .65), ("short", .095)]:
        sequence = np.zeros((round((16 * interval + .7) * SR), 2), np.float32)
        for i in range(16):
            x = RENDERED[f"typewriter_key_{bank}_{i%8+1:02}"]
            at = round(i * interval * SR)
            sequence[at:at+len(x)] += np.repeat(x, 2, axis=1)
        cues.append({"start_s": round(position / SR, 3), "id": "typewriter_" + bank + "_sequence",
                     "label_zh": "打字机素材轮播" if bank == "clean" else "短音连续播放示例（不是最终对话效果）"})
        segments += [sequence, np.zeros((SR, 2), np.float32)]
        position += len(sequence) + SR
    wav24(ROOT / "audition.wav", np.concatenate(segments))
    with (ROOT / "audition-index.csv").open("w", encoding="utf-8-sig", newline="") as out:
        writer = csv.DictWriter(out, fieldnames=["start_s", "id", "label_zh"])
        writer.writeheader()
        writer.writerows(cues)
    # Folder GUIDs are stable; do not overwrite an existing folder importer.
    for folder in [PROJECT / "Assets/Audio", DEST, *sorted(p for p in DEST.rglob("*") if p.is_dir())]:
        meta = folder.with_name(folder.name + ".meta")
        if not meta.exists():
            meta.write_text(f"fileFormatVersion: 2\nguid: {guid_for(folder)}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n", encoding="utf-8")
    print(f"Built {len(CATALOG)} clips in {DEST}")
    print(f"Audition: {position / SR:.1f} seconds")


if __name__ == "__main__":
    build()
    catalogs_and_preview()
