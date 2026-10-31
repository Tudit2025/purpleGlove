using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PurpleGlove;
using PurpleGlove.Editor;

[InitializeOnLoad]
public static class ChineseFontPlaintextRegression
{
    const string Key = "ChineseFontPlaintextRegression.Play";
    static readonly string Output = Path.GetFullPath("../../output/chinese-font-validation/plaintext-regression");
    static int checks, completed;
    static double started;
    static bool done;
    static Camera camera;
    static PlaintextArchiveController records;
    static CipherWorkbench work;
    static ActArchiveController archive;
    static DialogueController dialogue;
    static SceneInputGate gate;
    static SceneObjectInteractor interactor;
    static DeskScenePreview view;
    static ChineseFontPlaintextRegression() { if (SessionState.GetBool(Key, false)) Install(); }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    static object Call(object value, string method, params object[] args) => value.GetType().GetMethod(method,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(value, args);
    static void Ungate() => typeof(SceneInputGate).GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gate, -1);
    static void Step(float time = 1f) => Call(records, "AdvanceTime", time);
    static void SetDraft(string text) { work.SetPlaintext(text); Step(); Step(); }
    static void EndLine()
    {
        Check(dialogue.ActiveEvent == records.completionLine, "Missing completion monologue");
        Call(dialogue, "AdvanceTime", 1f); dialogue.Advance(); dialogue.Advance(); Ungate();
    }
    static void Receive(int act)
    {
        Check(archive.ReceiveAct(act), "Cannot receive act " + act); Ungate(); Step();
        Check(records.WritingAct == act && work.Plaintext.Length == 0, "Draft not separated for act " + act);
    }
    static void Inspect(SceneObject item)
    {
        interactor.CancelInteraction(); Ungate(); view.SetView(DeskScenePreview.View.Desktop);
        Call(view, "AnimateView", 10f, Vector2.zero);
        interactor.Inspect(item); Call(item, "Animate", 2f);
        Check(interactor.InspectedObject == item, "Saved record cannot be inspected");
    }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        DeskSceneBuilder.InstallPlaintextArchives(); DeskSceneBuilder.InstallPlaintextArchives();
        EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
        var installed = Camera.main.GetComponent<PlaintextArchiveController>();
        Check(installed.entries.Length == 6 && installed.entries.All(e => e.document && e.act >= 1 && e.act <= 4), "Missing or fifth-act records");
        Check(installed.entries.All(e => !e.document.gameObject.activeSelf && e.plaintextInk.text.Length == 0), "Solution visible before completion");
        Check(installed.archive.acts.Sum(a => a.documents.Length) == 34, "Installer registered unearned records");
        Check(UnityEngine.Object.FindObjectsOfType<PlaintextArchiveController>(true).Length == 1, "Reinstall duplicated controller");
        Check(installed.workbench.plaintextInk.characterSize > 0.0013f * 1.6f, "Plaintext font not enlarged");
        foreach (var entry in installed.entries)
        {
            Check(PlaintextArchiveController.Normalize(entry.answer).Length > 0, "Empty expected plaintext");
            foreach (var text in entry.document.GetComponentsInChildren<TextMesh>(true))
                foreach (char c in text.text.Where(c => !char.IsWhiteSpace(c)).Distinct()) Check(text.font.HasCharacter(c), "Missing saved-paper glyph " + c);
        }
        foreach (char c in installed.completionLine.lines[0].text) Check(Camera.main.GetComponent<DialogueOverlay>().font.HasCharacter(c), "Missing completion glyph " + c);
        SessionState.SetInt(Key + ".Checks", checks); SessionState.SetBool(Key, true);
        Install(); EditorApplication.isPlaying = true;
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
        if (EditorApplication.timeSinceStartup - started > 90) { Finish(false, "Timeout"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.frameCount < 12 || done) return;
        try
        {
            camera = Camera.main; records = camera.GetComponent<PlaintextArchiveController>();
            work = records.workbench; archive = records.archive; dialogue = records.dialogue;
            gate = camera.GetComponent<SceneInputGate>(); interactor = camera.GetComponent<SceneObjectInteractor>();
            view = camera.GetComponent<DeskScenePreview>(); view.followMouse = false;
            dialogue.eventCompleted.AddListener(id => { if (id == records.completionLine.id) completed++; });
            Ungate(); Step();
            SetDraft("KEEP THE ORIGINAL"); Check(!records.entries.Any(e => e.saved), "Saved before delivery");
            Receive(1);
            foreach (string wrong in new[] { "KEEP THE ORIGINA", "KEEP THE ORIGINALX", "XKEEP THE ORIGINAL", "KEEP123THEORIGINAL", "保留原件" })
            { SetDraft(wrong); Check(!records.entries[0].saved && !dialogue.IsActive, "Incomplete/wrong plaintext accepted: " + wrong); }
            SetDraft("");
            Check(work.SelectTool(CipherWorkbench.Tool.Caesar), "Cannot select Caesar");
            Call(work, "AnimateToolTransfer", 1f); Call(work, "AnimateToolTransfer", 1f); Ungate();
            view.SetView(DeskScenePreview.View.DecodeFocus); Call(view, "AnimateView", 10f, Vector2.zero);
            work.SetCaesarShift(3);
            foreach (char c in "NHHS WKH RULJLQDO")
            {
                var key = new Event { type = EventType.KeyDown, character = c, keyCode = c == ' ' ? KeyCode.Space : (KeyCode)char.ToLowerInvariant(c) };
                Check((bool)Call(work, "HandleDecodeKey", key), "Keyboard rejected " + c);
            }
            Step(); Step(); Check(!records.entries[0].saved && work.HasPendingPlaintextStamps, "Saved before letter flight");
            Call(work, "AnimateLetterStamps", 0.16f); Step(); Check(!records.entries[0].saved, "Interrupted returning tile");
            Call(work, "AnimateLetterStamps", 0.3f); Step();
            Check(records.entries[0].saved && archive.acts[0].documents.Length == 3, "Caesar did not become act file");
            Check(view.CurrentView == DeskScenePreview.View.DecodeFocus, "Completion moved camera");
            Call(dialogue, "AdvanceTime", 1f); dialogue.Advance(); Capture("01-completed-writing");
            dialogue.Advance(); Ungate();
            string first = records.entries[0].plaintextInk.text; int instance = records.entries[0].document.GetInstanceID();
            SetDraft("keep the original!!!"); Check(!dialogue.IsActive && archive.acts[0].documents.Length == 3, "Duplicate first-act record");
            SetDraft("EDITED DRAFT"); Check(records.entries[0].plaintextInk.text == first, "Editing draft overwrote saved record");
            Inspect(records.entries[0].document); Capture("02-preserved-first-act"); interactor.CancelInteraction();
            Check(archive.CompleteAct(1), "Cannot archive first act"); Ungate(); Step();
            Check(!records.entries[0].document.gameObject.activeSelf, "Saved paper not shelved");

            Receive(2); SetDraft("request case seven");
            Check(records.entries[1].saved && !records.entries[2].saved, "Book result mismatch"); EndLine();
            SetDraft("request case seven\nleft glove");
            Check(records.entries[2].saved && !dialogue.IsActive && archive.acts[1].documents.Length == 9, "Second result or per-act announcement mismatch");
            Check(records.entries[2].writtenText == "left glove", "Second paper captured previous result");
            SetDraft("LEFT GLOVE"); Check(archive.acts[1].documents.Length == 9 && !dialogue.IsActive, "Second result duplicated");
            Check(archive.CompleteAct(2), "Cannot complete act two"); Ungate(); Step();

            Receive(3); SetDraft("KEEP THE ORIGINAL"); Check(!records.entries[3].saved, "Wrong act accepted answer");
            gate.SetConsole(true); work.SetPlaintext("france will cross at dawn."); Step(); Step();
            Check(!records.entries[3].saved, "Completion interrupted console"); gate.SetConsole(false); Ungate(); Step();
            Check(records.entries[3].saved, "Queued correct answer lost"); EndLine();
            Check(archive.CompleteAct(3), "Cannot complete act three"); Ungate(); Step();

            Receive(4); SetDraft("NORTHGATESAINTMARTIN"); Check(records.entries[4].saved, "Unspaced affine answer rejected"); EndLine();
            SetDraft(records.entries[5].answer.ToLowerInvariant());
            Check(records.entries[5].saved && !dialogue.IsActive && archive.acts[3].documents.Length == 11, "Rail record missing/repeated announcement");
            Check(PlaintextArchiveController.Normalize(records.entries[5].answer).Length == 82, "Rail plaintext length changed");
            Inspect(records.entries[5].document); Capture("03-preserved-long-plaintext"); interactor.CancelInteraction();
            Check(archive.CompleteAct(4), "Cannot complete act four"); Ungate(); Step();
            Receive(5); SetDraft("KEEP THE ORIGINAL");
            Check(archive.acts[4].documents.Length == 8 && !dialogue.IsActive && completed == 4, "Fifth act generated file or wrong announcement count");

            Check(archive.ShowActOnDesk(1), "Cannot revisit first act"); Ungate(); Step();
            Check(work.Plaintext == "EDITED DRAFT" && records.DraftForAct(5) == "KEEP THE ORIGINAL", "Act drafts leaked across chapters");
            Check(records.entries[0].document.GetInstanceID() == instance && records.entries[0].plaintextInk.text == first, "Shelving lost original saved content");
            Check(records.entries[0].document.gameObject.activeSelf && archive.acts.Sum(a => a.documents.Length) == 40, "Saved files not in archive");
            SetDraft("KEEP THE ORIGINAL"); Check(completed == 4 && !dialogue.IsActive, "Revisit repeated completion line");

            // Both Latin wide glyphs and Chinese must fit the enlarged physical writing region.
            view.SetView(DeskScenePreview.View.DecodeFocus); Call(view, "AnimateView", 10f, Vector2.zero);
            work.SetPlaintext(new string('W', 100) + "\n保留原件，核验内容与来源。\n" + new string('M', 90));
            string fullDraft = work.Plaintext;
            Check(work.plaintextInk.text.Split('\n').Length <= 3, "Enlarged writing used too many lines");
            foreach (string line in work.plaintextInk.text.Split('\n'))
            {
                float width = 0;
                foreach (char c in line) if (work.plaintextInk.font.GetCharacterInfo(c, out var glyph, work.plaintextInk.fontSize)) width += glyph.advance * work.plaintextInk.characterSize / 10;
                Check(width <= 0.551f, "Enlarged line exceeds physical width");
            }
            Check(work.plaintextInk.GetComponent<Renderer>().localBounds.size.y < 0.116f, "Enlarged text exceeds writing height");
            Check(work.Plaintext == fullDraft && work.Plaintext.Length > work.plaintextInk.text.Length, "Scrolling discarded full draft");
            Capture("04-enlarged-writing-wrap");
            work.SetPlaintext("M"); float glyphHeight = work.plaintextInk.GetComponent<Renderer>().localBounds.size.y;
            work.SetPlaintext("M\nM\nM");
            float expectedY = work.plaintextInk.GetComponent<Renderer>().localBounds.min.y + glyphHeight * 0.5f;
            var stampWorld = (Vector3)Call(work, "PlaintextStampPosition");
            float stampY = work.plaintextInk.transform.InverseTransformPoint(stampWorld).y;
            Check(Mathf.Abs(stampY - expectedY) < 0.003f, "Stamp misaligned with large third-line glyph: " + stampY + " expected " + expectedY);
            foreach (var entry in records.entries)
            {
                Check(entry.saved, "Unregistered solution " + entry.id);
                foreach (var text in entry.document.GetComponentsInChildren<TextMesh>(true))
                {
                    var bounds = text.GetComponent<Renderer>().localBounds;
                    for (int i = 0; i < 8; i++)
                    {
                        var p = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        p = entry.document.transform.InverseTransformPoint(text.transform.TransformPoint(p));
                        Check(Mathf.Abs(p.x) < 0.31f && Mathf.Abs(p.z) < 0.18f, "Saved text outside paper: " + entry.id + "/" + text.name);
                    }
                }
            }
            Finish(true, "PASS: " + (checks + SessionState.GetInt(Key + ".Checks", 0)) + " assertions in Unity 2022.3.62f2 Play Mode.\n"
                + "Six solutions in acts 1-4; exact normalized matching, no fifth-act record, one completion dialogue per act.\n"
                + "Actual Caesar keyboard and letter flights; wait for return; wrong/incomplete text rejected; multi-result and console defer.\n"
                + "Original record identity/content retained through shelving/revisit; separate drafts; repeated answers do not duplicate.\n"
                + "Larger text, glyph-width wrapping, three visible lines, full draft retained, stamp alignment, saved-paper bounds and Chinese glyphs.\n"
                + "Synthetic keyboard/frame replay, not OS input testing.\n");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Finish(bool success, string message)
    {
        if (done) return; done = true; SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Output + "/validation.txt", message);
        if (success) Debug.Log("PLAINTEXT_ARCHIVE_VALIDATION_PASS"); else Debug.LogError(message);
        EditorApplication.Exit(success ? 0 : 1);
    }
    static void Capture(string name)
    {
        var target = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32);
        var oldTarget = camera.targetTexture; var active = RenderTexture.active;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render();
            if (dialogue.IsActive)
            {
                var overlay = dialogue.Overlay.OverlayCamera; var oldOverlay = overlay.targetTexture;
                overlay.targetTexture = target; Canvas.ForceUpdateCanvases(); overlay.Render(); overlay.targetTexture = oldOverlay;
            }
            RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally { camera.targetTexture = oldTarget; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(image); }
    }
}
