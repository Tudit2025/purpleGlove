using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PurpleGlove;
using PurpleGlove.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class HandbellFlowValidation
{
    const string Key = "HandbellFlowValidation.Play";
    static readonly string Output = Path.GetFullPath("../../output/handbell-validation/first-act-regression");
    static int checks, preserved, completed, delivered;
    static bool done;
    static double started;
    static Camera camera;
    static FirstActFlow flow;
    static DialogueController dialogue;
    static SceneInputGate gate;
    static DeskScenePreview view;
    static ActArchiveController archive;
    static PlaintextArchiveController records;
    static DialogueTestConsole console;
    static HandbellFlowValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static object Call(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static void Ungate() => typeof(SceneInputGate).GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gate, -1);
    static void Step(float seconds = 0) => Call(flow, "AdvanceTime", seconds);
    static void RecordStep() { Ungate(); Call(records, "AdvanceTime", 1f); }

    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            var before = Camera.main.GetComponent<PlaintextArchiveController>();
            var ids = before.entries.Select(e => GlobalObjectId.GetGlobalObjectIdSlow(e.document).ToString()).ToArray();
            DeskFirstActFlowBuilder.Install(); DeskFirstActFlowBuilder.Install();
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            var installed = Camera.main.GetComponent<FirstActFlow>();
            Check(installed && installed.records && installed.archive && installed.dialogue && installed.story && installed.delivery && installed.knockAudio, "Missing saved flow references");
            Check(UnityEngine.Object.FindObjectsOfType<FirstActFlow>(true).Length == 1, "Duplicate flow installer");
            Check(ids.SequenceEqual(installed.records.entries.Select(e => GlobalObjectId.GetGlobalObjectIdSlow(e.document).ToString())), "Installer replaced plaintext papers");
            Check(installed.knockDelay == 5, "Expected five-second delay");
            Check(installed.records.entries.All(e => !e.document.transform.Find("Record title") && !e.document.transform.Find("Record act")), "Removed supplementary labels reappeared");
            Check(installed.firstVisit.arrivalSound && installed.firstVisit.initialVisitor && !installed.deskSummary.arrivalSound, "Knock or speaker configuration invalid");
            Check(installed.deskSummary.lines.All(l => l.speaker.isProtagonist), "Summary is not Luca's monologue");
            foreach (var sequence in new[] { installed.firstVisit, installed.deskSummary, installed.secondActIntro })
            {
                Check(sequence.TryValidate(out _), "Invalid sequence " + sequence.name);
                Check(installed.dialogue.events.Count(e => e == sequence) == 1, "Duplicate/missing console event");
                foreach (char c in string.Concat(sequence.lines.Select(l => l.text)).Where(c => !char.IsWhiteSpace(c)).Distinct())
                    Check(Camera.main.GetComponent<DialogueOverlay>().font.HasCharacter(c), "Missing glyph " + c);
            }
            SessionState.SetInt(Key + ".Checks", checks); SessionState.SetBool(Key, true); Install(); EditorApplication.isPlaying = true;
        }
        catch (Exception e) { Finish(false, e.ToString()); }
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
        for (int n = 0; dialogue.IsActive && n < 100; n++)
        {
            Call(view, "AnimateView", 2f, Vector2.zero);
            Call(dialogue, "AdvanceTime", 2f);
            if (dialogue.eyeShutter.IsMoving) Call(dialogue.eyeShutter, "Advance", 2f);
            Call(dialogue, "AdvanceTime", 2f);
            dialogue.Advance();
        }
        Check(!dialogue.IsActive, "Dialogue did not finish"); Ungate();
    }
    static void ShowFullLine()
    {
        Call(view, "AnimateView", 2f, Vector2.zero);
        Call(dialogue, "AdvanceTime", 2f);
        if (dialogue.eyeShutter.IsMoving) Call(dialogue.eyeShutter, "Advance", 2f);
        Call(dialogue, "AdvanceTime", 2f); if (dialogue.IsTyping) dialogue.Advance();
    }
    static void Tick()
    {
        if (done) return;
        if (EditorApplication.timeSinceStartup - started > 120) { Finish(false, "Timeout"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.frameCount < 5) return;
        try
        {
            checks = SessionState.GetInt(Key + ".Checks", 0);
            camera = Camera.main; flow = camera.GetComponent<FirstActFlow>(); records = flow.records;
            archive = flow.archive; dialogue = flow.dialogue; view = camera.GetComponent<DeskScenePreview>();
            gate = camera.GetComponent<SceneInputGate>(); console = camera.GetComponent<DialogueTestConsole>();
            var work = records.workbench; var pile = UnityEngine.Object.FindFirstObjectByType<ActCandlePile>();
            view.followMouse = false; console.SetOpen(false); Ungate();
            records.plaintextPreserved.AddListener(_ => preserved++); archive.actCompleted.AddListener(_ => completed++);
            flow.delivery.deliveryCompleted.AddListener(_ => delivered++);
            Step(20); Check(flow.State == FirstActFlow.Phase.Idle && !flow.KnockPlayed, "Flow started before earned plaintext");
            Check(archive.ReceiveAct(1), "Cannot receive first act"); RecordStep();
            work.SetPlaintext("KEEP THE ORIGINA"); RecordStep(); RecordStep();
            Check(flow.State == FirstActFlow.Phase.Idle && preserved == 0, "Wrong answer started ending");
            work.SetPlaintext(""); RecordStep();
            Check(work.SelectTool(CipherWorkbench.Tool.Caesar), "Cannot select Caesar");
            Call(work, "AnimateToolTransfer", 1f); Call(work, "AnimateToolTransfer", 1f); Ungate();
            view.SetView(DeskScenePreview.View.DecodeFocus); Call(view, "AnimateView", 10f, Vector2.zero); work.SetCaesarShift(3);
            foreach (char c in "NHHS WKH RULJLQDO")
                Check((bool)Call(work, "HandleDecodeKey", new Event { type = EventType.KeyDown, character = c, keyCode = c == ' ' ? KeyCode.Space : (KeyCode)char.ToLowerInvariant(c) }), "Decode key rejected");
            RecordStep(); RecordStep();
            Check(preserved == 0 && flow.State == FirstActFlow.Phase.Idle && work.HasPendingPlaintextStamps, "Flow began before stamps settled");
            Call(work, "AnimateLetterStamps", 0.16f); RecordStep();
            Check(preserved == 0, "Flow began during tile return");
            Call(work, "AnimateLetterStamps", 0.3f); RecordStep();
            Check(preserved == 1 && records.entries[0].saved && flow.State == FirstActFlow.Phase.Delay, "Preservation did not arm ending once");
            Check(dialogue.ActiveEvent == records.completionLine, "Completion announcement missing");
            ShowFullLine(); Capture("01-plaintext-preserved");
            Step(4.9f); Check(!flow.KnockPlayed && dialogue.ActiveEvent == records.completionLine, "Knock fired early");
            console.SetOpen(true); Step(20); Check(flow.DelayElapsed < 5 && !flow.KnockPlayed, "Console did not pause countdown");
            console.SetOpen(false); Ungate(); Step(0.11f);
            Check(flow.KnockPlayed && flow.knockAudio.clip == flow.firstVisit.arrivalSound && flow.State == FirstActFlow.Phase.VisitPending, "No knock after five seconds");
            Step(2); Check(dialogue.ActiveEvent == records.completionLine, "Knock interrupted unread completion line");
            ReadAll();
            var bell = UnityEngine.Object.FindFirstObjectByType<DeskHandbell>();
            Check(bell.strikes.All(s => s && s.channels == 1 && s.frequency == 48000 && s.length > .8f), "Final audio variants not imported");
            Check(bell && bell.TryInteract(), "Cannot pick up bell before pending visit");
            Ungate(); Step(1);
            Check(flow.State == FirstActFlow.Phase.VisitPending && !dialogue.IsActive, "Visit interrupts bell pickup");
            Call(bell, "AdvanceTime", .6f);
            Check(dialogue.ActiveEvent == bell.notNeededLine, "Bell refusal is missing");
            Step(1); Check(flow.State == FirstActFlow.Phase.VisitPending, "Visit interrupts bell monologue");
            ReadAll(); Call(bell, "AdvanceTime", .02f); Ungate(); Step(1);
            Check(flow.State == FirstActFlow.Phase.VisitPending && bell.IsBusy, "Visit interrupts bell putdown");
            Call(bell, "AdvanceTime", 1f); Ungate(); Step(1);
            Check(flow.State == FirstActFlow.Phase.Visit && dialogue.ActiveEvent == flow.firstVisit && view.DialogueLocked, "Secretary visit not started at doorway");
            ShowFullLine(); Capture("02-secretary-at-door");
            // Stop/resume must not archive, consume another reserve or replay the saved paper.
            console.Execute("stop"); Ungate();
            Check(flow.State == FirstActFlow.Phase.Suspended && !gate.StoryActive && !view.DialogueLocked && archive.CurrentAct == 1, "Visit cancellation leaked state");
            if (dialogue.eyeShutter.IsMoving) Call(dialogue.eyeShutter, "Advance", 2f);
            Check(console.Execute("continue").Contains("继续"), "Cannot resume visit"); Ungate(); Step(1); ReadAll();
            Check(flow.State == FirstActFlow.Phase.ReturnPending, "Visit did not queue desktop return: " + flow.State);
            Step(); Check(flow.State == FirstActFlow.Phase.Returning && view.StoryLocked && gate.StoryActive, "Desktop return not locked");
            Step(flow.returnSeconds * 0.5f); Check(!dialogue.IsActive, "Summary began before return finished");
            Step(flow.returnSeconds);
            Check(flow.State == FirstActFlow.Phase.Summary && dialogue.ActiveEvent == flow.deskSummary && view.CurrentView == DeskScenePreview.View.Desktop, "Summary not at desktop");
            Check(Mathf.Abs(camera.transform.position.y - 1.5f) < 0.01f && !view.CanNavigate, "Summary uses zoom view or permits navigation");
            ShowFullLine(); Capture("03-luca-desktop-summary");
            console.Execute("stop"); Ungate();
            Check(!view.StoryLocked && !gate.StoryActive && flow.State == FirstActFlow.Phase.Suspended, "Summary cancellation leaked camera lock");
            console.Execute("continue"); Ungate(); Step(); Step(2); ReadAll();
            Check(flow.State == FirstActFlow.Phase.TransitionPending && archive.CurrentAct == 1, "Summary advanced before transition");
            Step(); Check(flow.State == FirstActFlow.Phase.Transition && flow.story.IsActive, "Second act transition missing");
            Call(flow.story, "AdvanceTime", 10f);
            Check(archive.DeskAct == 0 && archive.CurrentAct == 1 && pile.RemainingCount == 3, "Black-phase staging incorrect");
            console.Execute("stop"); Ungate();
            Check(archive.DeskAct == 1 && !archive.IsCompleted(1) && pile.RemainingCount == 4, "Cancelled black phase committed progress");
            console.Execute("continue"); Ungate(); Step();
            for (int n = 0; n < 3; n++) Call(flow.story, "AdvanceTime", 10f);
            Capture("04-second-act-title");
            for (int n = 0; n < 3; n++) Call(flow.story, "AdvanceTime", 10f);
            Ungate();
            Check(flow.State == FirstActFlow.Phase.SecondIntroPending && archive.CurrentAct == 2 && archive.IsCompleted(1) && completed == 1, "First act did not commit exactly once");
            Check(!records.entries[0].document.gameObject.activeSelf && !archive.IsAvailable(2) && pile.RemainingCount == 3, "Transition bypassed delivery or lost archive state");
            Step(); Check(dialogue.ActiveEvent == flow.secondActIntro, "Missing second act introduction");
            ShowFullLine(); Capture("05-second-act-introduction"); ReadAll(); Step();
            Check(flow.State == FirstActFlow.Phase.Delivery && flow.delivery.ActiveAct == 2 && !archive.IsAvailable(2), "Second packet not started or unlocked early");
            Call(view, "AnimateView", 2f, Vector2.zero);
            Call(flow.delivery.transferDrum, "Advance", 2f); Call(flow.delivery, "AdvanceTime", 2f);
            Check(flow.delivery.State == SealedDeliveryController.Phase.AwaitingPickup, "Packet not collectible");
            Capture("06-second-act-packet");
            console.Execute("stop"); Ungate();
            Check(!archive.IsAvailable(2) && !gate.DeliveryActive && !view.DeliveryLocked && archive.CurrentAct == 2, "Cancelled packet unlocked act or leaked input");
            console.Execute("continue"); Ungate(); Step();
            Call(view, "AnimateView", 2f, Vector2.zero);
            for (int n = 0; n < 3; n++) { Call(flow.delivery.transferDrum, "Advance", 2f); Call(flow.delivery, "AdvanceTime", 2f); }
            Ungate(); Check(flow.delivery.CollectPacket(), "Cannot collect second packet");
            Call(flow.delivery, "AdvanceTime", 2f); Call(flow.delivery, "AdvanceTime", 1f); Ungate();
            Check(flow.State == FirstActFlow.Phase.Complete && delivered == 1 && archive.DeskAct == 2 && archive.IsAvailable(2) && archive.acts[1].documents.Length == 7, "Second act failed to reach playable desk");
            Call(view, "AnimateView", 10f, Vector2.zero); Capture("07-second-act-desktop");
            Check(!gate.StoryActive && !gate.DeliveryActive && !gate.DialogueActive && view.CanNavigate, "Final input lock leaked");
            int original = records.entries[0].document.GetInstanceID();
            Check(archive.ShowActOnDesk(1), "Cannot revisit first act"); RecordStep();
            work.SetPlaintext("KEEP THE ORIGINAL"); RecordStep(); RecordStep(); Step(20);
            Check(preserved == 1 && completed == 1 && delivered == 1 && flow.State == FirstActFlow.Phase.Complete, "Revisit retriggered ending");
            Check(records.entries[0].document.GetInstanceID() == original && records.entries[0].plaintextInk.text == "KEEP THE ORIGINAL" && pile.RemainingCount == 3, "Archive lost plaintext identity or replenished candles");
            flow.enabled = false; flow.enabled = true; Step(20); Check(flow.State == FirstActFlow.Phase.Complete, "Re-enable replayed completed ending");
            Finish(true, "PASS: " + checks + " assertions. Unity 2022.3.62f2 / Built-in / Direct3D 11.\n"
                + "Final handbell audio imports; pending visit waits through bell pickup, refusal and putdown before opening the door view.\n"
                + "Idempotent saved-scene installer and Chinese glyphs; actual Caesar keyboard decoding and completed tile stamps; five-second knock, console pause, unread-line wait.\n"
                + "Secretary at door; animated return to desktop before Luca summary; act-two title; exactly-once archive completion; three remaining reserves; existing Beatrice introduction and click-to-collect sealed packet.\n"
                + "Stop/continue during visit, summary, opaque transition and delivery; rollback before chapter commit; preserved original papers; old-act revisit and disable/re-enable do not replay.\n");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Capture(string name)
    {
        var target = RenderTexture.GetTemporary(1600, 900, 24); var active = RenderTexture.active;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        var previous = camera.targetTexture; var overlay = dialogue.Overlay.OverlayCamera; var previousOverlay = overlay.targetTexture;
        var canvas = (Canvas)Field(camera.GetComponent<ActTransitionOverlay>(), "canvas"); Camera titleCamera = null;
        try
        {
            camera.targetTexture = target; camera.Render();
            if (overlay.enabled) { overlay.targetTexture = target; Canvas.ForceUpdateCanvases(); overlay.Render(); }
            if (canvas.enabled)
            {
                titleCamera = new GameObject("Validation title camera").AddComponent<Camera>();
                titleCamera.cullingMask = 1 << 5; titleCamera.clearFlags = CameraClearFlags.Depth;
                titleCamera.nearClipPlane = 0.01f; titleCamera.farClipPlane = 1; titleCamera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = titleCamera; canvas.planeDistance = 0.1f;
                Canvas.ForceUpdateCanvases(); titleCamera.Render();
            }
            RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            if (titleCamera) UnityEngine.Object.DestroyImmediate(titleCamera.gameObject);
            camera.targetTexture = previous; overlay.targetTexture = previousOverlay; RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(image);
        }
    }
    static void Finish(bool success, string message)
    {
        if (done) return; done = true; SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/validation.txt", message);
        if (success) Debug.Log(message); else Debug.LogError(message); EditorApplication.Exit(success ? 0 : 1);
    }
}
