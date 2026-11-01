using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PurpleGlove;
using PurpleGlove.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Copied into the isolated validation project's Editor folder; not included in the game.
[InitializeOnLoad]
public static partial class OperationalRackValidation
{
    const string Key = "OperationalRackValidation.Play";
    static readonly string Output = Path.GetFullPath("../../output/operational-rack-validation");
    static int checks, received, deliveries, completed, entered;
    static bool done;
    static double started;
    static Camera camera;
    static ThirdActFlow flow;
    static ActArchiveController archive;
    static DialogueController dialogue;
    static SealedDeliveryController delivery;
    static PlaintextArchiveController records;
    static CipherWorkbench work;
    static DeskScenePreview view;
    static SceneInputGate gate;
    static SceneObjectInteractor interactor;
    static OperationalRackValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static object Call(object target, string name, params object[] args) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    static void Ungate() => typeof(SceneInputGate).GetField("ignoreThroughFrame", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(gate, -1);
    static void Step(float seconds = 0)
    {
        if (!dialogue.IsActive && dialogue.eyeShutter.IsMoving) Call(dialogue.eyeShutter, "Advance", 3f);
        Ungate(); Call(flow, "AdvanceTime", seconds);
    }
    static void Record() { Ungate(); Call(records, "AdvanceTime", 1f); }

    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Output);
            DeskSceneBuilder.InstallOperationalDocumentRack();
            var first = Camera.main.GetComponent<ThirdActFlow>();
            var originalEvents = new[] { first.charles, first.cue, first.request, first.secretary, first.carafa, first.summary };
            string originalLine = first.cue.lines[0].text;
            first.cue.lines[0].text = originalLine + "（安装保留验证）"; EditorUtility.SetDirty(first.cue); AssetDatabase.SaveAssets();
            DeskSceneBuilder.InstallOperationalDocumentRack();
            var installed = Camera.main.GetComponent<ThirdActFlow>();
            Check(installed && installed.records && installed.verification && installed.verification.flow == installed, "Missing flow wiring");
            Check(installed.cue.lines[0].text.EndsWith("（安装保留验证）"), "Installer overwrites edited dialogue");
            installed.cue.lines[0].text = originalLine; EditorUtility.SetDirty(installed.cue); AssetDatabase.SaveAssets();
            Check(UnityEngine.Object.FindObjectsOfType<ThirdActFlow>(true).Length == 1, "Duplicate flow");
            Check(installed.archive.acts[2].documents.Length == 5 && installed.archive.acts[2].deferredDocuments.Length == 1, "Wrong packet grouping");
            Check(installed.records.entries.Single(e => e.id == "vigenere").sourceDocument == installed.archive.acts[2].documents[1], "Stale cipher source");
            foreach (var sequence in originalEvents)
            {
                Check(sequence.TryValidate(out _), "Invalid dialogue " + sequence.id);
                Check(installed.dialogue.events.Count(e => e == sequence) == 1, "Duplicate event " + sequence.id);
                foreach (char c in string.Concat(sequence.lines.Select(l => l.text)).Where(c => !char.IsWhiteSpace(c)).Distinct())
                    Check(Camera.main.GetComponent<DialogueOverlay>().font.HasCharacter(c), "Missing UI glyph " + c);
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
    static void Inspect(SceneObject paper)
    {
        Ungate(); view.SetView(DeskScenePreview.View.Desktop); Call(view, "AnimateView", 3f, Vector2.zero);
        interactor.Inspect(paper);
        var workingForm = paper.GetComponent<DeskOperationalDocument>();
        if (workingForm) Call(workingForm, "AdvanceTime", 2f);
        Call(paper, "Animate", 3f);
        Check(interactor.InspectedObject == paper, "Inspection rejected");
    }
    static void ResumeDialogue(DialogueEvent sequence, ThirdActFlow.Phase playing)
    {
        Step(); Check(dialogue.ActiveEvent == sequence && flow.State == playing, "Wrong next dialogue " + sequence.id);
        flow.Stop(); Check(flow.State == ThirdActFlow.Phase.Suspended && !dialogue.IsActive, "Dialogue stop failed");
        Check(flow.Resume(), "Cannot resume dialogue"); Step();
        Check(dialogue.ActiveEvent == sequence && flow.State == playing, "Wrong resumed dialogue");
        ReadAll();
    }
    static void Tick()
    {
        if (done) return;
        if (EditorApplication.timeSinceStartup - started > 150) { Finish(false, "Timeout"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.frameCount < 5) return;
        try
        {
            checks = SessionState.GetInt(Key + ".Checks", 0);
            camera = Camera.main; flow = camera.GetComponent<ThirdActFlow>(); archive = flow.archive; dialogue = flow.dialogue;
            records = flow.records; delivery = flow.delivery; view = camera.GetComponent<DeskScenePreview>();
            gate = camera.GetComponent<SceneInputGate>(); work = camera.GetComponent<CipherWorkbench>(); interactor = camera.GetComponent<SceneObjectInteractor>();
            camera.GetComponent<FirstActFlow>().enabled = false; camera.GetComponent<SecondActFlow>().enabled = false;
            flow.story.Stop(); dialogue.Stop(); Ungate();
            VerifyFirstActRack();
            Check(archive.CompleteAct(1) && archive.CompleteAct(2), "Cannot prepare third act");
            var pile = UnityEngine.Object.FindFirstObjectByType<ActCandlePile>();
            Check(flow.story.PlayActTransition(3), "Cannot prepare candle state");
            for (int i = 0; i < 6; i++) Call(flow.story, "AdvanceTime", 10f);
            Check(pile.RemainingCount == 2, "Third-act candle count wrong");
            archive.actReceived.AddListener(a => received++); delivery.deliveryCompleted.AddListener(a => deliveries++);
            archive.actCompleted.AddListener(a => { if (a == 3) completed++; }); flow.story.actEntered.AddListener(a => { if (a == 4) entered++; });
            Check(!flow.verification.CanSign && !flow.verification.TrySign(), "Undelivered form can sign");
            Check(delivery.BeginDelivery(3), "Initial delivery rejected"); ReadyPacket();
            Check(!archive.IsDocumentAvailable(3, archive.acts[2].documents[4]), "French original exposed before landing");
            LandPacket();
            var papers = archive.acts[2].documents.ToArray(); var form = papers[2]; var receipt = papers[3]; var original = papers[4];
            Check(papers.Count(p => p.gameObject.activeSelf) == 4 && !receipt.gameObject.activeSelf && !flow.FilesComplete, "Wrong first-round documents");
            Check(original.gameObject.activeSelf && archive.IsDocumentAvailable(3, original), "French original missing from first packet");
            Check(original.transform.Find("French signature").GetComponent<TextMesh>().text == "Charles", "French signature missing");
            Check(original.transform.Find("French original").GetComponent<TextMesh>().text.Contains("Les troupes françaises franchiront"), "French original text missing");
            var mat = GameObject.Find("WritingMat").transform;
            foreach (var paper in papers.Where(p => p.gameObject.activeSelf && !p.GetComponent<DeskOperationalDocument>()))
            {
                var box = paper.GetComponent<BoxCollider>();
                foreach (float x in new[] { -box.size.x / 2, box.size.x / 2 }) foreach (float z in new[] { -box.size.z / 2, box.size.z / 2 })
                {
                    var point = mat.InverseTransformPoint(paper.transform.TransformPoint(new Vector3(x, 0, z)));
                    Check(Mathf.Abs(point.x) < 0.49f && Mathf.Abs(point.z) < 0.54f, "Document outside mat: " + paper.name);
                }
            }
            Inspect(original); ValidatePaper(original); Capture("00-signed-french-original"); interactor.CancelInteraction();
            Call(view, "AnimateView", 3f, Vector2.zero); Capture("00-first-round-four-documents");
            archive.StoreDeskAct(); archive.ShowActOnDesk(3); Ungate();
            Check(!receipt.gameObject.activeSelf && !archive.IsDocumentAvailable(3, receipt), "Shelving bypasses receipt lock");
            Check(original.gameObject.activeSelf && original.transform.Find("French signature").GetComponent<TextMesh>().text == "Charles", "Shelving lost French original");
            Inspect(form); Check(!flow.verification.TrySign(), "Unsolved form can sign"); interactor.CancelInteraction();
            Record(); work.SetPlaintext("FRANCE WILL CROSS AT DAW"); Record(); Record(); Step();
            Check(!flow.verification.CanSign, "Partial answer permits signature");
            work.SetPlaintext("FRANCE WILL CROSS AT DAWN"); Record(); Record();
            Check(records.entries.Single(e => e.id == "vigenere").saved, "Correct answer not preserved");
            if (dialogue.IsActive) ReadAll(); Step(); flow.verification.RefreshInk();
            Check(flow.State == ThirdActFlow.Phase.WaitingForSignature && flow.verification.CanSign && !flow.verification.Signed, "Solution bypassed signature");
            Check(!flow.verification.TrySign(), "Signature without inspection accepted");
            Check(archive.ShowActOnDesk(1), "Cannot review old act before signing" );
            Inspect(form); flow.verification.RefreshInk(); ValidatePaper(form); Capture("01-ready-to-sign");
            var signPoint = camera.WorldToScreenPoint(form.transform.TransformPoint(new Vector3(0.06f, 0.01f, -0.01f)));
            var sourcePoint = camera.WorldToScreenPoint(form.transform.TransformPoint(new Vector3(0.06f, 0.01f, -0.22f)));
            Check(flow.verification.IsSignatureAtPointer(camera, signPoint) && !flow.verification.IsSignatureAtPointer(camera, sourcePoint), "Signature hit area mixes source/transcript");
            gate.SetConsole(true); Check(!flow.verification.TrySign(), "Console permits signing"); gate.SetConsole(false); Ungate();
            Check(flow.verification.TrySign() && flow.verification.signatureInk.text == "Luca", "Physical signature failed");
            Check(!flow.verification.TrySign(), "Duplicate signing accepted");
            interactor.CancelInteraction(); Step(); Check(dialogue.ActiveEvent == flow.charles, "Signature did not trigger Charles");
            OpenDialogue(); dialogue.Advance(); Capture("02-charles-at-door");
            flow.Stop(); Check(flow.Resume(), "Charles cannot resume"); Step(); ReadAll();
            Check(flow.State == ThirdActFlow.Phase.SupplementPending && !flow.FilesComplete, "Charles dialogue counted as received");
            Step(); ReadyPacket(); Check(!receipt.gameObject.activeSelf, "Receipt visible before pickup");
            flow.Stop(); Check(!flow.FilesComplete && !delivery.IsActive, "Cancelled receipt unlocked files");
            Check(flow.Resume(), "Supplement cannot resume"); Step(); ReadyPacket(); LandPacket();
            Check(flow.FilesComplete && archive.IsDocumentAvailable(3, receipt) && archive.CurrentAct == 3 && pile.RemainingCount == 2, "Supplement alters act/candles");
            Check(received == 1 && deliveries == 2 && !delivery.BeginSupplement(3), "Repeated receipt/act delivery");
            Inspect(receipt); ValidatePaper(receipt); Capture("03-charles-receipt"); Step();
            Check(!dialogue.IsActive, "Cue interrupts receipt inspection"); interactor.CancelInteraction();
            ResumeDialogue(flow.cue, ThirdActFlow.Phase.Cue);
            Check(flow.State == ThirdActFlow.Phase.BellReady && flow.bell.ringAllowed, "Cue did not enable bell");
            flow.Stop(); Check(!flow.bell.ringAllowed && flow.Resume() && flow.bell.ringAllowed, "BellReady cannot resume");
            Ungate(); Check(flow.bell.TryInteract(), "Bell interaction rejected"); Call(flow.bell, "AdvanceTime", 1f); Call(flow.bell, "AdvanceTime", 0.15f);
            Check(flow.State == ThirdActFlow.Phase.RequestPending && flow.bell.StrikeCount == 1, "No real bell strike request");
            Step(); Check(!dialogue.IsActive, "Request starts before bell returned");
            Call(flow.bell, "AdvanceTime", 2f); Call(flow.bell, "AdvanceTime", 1f); Call(flow.bell, "AdvanceTime", 1f);
            ResumeDialogue(flow.request, ThirdActFlow.Phase.Request);
            ResumeDialogue(flow.secretary, ThirdActFlow.Phase.Secretary);
            ResumeDialogue(flow.carafa, ThirdActFlow.Phase.Carafa);
            Check(!archive.IsAvailable(4) && flow.State == ThirdActFlow.Phase.ReturnPending, "Fourth-act originals arrived early");
            Step(); Check(flow.State == ThirdActFlow.Phase.Returning && gate.StoryActive, "No return to desk");
            var pose = camera.transform.position; gate.SetConsole(true); Call(flow, "AdvanceTime", 20f);
            Check(camera.transform.position == pose && flow.State == ThirdActFlow.Phase.Returning, "Console failed to pause return"); gate.SetConsole(false);
            Step(2f); Check(dialogue.ActiveEvent == flow.summary && view.CurrentView == DeskScenePreview.View.Desktop, "Summary not at desk");
            OpenDialogue(); dialogue.Advance(); Capture("04-desk-summary");
            flow.Stop(); Check(!gate.StoryActive && !view.StoryLocked && flow.Resume(), "Summary cancellation leaked locks");
            Step(); Step(2f); ReadAll();
            var completeFiles = archive.acts[2].documents.ToArray(); Check(completeFiles.Length == 6, "Third act not complete");
            Step(); Check(flow.State == ThirdActFlow.Phase.Transition, "No fourth-act transition");
            Call(flow.story, "AdvanceTime", 10f);
            Check(archive.DeskAct == 0 && archive.CurrentAct == 3 && pile.RemainingCount == 1 && completed == 0, "Blackout staging wrong");
            flow.Stop(); Check(archive.DeskAct == 3 && archive.CurrentAct == 3 && pile.RemainingCount == 2, "Cancelled transition lost files/candle");
            Check(flow.verification.Signed && receipt.gameObject.activeSelf && !gate.StoryActive, "Rollback lost signature/receipt");
            Check(flow.Resume(), "Transition cannot resume"); Step();
            for (int i = 0; i < 6; i++) Call(flow.story, "AdvanceTime", 10f);
            Check(archive.CurrentAct == 4 && archive.IsCompleted(3) && flow.State == ThirdActFlow.Phase.Complete && completed == 1 && entered == 1, "Act four did not commit exactly once");
            Check(pile.RemainingCount == 1 && archive.DeskAct == 0, "Final archive/candle state wrong");
            archive.ShowActOnDesk(3); Ungate();
            Check(completeFiles.SequenceEqual(archive.acts[2].documents) && completeFiles.All(p => p.gameObject.activeSelf), "Recall lost third-act files");
            Check(flow.verification.Signed && flow.verification.signatureInk.text == "Luca" && !flow.verification.CanSign, "Recall lost or repeated signature");
            Inspect(form); ValidatePaper(form); Capture("05-signed-form-recalled"); interactor.CancelInteraction();
            Call(view, "AnimateView", 3f, Vector2.zero); Capture("06-complete-third-act-dossier");
            flow.TranscriptSigned(); records.plaintextPreserved.Invoke(3); flow.enabled = false; flow.enabled = true; Step(20);
            Check(flow.State == ThirdActFlow.Phase.Complete && completed == 1 && deliveries == 2 && entered == 1, "Recall replayed flow");
            VerifyFourthActOpening();
            VerifyFinalRack();
            Finish(true, "PASS: " + checks + " assertions. Unity 2022.3.62f2 Play Mode.\n"
                + "Persistent operational rack; first-act transfer application; seven gated forms; desktop-only interaction and return; signature while reviewing an earlier act; completed-act recall without shelving forms; fourth-act opening and two separate sealed visuals; four authored papers; affine paragraph spacing and both cipher solutions; opening/delivery stop-resume; immutable archive and two solved plaintexts; four-document third-act initial packet with signed French original; separate Charles supplement; initial/supplement delivery gates; wrong/partial/correct plaintext; separate signature hit area; no signature on decryption; physical signing; Charles visit and receipt; immutable third-act dossier; real bell strike and putdown; request/secretary/Carafa order; pause/stop/resume; desktop summary; blackout rollback; act-four commit exactly once; recall without replay; document bounds and glyphs.\n");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void ValidatePaper(SceneObject paper)
    {
        var box = paper.GetComponent<BoxCollider>();
        foreach (var ink in paper.GetComponentsInChildren<TextMesh>())
        {
            foreach (char c in ink.text.Where(c => !char.IsWhiteSpace(c)).Distinct()) Check(ink.font.HasCharacter(c), "Missing paper glyph " + c);
            var bounds = ink.GetComponent<Renderer>().localBounds;
            foreach (float x in new[] { bounds.min.x, bounds.max.x }) foreach (float y in new[] { bounds.min.y, bounds.max.y })
            {
                var p = paper.transform.InverseTransformPoint(ink.transform.TransformPoint(new Vector3(x, y, 0)));
                Check(Mathf.Abs(p.x) < box.size.x / 2 + 0.003f && Mathf.Abs(p.z) < box.size.z / 2 + 0.003f, "Ink outside paper " + ink.name);
            }
        }
    }
    static void Capture(string name)
    {
        var target = RenderTexture.GetTemporary(1600, 900, 24); var active = RenderTexture.active;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        var previous = camera.targetTexture; var overlay = dialogue.Overlay.OverlayCamera; var previousOverlay = overlay.targetTexture;
        try
        {
            camera.targetTexture = target; camera.Render();
            if (overlay.enabled) { overlay.targetTexture = target; Canvas.ForceUpdateCanvases(); overlay.Render(); }
            RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
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
