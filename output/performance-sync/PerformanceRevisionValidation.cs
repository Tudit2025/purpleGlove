using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PurpleGlove;
using PurpleGlove.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Execute only in the isolated project copy under Temp.
[InitializeOnLoad]
public static class PerformanceRevisionValidation
{
    const string Key = "PerformanceRevisionValidation.Play";
    const string Scene = "Assets/Art/DeskSceneV1/Scenes/PH_DeskRoom.unity";
    static readonly string Output = Path.GetFullPath("../../output/performance-sync");
    static int checks, scenario;
    static bool done, reload;
    static double started;
    static Camera camera;
    static DialogueController dialogue;
    static SceneInputGate gate;
    static DeskScenePreview view;
    static ActArchiveController archive;
    static PlaintextArchiveController records;
    static SealedDeliveryController delivery;
    static PrologueSequence story;
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static void Ungate() => typeof(SceneInputGate).GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gate, -1);
    static void Step(object target, float seconds = 0) { Ungate(); Call(target, "AdvanceTime", seconds); }
    static PerformanceRevisionValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Output); EditorSceneManager.OpenScene(Scene);
            var controller = Camera.main.GetComponent<DialogueController>();
            Check(controller.events.All(e => e && e.TryValidate(out _)), "Invalid or null dialogue event");
            Check(controller.events.Select(e => e.id).Distinct().Count() == controller.events.Length, "Duplicate event");
            Check(!controller.events.Any(e => e.id == "act3_request" || e.id == "act3_ring_hint"), "Removed third-act request is still registered");
            foreach (var e in controller.events)
                foreach (char c in string.Concat(e.lines.Select(l => l.text + string.Concat(l.choices.Select(ch => ch.text)))).Where(c => !char.IsWhiteSpace(c)).Distinct())
                    Check(Camera.main.GetComponent<DialogueOverlay>().font.HasCharacter(c), "Missing dialogue glyph " + c);
            var first = Camera.main.GetComponent<FirstActFlow>();
            var second = Camera.main.GetComponent<SecondActFlow>();
            Check(first.secretaryIntroduction && second.originalRequest, "Saved scene lacks revised flow references");
            Check(first.firstVisit.lines[0].choices.Length == 3 && first.firstVisit.lines.Skip(1).All(l => l.endEvent), "Branch terminal markers missing");
            Check(first.secretaryPauseSeconds == 2, "Wrong first-act pause");
            var book = second.records.entries.Single(e => e.id == "book");
            Check(book.answer == "REQUEST CASE FIFTY SEVEN", "Saved scene still expects case seven");
            var note = second.archive.acts[1].documents[0];
            Check(note.transform.Find("Coordinates").GetComponent<TextMesh>().text == BookCipherText.Cipher, "Paper coordinates differ from source");
            // Reinstall progression without overwriting the revised assets.
            string original = second.deskSummary.lines[0].text;
            second.deskSummary.lines[0].text += " [preserve]"; EditorUtility.SetDirty(second.deskSummary); AssetDatabase.SaveAssets();
            DeskSceneBuilder.InstallSecondActEnding();
            second = Camera.main.GetComponent<SecondActFlow>();
            Check(second.deskSummary.lines[0].text.EndsWith("[preserve]"), "Reinstall overwrote an authored line");
            second.deskSummary.lines[0].text = original; EditorUtility.SetDirty(second.deskSummary); AssetDatabase.SaveAssets();
            SessionState.SetInt(Key + ".Checks", checks); SessionState.SetBool(Key, true); Install(); EditorApplication.isPlaying = true;
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
    static void Resolve()
    {
        camera = Camera.main; dialogue = camera.GetComponent<DialogueController>(); gate = camera.GetComponent<SceneInputGate>();
        view = camera.GetComponent<DeskScenePreview>(); records = camera.GetComponent<PlaintextArchiveController>(); archive = records.archive;
        delivery = camera.GetComponent<SealedDeliveryController>(); story = camera.GetComponent<PrologueSequence>();
        story.Stop(); dialogue.Stop(); camera.GetComponent<DialogueTestConsole>().SetOpen(false);
        camera.GetComponent<FourthActOpening>().enabled = false;
        camera.GetComponent<FourthActFlow>().enabled = false;
        view.followMouse = false; Ungate();
    }
    static void Tick()
    {
        if (done) return;
        if (EditorApplication.timeSinceStartup - started > 200) { Finish(false, "Timed out"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating || Time.frameCount < 5) return;
        try
        {
            if (reload) { reload = false; EditorSceneManager.LoadSceneInPlayMode(Scene, new LoadSceneParameters(LoadSceneMode.Single)); return; }
            Resolve();
            if (scenario == 0) checks += SessionState.GetInt(Key + ".Checks", 0);
            if (scenario < 3) FirstAct(scenario);
            else if (scenario == 3) SecondAct();
            else ThirdAct();
            scenario++;
            if (scenario == 5) Finish(true, "PASS: " + checks + " checks. Revised saved-scene references, dialogue/choice glyphs, installer preservation, all three secretary replies without fallthrough, closed-shutter two-second pause, actual second-act bell and archive request, obsolete third-act request removed, delayed secretary after physical supplement pickup, stop/resume and console pause, chapter transitions, updated book answer and coordinates. Unity 2022.3.62f2 Play Mode.");
            else reload = true;
        }
        catch (Exception error) { Finish(false, "Scenario " + scenario + ": " + error); }
    }
    static void OpenDialogue()
    {
        for (int i = 0; dialogue.IsActive && dialogue.LineIndex < 0 && !dialogue.IsClosing && i < 8; i++)
        { Call(view, "AnimateView", 5f, Vector2.zero); Step(dialogue, 5); Call(dialogue.eyeShutter, "Advance", 5f); Step(dialogue, 0); }
    }
    static void ReadAll()
    {
        if (!dialogue.IsActive) return;
        OpenDialogue();
        for (int i = 0; dialogue.IsActive && !dialogue.IsClosing && i < 100; i++)
        { OpenDialogue(); Check(!dialogue.IsChoosing, "Unexpected choice"); dialogue.Advance(); }
        if (dialogue.IsClosing) { Call(dialogue.eyeShutter, "Advance", 5f); Step(dialogue); }
        Check(!dialogue.IsActive, "Dialogue did not end"); Ungate();
    }
    static void Solve(string answer)
    {
        Step(records, 1); records.workbench.SetPlaintext(answer); Step(records, 1); Step(records, 1); ReadAll();
    }
    static void LandPacket()
    {
        for (int i = 0; i < 6 && delivery.State != SealedDeliveryController.Phase.AwaitingPickup; i++)
        { Call(delivery.transferDrum, "Advance", 5f); Step(delivery, 5); }
        Check(delivery.State == SealedDeliveryController.Phase.AwaitingPickup, "Packet not collectible");
        Ungate(); Check(delivery.CollectPacket(), "Pickup failed"); Step(delivery, 5); Step(delivery, 5);
    }
    static void Ring(DeskHandbell bell, object flow)
    {
        Ungate(); Check(bell.TryInteract(), "Bell cannot be lifted"); Step(bell, 1); Step(bell, 0.15f);
        Check(bell.StrikeCount > 0, "No physical strike"); Step(flow);
        Check(!dialogue.IsActive, "Dialogue starts before bell putdown");
        Step(bell, 2); Step(bell, 1); Step(bell, 1); Step(flow);
    }
    static void FirstAct(int choice)
    {
        camera.GetComponent<SecondActFlow>().enabled = false; camera.GetComponent<ThirdActFlow>().enabled = false;
        var flow = camera.GetComponent<FirstActFlow>();
        Check(archive.ReceiveAct(1), "Cannot receive first act"); Solve("KEEP THE ORIGINAL");
        Check(flow.State == FirstActFlow.Phase.Delay, "Saved plaintext did not arm first-act ending");
        Step(flow, 4.9f); Check(!flow.KnockPlayed, "Early knock"); Step(flow, 0.11f); Step(flow, 2);
        Check(dialogue.ActiveEvent == flow.secretaryIntroduction, "Clerk must introduce secretary first");
        ReadAll(); Check(flow.State == FirstActFlow.Phase.SecretaryPause && !dialogue.eyeShutter.AtSecondStop, "Pause must begin after closed shutter");
        gate.SetConsole(true); Call(flow, "AdvanceTime", 10f); gate.SetConsole(false);
        Step(flow, 1.9f); Check(flow.State == FirstActFlow.Phase.SecretaryPause, "Pause shorter than two seconds");
        flow.Stop(); Check(flow.Resume(), "Cannot resume secretary pause"); Step(flow, 0.11f); Step(flow);
        Check(dialogue.ActiveEvent == flow.firstVisit, "Secretary did not arrive after pause"); OpenDialogue();
        Check(!dialogue.IsChoosing && dialogue.IsTyping, "Secretary must speak before choices");
        dialogue.Advance(); Check(!dialogue.IsChoosing, "Revealing speech must not also open choices");
        dialogue.Advance(); Check(dialogue.IsChoosing, "Finished speech did not lead to choices");
        if (choice == 0) Capture("first-act-three-choices");
        Ungate(); Check(dialogue.SelectChoice(choice), "Choice rejected");
        Check(dialogue.LineIndex == choice + 1 && dialogue.CurrentLine.endEvent, "Wrong branch response");
        string response = dialogue.CurrentLine.text; ReadAll();
        Check(flow.State == FirstActFlow.Phase.ReturnPending, "Reply fell through or did not finish");
        Step(flow); Step(flow, 2); Check(dialogue.ActiveEvent == flow.deskSummary, "Missing desk summary"); ReadAll();
        Step(flow); for (int i = 0; i < 8 && story.IsActive; i++) Step(story, 10);
        Check(archive.CurrentAct == 2 && flow.State == FirstActFlow.Phase.SecondIntroPending, "First-act transition failed");
    }
    static void SecondAct()
    {
        camera.GetComponent<FirstActFlow>().enabled = false; camera.GetComponent<ThirdActFlow>().enabled = false;
        var flow = camera.GetComponent<SecondActFlow>();
        Check(archive.ReceiveAct(1) && archive.CompleteAct(1) && archive.ReceiveAct(2), "Cannot prepare act two");
        Solve("REQUEST CASE SEVEN"); Check(!records.entries.Single(e => e.id == "book").saved, "Old archive answer still accepted");
        Solve("REQUEST CASE FIFTY SEVEN"); Check(records.entries.Single(e => e.id == "book").saved, "Revised book solution rejected");
        Step(flow); Check(dialogue.ActiveEvent == flow.cue, "Missing first request cue"); ReadAll();
        Ring(flow.bell, flow); Check(dialogue.ActiveEvent == flow.request, "Initial archive request missing"); ReadAll();
        Step(flow, 6); Step(flow); Check(dialogue.ActiveEvent == flow.arrival, "Archive arrival missing"); ReadAll(); Step(flow); LandPacket();
        Check(flow.State == SecondActFlow.Phase.WaitingForPigpen, "Second-act supplement did not land");
        var note = archive.acts[1].documents[0];
        Ungate(); camera.GetComponent<SceneObjectInteractor>().Inspect(note); Call(note, "Animate", 3f); Capture("book-cipher-case-57");
        ValidatePaper(note); camera.GetComponent<SceneObjectInteractor>().CancelInteraction();
        Solve("LEFT GLOVE"); Step(flow); Step(flow, 2); Check(dialogue.ActiveEvent == flow.deskSummary, "Pigpen summary missing"); ReadAll();
        Check(flow.State == SecondActFlow.Phase.OriginalBellReady && flow.bell.ringAllowed && archive.CurrentAct == 2, "Summary must wait for second bell");
        Step(flow, 100); Check(archive.CurrentAct == 2, "Second act advanced without player's request");
        flow.Stop(); Check(!flow.bell.ringAllowed && flow.Resume() && flow.bell.ringAllowed, "Original-request permission not restored");
        Ring(flow.bell, flow); Check(dialogue.ActiveEvent == flow.originalRequest, "New second-act archive request missing");
        flow.Stop(); Check(flow.Resume(), "Request cannot resume"); Call(dialogue.eyeShutter, "Advance", 5f); Step(flow); ReadAll();
        Check(flow.State == SecondActFlow.Phase.TransitionPending, "Request must lead to third act");
        Step(flow); for (int i = 0; i < 8 && story.IsActive; i++) Step(story, 10);
        Check(archive.CurrentAct == 3 && flow.State == SecondActFlow.Phase.ThirdIntroPending, "Act three not entered");
        Step(flow); ReadAll(); Step(flow); LandPacket(); Check(flow.State == SecondActFlow.Phase.Complete, "Third-act initial packet missing");
    }
    static void ThirdAct()
    {
        camera.GetComponent<FirstActFlow>().enabled = false; camera.GetComponent<SecondActFlow>().enabled = false;
        var flow = camera.GetComponent<ThirdActFlow>();
        Check(archive.ReceiveAct(1) && archive.CompleteAct(1) && archive.ReceiveAct(2) && archive.CompleteAct(2) && archive.ReceiveAct(3), "Cannot prepare third act");
        Solve(records.entries.Single(e => e.id == "vigenere").answer);
        Call(flow.verification, "PrepareCompletedForTesting"); flow.TranscriptSigned(); Step(flow);
        Check(dialogue.ActiveEvent == flow.charles, "Charles visit missing"); ReadAll(); Step(flow);
        Check(flow.State == ThirdActFlow.Phase.Supplement && !flow.FilesComplete, "Receipt must require pickup");
        Step(flow, 30); Check(!dialogue.IsActive && !flow.FilesComplete, "Secretary bypassed receipt pickup"); LandPacket();
        Check(flow.State == ThirdActFlow.Phase.SecretaryDelay && flow.FilesComplete, "Receipt must enter short pause");
        gate.SetConsole(true); Call(flow, "AdvanceTime", 30f); gate.SetConsole(false);
        Step(flow, 1.9f); Check(flow.State == ThirdActFlow.Phase.SecretaryDelay, "Console advanced delay");
        flow.Stop(); Check(flow.Resume(), "Cannot resume third-act pause"); Step(flow, 0.11f); Step(flow);
        Check(dialogue.ActiveEvent == flow.secretary && !flow.bell.ringAllowed, "Secretary should follow without another bell");
        ReadAll(); Step(flow); Check(dialogue.ActiveEvent == flow.carafa, "Carafa missing after secretary"); ReadAll();
        Step(flow); Step(flow, 2); Check(dialogue.ActiveEvent == flow.summary, "Third-act summary missing"); ReadAll();
        Step(flow); for (int i = 0; i < 8 && story.IsActive; i++) Step(story, 10);
        Check(archive.CurrentAct == 4 && flow.State == ThirdActFlow.Phase.Complete, "Originals must arrive in act four");
    }
    static void ValidatePaper(SceneObject paper)
    {
        var box = paper.GetComponent<BoxCollider>();
        foreach (var ink in paper.GetComponentsInChildren<TextMesh>())
        {
            var bounds = ink.GetComponent<Renderer>().localBounds;
            foreach (float x in new[] { bounds.min.x, bounds.max.x }) foreach (float y in new[] { bounds.min.y, bounds.max.y })
            {
                var p = paper.transform.InverseTransformPoint(ink.transform.TransformPoint(new Vector3(x, y, 0)));
                Check(Mathf.Abs(p.x) < box.size.x / 2 + 0.003f && Mathf.Abs(p.z) < box.size.z / 2 + 0.003f, "Ink outside paper: " + ink.name);
                if (ink.name == "Coordinates") Check(p.z > -0.065f, "Cipher coordinates overlap the redacted name");
            }
        }
    }
    static void Capture(string name)
    {
        var target = RenderTexture.GetTemporary(1600, 900, 24); var active = RenderTexture.active;
        var picture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        var previous = camera.targetTexture; var overlay = dialogue.Overlay.OverlayCamera; var previousOverlay = overlay.targetTexture;
        try
        {
            camera.targetTexture = target; camera.Render();
            if (overlay.enabled) { overlay.targetTexture = target; Canvas.ForceUpdateCanvases(); overlay.Render(); }
            RenderTexture.active = target; picture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); picture.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", picture.EncodeToPNG());
        }
        finally { camera.targetTexture = previous; overlay.targetTexture = previousOverlay; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(picture); }
    }
    static void Finish(bool success, string message)
    {
        if (done) return; done = true; SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/flow-validation.txt", message);
        if (success) Debug.Log(message); else Debug.LogError(message); EditorApplication.Exit(success ? 0 : 1);
    }
}
