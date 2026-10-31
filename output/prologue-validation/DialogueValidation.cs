using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PurpleGlove;
using PurpleGlove.Editor;

[InitializeOnLoad]
public static class DialogueValidation
{
    const string Key = "PH.DialogueValidation";
    static readonly string Output = Path.GetFullPath("../../output/prologue-validation/dialogue-regression");
    static int checks, stage, completed, cancelled;
    static double started;
    static DialogueController dialogue;
    static DialogueTestConsole console;
    static SceneInputGate gate;
    static DeskScenePreview view;
    static SceneObjectInteractor interactor;
    static SceneObject notebook;
    static DialogueValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    static object Call(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, args);

    public static void Run()
    {
        Directory.CreateDirectory(Output);
        try
        {
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            int items = UnityEngine.Object.FindObjectsOfType<SceneObject>(true).Length;
            DeskDialogueBuilder.Install(); // Re-running must preserve the room and produce no duplicate controls.
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            Check(items == UnityEngine.Object.FindObjectsOfType<SceneObject>(true).Length, "Installer changed room props");
            Check(UnityEngine.Object.FindObjectsOfType<DialogueController>().Length == 1, "Duplicate controller");
            Check(UnityEngine.Object.FindObjectsOfType<VisitorEyes>().Length == 1, "Duplicate portrait");
            var d = Camera.main.GetComponent<DialogueController>();
            Check(d.speakers.Length == 8 && d.events.Length == 7, "Incomplete cast/events");
            Check(d.speakers.Select(s => s.textColor).Distinct().Count() == 8, "Duplicate colours");
            Check(d.speakers.Select(s => s.pitch).Distinct().Count() == 8, "Duplicate voice pitches");
            Check(d.speakers.Count(s => s.isProtagonist) == 1, "Wrong protagonist count");
            Check(!d.visitor.portrait.enabled && !d.visitor.portrait.GetComponent<Collider>(), "Portrait visible/collidable at rest");
            Check(d.visitor.portrait.transform.position.z > d.eyeShutter.transform.position.z, "Portrait is not behind the door");
            Check(d.visitor.portrait.sharedMaterial.shader.isSupported, "Eye shader unsupported");
            var font = d.GetComponent<DialogueOverlay>().font;
            foreach (var speaker in d.speakers)
            {
                Check(speaker.typingClips.Length >= 4 && speaker.typingClips.All(c => c), "Missing voice samples");
                if (speaker.isProtagonist) continue;
                Check(speaker.eyes && speaker.eyes.filterMode == FilterMode.Point, "Missing/crushed eye texture");
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(speaker.eyes));
                Check(!importer.mipmapEnabled && importer.textureCompression == TextureImporterCompression.Uncompressed, "Filtered eye texture");
            }
            foreach (var sequence in d.events)
            {
                Check(sequence.TryValidate(out _), "Invalid event " + sequence.name);
                Check(sequence.arrivalSound && sequence.initialVisitor, "Missing arrival presentation");
                foreach (var line in sequence.lines)
                {
                    Check(line.text.Count(c => "。！？".Contains(c)) == 1, "Line contains more/less than one sentence");
                    foreach (char c in line.text) Check(font.HasCharacter(c), "Missing glyph: " + c);
                }
            }
            SessionState.SetInt(Key + ".checks", checks);
            SessionState.SetBool(Key, true); Install(); EditorApplication.isPlaying = true;
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    static void Install()
    {
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
    }
    static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Finish(false, message + "\n" + stack); }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup - started > 90) { Finish(false, "Timeout at stage " + stage); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isPaused) return;
        try
        {
            if (stage == 0)
            {
                dialogue = Camera.main.GetComponent<DialogueController>();
                if (!dialogue || !dialogue.Overlay || !dialogue.Overlay.Panel) return;
                console = dialogue.GetComponent<DialogueTestConsole>(); gate = dialogue.GetComponent<SceneInputGate>();
                view = dialogue.GetComponent<DeskScenePreview>(); interactor = dialogue.GetComponent<SceneObjectInteractor>();
                notebook = UnityEngine.Object.FindFirstObjectByType<NotebookBook>().GetComponent<SceneObject>();
                dialogue.eventCompleted.AddListener(id => completed++); dialogue.eventCancelled.AddListener(id => cancelled++);
                Check(view.SetView(DeskScenePreview.View.Bookshelf), "Cannot select shelf before event");
                interactor.Inspect(notebook); Call(notebook, "Animate", 2f);
                Check(interactor.InspectedObject == notebook, "Inspection setup failed");
                Check(dialogue.Play("arrival"), "Cannot start arrival");
                Check(!interactor.InspectedObject && notebook.State == SceneObject.ObjectState.Resting, "Inspection not cancelled");
                Check(gate.BlocksWorldInput && view.DialogueLocked && view.CurrentView == DeskScenePreview.View.Doorway, "Missing modal lock");
                Check(!view.SetView(DeskScenePreview.View.Desktop), "View switch escaped lock");
                interactor.Inspect(notebook); Check(!interactor.InspectedObject, "Direct inspection escaped lock");
                var drum = UnityEngine.Object.FindObjectsOfType<DoorMechanism>().Single(m => m.mechanism == DoorMechanism.Mechanism.TransferDrum);
                Vector2 pointer = Camera.main.WorldToScreenPoint(drum.transform.TransformPoint(new Vector3(0, -0.226f, -0.13f)));
                Call(interactor, "ProcessInput", pointer, true, true, false);
                Check(!drum.IsMoving, "Click escaped into the transfer drum");
                Camera.main.transform.rotation = Quaternion.identity; Call(view, "LateUpdate");
                Check(Quaternion.Angle(Camera.main.transform.rotation, Quaternion.Euler(view.doorwayEuler)) < .001f
                    && Camera.main.fieldOfView == DeskScenePreview.DoorwayFieldOfView, "Mouse/look displaced locked camera");
                Check(!dialogue.Play("pressure"), "Overlapping event accepted");
                Check(!dialogue.Play("missing"), "Unknown event accepted");
                Arrive();
                Check(dialogue.LineIndex == 0 && dialogue.VisibleText.Length == 0, "Arrival already advanced a sentence");
                Call(dialogue, "AdvanceTime", .001f);
                Check(dialogue.VisibleText.Length == 1 && dialogue.IsTyping, "Typewriter did not reveal one element");
                Check(dialogue.Voice.PlayedCount > 0, "No typing sound scheduled");
                int soundCount = dialogue.Voice.PlayedCount;
                dialogue.Advance();
                Check(!dialogue.IsTyping && dialogue.VisibleText == dialogue.CurrentLine.text, "Skip failed to reveal full sentence");
                Check(dialogue.Voice.PlayedCount == soundCount, "Skip played an audio burst");
                Bounds(false); Capture("01-npc-middle"); Capture("npc-4x3", 1024, 768); Capture("npc-ultrawide", 2560, 1080);
                var visitor = dialogue.visitor.CurrentVisitor;
                dialogue.Advance(); Call(dialogue, "AdvanceTime", .001f);
                Check(dialogue.CurrentLine.speaker.isProtagonist && dialogue.visitor.CurrentVisitor == visitor, "Visitor lost during Luca reply");
                Bounds(true); dialogue.Advance(); Capture("02-luca-bottom");
                console.SetOpen(true);
                Check(gate.ConsoleOpen && gate.BlocksWorldInput, "Console did not lock world");
                string text = dialogue.VisibleText; Call(dialogue, "Update");
                Check(dialogue.VisibleText == text, "Console did not pause dialogue");
                Check(!dialogue.Voice.GetComponentsInChildren<AudioSource>().Any(s => s.isPlaying), "Typing audio continued in console");
                float speed = dialogue.speedOverride, volume = dialogue.Voice.masterVolume;
                console.Execute("speed NaN"); console.Execute("volume Infinity"); console.Execute("speed 500");
                Check(dialogue.speedOverride == speed && dialogue.Voice.masterVolume == volume, "Invalid numeric command accepted");
                console.Execute("speed 32"); console.Execute("volume 0");
                Check(dialogue.speedOverride == 32 && dialogue.Voice.masterVolume == 0, "Valid settings rejected");
                console.Execute("stop");
                Check(!dialogue.IsActive && !view.DialogueLocked && gate.ConsoleOpen, "Stopping lost console lock");
                Check(view.CurrentView == DeskScenePreview.View.Bookshelf && !dialogue.eyeShutter.AtSecondStop, "State not restored");
                Check(cancelled == 1 && completed == 0 && !dialogue.visitor.portrait.enabled, "Cancel fired wrong callbacks/left eyes");
                console.SetOpen(false);
                Check(gate.BlocksWorldInput, "Closing click not consumed");
                stage++; return;
            }
            if (stage == 1)
            {
                if (gate.BlocksWorldInput) return;
                Check(view.SetView(DeskScenePreview.View.Desktop), "Input not restored next frame");
                interactor.Inspect(notebook); Check(interactor.InspectedObject, "World remained locked"); interactor.CancelInteraction();
                // Starting from an open shutter, interrupting while arriving, and disabling must restore exactly.
                dialogue.eyeShutter.SetPoseImmediately(true); Check(dialogue.Play("beatrice"), "Open-shutter start failed");
                dialogue.enabled = false;
                Check(!dialogue.IsActive && !view.DialogueLocked && dialogue.eyeShutter.AtSecondStop, "Disable did not restore open shutter");
                dialogue.enabled = true; dialogue.eyeShutter.SetPoseImmediately(false);
                Check(dialogue.Play("beatrice"), "Restart after disable failed");
                Check(dialogue.eyeShutter.IsMoving, "Window did not animate"); dialogue.Stop();
                Check(!dialogue.eyeShutter.IsMoving && !dialogue.eyeShutter.AtSecondStop, "Cancel during opening stranded shutter");
                console.Execute("speed 0"); console.Execute("volume 0.75");
                foreach (var sequence in dialogue.events)
                {
                    Check(dialogue.Play(sequence), "Event start failed: " + sequence.id); Arrive();
                    for (int i = 0; i < sequence.lines.Length; i++)
                    {
                        Check(dialogue.LineIndex == i && dialogue.CurrentLine == sequence.lines[i], "Line order changed");
                        Call(dialogue, "AdvanceTime", .001f);
                        var sources = dialogue.Voice.GetComponentsInChildren<AudioSource>();
                        var played = sources.FirstOrDefault(s => s.clip && Mathf.Abs(s.pitch - dialogue.CurrentLine.speaker.pitch) < .04f);
                        Check(played, "Speaker voice pitch not applied");
                        if (!dialogue.CurrentLine.speaker.isProtagonist)
                            Check(dialogue.visitor.CurrentVisitor == dialogue.CurrentLine.speaker, "Wrong visitor portrait");
                        Bounds(dialogue.CurrentLine.speaker.isProtagonist);
                        dialogue.Advance();
                        Check(dialogue.VisibleText == sequence.lines[i].text, "Sentence truncated");
                        if (i == 0) Capture("event-" + sequence.id);
                        dialogue.Advance();
                    }
                    Check(!dialogue.IsActive && !dialogue.visitor.portrait.enabled && !gate.DialogueActive, "Completion left modal state");
                }
                Check(completed == 7 && cancelled == 3, "Incorrect completion/cancel callback count");
                var unicode = ScriptableObject.CreateInstance<DialogueEvent>();
                unicode.id = "unicode"; unicode.initialVisitor = dialogue.speakers[1]; unicode.arrivalDelay = 0;
                unicode.lines = new[] { new DialogueEvent.Line { speaker = dialogue.speakers[0], text = "甲😀e\u0301乙。" } };
                Check(dialogue.Play(unicode), "Unicode test event rejected"); Arrive();
                Call(dialogue, "AdvanceTime", .001f); Check(dialogue.VisibleText == "甲", "First grapheme wrong");
                Call(dialogue, "AdvanceTime", 1f / 24); Check(dialogue.VisibleText == "甲😀", "Surrogate pair split");
                Call(dialogue, "AdvanceTime", 1f / 24); Check(dialogue.VisibleText == "甲😀e\u0301", "Combining mark split");
                dialogue.Stop(); UnityEngine.Object.Destroy(unicode);
                var empty = ScriptableObject.CreateInstance<DialogueEvent>(); empty.id = "empty";
                Check(!dialogue.Play(empty) && !gate.DialogueActive, "Invalid event acquired locks"); UnityEngine.Object.Destroy(empty);
                stage++; return;
            }
            if (stage == 2)
            {
                if (gate.BlocksWorldInput) return;
                console.SetOpen(true);
                Check(console.Execute("play arrival").StartsWith("开始"), "Console play command failed");
                Check(!console.IsOpen && dialogue.IsActive, "Console play did not close/start");
                dialogue.Stop();
                Finish(true, "PASS: " + (checks + SessionState.GetInt(Key + ".checks", 0)) + " assertions. Unity 2022.3.62f2 / Play Mode / D3D11.\n"
                    + "8 unique speaker colours/voices; 7 pixel eye assets; 7 valid events / 40 single-sentence lines.\n"
                    + "Installer idempotence; saved scene reopen; all Chinese glyphs; shader and texture settings.\n"
                    + "Inspection cancellation, ray/direct interaction blocking, exact doorway lock, previous view/shutter restoration.\n"
                    + "Grapheme reveal; skip without audio burst; single panel/thirds/colour; visitor persists on protagonist reply.\n"
                    + "Console pause/settings/invalid commands/input suppression; every event completes; cancel/disable/arrival interruption cleanup.\n"
                    + "Screenshots contain actual camera + dialogue canvas; audio scheduling verified, subjective listening still recommended.\n");
            }
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    static void Arrive()
    { Call(dialogue.eyeShutter, "Advance", 10f); Call(dialogue, "AdvanceTime", 10f); }
    static void Bounds(bool protagonist)
    {
        var panel = dialogue.Overlay.Panel;
        float bottom = protagonist ? 0 : 1f / 3, top = protagonist ? 1f / 3 : 2f / 3;
        Check(panel.anchorMin.y >= bottom && panel.anchorMax.y <= top, "Text crossed its assigned third");
        Check(dialogue.Overlay.Body.color == dialogue.CurrentLine.speaker.textColor, "Wrong text colour");
        Canvas.ForceUpdateCanvases();
        var text = dialogue.Overlay.Body;
        Check(text.cachedTextGenerator.lineCount <= 3, "Sentence overflowed the text area");
    }
    static void Capture(string name, int width = 1600, int height = 900)
    {
        var camera = Camera.main;
        var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        var overlayCamera = dialogue.Overlay.OverlayCamera;
        var oldOverlayTarget = overlayCamera.targetTexture;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; overlayCamera.targetTexture = target;
            Canvas.ForceUpdateCanvases(); camera.Render(); overlayCamera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget; overlayCamera.targetTexture = oldOverlayTarget; RenderTexture.active = oldActive;
            RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(image);
        }
    }
    static void Finish(bool success, string message)
    {
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Output + "/validation.txt", message);
        if (success) Debug.Log("DIALOGUE_VALIDATION_PASS"); else Debug.LogError(message);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
