using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PurpleGlove;
using PurpleGlove.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run in an isolated project copy with -executeMethod FirstActOpeningValidation.Run.
[InitializeOnLoad]
public static class FirstActOpeningValidation
{
    const string Key = "FirstActOpeningValidation.Play";
    static readonly string Output = Path.GetFullPath("../../output/first-act-opening-validation");
    static int checks, visits, deliveries;
    static bool done;
    static double started;
    static FirstActFlow flow;
    static DialogueController dialogue;
    static SceneInputGate gate;
    static DeskScenePreview view;
    static PrologueSequence story;
    static SealedDeliveryController delivery;
    static ActArchiveController archive;
    static FirstActOpeningValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static object Call(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    static void Ungate() => typeof(SceneInputGate).GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gate, -1);
    static void Step(float seconds = 0) { Ungate(); Call(flow, "AdvanceTime", seconds); }

    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            var installed = Camera.main.GetComponent<FirstActFlow>();
            Check(installed && installed.openingVisit && installed.openingDelay == 5, "Saved scene lacks opening configuration");
            var opening = installed.openingVisit;
            Check(opening.TryValidate(out _) && opening.closeShutterOnComplete && opening.arrivalSound, "Invalid opening asset");
            Check(opening.initialVisitor.id == "clerk" && opening.lines.All(l => l.speaker.id == "clerk"), "Opening must use the guild steward");
            Check(opening.lines.Any(l => l.text.Contains("公爵") && l.text.Contains("核验委托"))
                && opening.lines.Any(l => l.text.Contains("行会交付")), "Missing commission/file introduction");
            var lineText = opening.lines.Select(l => l.text).ToArray();
            DeskFirstActFlowBuilder.Install(); DeskFirstActFlowBuilder.Install();
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            installed = Camera.main.GetComponent<FirstActFlow>();
            Check(installed.openingVisit == opening && lineText.SequenceEqual(opening.lines.Select(l => l.text)), "Installer changed authored dialogue");
            Check(installed.dialogue.events.Count(e => e == opening) == 1, "Missing/duplicate event in console");
            SessionState.SetInt(Key + ".Checks", checks); SessionState.SetBool(Key, true);
            Install(); EditorApplication.isPlaying = true;
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
    static void OpenDialogue()
    {
        if (view.IsEnteringDoorway) Call(view, "AnimateView", 2f, Vector2.zero);
        Call(dialogue, "AdvanceTime", 2f);
        if (dialogue.eyeShutter.IsMoving) Call(dialogue.eyeShutter, "Advance", 2f);
        Call(dialogue, "AdvanceTime", 2f);
        Check(dialogue.LineIndex == 0, "First line did not wait for camera and shutter");
    }
    static void ReadLines()
    {
        for (int n = 0; dialogue.IsActive && !dialogue.IsClosing && n < 100; n++) dialogue.Advance();
        Ungate();
    }
    static void Tick()
    {
        if (done) return;
        if (EditorApplication.timeSinceStartup - started > 120) { Finish(false, "Timeout"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.frameCount < 5) return;
        try
        {
            checks = SessionState.GetInt(Key + ".Checks", 0);
            var camera = Camera.main;
            flow = camera.GetComponent<FirstActFlow>(); dialogue = flow.dialogue; story = flow.story;
            delivery = flow.delivery; archive = flow.archive;
            gate = camera.GetComponent<SceneInputGate>(); view = camera.GetComponent<DeskScenePreview>();
            dialogue.eventCompleted.AddListener(id => { if (id == flow.openingVisit.id) visits++; });
            delivery.deliveryCompleted.AddListener(act => { if (act == 1) deliveries++; });

            Step(20); Check(flow.State == FirstActFlow.Phase.Idle && !delivery.IsActive, "Opening started before act entry");
            Check(story.PlayPrologue(), "Could not start actual prologue");
            Call(dialogue, "AdvanceTime", 2f); ReadLines();
            Check(story.State == PrologueSequence.Phase.OpeningDoor && flow.State == FirstActFlow.Phase.Idle, "Opening began during prologue");
            Call(story, "AdvanceTime", 3f);
            for (int i = 0; i < 100 && story.IsActive; i++)
            {
                Step(0.1f);
                Check(flow.State == FirstActFlow.Phase.Idle, "Five-second delay counted during transition");
                Call(story, "AdvanceTime", 0.1f);
            }
            Check(!story.IsActive && story.PresentedAct == 1 && flow.State == FirstActFlow.Phase.OpeningDelay, "Fade-in did not arm opening");
            Check(flow.DelayElapsed == 0 && !archive.IsAvailable(1), "Opening revealed documents early");
            Step(2); gate.SetConsole(true); Step(20);
            Check(flow.DelayElapsed == 2 && !flow.KnockPlayed, "Console failed to pause opening delay");
            gate.SetConsole(false); flow.Stop(); Step(20);
            Check(flow.State == FirstActFlow.Phase.Suspended && flow.DelayElapsed == 2, "Stop failed to suspend delay");
            Check(flow.Resume(), "Could not resume opening delay");
            story.actEntered.Invoke(1);
            Step(2.99f); Check(!flow.KnockPlayed && !dialogue.IsActive, "Knock fired before five seconds");
            Step(0.02f);
            Check(flow.State == FirstActFlow.Phase.OpeningVisitPending && flow.KnockPlayed
                && flow.knockAudio.clip == flow.openingVisit.arrivalSound && !dialogue.IsActive, "Knock must precede dialogue");
            float knockLength = flow.openingVisit.arrivalSound.length;
            Step(knockLength * 0.5f); Check(!dialogue.IsActive, "Dialogue cut off knock");
            flow.Stop(); Check(flow.Resume(), "Could not resume interrupted knock");
            Step(knockLength * 0.5f); Check(!dialogue.IsActive, "Interrupted knock was not replayed in full");
            gate.SetConsole(true); Step(20); Check(!dialogue.IsActive, "Console failed to pause knock wait");
            gate.SetConsole(false);
            Check(dialogue.PlayInPlace(flow.deskSummary), "Could not simulate another active line");
            Step(knockLength);
            Check(flow.State == FirstActFlow.Phase.OpeningVisitPending && dialogue.ActiveEvent == flow.deskSummary, "Opening interrupted existing dialogue");
            Call(dialogue, "AdvanceTime", 2f); ReadLines();
            // The player may have opened the shutter during the five-second free interval.
            dialogue.eyeShutter.SetPoseImmediately(true);
            Step();
            Check(flow.State == FirstActFlow.Phase.OpeningVisit && dialogue.ActiveEvent == flow.openingVisit, "Steward visit missing");
            Check(!dialogue.arrivalAudio.isPlaying && !delivery.IsActive, "Duplicate knock or premature delivery");
            OpenDialogue();
            flow.Stop(); Check(flow.State == FirstActFlow.Phase.Suspended && !gate.DialogueActive, "Visit cancellation leaked lock");
            Check(flow.Resume(), "Could not resume steward dialogue"); Step(); OpenDialogue();
            ReadLines();
            Check(dialogue.IsClosing && dialogue.eyeShutter.IsMoving && view.DialogueLocked && visits == 0, "Visit finished before shutter closed");
            Step(10); Check(!delivery.IsActive && !delivery.packet.gameObject.activeSelf, "Packet entered during shutter closure");
            Call(dialogue.eyeShutter, "Advance", dialogue.eyeShutter.travelSeconds * 0.5f);
            Call(dialogue, "AdvanceTime", 0f); Step();
            Check(dialogue.IsClosing && !delivery.IsActive, "Delivery started halfway through closure");
            Call(dialogue.eyeShutter, "Advance", 2f); Call(dialogue, "AdvanceTime", 0f);
            Check(!dialogue.IsActive && !dialogue.eyeShutter.AtSecondStop && visits == 1
                && flow.State == FirstActFlow.Phase.OpeningDeliveryPending, "Closure did not queue delivery");
            Step(); Check(flow.State == FirstActFlow.Phase.OpeningDelivery && delivery.ActiveAct == 1, "Existing first-act delivery not started");
            flow.Stop(); Check(!archive.IsAvailable(1) && !gate.DeliveryActive && !view.DeliveryLocked, "Cancelled delivery unlocked papers or leaked input");
            Check(flow.Resume(), "Could not resume first delivery"); Step();
            for (int i = 0; i < 5 && delivery.State != SealedDeliveryController.Phase.AwaitingPickup; i++)
            { Call(delivery.transferDrum, "Advance", 2f); Call(delivery, "AdvanceTime", 2f); }
            Check(delivery.State == SealedDeliveryController.Phase.AwaitingPickup && delivery.firstActPacketsVisual.activeSelf
                && !archive.IsAvailable(1), "First delivery must wait for player pickup");
            Ungate(); Check(delivery.CollectPacket(), "First packet pickup rejected");
            Call(delivery, "AdvanceTime", 2f); Check(!archive.IsAvailable(1), "Papers unlocked before landing");
            Call(delivery, "AdvanceTime", 2f);
            Check(flow.State == FirstActFlow.Phase.Idle && deliveries == 1 && archive.IsAvailable(1)
                && archive.DeskAct == 1 && archive.acts[0].documents.Length == 2, "Delivery failed to reveal original first-act papers");
            story.actEntered.Invoke(1); flow.enabled = false; flow.enabled = true; Step(20);
            Check(flow.State == FirstActFlow.Phase.Idle && visits == 1 && deliveries == 1, "Opening replayed on repeated entry/enable");
            flow.records.plaintextPreserved.Invoke(1);
            Check(flow.State == FirstActFlow.Phase.Delay && flow.DelayElapsed == 0 && !flow.KnockPlayed, "Opening prevented earned ending");
            flow.Stop();
            Finish(true, "PASS: " + checks + " assertions. Unity 2022.3.62f2 Play Mode.\n"
                + "Actual prologue and fade-in; five-second wait; complete knock before dialogue; busy-line and console waits; authored steward references.\n"
                + "Pre-opened shutter closes fully before delivery; original two-packet pickup and landing unlock; no duplicate visit/delivery; ending still arms.\n"
                + "Stop/continue during delay, knock, visit and delivery; additive/idempotent installation preserves dialogue.\n");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
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
