using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PurpleGlove;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Run in the isolated project copy, never in the user's editing session.
[InitializeOnLoad]
public static class DialoguePresentationValidation
{
    const string Key = "DialoguePresentationValidation.Play";
    static readonly string Output = Path.GetFullPath("../../output/dialogue-presentation-validation");
    static DialogueController dialogue;
    static SceneInputGate gate;
    static DeskScenePreview view;
    static Camera camera;
    static int checks;
    static bool done;
    static double started;
    static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(obj, args);
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); checks++; }
    static void Ungate() => typeof(SceneInputGate).GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gate, -1);
    static void Step(float dt = 0) { Ungate(); Call(dialogue, "AdvanceTime", dt); }
    static void Door(float dt = 5) => Call(dialogue.eyeShutter, "Advance", dt);
    static DialoguePresentationValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene("Assets/Art/DeskSceneV1/Scenes/PH_DeskRoom.unity");
        SessionState.SetBool(Key, true); Install(); EditorApplication.isPlaying = true;
    }
    static void Install()
    {
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
    }
    static void Log(string text, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Finish(false, text + "\n" + stack); }
    static void Tick()
    {
        if (done) return;
        if (EditorApplication.timeSinceStartup - started > 150) { Finish(false, "Timed out"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating || Time.frameCount < 5) return;
        try
        {
            camera = Camera.main; dialogue = camera.GetComponent<DialogueController>(); gate = camera.GetComponent<SceneInputGate>();
            view = camera.GetComponent<DeskScenePreview>(); view.followMouse = false;
            camera.GetComponent<PrologueSequence>().Stop(); dialogue.Stop();
            foreach (var behaviour in camera.GetComponents<MonoBehaviour>())
                if (behaviour.GetType().Name.EndsWith("Flow") || behaviour.GetType().Name.EndsWith("Opening")) behaviour.enabled = false;
            camera.GetComponent<DialogueTestConsole>().SetOpen(false); Ungate();
            Appearance(); VisitorTransitions(); Choices();
            Finish(true, "PASS: " + checks + " checks. Saved-scene YinPin ZhuSong font and glyph coverage; distinct secretary portrait; real Beatrice/clerk and same-visitor conversations; hidden portrait switch between full shutter close/open; event boundaries; console pause; cancellation and disabled mechanism recovery; NPC speech before three standalone choices; instant protagonist confirmations; pointer hits and unclipped options at 16:9, 4:3 and ultrawide; no left accent. Unity 2022.3.62f2 Play Mode.");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Appearance()
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/DeskSceneV1/Fonts/YinPinZhuSong.ttf");
        Check(font && dialogue.Overlay.font == font && dialogue.Overlay.Body.font == font, "Dialogue uses another font");
        foreach (char c in string.Concat(dialogue.events.SelectMany(e => e.lines).Select(l => l.text + string.Concat(l.choices.Select(ch => ch.text))))
            .Where(c => !char.IsWhiteSpace(c)).Distinct()) Check(font.HasCharacter(c), "Missing glyph U+" + ((int)c).ToString("X4") + " " + c);
        var secretary = dialogue.speakers.Single(s => s.id == "ducal_secretary");
        var clerk = dialogue.speakers.Single(s => s.id == "clerk");
        Check(secretary.eyes && secretary.eyes != clerk.eyes && secretary.eyes.name == "visitor_duke", "Secretary portrait not replaced");
        Check(!dialogue.Overlay.Panel.Find("Speaker colour"), "Left accent remains");
    }
    static void Ready()
    {
        for (int i = 0; i < 8 && dialogue.IsActive && dialogue.LineIndex < 0; i++)
        { Call(view, "AnimateView", 5f, Vector2.zero); Door(); Step(5); }
        Check(dialogue.IsActive && dialogue.LineIndex >= 0 && dialogue.eyeShutter.AtSecondStop, "Speech did not wait for open shutter");
    }
    static void Next()
    {
        if (dialogue.IsTyping) dialogue.Advance();
        dialogue.Advance();
    }
    static void Reset()
    {
        dialogue.Stop(); Door(); dialogue.eyeShutter.SetPoseImmediately(false); dialogue.visitor.Show(null); Ungate();
    }
    static void CheckSwitch(DialogueSpeaker from, DialogueSpeaker to)
    {
        Check(dialogue.IsChangingVisitor && dialogue.eyeShutter.IsMoving && dialogue.visitor.CurrentVisitor == from, "Portrait switched before closing");
        Check(dialogue.CurrentLine == null && !dialogue.Overlay.OverlayCamera.enabled && !dialogue.IsChoosing, "Text visible during change");
        dialogue.Advance(); Check(!dialogue.SelectChoice(0), "Transition accepted choice");
        Door(dialogue.eyeShutter.travelSeconds / 2); Step();
        Check(dialogue.IsChangingVisitor && dialogue.visitor.CurrentVisitor == from && dialogue.eyeShutter.IsMoving, "Half-closed portrait changed");
        Door(); Check(!dialogue.eyeShutter.AtSecondStop && dialogue.visitor.CurrentVisitor == from, "Closed state invalid");
        gate.SetConsole(true); Step(2); Check(dialogue.visitor.CurrentVisitor == from, "Console advanced portrait change"); gate.SetConsole(false);
        Step(); Check(dialogue.visitor.CurrentVisitor == to && dialogue.eyeShutter.IsMoving && !dialogue.Overlay.OverlayCamera.enabled, "Hidden portrait change/open failed");
        Door(dialogue.eyeShutter.travelSeconds / 2); Step(); Check(dialogue.CurrentLine == null, "Speech started mid-opening");
        Door(); Step(5); Check(!dialogue.IsChangingVisitor && dialogue.CurrentLine.speaker == to && dialogue.eyeShutter.AtSecondStop, "New visitor did not speak after opening");
    }
    static void VisitorTransitions()
    {
        Reset(); var sequence = dialogue.events.Single(e => e.id == "beatrice");
        Check(dialogue.Play(sequence), "Cannot play Beatrice visit"); Ready();
        var clerk = sequence.lines[0].speaker; var guest = sequence.lines[1].speaker;
        Check(dialogue.visitor.CurrentVisitor == clerk, "Guest appeared during clerk introduction");
        Next(); CheckSwitch(clerk, guest);
        Next(); Check(!dialogue.IsChangingVisitor && dialogue.LineIndex == 2, "Same visitor closed shutter");
        Next(); Next(); Check(dialogue.CurrentLine.speaker.isProtagonist && !dialogue.IsChangingVisitor && dialogue.visitor.CurrentVisitor == guest, "Luca reply changed visitor");
        Next(); CheckSwitch(guest, clerk); Next();
        Check(dialogue.IsClosing, "End did not wait for closing"); Door(); Step(); Check(!dialogue.IsActive, "Visit failed to finish");

        var synthetic = ScriptableObject.CreateInstance<DialogueEvent>(); synthetic.id = "visitor_boundary"; synthetic.arrivalDelay = 0;
        synthetic.lines = new[] { new DialogueEvent.Line { speaker = guest, text = "来访者交接。" } };
        dialogue.eyeShutter.SetPoseImmediately(true); dialogue.visitor.Show(clerk);
        Check(dialogue.Play(synthetic), "Cannot play with open shutter"); Next(); CheckSwitch(clerk, guest);
        Next(); Check(!dialogue.IsActive && dialogue.visitor.CurrentVisitor == guest && dialogue.eyeShutter.AtSecondStop, "Retained open visitor vanished");
        synthetic.lines[0].speaker = clerk; Check(dialogue.Play(synthetic), "Next open-shutter visit failed"); CheckSwitch(guest, clerk); Reset();
        for (int phase = 0; phase < 2; phase++)
        {
            dialogue.eyeShutter.SetPoseImmediately(true); dialogue.visitor.Show(guest);
            Check(dialogue.Play(synthetic), "Cancel test failed to start");
            if (phase == 1) { Door(); Step(); }
            dialogue.Stop(); Door(); Check(!dialogue.IsActive && !dialogue.IsChangingVisitor && !gate.DialogueActive && !view.DialogueLocked, "Cancel stranded transition");
            Reset(); Check(dialogue.Play(synthetic), "Cannot restart after cancel"); Ready(); Reset();
        }
        dialogue.eyeShutter.SetPoseImmediately(true); dialogue.visitor.Show(guest); Check(dialogue.Play(synthetic), "Disable test failed to start");
        dialogue.eyeShutter.enabled = false; Step(5); Step(5);
        Check(!dialogue.IsChangingVisitor && dialogue.CurrentLine.speaker == clerk, "Disabled shutter stranded dialogue");
        dialogue.eyeShutter.enabled = true; Reset(); UnityEngine.Object.Destroy(synthetic);
    }
    static void Choices()
    {
        var sequence = dialogue.events.Single(e => e.id == "act1_secretary");
        for (int option = 0; option < 3; option++)
        {
            Reset(); Check(dialogue.Play(sequence), "Cannot play secretary"); Ready();
            Check(dialogue.IsTyping && !dialogue.IsChoosing && dialogue.Overlay.Body.enabled, "NPC choice skipped speech");
            Ungate(); Check(!dialogue.SelectChoice(option), "Option selected before speech");
            int sounds = dialogue.Voice.PlayedCount; Step(.01f);
            Check(dialogue.Voice.PlayedCount > sounds && dialogue.VisibleText.Length > 0, "NPC prompt lost typing/voice");
            if (option == 0) dialogue.Advance(); else Step(100);
            Check(!dialogue.IsTyping && !dialogue.IsChoosing && dialogue.VisibleText == dialogue.CurrentLine.text, "Speech did not finish normally");
            if (option == 0) Capture("secretary-speaking");
            dialogue.Advance(); Check(dialogue.IsChoosing && !dialogue.Overlay.Body.enabled && dialogue.Overlay.Body.text == "", "NPC text remains above choices");
            Check(!dialogue.SelectChoice(option), "Opening click selected choice");
            if (option == 0)
            {
                Capture("secretary-options"); Capture("secretary-options-4x3", 1024, 768); Capture("secretary-options-ultrawide", 2560, 1080);
            }
            gate.SetConsole(true); Ungate(); Check(!dialogue.SelectChoice(option), "Console selected choice"); gate.SetConsole(false);
            Ungate(); Check(dialogue.SelectChoice(option), "Valid choice failed");
            Check(dialogue.LineIndex == option + 1 && dialogue.IsTyping && dialogue.Overlay.Body.enabled, "Wrong branch or body stayed hidden");
            Next(); Door(); Step(); Check(!dialogue.IsActive, "Branch fell through to other responses");
        }
        var luca = dialogue.speakers.Single(s => s.isProtagonist);
        var prompt = ScriptableObject.CreateInstance<DialogueEvent>(); prompt.id = "protagonist_choice"; prompt.arrivalDelay = 0;
        prompt.lines = new[] { new DialogueEvent.Line { speaker = luca, text = "现在要摇铃交卷吗？", choices = new[] {
            new DialogueEvent.Choice { id = "yes", text = "摇铃交卷。" }, new DialogueEvent.Choice { id = "no", text = "再检查一下。" } } } };
        Reset(); Check(dialogue.PlayInPlace(prompt) && dialogue.IsChoosing && !dialogue.IsTyping && dialogue.Overlay.Body.enabled, "Protagonist confirmation changed");
        Capture("protagonist-confirmation"); Ungate(); Check(dialogue.SelectChoice(1) && !dialogue.IsActive, "Protagonist option failed"); UnityEngine.Object.Destroy(prompt);
    }
    static void Capture(string name, int width = 1600, int height = 900)
    {
        var rt = RenderTexture.GetTemporary(width, height, 24); var active = RenderTexture.active;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        var overlay = dialogue.Overlay.OverlayCamera; var oldCamera = camera.targetTexture; var oldOverlay = overlay.targetTexture;
        try
        {
            camera.targetTexture = overlay.targetTexture = rt;
            Call(dialogue.Overlay, "LateUpdate"); Canvas.ForceUpdateCanvases(); camera.Render(); overlay.Render();
            RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply(); File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
            if (dialogue.IsChoosing)
                for (int i = 0; i < dialogue.CurrentLine.choices.Length; i++)
                {
                    var row = (RectTransform)dialogue.Overlay.Panel.Find("Player response " + (i + 1)); var label = row.GetComponentInChildren<Text>();
                    Check(label.font == dialogue.Overlay.font && label.text == dialogue.CurrentLine.choices[i].text, "Choice font/text mismatch");
                    Check(row.rect.height >= 36 && label.cachedTextGenerator.characterCountVisible >= label.text.Length, "Choice clipped at " + width + "x" + height);
                    var pointer = RectTransformUtility.WorldToScreenPoint(overlay, row.TransformPoint(row.rect.center));
                    Check(dialogue.Overlay.ChoiceAtPointer(pointer) == i, "Pointer misses row " + i);
                }
        }
        finally { camera.targetTexture = oldCamera; overlay.targetTexture = oldOverlay; RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(image); }
    }
    static void Finish(bool success, string message)
    {
        if (done) return; done = true; SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/validation.txt", message);
        if (success) Debug.Log(message); else Debug.LogError(message); EditorApplication.Exit(success ? 0 : 1);
    }
}
