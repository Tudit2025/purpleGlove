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

// Runs against an isolated project copy, including actual scene meshes and dialogue.
[InitializeOnLoad]
public static class EpilogueValidation
{
    const string Key = "EpilogueValidation.Run";
    const string ScenePath = "Assets/Art/DeskSceneV1/Scenes/PH_DeskRoom.unity";
    static readonly string Output = Path.GetFullPath("../../output/epilogue-validation");
    static int checks, scenario;
    static bool done, reload;
    static double started;
    static Camera camera;
    static SceneInputGate gate;
    static SceneObjectInteractor interactor;
    static DeskScenePreview view;
    static ActArchiveController archive;
    static PlaintextArchiveController records;
    static DialogueController dialogue;
    static SealedDeliveryController delivery;
    static FourthActFlow fourth;
    static FifthActFlow fifth;
    static EpilogueFlow epilogue;
    static readonly System.Collections.Generic.Dictionary<int, string> variants = new System.Collections.Generic.Dictionary<int, string>();
    static object Call(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); checks++; }
    static void Ungate() => typeof(SceneInputGate).GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gate, -1);
    static void Step(object flow, float seconds = 0) { Ungate(); Call(flow, "AdvanceTime", seconds); }
    static EpilogueValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Output); EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetInt(Key + ".Checks", checks); SessionState.SetBool(Key, true); Install(); EditorApplication.isPlaying = true;
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    public static void RunVisuals()
    {
        SessionState.SetBool(Key + ".Visuals", true);
        try
        {
            DeskSceneBuilder.InstallEpilogue();
            var installed = Camera.main.GetComponent<EpilogueFlow>();
            var edited = installed.openEnding; string originalLine = edited.lines[0].text;
            edited.lines[0].text = originalLine + " [preserve]"; EditorUtility.SetDirty(edited); AssetDatabase.SaveAssets();
            DeskSceneBuilder.InstallEpilogue();
            installed = Camera.main.GetComponent<EpilogueFlow>();
            Check(installed.openEnding == edited && edited.lines[0].text.EndsWith("[preserve]"), "Installer overwrote authored dialogue");
            edited.lines[0].text = originalLine; EditorUtility.SetDirty(edited); AssetDatabase.SaveAssets();
            Check(UnityEngine.Object.FindObjectsOfType<EpilogueFlow>(true).Length == 1, "Duplicate epilogue controller");
            Check(installed.fifth.epilogue == installed && installed.reportReceipt && installed.reunionReceipt, "Saved epilogue references missing");
        }
        catch (Exception e) { Finish(false, e.ToString()); return; }
        Run();
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
        if (done) return;
        if (EditorApplication.timeSinceStartup - started > 240) { Finish(false, "Timed out"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        try
        {
            if (reload) { reload = false; EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single)); return; }
            Resolve();
            if (scenario == 0) checks += SessionState.GetInt(Key + ".Checks", 0);

            RunScenario();
            if (SessionState.GetBool(Key + ".Visuals", false)) scenario = scenario == 0 ? 1 : scenario == 1 ? 2 : scenario == 2 ? 8 : 16;
            else scenario++;
            if (scenario == 16) Finish(true, "PASS: " + checks + " checks; all 16 final dispositions, four endings, act-five handoff to act-six card, physical result and reunion receipts, closed-door/aperture dialogue invariant, final door/fade synchronization, no confession variations outside reunion, stop/continue, paused finale, and previous act-four/five interactions.\nPlay Mode scene: " + ScenePath);
            else reload = true;
        }
        catch (Exception e) { Finish(false, "Scenario " + scenario + ": " + e); }
    }
    static void Resolve()
    {
        camera = Camera.main; gate = camera.GetComponent<SceneInputGate>(); interactor = camera.GetComponent<SceneObjectInteractor>();
        view = camera.GetComponent<DeskScenePreview>(); records = camera.GetComponent<PlaintextArchiveController>(); archive = records.archive;
        dialogue = camera.GetComponent<DialogueController>(); delivery = camera.GetComponent<SealedDeliveryController>();
        fourth = camera.GetComponent<FourthActFlow>(); fifth = camera.GetComponent<FifthActFlow>(); epilogue = camera.GetComponent<EpilogueFlow>();
        camera.GetComponent<FirstActFlow>().enabled = false; camera.GetComponent<SecondActFlow>().enabled = false;
        camera.GetComponent<FourthActOpening>().enabled = false;
        camera.GetComponent<DialogueTestConsole>().SetOpen(false); Ungate();
    }
    static void ReadAll()
    {
        if (!dialogue.IsActive) return;
        Call(view, "AnimateView", 5f, Vector2.zero); Step(dialogue, 5f); Call(dialogue.eyeShutter, "Advance", 5f); Step(dialogue, 5f);
        for (int i = 0; dialogue.IsActive && !dialogue.IsClosing && i < 100; i++) dialogue.Advance();
        if (dialogue.IsClosing) { Call(dialogue.eyeShutter, "Advance", 5f); Step(dialogue, 0f); }
        Check(!dialogue.IsActive, "Dialogue remained active"); Ungate();
    }
    static void Solve(string id)
    {
        var entry = records.entries.Single(e => e.id == id);
        Step(records, 0); records.workbench.SetPlaintext(entry.answer); Step(records, 0); Step(records, 1);
        Check(entry.saved, "Plaintext did not preserve " + id); ReadAll();
    }
    static void Inspect(SceneObject paper)
    {
        interactor.CancelInteraction(); Ungate(); view.SetView(DeskScenePreview.View.Desktop); Call(view, "AnimateView", 3f, Vector2.zero);
        interactor.Inspect(paper); Call(paper, "Animate", 3f);
        var rack = paper.GetComponent<DeskOperationalDocument>(); if (rack && rack.IsOpen) Step(rack, 2);
        Check(interactor.InspectedObject == paper, "Cannot inspect " + paper.name); Ungate();
    }
    static void Close() { interactor.CancelInteraction(); Ungate(); }
    static void Choose(SceneObject paper, int index)
    {
        Inspect(paper); var choices = paper.GetComponent<FifthActDocument>(); choices.RefreshInk();
        var area = choices.areas[index];
        Vector3 point = camera.WorldToScreenPoint(paper.transform.TransformPoint(new Vector3(area.center.x, 0.011f, area.center.y)));
        Check(choices.ChoiceAtPointer(camera, point) == index, "Paper pointer hit wrong option " + index);
        Check(choices.TryChoose(index), "Paper choice rejected " + index);
        if (choices.kind == FifthActDocument.Kind.Confession)
        {
            Check(dialogue.IsChoosing && fifth.State == FifthActFlow.Phase.BurnChoice && !fifth.ConfessionBurned, "Burn bypassed choice");
            ChooseResponse(0); Check(fifth.State == FifthActFlow.Phase.BurnPending, "Burn choice did not defer action");
            Step(fifth);
        }
        else ReadAll();
    }
    static void BeginFourth()
    {
        Check(archive.ReceiveAct(1) && archive.CompleteAct(1), "Setup first act");
        Check(archive.ReceiveAct(2) && archive.CompleteAct(2), "Setup second act");
        Check(archive.ReceiveAct(3), "Setup third act");
        Solve("vigenere"); Inspect(fifth.verification.GetComponent<SceneObject>());
        Check(fifth.verification.TrySign(), "Real third-act signature failed");
        camera.GetComponent<ThirdActFlow>().enabled = false; Close();
        Check(archive.CompleteAct(3) && archive.ReceiveAct(4), "Setup fourth act"); Ungate();
    }
    static void RunScenario()
    {
        BeginFourth();
        string first = scenario % 2 == 0 ? "affine" : "rail", second = first == "affine" ? "rail" : "affine";
        Check(!fourth.confession.Available && !fifth.confession.gameObject.activeSelf, "Unsolved confession visible in rack");
        Solve(first); Step(fourth, 1); Check(fourth.State == FourthActFlow.Phase.Waiting, "One solution advanced act four");
        if (first == "rail")
        {
            Check(fourth.confession.Available && fifth.confession.gameObject.activeSelf
                && fifth.confession.transform.parent == fourth.confession.storageAnchor, "Confession not immediately stored in rack");
            Inspect(fifth.confession); fifth.confession.GetComponent<FifthActDocument>().RefreshInk();
            Check(fourth.confession.IsReady && !fifth.confession.GetComponent<FifthActDocument>().TryChoose(0), "Fourth-act confession cannot be read or can burn prematurely");
            if (scenario == 1) Capture("00-confession-in-fourth-act"); Close();
            archive.ShowActOnDesk(3);
            Check(fifth.confession.gameObject.activeSelf && fifth.confession.transform.parent == fourth.confession.storageAnchor, "Confession entered old-act shelf");
            archive.ShowActOnDesk(4); Ungate();
        }
        Solve(second); Step(fourth); Step(fourth); Step(fourth, 2);
        Check(fourth.State == FourthActFlow.Phase.Summary, "Both solutions did not start summary");
        if (scenario == 0)
        {
            fourth.Stop(); Check(archive.CurrentAct == 4 && fourth.Resume(), "Cancelled monologue advanced the act");
            Step(fourth); Step(fourth, 2); Check(fourth.State == FourthActFlow.Phase.Summary, "Monologue did not resume");
        }
        ReadAll(); Check(fourth.State == FourthActFlow.Phase.TransitionPending, "Monologue did not proceed directly to transition");
        Check(fifth.confession.gameObject.activeSelf && fifth.confession.transform.parent == fourth.confession.storageAnchor, "Confession not in rack before transition");
        Step(fourth);
        if (scenario == 0)
        {
            Step(fourth.story, 5); Check(archive.DeskAct == 0 && archive.CurrentAct == 4, "Transition committed before fade-in");
            fourth.Stop(); Check(archive.CurrentAct == 4 && archive.DeskAct == 4, "Cancelled transition lost dossier");
            Check(fourth.Resume(), "Fourth transition cannot resume"); Step(fourth);
        }
        for (int i = 0; fourth.story.IsActive && i < 9; i++) Step(fourth.story, 5);
        Check(archive.CurrentAct == 5 && fourth.State == FourthActFlow.Phase.Complete, "Fifth act not entered");
        Check(fifth.verification.Signed && fifth.verification.signatureInk.text == "Luca", "Transcript signature reset");
        Check(fourth.confession.Available && fourth.confession.gameObject.activeSelf, "Confession did not move into rack");
        Check(UnityEngine.Object.FindFirstObjectByType<ActCandlePile>().RemainingCount == 0, "Last reserve candle not used");
        Step(fifth); Step(fifth); Check(fifth.State == FifthActFlow.Phase.Intro, "Fifth introduction not started"); ReadAll(); Step(fifth);
        for (int i = 0; delivery.State != SealedDeliveryController.Phase.AwaitingPickup && i < 5; i++)
        { Call(delivery.transferDrum, "Advance", 5f); Step(delivery, 5); }
        Ungate(); Check(delivery.CollectPacket(), "Fifth letters cannot be received"); Step(delivery, 5); Step(delivery, 5);
        Check(fifth.CanChoose && archive.acts[4].documents.Length == 3, "Wrong fifth-act documents");
        Check(!fifth.GuaranteesSource && fifth.OriginalCustody == FifthActFlow.Custody.Guild && !fifth.ConfessionBurned, "Unsafe initial dispositions");
        if (scenario == 0)
        {
            foreach (var paper in archive.acts[4].documents) { Inspect(paper); Capture("letter-" + paper.name); Close(); }
            Choose(fifth.verification.GetComponent<SceneObject>(), 1); Check(fifth.GuaranteesSource, "Guarantee not recorded");
            Choose(fifth.verification.GetComponent<SceneObject>(), 0); Check(!fifth.GuaranteesSource && fifth.SourceRevised, "Guarantee cannot be corrected");
            Capture("01-source-choice"); Close();
            foreach (int custody in new[] { 1, 2, 3, 0 }) Choose(fifth.transferApplication, custody);
            Capture("02-transfer-choice"); Close();
            Inspect(fifth.confession); fifth.confession.GetComponent<FifthActDocument>().RefreshInk(); Capture("03-confession"); Close();
        }
        if (scenario == 0) NegativeChoices();
        bool guarantee = scenario >= 8, burn = scenario % 2 == 1;
        int recipient = scenario / 2 % 4;
        if (guarantee) { Choose(fifth.verification.GetComponent<SceneObject>(), 1); Close(); }
        if (recipient != 0) { Choose(fifth.transferApplication, recipient); Close(); }
        if (burn)
        {
            Choose(fifth.confession, 0); Check(fifth.State == FifthActFlow.Phase.Burning, "Burn not started");
            if (scenario == 1)
            {
                Step(fifth, 0.5f); fifth.Stop(); Check(!fifth.ConfessionBurned && fifth.Resume(), "Cancelled burn destroyed paper");
                Ungate(); Choose(fifth.confession, 0);
            }
            Step(fifth, 1.4f); if (scenario == 1) Capture("04-burning"); Step(fifth, 5);
            Check(fifth.ConfessionBurned && !fifth.confession.gameObject.activeSelf, "Burn did not remove paper");
            Check(archive.ShowActOnDesk(4) && !fifth.confession.gameObject.activeSelf, "Burned transcript regenerated on recall");
            Check(records.entries.Single(e => e.id == "rail").saved && archive.acts[3].documents[2].gameObject.activeSelf, "Burn destroyed cipher original");
            archive.ShowActOnDesk(5); Ungate(); if (scenario == 1) Capture("05-burned-rack");
        }
        Close(); fifth.bell.SetRingAllowed(true); Check(fifth.bell.TryInteract(), "Bell not usable");
        Step(fifth); Step(fifth.bell, 1);
        Check(dialogue.IsChoosing && fifth.bell.StrikeCount == 0, "Bell rang before choice");
        if (scenario == 0) Capture("01-ring-choice");
        ChooseResponse(0);
        Check(fifth.bell.StrikeCount == 0 && fifth.State == FifthActFlow.Phase.Choosing, "Selection committed before physical strike");
        Step(fifth.bell, 2); Step(fifth.bell, 1); Step(fifth.bell, 1);
        Check(fifth.State == FifthActFlow.Phase.HandoffPending && !fifth.bell.IsBusy, "Bell handoff did not await putting down");
        Step(fifth); Check(fifth.State == FifthActFlow.Phase.Handoff, "Clerk did not return"); ReadAll(); Step(fifth);
        Call(view, "AnimateView", 3f, Vector2.zero); Call(delivery.transferDrum, "Advance", 3f); Step(fifth); Step(fifth, 3);
        Check(fifth.State == FifthActFlow.Phase.TurningOutside && !fifth.Dispatched, "Dispatch committed before rotor finished");
        if (scenario == 0)
        {
            Capture("06-documents-in-drum"); fifth.Stop(); Check(!fifth.Dispatched && fifth.Resume(), "Pre-dispatch cancel committed handoff");
            Step(fifth); Call(view, "AnimateView", 3f, Vector2.zero); Call(delivery.transferDrum, "Advance", 3f); Step(fifth); Step(fifth, 3);
        }
        Call(delivery.transferDrum, "Advance", 3f); Step(fifth);
        Check(fifth.Dispatched && archive.CurrentAct == 5, "Handoff or fade boundary incorrect");
        if (scenario == 1)
        {
            fifth.Stop(); Check(fifth.Dispatched && fifth.Resume(), "Dispatched state rolled back");
        }
        Step(fifth);
        Check(fifth.curtain.Title == "第六幕 · 尾声", "Missing sixth-act card");
        Step(fifth, 3);
        Check(fifth.curtain.Opacity == 1 && archive.CurrentAct == 5 && !dialogue.IsActive, "Transition committed before fade-in");
        if (scenario == 0)
        {
            Step(fifth, 3); Step(fifth, 3);
            Capture("act6-title");
        }
        if (scenario == 0)
        {
            fifth.Stop(); Check(fifth.Dispatched && archive.CurrentAct == 5 && fifth.Resume(), "Transition cancellation lost submission"); Step(fifth);
        }
        for (int i = 0; fifth.State != FifthActFlow.Phase.Complete && i < 12; i++) Step(fifth, 3);
        Check(fifth.State == FifthActFlow.Phase.Complete && archive.CurrentAct == 6 && fifth.curtain.Opacity == 0, "Act six not entered");
        string expected = guarantee ? "背书" : recipient != 0 ? "真相易手" : burn ? "缺页" : "开门";
        Check(fifth.Ending == expected && epilogue.EndingName == expected, "Wrong ending for combination " + scenario);
        Check(!fifth.SetCustody(FifthActFlow.Custody.France) && !fifth.BeginBurn(), "Choices changed after submission");
        RunEpilogue();
        archive.ShowActOnDesk(1); archive.ShowActOnDesk(3); archive.ShowActOnDesk(4);
        Check(!fifth.transferApplication.gameObject.activeSelf && !fifth.verification.gameObject.activeSelf && !fifth.confession.gameObject.activeSelf
            && fifth.originals.All(p => !p.gameObject.activeSelf), "Dispatched documents regenerated on recall");
    }

    static void RunEpilogue()
    {
        var closed = epilogue.doorLeaf.localRotation;
        int completed = 0; epilogue.completed.AddListener(() => completed++);
        var interruptions = new System.Collections.Generic.HashSet<EpilogueFlow.Phase>();
        string transcript = "";
        bool halfway = false, sawTime = false;
        for (int i = 0; i < 240 && epilogue.State != EpilogueFlow.Phase.Complete; i++)
        {
            var state = epilogue.State;
            Check(gate.BlocksWorldInput, "World input escaped during epilogue " + state);
            if (state != EpilogueFlow.Phase.FinalFade) Check(Quaternion.Angle(closed, epilogue.doorLeaf.localRotation) < 0.01f, "Door opened before final fade");
            if (state == EpilogueFlow.Phase.TimeTransition)
            { sawTime = true; Check(epilogue.curtain.Title.Contains("六点半"), "Reunion lacks elapsed time"); }
            bool interrupt = (scenario == 0 && (state == EpilogueFlow.Phase.Report || state == EpilogueFlow.Phase.TurningInside
                || state == EpilogueFlow.Phase.TimeTransition || state == EpilogueFlow.Phase.Arrival || state == EpilogueFlow.Phase.Ending))
                || (scenario == 1 && (state == EpilogueFlow.Phase.Flying || state == EpilogueFlow.Phase.TurningOutside));
            if (interrupt && interruptions.Add(state))
            {
                bool hadReport = epilogue.ReportReceived, hadReunion = epilogue.ReunionReceived;
                var console = camera.GetComponent<DialogueTestConsole>();
                Check(console.Execute("stop").Contains("中止"), "Console cannot stop epilogue");
                Check(epilogue.State == EpilogueFlow.Phase.Suspended && !dialogue.IsActive, "Stop stranded dialogue");
                Check(epilogue.ReportReceived == hadReport && epilogue.ReunionReceived == hadReunion, "Stop rolled back delivered receipt");
                Check(console.Execute("continue").Contains("第六幕"), "Console cannot resume epilogue");
                Call(dialogue.eyeShutter, "Advance", 5f); continue;
            }
            if (dialogue.IsActive)
            {
                Check(epilogue.State == EpilogueFlow.Phase.Report || epilogue.State == EpilogueFlow.Phase.Arrival || epilogue.State == EpilogueFlow.Phase.Ending, "Unexpected epilogue dialogue");
                Check(view.DialogueLocked && !view.StoryLocked, "Dialogue bypasses visitation view");
                Call(view, "AnimateView", 5f, Vector2.zero); Step(dialogue, 5); Call(dialogue.eyeShutter, "Advance", 5f); Step(dialogue, 5);
                Check(dialogue.eyeShutter.AtSecondStop && !dialogue.eyeShutter.IsMoving, "Text started behind closed shutter");
                if (scenario >= 2 && state == EpilogueFlow.Phase.Ending)
                    transcript = string.Join("|", dialogue.ActiveEvent.lines.Select(l => l.text));
                if (dialogue.IsTyping) dialogue.Advance();
                if (state == EpilogueFlow.Phase.Ending && (scenario == 0 || scenario == 1 || scenario == 2 || scenario == 8))
                {
                    // The open branch starts with a two-character greeting; show its substantive reply too.
                    if (scenario == 0) { dialogue.Advance(); dialogue.Advance(); }
                    Capture("ending-" + scenario + "-visitor");
                }
                for (int j = 0; dialogue.IsActive && !dialogue.IsClosing && j < 100; j++)
                {
                    Check(Quaternion.Angle(closed, epilogue.doorLeaf.localRotation) < 0.01f && dialogue.eyeShutter.AtSecondStop, "Speech after opening or without aperture");
                    Check(!dialogue.CurrentLine.text.Contains("{"), "Unresolved ending placeholder"); dialogue.Advance();
                }
                Check(dialogue.IsClosing, "Visitor did not close aperture before continuation");
                Step(epilogue, 1); Check(epilogue.State == state, "Receipt or finale began before shutter closed");
                Call(dialogue.eyeShutter, "Advance", 5f); Step(dialogue);
                Check(!dialogue.IsActive && !dialogue.eyeShutter.AtSecondStop, "Visit did not close properly");
                continue;
            }
            if (state == EpilogueFlow.Phase.FinalFade && !halfway)
            {
                halfway = true;
                float angle = Quaternion.Angle(closed, epilogue.doorLeaf.localRotation), opacity = epilogue.curtain.Opacity;
                gate.SetConsole(true); Step(epilogue, 10);
                Check(epilogue.curtain.Opacity == opacity && Quaternion.Angle(closed, epilogue.doorLeaf.localRotation) == angle, "Console failed to pause finale");
                gate.SetConsole(false); Step(epilogue, epilogue.doorOpenSeconds * 0.5f);
                Check(epilogue.curtain.Opacity > 0 && epilogue.curtain.Opacity < 1, "Missing simultaneous fade");
                Check(epilogue.Reunited ? Quaternion.Angle(closed, epilogue.doorLeaf.localRotation) > 5 : Quaternion.Angle(closed, epilogue.doorLeaf.localRotation) < 0.01f, "Wrong ending opens door");
                if (scenario == 0 || scenario == 1 || scenario == 2 || scenario == 8) Capture("ending-" + scenario + "-fade");
                if (scenario == 0)
                {
                    epilogue.Stop(); Check(Quaternion.Angle(closed, epilogue.doorLeaf.localRotation) < 0.01f && epilogue.curtain.Opacity == 0, "Finale stop failed to restore door");
                    Check(epilogue.Resume(), "Finale cannot resume");
                }
                continue;
            }
            if (state == EpilogueFlow.Phase.TurningInside || state == EpilogueFlow.Phase.Flying || state == EpilogueFlow.Phase.TurningOutside)
                Check(!dialogue.eyeShutter.AtSecondStop && !dialogue.IsActive, "Delivery during conversation");
            if (!view.StoryLocked) Call(view, "AnimateView", 5f, Vector2.zero);
            Call(epilogue.drum, "Advance", 5f); Step(epilogue, 3);
            if (epilogue.State == EpilogueFlow.Phase.ReceiptHold && (scenario == 0 || scenario == 8))
                Capture("receipt-" + scenario + (epilogue.ReunionReceived ? "-reunion" : "-result"));
        }
        Check(epilogue.State == EpilogueFlow.Phase.Complete && completed == 1 && epilogue.curtain.Opacity == 1, "Epilogue failed to finish once");
        Check(epilogue.ReportReceived && epilogue.reportReceipt.gameObject.activeSelf, "Missing physical result receipt");
        Check(epilogue.ReunionReceived == epilogue.Reunited && sawTime == epilogue.Reunited, "Reunion in wrong ending");
        Check(!dialogue.IsActive && !dialogue.eyeShutter.AtSecondStop && gate.StoryActive, "Final state leaves dialogue or input active");
        Check(epilogue.curtain.Title.Contains(epilogue.EndingName), "Final card has wrong ending");
        if (scenario == 0 || scenario == 1 || scenario == 2 || scenario == 8) Capture("ending-" + scenario + "-complete");
        if (scenario >= 2)
        {
            if (scenario % 2 == 0) variants[scenario / 2] = transcript;
            else Check(variants[scenario / 2] == transcript, "Confession changed transfer/endorsement ending");
        }
        Step(epilogue, 20); Check(completed == 1 && !epilogue.Begin(), "Ending replayed automatically");
    }

    static Vector2 ResponsePointer(int index)
    {
        Canvas.ForceUpdateCanvases(); Call(dialogue.Overlay, "LateUpdate"); Canvas.ForceUpdateCanvases();
        var row = (RectTransform)dialogue.Overlay.Panel.Find("Player response " + (index + 1));
        return RectTransformUtility.WorldToScreenPoint(dialogue.Overlay.OverlayCamera, row.TransformPoint(row.rect.center));
    }
    static void ChooseResponse(int index)
    {
        Check(dialogue.IsChoosing && !dialogue.IsTyping && dialogue.VisibleText == dialogue.CurrentLine.text, "Choice is not immediate/full");
        int count = dialogue.Voice.PlayedCount;
        var sequence = dialogue.ActiveEvent;
        for (int i = 0; i < dialogue.CurrentLine.choices.Length; i++)
        {
            var pointer = ResponsePointer(i);
            var row = dialogue.Overlay.Panel.Find("Player response " + (i + 1));
            var label = row.GetComponentInChildren<UnityEngine.UI.Text>();
            Check(row.gameObject.activeSelf && label.text == dialogue.CurrentLine.choices[i].text, "Incomplete response text");
            Check(dialogue.Overlay.ChoiceAtPointer(pointer) == i, "Response pointer misses " + i);
        }
        dialogue.Advance(); Step(dialogue, 20);
        Check(dialogue.ActiveEvent == sequence && dialogue.IsChoosing && dialogue.Voice.PlayedCount == count, "Choice advanced or played voice without selection");
        Check(!dialogue.SelectChoice(-1) && !dialogue.SelectChoice(99), "Invalid response accepted");
        Call(dialogue, "ProcessChoicePointer", Vector2.zero, true, true);
        Check(dialogue.IsChoosing, "Blank click selected response");
        Ungate(); Call(dialogue, "ProcessChoicePointer", ResponsePointer(index), true, true);
        Check(gate.FrameConsumed, "Response click leaked through gate");
        Check(dialogue.Voice.PlayedCount == count, "Selection played typing voice");
        Check(!dialogue.SelectChoice(index), "Duplicate response accepted");
    }
    static void GenericChoices()
    {
        var speaker = dialogue.speakers.Single(s => s.isProtagonist);
        var sequence = ScriptableObject.CreateInstance<DialogueEvent>(); sequence.id = "choice_validation"; sequence.arrivalDelay = 0;
        sequence.lines = new[] {
            new DialogueEvent.Line { speaker = speaker, text = "我该怎样回应？", choices = new[] {
                new DialogueEvent.Choice { id = "a", text = "先保留文件，等我核对完再决定。", nextLine = 1 },
                new DialogueEvent.Choice { id = "b", text = "我想继续查阅这些文件，再确认各方的安排。", nextLine = 2 },
                new DialogueEvent.Choice { id = "c", text = "暂时不作回答。" },
                new DialogueEvent.Choice { id = "d", text = "结束这次交谈。" } } },
            new DialogueEvent.Line { speaker = speaker, text = "这是第一种回应。" },
            new DialogueEvent.Line { speaker = speaker, text = "这是第二种回应。" } };
        int selected = 0, completed = 0, cancelled = 0;
        UnityEngine.Events.UnityAction<string, string> onChoice = (id, choice) => { if (id == sequence.id && choice == "b") selected++; };
        UnityEngine.Events.UnityAction<string> onComplete = id => { if (id == sequence.id) completed++; };
        UnityEngine.Events.UnityAction<string> onCancel = id => { if (id == sequence.id) cancelled++; };
        dialogue.choiceSelected.AddListener(onChoice); dialogue.eventCompleted.AddListener(onComplete); dialogue.eventCancelled.AddListener(onCancel);
        Check(sequence.TryValidate(out _), "Valid choice event rejected");
        sequence.lines[0].choices[3].id = "a"; Check(!sequence.TryValidate(out _), "Duplicate ID accepted"); sequence.lines[0].choices[3].id = "d";
        sequence.lines[0].choices[3].nextLine = 99; Check(!sequence.TryValidate(out _), "Invalid destination accepted"); sequence.lines[0].choices[3].nextLine = -1;
        int sounds = dialogue.Voice.PlayedCount; Ungate(); Check(dialogue.PlayInPlace(sequence), "Generic choice cannot play");
        Check(!dialogue.SelectChoice(0), "Opening click selected response");
        Check(dialogue.Voice.PlayedCount == sounds && gate.BlocksWorldInput, "Choice voice or input lock wrong");
        var console = camera.GetComponent<DialogueTestConsole>(); console.SetOpen(true);
        Check(!dialogue.SelectChoice(0) && console.Execute("next").Contains("选择"), "Console selected response");
        console.SetOpen(false); Ungate(); Capture("00-four-responses");
        ChooseResponse(1); Check(dialogue.LineIndex == 2 && selected == 1 && completed == 0, "Wrong branch/event notification");
        Check(dialogue.IsTyping, "Normal dialogue lost typewriter"); Step(dialogue, .001f);
        Check(dialogue.Voice.PlayedCount > sounds, "Normal dialogue lost voice"); ReadAll(); Check(completed == 1, "Normal branch did not complete");
        Check(dialogue.PlayInPlace(sequence), "Choice replay failed"); dialogue.Stop();
        Check(cancelled == 1 && selected == 1 && !gate.DialogueActive && !dialogue.Overlay.OverlayCamera.enabled, "Cancelled choice left effects");
        Check(dialogue.PlayInPlace(sequence), "Choice after cancel failed"); dialogue.enabled = false;
        Check(!dialogue.IsActive && !gate.DialogueActive, "Disabled choice stranded gate"); dialogue.enabled = true;
        dialogue.choiceSelected.RemoveListener(onChoice); dialogue.eventCompleted.RemoveListener(onComplete); dialogue.eventCancelled.RemoveListener(onCancel);
        UnityEngine.Object.Destroy(sequence); Ungate();
    }
    static void NegativeChoices()
    {
        Inspect(fifth.confession); var paper = fifth.confession; var pose = paper.transform.position;
        Check(fifth.BeginBurn(), "Cannot open burn choice");
        var console = camera.GetComponent<DialogueTestConsole>(); console.SetOpen(true); Ungate();
        Check(dialogue.IsChoosing && interactor.InspectedObject == paper && !dialogue.SelectChoice(0), "Console cancelled/accepted burn choice");
        console.SetOpen(false); Ungate();
        Capture("02-burn-choice"); Capture("burn-choice-4x3", 1024, 768); Capture("burn-choice-ultrawide", 2560, 1080); ChooseResponse(1);
        Check(fifth.CanChoose && !fifth.ConfessionBurned && paper.gameObject.activeSelf && interactor.InspectedObject == paper
            && paper.transform.position == pose, "Keep moved/destroyed paper");
        Ungate(); Check(fifth.BeginBurn(), "Cannot reconsider burn"); dialogue.Stop();
        Check(fifth.CanChoose && !fifth.ConfessionBurned, "Cancelled choice burned paper");
        Ungate(); Check(fifth.BeginBurn(), "Cannot reopen burn"); fifth.Stop();
        Check(!dialogue.IsActive && !fifth.ConfessionBurned && fifth.Resume(), "Stopped choice cannot resume"); Close();
        Step(fifth); Check(fifth.bell.TryInteract(), "Cannot pick up bell"); Step(fifth.bell, 1);
        console.SetOpen(true); Ungate();
        Check(dialogue.IsChoosing && fifth.bell.IsBusy && !dialogue.SelectChoice(0), "Console cancelled/accepted bell choice");
        console.SetOpen(false); Ungate(); ChooseResponse(1); Step(fifth.bell, 1);
        Check(fifth.CanChoose && fifth.bell.StrikeCount == 0 && !fifth.bell.IsBusy, "Refused ring submitted");
        Ungate(); Check(fifth.bell.TryInteract(), "Cannot reconsider ring"); Step(fifth.bell, 1); fifth.bell.PutDown(); Step(fifth.bell, 1);
        Check(!dialogue.IsActive && fifth.CanChoose && fifth.bell.StrikeCount == 0 && !gate.PropActionActive, "Escape/putdown rang or stranded gate");
        Ungate(); Check(fifth.bell.TryInteract(), "Cannot reopen bell"); Step(fifth.bell, 1); fifth.Stop();
        Check(!dialogue.IsActive && !fifth.bell.IsBusy && fifth.bell.StrikeCount == 0 && fifth.Resume(), "Stopped bell cannot resume"); Ungate();
    }

    static void Capture(string name, int width = 1600, int height = 900)
    {
        var target = RenderTexture.GetTemporary(width, height, 24); var active = RenderTexture.active;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false); var previous = camera.targetTexture;
        var overlay = dialogue.Overlay.OverlayCamera; var previousOverlay = overlay.targetTexture;
        var curtainCanvas = fifth.curtain.transform.Find("Act transition overlay").GetComponent<Canvas>();
        var previousMode = curtainCanvas.renderMode; var previousCamera = curtainCanvas.worldCamera;
        float previousDistance = curtainCanvas.planeDistance;
        try
        {
            curtainCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            curtainCanvas.worldCamera = overlay; curtainCanvas.planeDistance = 0.09f;
            camera.targetTexture = target; overlay.targetTexture = target;
            Canvas.ForceUpdateCanvases(); Call(dialogue.Overlay, "LateUpdate"); Canvas.ForceUpdateCanvases();
            camera.Render(); if (overlay.enabled || curtainCanvas.enabled) overlay.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply(); File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
            if (dialogue.IsChoosing)
                for (int i = 0; i < dialogue.CurrentLine.choices.Length; i++)
                {
                    var row = (RectTransform)dialogue.Overlay.Panel.Find("Player response " + (i + 1));
                    var label = row.GetComponentInChildren<UnityEngine.UI.Text>();
                    Check(row.rect.height >= 36 && label.cachedTextGenerator.characterCountVisible >= label.text.Length,
                        "Response clipped on first render at " + width + "x" + height + ": " + i);
                }
        }
        finally { curtainCanvas.renderMode = previousMode; curtainCanvas.worldCamera = previousCamera; curtainCanvas.planeDistance = previousDistance;
            overlay.targetTexture = previousOverlay; camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(image); }
    }
    static void Finish(bool success, string message)
    {
        if (done) return; done = true; SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        bool visuals = SessionState.GetBool(Key + ".Visuals", false); SessionState.SetBool(Key + ".Visuals", false);
        if (visuals) message = message.Replace("all 16 final dispositions", "four representative dispositions and idempotent installation");
        Directory.CreateDirectory(Output); File.WriteAllText(Output + (visuals ? "/visual-validation.txt" : "/validation.txt"), message);
        if (success) Debug.Log(message); else Debug.LogError(message); EditorApplication.Exit(success ? 0 : 1);
    }
}
