"""Create Unity references and bundled attribution without rewriting the scene."""
from pathlib import Path
import json
import re
import uuid

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]


def ensure_meta(path, importer="DefaultImporter", folder=False):
    target = Path(str(path) + ".meta")
    if target.exists():
        return re.search(r"guid: (\w+)", target.read_text())[1]
    guid = uuid.uuid5(uuid.NAMESPACE_URL, "purpleglove/music/" + path.relative_to(PROJECT).as_posix()).hex
    text = "fileFormatVersion: 2\nguid: " + guid + "\n" + ("folderAsset: yes\n" if folder else "")
    text += importer + ":\n  externalObjects: {}\n"
    if importer == "MonoImporter":
        text += "  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n"
    if importer == "NativeFormatImporter":
        text += "  mainObjectFileID: 11400000\n"
    text += "  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
    target.write_text(text, encoding="utf-8")
    return guid


if __name__ == "__main__":
    manifest = json.loads((HERE / "manifest.json").read_text())
    tracks = {t["id"]: t for t in manifest["tracks"]}
    assert len(tracks) == 5, "All five tracks must finish rendering first."
    for name in ["StoryMusicSettings", "StoryMusicVoice", "StoryMusicDirector"]:
        ensure_meta(PROJECT / f"Assets/Scripts/Presentation/{name}.cs", "MonoImporter")
    script_guid = ensure_meta(PROJECT / "Assets/Scripts/Presentation/StoryMusicSettings.cs", "MonoImporter")
    for folder in ["Assets/Resources", "Assets/Resources/PHMusic", "Assets/StreamingAssets"]:
        path = PROJECT / folder
        path.mkdir(exist_ok=True)
        ensure_meta(path, folder=True)
    credits = ("配乐：Scott Buckley\nPhoenix (2026) · Anabasis I · Unraveling · Hiraeth · Undertow\n"
               "www.scottbuckley.com.au · CC BY 4.0\nhttps://creativecommons.org/licenses/by/4.0/\n"
               "本游戏版本经剪辑、循环处理与响度调整")
    lines = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
             "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
             "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
             f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}", "  m_Name: StoryMusic", "  m_EditorClassIdentifier:"]
    for name, ident in [("investigation", "phoenix"), ("pressure", "anabasis"), ("reflection", "unraveling"), ("reunion", "hiraeth"), ("loss", "undertow")]:
        lines.append("  " + name + ":")
        for part in ["intro", "loop", "outro"]:
            clip = tracks[ident]["clips"].get(part)
            ref = "{fileID: 0}" if clip is None else "{fileID: 8300000, guid: " + clip["guid"] + ", type: 3}"
            lines.append("    " + part + ": " + ref)
    lines += ["  candlelitMonastery: {fileID: 0}", "  volume: 0.4", "  crossfadeSeconds: 4", "  dialogueGain: 0.65", "  deliveryGain: 0.5",
              "  credits: " + json.dumps(credits, ensure_ascii=False)]
    asset = PROJECT / "Assets/Resources/PHMusic/StoryMusic.asset"
    if not asset.exists():
        asset.write_text("\n".join(lines) + "\n", encoding="utf-8")
    ensure_meta(asset, "NativeFormatImporter")
    attribution = ["PURPLE GLOVE - MUSIC CREDITS", "Music by Scott Buckley - released under CC-BY 4.0. www.scottbuckley.com.au",
                   "License: https://creativecommons.org/licenses/by/4.0/", "", "Tracks and original source pages:"]
    for track in tracks.values():
        attribution += [track["title"] + " by Scott Buckley", track["page"], ""]
    attribution += ["Game adaptations: excerpts, cyclic crossfades, intro/loop/outro editing, fixed loudness adjustment and PCM conversion.",
                    "The composer does not endorse these edits. Original audio is MP3; PCM conversion does not restore lost information.",
                    "Source and edit details: output/music-loop-v1/sources.json and manifest.json in the development project.",
                    "No Candlelit Monastery recording is included; that asset remains an optional purchased replacement."]
    for name in ["Assets/Audio/PH/Music/CREDITS.txt", "Assets/StreamingAssets/MUSIC_CREDITS.txt"]:
        path = PROJECT / name
        path.write_text("\n".join(attribution)+"\n", encoding="utf-8")
        ensure_meta(path, "TextScriptImporter" if "StreamingAssets" not in name else "DefaultImporter")
    print("Installed StoryMusic settings, script references, and packaged credits.")
