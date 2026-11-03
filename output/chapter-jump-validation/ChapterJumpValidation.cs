using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PurpleGlove;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Copy into an isolated project's Assets/Validation/Editor; run ChapterJumpValidation.Run.
[InitializeOnLoad]
public static class ChapterJumpValidation
{
    const string Key = "ChapterJumpValidation.Run";
    const string ScenePath = "Assets/Art/DeskSceneV1/Scenes/PH_DeskRoom.unity";
    static readonly string Output = Path.GetFullPath("../../output/chapter-jump-validation");
    static readonly int[] Targets = { 0, 1, 2, 3, 3, 4, 5, 5, 6, 6, 5, 6, 6, 1 };
    static readonly int[] Presets = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 2, 3, 0 };
    static int scenario, checks, oldCameraId, phase, frames;
    static bool done;
    static double started;
    static Camera camera;
    static DialogueTestConsole console;
    static SceneInputGate gate;
    static DeskScenePreview view;
    static SceneObjectInteractor interactor;
    static ActArchiveController archive;
    static PlaintextArchiveController records;
    static DialogueController dialogue;
    static SealedDeliveryController delivery;
    static PrologueSequence story;
    static FifthActFlow fifth;
    static EpilogueFlow epilogue;
    static object Call(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static void Ungate() => typeof(SceneInputGate).GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gate, -1);
    static void Step(object target, float seconds = 0) { Ungate(); Call(target, "AdvanceTime", seconds); }
    static ChapterJumpValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene(ScenePath);
        SessionState.SetBool(Key, true); Install(); EditorApplication.isPlaying = true;
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
        camera = Camera.main; console = camera.GetComponent<DialogueTestConsole>(); gate = camera.GetComponent<SceneInputGate>();
        view = camera.GetComponent<DeskScenePreview>(); interactor = camera.GetComponent<SceneObjectInteractor>();
        records = camera.GetComponent<PlaintextArchiveController>(); archive = records.archive;
        dialogue = camera.GetComponent<DialogueController>(); delivery = camera.GetComponent<SealedDeliveryController>();
        story = camera.GetComponent<PrologueSequence>(); fifth = camera.GetComponent<FifthActFlow>(); epilogue = camera.GetComponent<EpilogueFlow>();
    }
    static void Tick()
    {
        if (done) return;
        if (EditorApplication.timeSinceStartup - started > 240) { Finish(false, "Timeout at scenario " + scenario + " phase " + phase); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating || !Camera.main) return;
        try
        {
            Resolve();
            if (phase == 0)
            {
                Check(!console.JumpToAct(-1) && !console.JumpToAct(7) && !console.JumpToAct(6, (DialogueTestConsole.EndingPreset)99), "Invalid jump accepted");
                oldCameraId = camera.GetInstanceID();
                Check(console.JumpToAct(Targets[scenario], (DialogueTestConsole.EndingPreset)Presets[scenario]), "Jump request rejected");
                Check(console.IsJumping && !console.JumpToAct(1), "Duplicate request accepted"); phase = 1; return;
            }
            if (phase == 1)
            {
                if (camera.GetInstanceID() == oldCameraId || console.LastStartedAct < 0) return;
                ValidateChapter();
                if (scenario == 0)
                {
                    console.SetOpen(true);
                    ScreenCapture.CaptureScreenshot(Path.Combine(Output, "chapter-console.png"));
                    phase = 2; frames = 0; return;
                }
                Next(); return;
            }
            if (phase == 2 && ++frames > 20) Next();
        }
        catch (Exception error) { Finish(false, "Scenario " + scenario + " (act " + Targets[scenario] + "): " + error); }
    }
    static void Next()
    {
        scenario++; phase = 0;
        if (scenario == Targets.Length) Finish(true, "PASS: " + checks + " checks across " + Targets.Length + " real asynchronous scene reloads.\n"
            + "Acts 0-6; all four endings; prior plaintext/supplements/signatures; normal opening dialogues and deliveries; actual puzzles and signature; "
            + "reset during delivery, burned confession, repeated same-act jump, final black to first act; console pause and invalid/duplicate requests.");
    }
    static void ValidateChapter()
    {
        int target = Targets[scenario];
        Check(console.LastStartedAct == target && !console.IsJumping && !console.IsOpen, "Wrong chapter or console retained");
        Check(archive.CurrentAct == Math.Max(1, target) && archive.DeskAct == 0, "Archive starts on wrong act");
        Check(!delivery.IsActive && !delivery.transferDrum.Cargo && !interactor.InspectedObject, "Old cargo/inspection leaked");
        Check(camera.GetComponent<FirstActFlow>().enabled && camera.GetComponent<SecondActFlow>().enabled
            && camera.GetComponent<ThirdActFlow>().enabled && camera.GetComponent<FourthActFlow>().enabled, "Jump disabled normal progression");
        for (int act = 1; act <= 5; act++)
        {
            Check(archive.IsCompleted(act) == (act < target), "Wrong completion: " + act);
            Check(archive.IsAvailable(act) == (act < target), "Wrong delivery unlock: " + act);
            if (act < target) Check(!archive.HasPendingSupplement(act), "Missing previous supplemental documents");
        }
        foreach (var entry in records.entries)
        {
            Check(entry.saved == (entry.act < target), "Wrong plaintext completion: " + entry.id);
            if (entry.saved)
                Check(entry.writtenText == entry.answer && entry.plaintextInk.text == PlaintextArchiveController.WrapAnswer(entry.answer)
                    && records.DraftForAct(entry.act) == "", "Wrong preserved plaintext or uncleared completed draft: " + entry.id);
        }
        Check(fifth.verification.Signed == (target > 3), "Incorrect transcript signature");
        if (target < 6) Check(!fifth.Dispatched && !fifth.ConfessionBurned && !fifth.GuaranteesSource
            && fifth.OriginalCustody == FifthActFlow.Custody.Guild && !fifth.ashes.activeSelf, "Fifth-act choices not reset");
        if (target == 0)
        {
            Check(story.State == PrologueSequence.Phase.Monologue && dialogue.ActiveEvent == story.prologue, "Prologue not started");
            return;
        }
        console.SetOpen(true);
        float opacity = fifth.curtain.Opacity;
        if (target == 6) Call(console, "Update"); else Call(story, "Update");
        Check(fifth.curtain.Opacity == opacity && gate.ConsoleOpen, "Console failed to pause chapter card");
        console.SetOpen(false);
        for (int i = 0; i < 12 && fifth.curtain.IsActive; i++)
            if (target == 6) fifth.curtain.Advance(5); else Step(story, 5);
        Check(!fifth.curtain.IsActive, "Chapter card did not finish");
        Check(UnityEngine.Object.FindFirstObjectByType<ActCandlePile>().RemainingCount == Math.Max(0, 5 - target), "Wrong candles");
        if (target == 6) { ValidateEpilogue(); return; }
        string[] intros = { "", "act1_arrival", "beatrice", "act3_arrival", "act4_arrival", "act5_arrival" };
        bool sawIntro = false;
        for (int i = 0; i < 35 && delivery.State != SealedDeliveryController.Phase.AwaitingPickup; i++)
        {
            if (dialogue.IsActive)
            {
                Check(dialogue.ActiveEvent.id == intros[target], "Wrong opening dialogue: " + dialogue.ActiveEvent.id);
                sawIntro = true; ReadAll();
            }
            AdvanceFlows(); Call(view, "AnimateView", 5f, Vector2.zero);
            Call(delivery.transferDrum, "Advance", 5f); Step(delivery, 5);
        }
        Check(sawIntro && delivery.State == SealedDeliveryController.Phase.AwaitingPickup, "Opening did not lead to delivery");
        Check(!archive.IsAvailable(target), "Target documents unlocked before pickup");
        if (scenario == 3) return; // Next jump repeats act three while its delivery is still active.
        Ungate(); Check(delivery.CollectPacket(), "Cannot collect target packet");
        for (int i = 0; delivery.IsActive && i < 6; i++) Step(delivery, 5);
        Check(archive.IsAvailable(target) && archive.DeskAct == target && !delivery.IsActive, "Packet did not unlock target");
        Call(view, "AnimateView", 5f, Vector2.zero); Ungate();
        Check(!gate.BlocksWorldInput, "Input remained locked after receiving");
        if (target == 1) { Solve("caesar"); Check(camera.GetComponent<FirstActFlow>().State == FirstActFlow.Phase.Delay, "Act one cannot progress"); }
        if (target == 2) { Solve("book"); Check(camera.GetComponent<SecondActFlow>().State == SecondActFlow.Phase.CuePending, "Act two cannot progress"); }
        if (target == 3)
        {
            Solve("vigenere");
            var third = camera.GetComponent<ThirdActFlow>(); Step(third);
            Check(dialogue.ActiveEvent == third.signatureHint, "Missing signature hint after plaintext confirmation"); ReadAll();
            Inspect(fifth.verification.GetComponent<SceneObject>());
            Check(fifth.verification.TrySign() && camera.GetComponent<ThirdActFlow>().State == ThirdActFlow.Phase.CharlesPending, "Act three signing cannot progress");
        }
        if (target == 4)
        {
            Solve("affine"); Solve("rail"); Step(camera.GetComponent<FourthActFlow>());
            Check(camera.GetComponent<FourthActFlow>().State != FourthActFlow.Phase.Waiting, "Act four cannot progress");
        }
        if (target == 5)
        {
            Check(fifth.CanChoose && fifth.confession.gameObject.activeSelf, "Fifth act lacks signature or confession");
            if (scenario == 6)
            {
                Inspect(fifth.confession); Check(fifth.BeginBurn() && dialogue.IsChoosing, "Burn choice not available");
                Ungate(); Check(dialogue.SelectChoice(0), "Cannot choose burn"); Step(fifth); Step(fifth, 5);
                Check(fifth.ConfessionBurned && !fifth.confession.gameObject.activeSelf, "Burn did not finish before reset");
            }
        }
    }
    static void AdvanceFlows()
    {
        Step(camera.GetComponent<FirstActFlow>(), 20); Step(camera.GetComponent<SecondActFlow>(), 20);
        Step(camera.GetComponent<ThirdActFlow>(), 20); Ungate(); Call(camera.GetComponent<FourthActOpening>(), "Advance");
        Step(camera.GetComponent<FourthActFlow>(), 20); Step(fifth, 20);
    }
    static void ReadAll()
    {
        for (int i = 0; dialogue.IsActive && i < 100; i++)
        {
            // Current conversations switch visitors between lines; advance those shutter animations too.
            Call(view, "AnimateView", 5f, Vector2.zero); Call(dialogue.eyeShutter, "Advance", 5f); Step(dialogue, 5);
            Ungate(); dialogue.Advance();
        }
        Check(!dialogue.IsActive, "Dialogue stuck"); Ungate();
    }
    static void Solve(string id)
    {
        var entry = records.entries.Single(e => e.id == id);
        Step(records); records.workbench.SetPlaintext(entry.answer); Step(records); Step(records, 1);
        Check(entry.saved, "Cannot solve " + id);
        Check(records.workbench.Plaintext == "" && records.DraftForAct(entry.act) == "", "Solved draft was not cleared: " + id);
        if (dialogue.IsActive) ReadAll();
    }
    static void Inspect(SceneObject paper)
    {
        interactor.CancelInteraction(); Ungate(); view.SetView(DeskScenePreview.View.Desktop); Call(view, "AnimateView", 5f, Vector2.zero);
        interactor.Inspect(paper); Call(paper, "Animate", 3f);
        var rack = paper.GetComponent<DeskOperationalDocument>(); if (rack && rack.IsOpen) Step(rack, 3);
        Check(interactor.InspectedObject == paper, "Cannot inspect paper"); Ungate();
    }
    static void ValidateEpilogue()
    {
        string expected = new[] { "开门", "缺页", "真相易手", "背书" }[Presets[scenario]];
        Check(fifth.Dispatched && fifth.State == FifthActFlow.Phase.Complete && epilogue.EndingName == expected, "Wrong ending preset");
        Check(fifth.ConfessionBurned == (Presets[scenario] == 1) && fifth.GuaranteesSource == (Presets[scenario] == 3), "Wrong preset decisions");
        Check(!fifth.confession.gameObject.activeSelf && !fifth.verification.gameObject.activeSelf
            && !fifth.transferApplication.gameObject.activeSelf && fifth.originals.All(p => !p.gameObject.activeSelf), "Submitted papers reappeared");
        var closed = epilogue.doorLeaf.localRotation;
        for (int i = 0; i < 180 && epilogue.State != EpilogueFlow.Phase.Complete; i++)
        {
            if (dialogue.IsActive)
            {
                Check(Quaternion.Angle(closed, epilogue.doorLeaf.localRotation) < 0.01f, "Door opened during dialogue"); ReadAll();
            }
            if (!view.StoryLocked) Call(view, "AnimateView", 5f, Vector2.zero);
            Call(epilogue.drum, "Advance", 5f); Step(epilogue, 3);
        }
        Check(epilogue.State == EpilogueFlow.Phase.Complete && epilogue.ReportReceived && epilogue.curtain.Opacity == 1, "Epilogue failed to finish");
        Check(epilogue.ReunionReceived == (Presets[scenario] < 2), "Wrong reunion branch");
        console.SetOpen(true);
        var canvas = epilogue.curtain.GetComponentInChildren<Canvas>(true);
        Check(console.IsOpen && gate.ConsoleOpen && !canvas.enabled, "Console inaccessible over final black");
    }
    static void Finish(bool success, string detail)
    {
        if (done) return; done = true;
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "validation.txt"), detail);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
