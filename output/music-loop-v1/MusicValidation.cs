using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PurpleGlove;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MusicValidation
{
    const string Key = "MusicValidation.Running";
    static readonly string Output = Path.GetFullPath("../../output/music-loop-v1");
    static int checks, stage;
    static bool done;
    static double started, mark, frozen;
    static StoryMusicDirector music;
    static StoryMusicVoice voice;
    static GameObject transportRoot;
    static AudioSource loopSource;
    static int previousSample, wraps;
    static MusicValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static void Set(object target, string property, object value) => target.GetType().GetField("<" + property + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    public static void Run()
    {
        try
        {
            if (!Path.GetFullPath(".").Replace('\\','/').EndsWith("/Temp/music-validation")) throw new Exception("Use isolated project");
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene("Assets/Art/DeskSceneV1/Scenes/PH_DeskRoom.unity");
            var settings = Resources.Load<StoryMusicSettings>("PHMusic/StoryMusic");
            Check(settings, "Missing config");
            foreach (var cue in new[] { settings.investigation, settings.pressure, settings.reflection, settings.reunion, settings.loss })
            {
                Check(cue != null && cue.loop, "Missing loop");
                Check(cue.loop.channels == 2 && cue.loop.frequency == 48000 && cue.loop.length >= 40, "Bad loop format");
                var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(cue.loop));
                Check(importer.defaultSampleSettings.loadType == AudioClipLoadType.Streaming, "Music not streaming");
                Check(importer.defaultSampleSettings.compressionFormat == AudioCompressionFormat.PCM, "PCM seam not retained");
            }
            Check(settings.reunion.intro && settings.reunion.outro && settings.loss.intro && settings.loss.outro, "Missing ending sections");
            Check(!settings.candlelitMonastery, "Unexpected paid asset");
            Check(File.Exists("Assets/StreamingAssets/MUSIC_CREDITS.txt"), "Credits not bundled");
            var font = Camera.main.GetComponent<DialogueOverlay>().font;
            foreach (char c in settings.credits.Where(c => !char.IsWhiteSpace(c)).Distinct()) Check(font.HasCharacter(c), "Credit glyph missing: " + c);
            SessionState.SetInt(Key + ".Checks", checks); SessionState.SetBool(Key, true); Install();
            EditorApplication.isPlaying = true;
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Install()
    {
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
    }
    static void Log(string text, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Finish(false, text + "\n" + stack); }
    static void CheckMood(StoryMusicDirector.Mood expected)
    {
        music.Advance(5, false); Check(music.CurrentMood == expected, "Wrong mood: " + expected);
        Check(music.GetComponentsInChildren<AudioSource>().Count(s => s.volume > 0) <= 3, "Outgoing voice not faded");
    }
    static AudioClip Tone(string name, float duration)
    {
        var clip = AudioClip.Create(name, Mathf.RoundToInt(48000 * duration), 2, 48000, false);
        var samples = new float[clip.samples * 2];
        for (int i = 0; i < clip.samples; i++) samples[i*2] = samples[i*2+1] = Mathf.Sin(i * Mathf.PI * 2 * 200 / 48000) * .01f;
        clip.SetData(samples, 0); return clip;
    }
    static void Tick()
    {
        if (done) return;
        try
        {
            if (EditorApplication.timeSinceStartup - started > 100) throw new Exception("Music test timed out; DSP state=" + stage);
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (stage == 0)
            {
                music = UnityEngine.Object.FindFirstObjectByType<StoryMusicDirector>();
                if (!music) return;
                checks = SessionState.GetInt(Key + ".Checks", 0);
                Check(UnityEngine.Object.FindObjectsByType<StoryMusicDirector>(FindObjectsSortMode.None).Length == 1, "Duplicate bootstrap");
                var records = Camera.main.GetComponent<PlaintextArchiveController>(); var archive = records.archive;
                var story = Camera.main.GetComponent<PrologueSequence>(); var fifth = Camera.main.GetComponent<FifthActFlow>();
                music.enabled = false; // Advance explicitly, independently of unrelated story timing.
                Set(archive, "CurrentAct", 1); CheckMood(StoryMusicDirector.Mood.Silence);
                Set(story, "PresentedAct", 1); CheckMood(StoryMusicDirector.Mood.Investigation);
                Set(archive, "CurrentAct", 2); CheckMood(StoryMusicDirector.Mood.Investigation);
                var voices = Field<StoryMusicVoice[]>(music, "voices");
                int active = Field<int>(music, "activeVoice");
                double origin = Field<double>(voices[active], "origin");
                music.Advance(1, false); Check(Field<double>(voices[active], "origin") == origin, "Same mood restarted");
                Set(archive, "CurrentAct", 3); CheckMood(StoryMusicDirector.Mood.Pressure);
                Set(archive, "DeskAct", 1); CheckMood(StoryMusicDirector.Mood.Pressure);
                Set(archive, "CurrentAct", 4); CheckMood(StoryMusicDirector.Mood.Investigation);
                records.entries.Single(e => e.id == "rail").saved = true; CheckMood(StoryMusicDirector.Mood.Reflection);
                Set(archive, "CurrentAct", 5); CheckMood(StoryMusicDirector.Mood.Reflection);
                Set(archive, "CurrentAct", 6);
                foreach (bool guarantee in new[] { false, true })
                foreach (bool burned in new[] { false, true })
                foreach (FifthActFlow.Custody custody in Enum.GetValues(typeof(FifthActFlow.Custody)))
                {
                    Set(fifth, "GuaranteesSource", guarantee); Set(fifth, "ConfessionBurned", burned); Set(fifth, "OriginalCustody", custody);
                    CheckMood(!guarantee && custody == FifthActFlow.Custody.Guild ? StoryMusicDirector.Mood.Reunion : StoryMusicDirector.Mood.Loss);
                }
                var gate = Camera.main.GetComponent<SceneInputGate>();
                gate.SetDelivery(true); music.Advance(1, false); Check(Math.Abs(music.DuckGain-music.settings.deliveryGain)<.001, "Delivery duck missing");
                gate.SetDelivery(false); music.Advance(1, false); Check(music.DuckGain == 1, "Duck did not recover");
                var dialogue = Camera.main.GetComponent<DialogueController>();
                var dummy = ScriptableObject.CreateInstance<DialogueEvent>(); Set(dialogue, "ActiveEvent", dummy);
                music.Advance(1, false); Check(Math.Abs(music.DuckGain-music.settings.dialogueGain)<.001, "Dialogue duck missing");
                Set(dialogue, "ActiveEvent", null); UnityEngine.Object.Destroy(dummy);
                music.muted = true; music.Advance(1, false); Check(music.GetComponentsInChildren<AudioSource>().All(s => s.volume == 0), "Mute did not silence music");
                music.muted = false; music.Advance(1, true); Check(music.Paused, "Pause missing"); music.Advance(1, false);
                Set(Camera.main.GetComponent<EpilogueFlow>(), "State", EpilogueFlow.Phase.Complete);
                music.Advance(1, false); Check(music.OutroRequested, "Ending outro missing");
                var creditCanvas = music.GetComponentInChildren<Canvas>(true);
                Check(creditCanvas && creditCanvas.enabled && creditCanvas.sortingOrder > 10000, "Credits hidden behind final fade");
                var curtain = Camera.main.GetComponent<ActTransitionOverlay>();
                curtain.BeginFinalFade("尾声 · 背书\n终", null); curtain.Advance(10);
                double at = Field<StoryMusicVoice[]>(music,"voices")[Field<int>(music,"activeVoice")].OutroAt;
                music.Advance(1, false); Check(Field<StoryMusicVoice[]>(music,"voices")[Field<int>(music,"activeVoice")].OutroAt == at, "Outro retriggered");
                foreach (var v in voices) v.Stop();
                transportRoot = new GameObject("Transport test"); voice = new StoryMusicVoice(transportRoot.transform, "Short cues");
                voice.Play(new StoryMusicSettings.Cue { intro = Tone("intro", .16f), loop = Tone("loop", .31f), outro = Tone("outro", .15f) });
                loopSource = Field<AudioSource>(voice, "loop"); mark = EditorApplication.timeSinceStartup; stage = 1; return;
            }
            if (stage == 1)
            {
                int sample = loopSource.timeSamples;
                if (sample < previousSample) wraps++;
                previousSample = sample;
                if (EditorApplication.timeSinceStartup - mark < 1.15) return;
                Check(voice.Elapsed > .7, "DSP clock not advancing"); Check(wraps >= 1, "Loop did not repeat");
                voice.SetPaused(true); frozen = voice.Elapsed; mark = EditorApplication.timeSinceStartup; stage = 2; return;
            }
            if (stage == 2)
            {
                if (EditorApplication.timeSinceStartup - mark < .2) return;
                Check(voice.Elapsed == frozen && !loopSource.isPlaying, "Paused clock/audio advanced");
                voice.FinishAtBoundary(); double boundary = voice.OutroAt;
                voice.SetPaused(false); Check(voice.OutroAt == boundary, "Resume lost pending outro");
                Check(voice.Elapsed >= frozen, "Resume moved transport backwards");
                mark = EditorApplication.timeSinceStartup; stage = 3; return;
            }
            if (stage == 3)
            {
                if (EditorApplication.timeSinceStartup - mark < 1) return;
                Check(voice.HasFinished, "Outro did not finish"); Check(!loopSource.isPlaying, "Loop continued under finished outro");
                voice.Play(new StoryMusicSettings.Cue { intro = Tone("early intro", .6f), loop = Tone("unused loop", .31f), outro = Tone("early outro", .2f) });
                voice.FinishAtBoundary(); Check(Math.Abs(voice.OutroAt - .6) < .001, "Early ending did not leave at intro boundary");
                voice.SetPaused(true); voice.SetPaused(false);
                Check(!loopSource.isPlaying, "Early ending queued a zero-duration loop");
                mark = EditorApplication.timeSinceStartup; stage = 5; return;
            }
            if (stage == 5)
            {
                if (EditorApplication.timeSinceStartup - mark < 1.1) return;
                Check(voice.HasFinished && !loopSource.isPlaying, "Early outro/resume failed");
                voice.Stop(); UnityEngine.Object.Destroy(transportRoot);
                SceneManager.LoadScene("Assets/Art/DeskSceneV1/Scenes/PH_DeskRoom.unity"); stage = 4; return;
            }
            if (stage == 4)
            {
                var directors = UnityEngine.Object.FindObjectsByType<StoryMusicDirector>(FindObjectsSortMode.None);
                Check(directors.Length == 1, "Reload duplicated or lost music");
                Check(directors[0].GetComponentsInChildren<AudioSource>().Length == 6, "Reload retained old audio voices");
                Check(directors[0].ResolveMood() == StoryMusicDirector.Mood.Silence, "Reload retained ending mood");
                Finish(true, "All five cues, 16 ending dispositions, preserved dossier review, mute/duck/pause, real DSP loop/outro, scene reload and bundled credits passed.");
            }
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Finish(bool passed, string message)
    {
        if (done) return; done = true; SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Path.Combine(Output, "unity-validation.txt"), (passed ? "PASS " : "FAIL ") + checks + " checks\n" + message);
        Debug.Log((passed ? "MUSIC_VALIDATION_PASSED " : "MUSIC_VALIDATION_FAILED ") + checks + " " + message);
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
