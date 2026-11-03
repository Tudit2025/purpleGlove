using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PurpleGlove;
using PurpleGlove.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run in Library/ThirdActValidationProject after syncing the project's Assets and ProjectSettings.
[InitializeOnLoad]
public static class SignatureHintValidation
{
    const string Key = "SignatureHintValidation.Play";
    static readonly string Output = Path.GetFullPath("../../output/signature-hint-validation");
    static bool done;
    static double started;
    static int checks;
    static Camera camera;
    static ThirdActFlow flow;
    static DialogueController dialogue;
    static SceneInputGate gate;
    static SceneObjectInteractor interactor;
    static DeskScenePreview view;
    static SignatureHintValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static void Ungate() => typeof(SceneInputGate).GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gate, -1);
    static void Step(object target, float seconds = 0) { Ungate(); Call(target, "AdvanceTime", seconds); }
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene("Assets/Art/DeskSceneV1/Scenes/PH_DeskRoom.unity");
            var installed = Camera.main.GetComponent<ThirdActFlow>();
            Check(installed.signatureHint && installed.signatureHint.TryValidate(out _), "Saved scene lacks a valid signature hint");
            Check(installed.dialogue.events.Count(e => e == installed.signatureHint) == 1, "Hint must be registered once");
            Check(installed.signatureHint.lines.All(l => l.speaker.isProtagonist), "Hint must be Luca's monologue");
            foreach (char c in string.Concat(installed.signatureHint.lines.Select(l => l.text)).Distinct())
                Check(Camera.main.GetComponent<DialogueOverlay>().font.HasCharacter(c), "Missing glyph " + c);
            var hint = installed.signatureHint;
            string text = hint.lines[0].text;
            hint.lines[0].text += " [preserve]";
            typeof(DeskSceneBuilder).GetMethod("ConfigureThirdAct", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { Camera.main });
            Check(installed.signatureHint == hint && hint.lines[0].text.EndsWith("[preserve]"), "Reinstall overwrote the hint");
            Check(installed.dialogue.events.Count(e => e == hint) == 1, "Reinstall duplicated hint");
            hint.lines[0].text = text;
            SessionState.SetInt(Key + ".Checks", checks); SessionState.SetBool(Key, true);
            Install(); EditorApplication.isPlaying = true;
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
    static void ReadAll()
    {
        for (int i = 0; dialogue.IsActive && i < 100; i++)
        {
            Call(view, "AnimateView", 5f, Vector2.zero);
            Step(dialogue, 5); Call(dialogue.eyeShutter, "Advance", 5f); Step(dialogue, 0);
            if (dialogue.IsActive && !dialogue.IsClosing && dialogue.LineIndex >= 0) dialogue.Advance();
        }
        Check(!dialogue.IsActive, "Dialogue did not end"); Ungate();
    }
    static void Inspect(SceneObject paper)
    {
        Ungate(); interactor.Inspect(paper);
        var form = paper.GetComponent<DeskOperationalDocument>();
        if (form) Call(form, "AdvanceTime", 2f);
        Call(paper, "Animate", 3f);
        Check(interactor.InspectedObject == paper, "Inspection rejected");
    }
    static void Tick()
    {
        if (done) return;
        if (EditorApplication.timeSinceStartup - started > 120) { Finish(false, "Timeout"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating || Time.frameCount < 5) return;
        try
        {
            checks = SessionState.GetInt(Key + ".Checks", 0);
            camera = Camera.main; flow = camera.GetComponent<ThirdActFlow>(); dialogue = flow.dialogue;
            gate = camera.GetComponent<SceneInputGate>(); interactor = camera.GetComponent<SceneObjectInteractor>(); view = camera.GetComponent<DeskScenePreview>();
            flow.story.Stop(); dialogue.Stop(); camera.GetComponent<DialogueTestConsole>().SetOpen(false);
            camera.GetComponent<FirstActFlow>().enabled = false; camera.GetComponent<SecondActFlow>().enabled = false;
            var archive = flow.archive; var records = flow.records; var verification = flow.verification;
            Step(flow); Check(!dialogue.IsActive && !verification.CanSign, "Hint/signature unlocked before act three");
            Check(archive.ReceiveAct(1) && archive.CompleteAct(1) && archive.ReceiveAct(2) && archive.CompleteAct(2) && archive.ReceiveAct(3), "Cannot prepare act three");
            string answer = records.entries.Single(e => e.id == "vigenere").answer;
            Step(records, 1); records.workbench.SetPlaintext(answer.Substring(0, answer.Length / 2)); Step(records, 1); Step(records, 1); Step(flow);
            Check(!dialogue.IsActive && !verification.CanSign, "Partial plaintext triggered hint or signature");
            records.workbench.SetPlaintext(answer); Step(records, 1); Step(records, 1);
            Check(records.entries.Single(e => e.id == "vigenere").saved, "Plaintext not preserved");
            Check(dialogue.ActiveEvent == records.completionLine, "Common completion line must play first");
            Step(flow); Check(dialogue.ActiveEvent == records.completionLine && !verification.CanSign, "Hint interrupted completion");
            ReadAll();
            gate.SetConsole(true); Call(flow, "AdvanceTime", 10f);
            Check(!dialogue.IsActive && !verification.CanSign, "Console did not pause hint"); gate.SetConsole(false);
            Inspect(verification.GetComponent<SceneObject>()); Step(flow);
            Check(!dialogue.IsActive && !verification.TrySign(), "Hint interrupts inspection or signing bypasses hint");
            interactor.CancelInteraction();
            var position = camera.transform.position; var rotation = camera.transform.rotation;
            Step(flow); Check(dialogue.ActiveEvent == flow.signatureHint && flow.State == ThirdActFlow.Phase.SignatureHint, "Missing signature hint");
            Check(camera.transform.position == position && camera.transform.rotation == rotation, "Hint changed camera");
            Check(!verification.CanSign && !verification.TrySign(), "Can sign before hint ends");
            dialogue.Stop(); Check(flow.State == ThirdActFlow.Phase.Suspended && !gate.DialogueActive, "Cancelled hint leaked dialogue lock");
            Check(flow.Resume(), "Hint cannot resume"); Step(flow);
            Check(dialogue.ActiveEvent == flow.signatureHint, "Cancelled hint did not replay");
            flow.Stop(); Check(flow.Resume(), "Explicit stop/resume failed"); Step(flow); ReadAll();
            Check(flow.State == ThirdActFlow.Phase.WaitingForSignature && verification.CanSign && !verification.Signed, "Hint did not unlock manual signing");
            records.plaintextPreserved.Invoke(3); Step(records, 1); Step(flow);
            Check(!dialogue.IsActive, "Hint repeated after completion");
            Check(archive.ShowActOnDesk(1), "Cannot recall old dossier"); Step(flow);
            Check(!dialogue.IsActive && verification.CanSign, "Recall repeated hint or blocked signature");
            Inspect(verification.GetComponent<SceneObject>()); Ungate();
            Check(verification.TrySign() && verification.signatureInk.text == "Luca", "Physical signature failed");
            Check(!verification.TrySign(), "Duplicate signature accepted");
            Step(flow); Check(!dialogue.IsActive, "Charles started before form returned");
            interactor.CancelInteraction(); Step(flow);
            Check(dialogue.ActiveEvent == flow.charles && flow.State == ThirdActFlow.Phase.Charles, "Signature failed to trigger Charles");
            ReadAll(); Check(flow.State == ThirdActFlow.Phase.SupplementPending && !flow.FilesComplete, "Charles no longer leads to receipt pickup");
            Finish(true, "PASS: " + checks + " checks. Unity 2022.3.62f2 Play Mode.\nSaved scene and builder wiring; Luca glyphs; unsolved/partial plaintext; common completion before hint; console/inspection gates; camera preserved; cancellation and resume; hint unlocks manual signature; no repeat after recall; signing while reviewing old dossier; Charles waits for form return and leads to supplement pickup.\n");
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    static void Finish(bool success, string message)
    {
        if (done) return; done = true; SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/validation.txt", message);
        if (success) Debug.Log(message); else Debug.LogError(message);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
