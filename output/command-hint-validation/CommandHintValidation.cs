using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using PurpleGlove;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Copy to an isolated project's Assets/Validation/Editor; execute CommandHintValidation.Run.
[InitializeOnLoad]
public static class CommandHintValidation
{
    const string Key = "CommandHintValidation.Play";
    static readonly string Output = Path.GetFullPath("../../output/command-hint-validation");
    static readonly StringBuilder Report = new StringBuilder();
    static bool done;
    static int checks, phase, frames;
    static double started;
    static Camera camera;
    static SceneInputGate gate;
    static SceneObjectInteractor interactor;
    static DeskScenePreview view;
    static DeskViewHud hud;
    static DeskCommandHints hints;
    static ActArchiveController archive;
    static PlaintextArchiveController records;
    static DialogueController dialogue;
    static SealedDeliveryController delivery;
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    static void Set(object target, string name, object value) => target.GetType().GetField("<" + name + ">k__BackingField",
        BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static void Ungate() => typeof(SceneInputGate).GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gate, -1);
    static void Step(object target, float seconds = 1) { Ungate(); Call(target, "AdvanceTime", seconds); }
    static CommandHintValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene("Assets/Art/DeskSceneV1/Scenes/PH_DeskRoom.unity");
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Show();
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
    static DeskCommandHints.Hint Expect(string task, string detail = null)
    {
        var hint = hints.Resolve();
        Check(hint.IsVisible && hint.task.Contains(task), "Expected " + task + ", got " + hint.task);
        if (detail != null) Check(hint.detail.Contains(detail), "Expected detail " + detail + ", got " + hint.detail);
        foreach (char c in (hint.task + hint.detail).Distinct()) Check(hud.labelFont.HasCharacter(c), "Missing glyph: " + c);
        Report.AppendLine(hint.task + " | " + hint.detail);
        return hint;
    }
    static void Hidden(string reason) => Check(!hints.Resolve().IsVisible, "Visible during " + reason);
    static void ReadAll()
    {
        for (int i = 0; dialogue.IsActive && i < 150; i++)
        {
            Call(view, "AnimateView", 5f, Vector2.zero);
            Step(dialogue, 5); Call(dialogue.eyeShutter, "Advance", 5f); Step(dialogue, 0);
            if (dialogue.IsActive && !dialogue.IsClosing && dialogue.LineIndex >= 0) dialogue.Advance();
        }
        Check(!dialogue.IsActive, "Dialogue stuck"); Ungate();
    }
    static void Solve(string id)
    {
        Step(records);
        records.workbench.SetPlaintext(records.entries.Single(e => e.id == id).answer);
        Step(records); Step(records);
        Check(records.entries.Single(e => e.id == id).saved, "Solution did not save: " + id);
        ReadAll();
    }
    static void Inspect(SceneObject paper)
    {
        Ungate(); interactor.Inspect(paper);
        var form = paper.GetComponent<DeskOperationalDocument>();
        if (form) Call(form, "AdvanceTime", 2f);
        Call(paper, "Animate", 3f);
        Check(interactor.InspectedObject == paper, "Could not inspect " + paper.name);
    }
    static void Capture(string file)
    {
        ScreenCapture.CaptureScreenshot(Path.Combine(Output, file + ".png"));
        frames = 0;
    }
    static void Tick()
    {
        if (done) return;
        if (EditorApplication.timeSinceStartup - started > 180) { Finish(false, "Timeout phase " + phase); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating || Time.frameCount < 8) return;
        try
        {
            if (phase == 0)
            {
                camera = Camera.main; gate = camera.GetComponent<SceneInputGate>(); view = camera.GetComponent<DeskScenePreview>();
                interactor = camera.GetComponent<SceneObjectInteractor>(); hud = camera.GetComponent<DeskViewHud>();
                records = camera.GetComponent<PlaintextArchiveController>(); archive = records.archive;
                dialogue = camera.GetComponent<DialogueController>(); delivery = camera.GetComponent<SealedDeliveryController>();
                camera.GetComponent<PrologueSequence>().Stop(); dialogue.Stop();
                camera.GetComponent<DialogueTestConsole>().SetOpen(false); view.followMouse = false;
                foreach (var memory in UnityEngine.Object.FindObjectsOfType<FirstInteractionMemory>(true)) memory.enabled = false;
                hints = new DeskCommandHints(camera.gameObject);
                Check(hud.showCommands && hud.labelFont, "Existing saved scene did not enable hint HUD/font");
                Expect("候执事");
                Check(delivery.BeginDelivery(1), "Delivery rejected"); Hidden("delivery animation");
                for (int i = 0; i < 8 && delivery.State != SealedDeliveryController.Phase.AwaitingPickup; i++)
                { Call(view, "AnimateView", 5f, Vector2.zero); Call(delivery.transferDrum, "Advance", 5f); Step(delivery, 5); }
                Expect("收取"); Ungate(); Check(delivery.CollectPacket(), "Pickup rejected"); Hidden("packet flight");
                for (int i = 0; i < 10 && delivery.IsActive; i++) { Step(delivery, 5); Call(delivery.transferDrum, "Advance", 5f); }
                Check(!delivery.IsActive && archive.IsAvailable(1), "Packet never landed");
                Expect("师父封袋");
                gate.SetConsole(true); Hidden("console"); gate.SetConsole(false);
                gate.SetDialogue(true); Hidden("dialogue"); gate.SetDialogue(false);
                gate.SetStory(true); Hidden("story"); gate.SetStory(false);
                gate.SetPropAction(true); Hidden("bell animation"); gate.SetPropAction(false);
                Check(!hud.BlocksPointer(new Vector2(Screen.width - 80, Screen.height - 60)), "Hint panel blocks desk input");
                gate.SetWorkbenchTyping(true); Expect("师父封袋", "Esc"); gate.SetWorkbenchTyping(false);
                Inspect(archive.acts[0].documents[1]); Expect("师父封袋", "Esc"); interactor.CancelInteraction();
                Ungate(); view.SetView(DeskScenePreview.View.Desktop); Call(view, "AnimateView", 5f, Vector2.zero);
                phase = 1; Capture("act1-command"); return;
            }
            if (++frames < 20) return;
            if (phase == 1)
            {
                Step(records); records.workbench.SetPlaintext("KEEP"); Step(records); Step(records); Expect("师父封袋");
                Solve("caesar"); Expect("译文已留存");
                var first = camera.GetComponent<FirstActFlow>(); first.enabled = false;
                Check(archive.CompleteAct(1) && archive.ReceiveAct(2), "Cannot enter second act");
                Expect("数字便条");
                archive.ShowActOnDesk(1); Expect("取回当前案卷", "第二幕"); archive.ShowActOnDesk(2);
                Solve("book");
                var second = camera.GetComponent<SecondActFlow>(); Step(second); ReadAll();
                Check(second.State == SecondActFlow.Phase.BellReady, "Real book solution did not unlock bell");
                Expect("摇铃", "手铃");
                Inspect(archive.acts[1].documents[0]); Expect("摇铃", "Esc"); interactor.CancelInteraction();
                Ungate(); Check(second.bell.TryInteract(), "Real bell refused hint's action");
                for (int i = 0; i < 12 && second.bell.IsBusy; i++) Step(second.bell, 5);
                Check(second.State == SecondActFlow.Phase.RequestPending, "Bell did not advance request");
                Expect("候执事");
                Step(second); ReadAll(); Step(second, 10); Step(second); ReadAll();
                Check(archive.ReceiveSupplement(2), "Cannot receive supplement"); Step(second);
                Expect("猪圈密文"); Solve("pigpen");
                Step(second, 5); Step(second, 5); ReadAll(); Expect("原始档案");
                second.enabled = false; Check(archive.CompleteAct(2) && archive.ReceiveAct(3), "Cannot enter third act");
                Expect("军情密文"); Solve("vigenere");
                var third = camera.GetComponent<ThirdActFlow>(); Step(third); Hidden("signature dialogue"); ReadAll();
                Expect("签署明文还原", "文书架");
                archive.ShowActOnDesk(1); Expect("签署明文还原"); archive.ShowActOnDesk(3);
                Inspect(third.verification.GetComponent<SceneObject>()); Expect("签署明文还原", "点击核验表");
                Ungate(); Check(third.verification.TrySign(), "Hint's signature action rejected");
                Expect("核验已签"); interactor.CancelInteraction();
                third.enabled = false;
                Check(archive.CompleteAct(3) && archive.ReceiveAct(4), "Cannot enter fourth act");
                Expect("双栏密信");
                Solve("rail"); Expect("仿射密文");
                Solve("affine"); Expect("两段译文已留存");
                // Exercise the opposite remaining-cipher state without registering an existing record twice.
                records.entries.Single(e => e.id == "rail").saved = false;
                Expect("栅栏密文"); records.entries.Single(e => e.id == "rail").saved = true;
                camera.GetComponent<FourthActFlow>().enabled = false;
                Check(archive.CompleteAct(4) && archive.ReceiveAct(5), "Cannot enter fifth act");
                var fifth = camera.GetComponent<FifthActFlow>(); Set(fifth, "State", FifthActFlow.Phase.Choosing);
                Expect("摇铃交卷", "自述处置");
                Inspect(fifth.verification.GetComponent<SceneObject>()); Expect("来源担保", "是否"); interactor.CancelInteraction();
                Inspect(fifth.transferApplication); Expect("原件去向"); interactor.CancelInteraction();
                Inspect(fifth.confession); Expect("自述明文", "可保留"); interactor.CancelInteraction();
                Set(fifth, "ConfessionBurned", true); Expect("摇铃交卷", "自述明文已毁"); Set(fifth, "ConfessionBurned", false);
                Ungate(); view.SetView(DeskScenePreview.View.Desktop); Call(view, "AnimateView", 5f, Vector2.zero);
                phase = 2; Capture("act5-command"); return;
            }
            if (phase == 2)
            {
                Check(archive.CompleteAct(5), "Cannot finish archive"); Hidden("epilogue");
                Finish(true, "PASS: " + checks + " checks. Unity 2022.3.62f2 Play Mode.\n"
                    + "Real packet pickup, partial/full plaintext, both second-act bell cues, third-act signature, either remaining fourth-act cipher, fifth-act neutral choices; "
                    + "old dossier recall, inspection/typing escape cues, modal suppression, epilogue, Chinese glyphs and existing-scene activation.\n\n" + Report);
            }
        }
        catch (Exception error) { Finish(false, "Phase " + phase + ": " + error); }
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
