using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using PurpleGlove;
using PurpleGlove.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SecondActValidation
{
    const string Key = "SecondActValidation.Play";
    static readonly string Output = Path.GetFullPath("../../output/second-act-validation");
    static int checks, received, deliveries;
    static bool done;
    static double started;
    static Camera camera;
    static SecondActFlow flow;
    static DialogueController dialogue;
    static SceneInputGate gate;
    static DeskScenePreview view;
    static ActArchiveController archive;
    static CipherWorkbench work;
    static PlaintextArchiveController records;
    static SealedDeliveryController delivery;
    static SecondActValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static object Call(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    static void Ungate() => typeof(SceneInputGate).GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gate, -1);
    static void Step(float seconds = 0) { Ungate(); Call(flow, "AdvanceTime", seconds); }
    static void RecordStep() { Ungate(); Call(records, "AdvanceTime", 1f); }

    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Output);
            DeskSceneBuilder.InstallSecondActProgression(); DeskSceneBuilder.InstallSecondActProgression();
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            var installed = Camera.main.GetComponent<SecondActFlow>();
            Check(installed && installed.records && installed.archive && installed.bell && installed.delivery, "Missing flow wiring");
            Check(UnityEngine.Object.FindObjectsOfType<SecondActFlow>(true).Length == 1, "Duplicate flow");
            Check(installed.archive.acts[1].documents.Length == 3 && installed.archive.acts[1].deferredDocuments.Length == 2, "Expected one initial and two deferred documents");
            Check(installed.records.entries.Single(e => e.id == "book").sourceDocument == installed.archive.acts[1].documents[0], "Book source reference");
            Check(installed.records.entries.Single(e => e.id == "pigpen").sourceDocument == installed.archive.acts[1].documents[2], "Pigpen source reference");
            Check(installed.books.Length == 2 && installed.books.All(b => b.spread && !b.spread.activeSelf && !b.enabled), "Book starting state");
            Check(installed.bell.consumePermissionOnRing, "Bell permission configuration was not persisted on prefab instance");
            Check(!installed.dialogue.events.Single(e => e.id == "beatrice").lines.Any(l => l.text.Contains("第七")), "Intro reveals cipher solution");
            foreach (var sequence in new[] { installed.cue, installed.request, installed.arrival })
            {
                Check(sequence.TryValidate(out _), "Invalid dialogue " + sequence.id);
                Check(installed.dialogue.events.Count(e => e == sequence) == 1, "Duplicate event " + sequence.id);
                foreach (char c in string.Concat(sequence.lines.Select(l => l.text)).Distinct().Where(c => !char.IsWhiteSpace(c)))
                    Check(Camera.main.GetComponent<DialogueOverlay>().font.HasCharacter(c), "Missing UI glyph " + c);
            }
            string Decode(string[] edition) => string.Concat(Regex.Matches(BookCipherText.Cipher, @"(\d+)-(\d+)-(\d+)").Cast<Match>().Select(m =>
            {
                int p = int.Parse(m.Groups[1].Value), l = int.Parse(m.Groups[2].Value), c = int.Parse(m.Groups[3].Value);
                Check(p == 12 || p == 13, "Cipher requires a third page");
                string line = CipherWorkbench.LettersOnly(edition[(p - 12) * 4 + l - 1], 100);
                Check(c > 0 && c <= line.Length, "Invalid coordinate in an edition"); return line[c - 1];
            }));
            Check(Decode(BookCipherText.Flower) == "REQUESTCASESEVEN", "New edition does not decode");
            Check(Decode(BookCipherText.Ordinary) == "CVSLELSKSNOLOHHR", "Old edition mismatch is not reproducible");
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
    static void OpenDialogue()
    {
        if (view.IsEnteringDoorway) Call(view, "AnimateView", 3f, Vector2.zero);
        Call(dialogue, "AdvanceTime", 5f);
        if (dialogue.eyeShutter.IsMoving) Call(dialogue.eyeShutter, "Advance", 3f);
        Call(dialogue, "AdvanceTime", 5f);
    }
    static void ReadAll()
    {
        OpenDialogue();
        for (int n = 0; dialogue.IsActive && !dialogue.IsClosing && n < 100; n++) dialogue.Advance();
        if (dialogue.IsClosing) { Call(dialogue.eyeShutter, "Advance", 3f); Call(dialogue, "AdvanceTime", 0f); }
        Check(!dialogue.IsActive, "Dialogue did not finish"); Ungate();
    }
    static void ReadyPacket()
    {
        for (int i = 0; i < 5 && delivery.State != SealedDeliveryController.Phase.AwaitingPickup; i++)
        { Call(delivery.transferDrum, "Advance", 3f); Call(delivery, "AdvanceTime", 3f); }
        Check(delivery.State == SealedDeliveryController.Phase.AwaitingPickup, "Packet unavailable"); Ungate();
    }
    static void LandPacket()
    {
        Check(delivery.CollectPacket(), "Pickup rejected"); Call(delivery, "AdvanceTime", 3f); Call(delivery, "AdvanceTime", 3f); Ungate();
    }
    static void Tick()
    {
        if (done) return;
        if (EditorApplication.timeSinceStartup - started > 150) { Finish(false, "Timeout"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.frameCount < 5) return;
        try
        {
            checks = SessionState.GetInt(Key + ".Checks", 0);
            camera = Camera.main; flow = camera.GetComponent<SecondActFlow>(); records = flow.records;
            archive = flow.archive; dialogue = flow.dialogue; delivery = flow.delivery;
            view = camera.GetComponent<DeskScenePreview>(); gate = camera.GetComponent<SceneInputGate>(); work = camera.GetComponent<CipherWorkbench>();
            camera.GetComponent<FirstActFlow>().enabled = false;
            camera.GetComponent<PrologueSequence>().Stop(); dialogue.Stop(); Ungate();
            Check(!archive.IsAvailable(2) && !flow.bell.ringAllowed, "Second act initially unlocked");
            Check(!flow.books[0].TryOpen(), "Book acts as tool before note receipt");
            Check(archive.CompleteAct(1), "Could not prepare second act");
            Check(dialogue.Play("beatrice"), "Missing introduction"); ReadAll();
            archive.actReceived.AddListener(a => received++); delivery.deliveryCompleted.AddListener(a => deliveries++);
            Check(delivery.BeginDelivery(2), "Cannot begin first second-act packet"); ReadyPacket();
            Check(archive.acts[1].documents.All(d => !d.gameObject.activeSelf), "Papers exposed before pickup"); LandPacket();
            Call(flow, "Update"); Ungate();
            Check(archive.acts[1].documents.Count(d => d.gameObject.activeSelf) == 1, "First packet must reveal exactly one note");
            Check(archive.HasPendingSupplement(2) && received == 1, "Initial receipt state wrong");
            archive.StoreDeskAct(); archive.ShowActOnDesk(2); Ungate();
            Check(archive.acts[1].documents.Count(d => d.gameObject.activeSelf) == 1, "Shelf bypasses partial unlock");
            RecordStep(); work.SetPlaintext("LEFT GLOVE"); RecordStep(); RecordStep();
            Check(!records.entries.Single(e => e.id == "pigpen").saved, "Unreceived pigpen answer saved"); work.SetPlaintext("");
            var note = archive.acts[1].documents[0]; var notePose = note.transform.localPosition;
            foreach (var book in flow.books)
            {
                var pose = book.transform.localPosition; var scale = book.transform.localScale;
                Ungate(); Check(book.TryOpen(), "Book tool failed to open");
                Check(work.BookTool == book && book.spread.activeSelf && view.CurrentView == DeskScenePreview.View.DecodeFocus, "Book did not enter work area");
                Ungate(); Check(work.UsesDecodeKeyboard && work.KeyboardMapping == "ABCDEFGHIJKLMNOPQRSTUVWXYZ", "Book is not plaintext input");
                Call(view, "AnimateView", 3f, Vector2.zero);
                Capture(book.sevenPetal ? "02-flower-spread" : "01-ordinary-spread");
                if (!book.sevenPetal)
                {
                    work.SetPlaintext("CVSLELSKSNOLOHHR"); RecordStep(); RecordStep();
                    Check(!records.entries.Single(e => e.id == "book").saved, "Wrong edition unlocks retrieval"); work.SetPlaintext("");
                }
                view.SetView(DeskScenePreview.View.Desktop); Call(work, "Update");
                Check(!work.BookTool && !book.spread.activeSelf && book.transform.localPosition == pose && book.transform.localScale == scale,
                    "Book failed to return home on exit");
                Check(note.transform.localPosition == notePose && note.enabled, "Note failed to return home");
            }
            Ungate(); Check(flow.books[1].TryOpen(), "Could not reopen flower edition"); Ungate();
            foreach (char c in "REQUEST CASE SEVEN")
                Check((bool)Call(work, "HandleDecodeKey", new Event { type = EventType.KeyDown, character = c, keyCode = c == ' ' ? KeyCode.Space : (KeyCode)((int)KeyCode.A + c - 'A') }), "Plaintext key rejected");
            Check(work.Plaintext == "REQUEST CASE SEVEN", "Keyboard substituted book plaintext");
            Call(work, "AnimateLetterStamps", 3f); RecordStep(); RecordStep();
            Check(records.entries.Single(e => e.id == "book").saved && flow.State == SecondActFlow.Phase.CuePending, "Book solution did not persist/queue hint");
            Check(!work.BookTool && note.transform.localPosition == notePose, "Preserving plaintext left book/note displaced");
            if (dialogue.IsActive) ReadAll(); Step();
            Check(dialogue.ActiveEvent == flow.cue && !flow.bell.ringAllowed, "Bell unlocked before hint"); ReadAll();
            Check(flow.State == SecondActFlow.Phase.BellReady && flow.bell.ringAllowed, "Hint did not unlock bell");
            Ungate(); Check(flow.bell.TryInteract(), "Bell rejected interaction");
            Call(flow.bell, "AdvanceTime", 1f); Call(flow.bell, "AdvanceTime", 0.15f);
            Check(flow.bell.StrikeCount == 1 && flow.State == SecondActFlow.Phase.RequestPending, "Actual bell strike did not queue request");
            Step(); Check(!dialogue.IsActive, "Dialogue starts while bell is held");
            Call(flow.bell, "AdvanceTime", 2f);
            Check(flow.bell.StrikeCount == 6, "Single-use call cut off the remaining bell strokes");
            Call(flow.bell, "AdvanceTime", 1f); Call(flow.bell, "AdvanceTime", 1f); Step();
            Check(dialogue.ActiveEvent == flow.request, "Door request missing");
            flow.Stop(); Check(flow.State == SecondActFlow.Phase.Suspended && !flow.bell.ringAllowed, "Request cancellation failed");
            Check(flow.Resume(), "Cannot resume request"); Step(); ReadAll();
            Check(flow.State == SecondActFlow.Phase.RetrievalDelay && archive.HasPendingSupplement(2), "Request reveals files before retrieval");
            gate.SetConsole(true); Call(flow, "AdvanceTime", 20f); Check(flow.RetrievalElapsed == 0, "Console failed to pause retrieval"); gate.SetConsole(false);
            Step(3); Check(flow.State == SecondActFlow.Phase.RetrievalDelay, "Retrieval delay too short");
            Step(3); Step(); Check(dialogue.ActiveEvent == flow.arrival, "Arrival visit missing"); ReadAll();
            Check(!dialogue.eyeShutter.AtSecondStop && flow.State == SecondActFlow.Phase.DeliveryPending, "Delivery queued before shutter closed");
            Step(); ReadyPacket(); Check(archive.HasPendingSupplement(2), "Supplement exposed before pickup");
            flow.Stop(); Check(archive.HasPendingSupplement(2) && !delivery.IsActive, "Cancelled delivery unlocks files");
            Check(flow.Resume(), "Supplement cannot resume"); Step(); ReadyPacket(); LandPacket();
            Check(flow.State == SecondActFlow.Phase.WaitingForPigpen && !archive.HasPendingSupplement(2), "Supplement completion failed");
            Check(received == 1 && deliveries == 2 && !delivery.BeginSupplement(2), "Duplicate act receipt/supplement");
            Check(archive.acts[1].documents.Length == 4 && archive.acts[1].documents.All(d => d.gameObject.activeSelf), "Final files or preserved plaintext missing");
            CapturePaper(archive.acts[1].documents[1], "03-ledger"); CapturePaper(archive.acts[1].documents[2], "04-pigpen-scrap");
            work.SetPlaintext("LEFT GLOVE"); RecordStep(); RecordStep();
            Check(records.entries.Single(e => e.id == "pigpen").saved, "Delivered pigpen cannot be preserved");
            if (dialogue.IsActive) ReadAll(); archive.StoreDeskAct(); archive.ShowActOnDesk(2);
            Check(archive.acts[1].documents.Length == 5 && archive.acts[1].documents.All(d => d.gameObject.activeSelf), "Shelving lost new papers or plaintext");
            Check(archive.CurrentAct == 2 && !flow.bell.ringAllowed, "Supplement advanced act or left repeat bell enabled");
            Finish(true, "PASS: " + checks + " assertions. Unity 2022.3.62f2 Play Mode.\n"
                + "One-note initial delivery; both editions and every coordinate; plaintext keyboard; book/note return; unavailable-answer guard; physical bell; request/arrival shutter and delay; stop/resume; two-file supplemental delivery; immutable plaintext and shelving.\n"
                + "Rendered both prayer spreads, official ledger and torn pigpen scrap.\n");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void CapturePaper(SceneObject paper, string name)
    {
        Ungate(); view.SetView(DeskScenePreview.View.Desktop); Call(view, "AnimateView", 3f, Vector2.zero);
        camera.GetComponent<SceneObjectInteractor>().Inspect(paper); Call(paper, "Animate", 3f);
        Capture(name); camera.GetComponent<SceneObjectInteractor>().CancelInteraction();
    }
    static void Capture(string name)
    {
        var target = RenderTexture.GetTemporary(1920, 1080, 24); var active = RenderTexture.active;
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false); var previous = camera.targetTexture;
        camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply(); File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(image);
    }
    static void Finish(bool success, string message)
    {
        if (done) return; done = true; SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/validation.txt", message);
        if (success) Debug.Log(message); else Debug.LogError(message); EditorApplication.Exit(success ? 0 : 1);
    }
}
