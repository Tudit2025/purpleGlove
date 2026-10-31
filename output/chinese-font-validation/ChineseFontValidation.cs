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
public static class ChineseFontValidation
{
    const string Key = "ChineseFontValidation.Play";
    static readonly string Output = Path.GetFullPath("../../output/chinese-font-validation");
    static int checks;
    static double started;
    static bool done;
    static Camera camera;
    static SceneObjectInteractor interactor;
    static SceneInputGate gate;
    static DeskScenePreview view;
    static ChineseFontValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    static TextMesh Ink(Transform parent, string name) => parent.Find(name).GetComponent<TextMesh>();
    static void FontIs(TextMesh text, string name) => Check(text.font.name.Contains(name), text.name + " font: " + text.font.name);
    static void Glyphs(TextMesh text, string value = null)
    {
        string missing = new string((value ?? text.text).Where(c => !char.IsWhiteSpace(c) && !text.font.HasCharacter(c)).Distinct().ToArray());
        Check(missing.Length == 0, text.name + " missing glyphs: " + missing);
    }
    static void Within(TextMesh text, Transform paper, float width, float height)
    {
        text.font.RequestCharactersInTexture(text.text, text.fontSize);
        var bounds = text.GetComponent<Renderer>().localBounds;
        for (int i = 0; i < 8; i++)
        {
            var p = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            p = paper.InverseTransformPoint(text.transform.TransformPoint(p));
            Check(Mathf.Abs(p.x) < width / 2 + 0.001f && Mathf.Abs(p.z) < height / 2 + 0.001f, paper.name + "/" + text.name + " outside paper: " + p);
        }
    }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        try
        {
            DeskSceneBuilder.ApplyChineseDocumentFonts();
            var first = UnityEngine.Object.FindObjectsOfType<TextMesh>(true).ToDictionary(t => PathOf(t.transform), t => t.text);
            DeskSceneBuilder.ApplyChineseDocumentFonts();
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            var second = UnityEngine.Object.FindObjectsOfType<TextMesh>(true).ToDictionary(t => PathOf(t.transform), t => t.text);
            Check(first.Count == second.Count && first.All(kv => second[kv.Key] == kv.Value), "Repeated installer changed text/duplicated labels");
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            Check(archive.acts.Select(a => a.documents.Length).SequenceEqual(new[] { 2, 7, 8, 9, 8 }), "Original evidence changed");
            FontIs(Ink(archive.acts[0].documents[0].transform, "Body"), "SourceHan");
            FontIs(Ink(archive.acts[0].documents[0].transform, "Ducal letterhead"), "Garamond");
            FontIs(Ink(archive.acts[0].documents[1].transform, "Seven petal note"), "WenKai");
            FontIs(Ink(archive.acts[0].documents[1].transform, "Cipher"), "Garamond");
            FontIs(Ink(archive.acts[1].documents[2].transform, "Body"), "Zhuque");
            FontIs(Ink(archive.acts[1].documents[1].transform, "Body"), "SourceHan");
            FontIs(Ink(archive.acts[3].documents[4].transform, "Body"), "WenKai");
            FontIs(Ink(archive.acts[3].documents[3].transform, "Left Body"), "SourceHan");
            var notebook = UnityEngine.Object.FindFirstObjectByType<NotebookBook>();
            foreach (var text in new[] { notebook.leftPage, notebook.rightPage, notebook.leafFront, notebook.leafBack }) FontIs(text, "WenKai");
            foreach (var spread in notebook.spreads) { Glyphs(notebook.leftPage, spread.left); Glyphs(notebook.rightPage, spread.right); }
            foreach (var act in archive.acts)
                foreach (var document in act.documents)
                    foreach (var text in document.GetComponentsInChildren<TextMesh>(true)) Glyphs(text);
            foreach (string name in new[] { "Book_Charcoal", "Book_SevenPetal" })
            {
                var book = GameObject.Find(name).transform;
                foreach (var text in book.GetComponentsInChildren<TextMesh>(true)) { FontIs(text, "Zhuque"); Glyphs(text); Within(text, book, 0.32f, 0.44f); }
                Check(book.GetComponentsInChildren<TextMesh>(true).Length == 2, "Cover labels duplicated");
            }
            var records = Camera.main.GetComponent<PlaintextArchiveController>();
            Check(records.entries.Length == 6 && records.entries.All(e => !e.document.gameObject.activeSelf), "Unearned records revealed");
            foreach (var entry in records.entries)
            {
                foreach (string name in new[] { "Translation", "Record title", "Record act" }) { var text = Ink(entry.document.transform, name); FontIs(text, "WenKai"); Glyphs(text); }
                FontIs(entry.plaintextInk, "Garamond");
            }
            SessionState.SetInt(Key + ".Checks", checks); SessionState.SetBool(Key, true);
            Install(); EditorApplication.isPlaying = true;
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;
    static void Install()
    {
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
    }
    static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Finish(false, message + "\n" + stack); }
    static void Inspect(SceneObject item)
    {
        interactor.CancelInteraction();
        typeof(SceneInputGate).GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gate, -1);
        item.gameObject.SetActive(true);
        view.SetView(DeskScenePreview.View.Desktop); Call(view, "AnimateView", 10f, Vector2.zero);
        interactor.Inspect(item); Call(item, "Animate", 2f);
        Check(interactor.InspectedObject == item, "Cannot inspect " + item.name);
        camera.Render();
    }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup - started > 90) { Finish(false, "Timeout"); return; }
        if (done || !EditorApplication.isPlaying || EditorApplication.isCompiling || Time.frameCount < 12) return;
        try
        {
            camera = Camera.main; interactor = camera.GetComponent<SceneObjectInteractor>();
            gate = camera.GetComponent<SceneInputGate>(); view = camera.GetComponent<DeskScenePreview>(); view.followMouse = false;
            foreach (var memory in UnityEngine.Object.FindObjectsOfType<FirstInteractionMemory>(true)) memory.enabled = false;
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            var notebook = UnityEngine.Object.FindFirstObjectByType<NotebookBook>();
            Inspect(notebook.GetComponent<SceneObject>());
            for (int spread = 0; spread < notebook.SpreadCount; spread++)
            {
                foreach (var text in new[] { notebook.leftPage, notebook.rightPage }) Within(text, notebook.transform, 0.62f, 0.444f);
                if (spread == 0) Capture("01-master-notebook");
                if (spread == 5) Capture("02-notebook-affine-lesson");
                if (spread + 1 < notebook.SpreadCount)
                {
                    Check(notebook.TurnPage(1), "Cannot turn notebook page");
                    FontIs(notebook.leafFront, "WenKai"); FontIs(notebook.leafBack, "WenKai");
                    Call(notebook, "AdvanceTurn", 1f);
                }
            }
            interactor.CancelInteraction();
            foreach (var act in archive.acts)
                foreach (var document in act.documents)
                {
                    document.gameObject.SetActive(true);
                    foreach (var text in document.GetComponentsInChildren<TextMesh>(true))
                        Within(text, document.transform, act == archive.acts[0] ? (document == act.documents[0] ? 0.42f : 0.35f) : 0.62f,
                            act == archive.acts[0] ? (document == act.documents[0] ? 0.60f : 0.27f) : 0.36f);
                    document.gameObject.SetActive(false);
                }
            Inspect(archive.acts[0].documents[0]); Capture("03-ducal-commission");
            Inspect(archive.acts[0].documents[1]); Capture("04-master-slip");
            Inspect(archive.acts[1].documents[2]); Capture("05-printed-ledger");
            Inspect(archive.acts[3].documents[4]); Capture("06-master-letter");
            foreach (string name in new[] { "Book_Charcoal", "Book_SevenPetal" })
            { Inspect(GameObject.Find(name).GetComponent<SceneObject>()); Capture("07-" + name); }
            var records = camera.GetComponent<PlaintextArchiveController>();
            foreach (var entry in records.entries)
            {
                entry.document.gameObject.SetActive(true); entry.plaintextInk.text = PlaintextArchiveController.WrapAnswer(entry.answer);
                foreach (var text in entry.document.GetComponentsInChildren<TextMesh>()) Within(text, entry.document.transform, 0.62f, 0.36f);
            }
            Inspect(records.entries.Last().document); Capture("08-preserved-plaintext");
            var chinese = UnityEngine.Object.FindObjectsOfType<TextMesh>(true).Where(t => t.font.name.Contains("WenKai") || t.font.name.Contains("Zhuque")).ToArray();
            // Force dynamic font atlas growth, then verify all active world-ink materials follow it.
            foreach (var font in chinese.Select(t => t.font).Distinct())
                font.RequestCharactersInTexture(string.Concat(notebook.spreads.Select(s => s.left + s.right)), 128);
            camera.Render();
            foreach (var text in chinese.Where(t => t.gameObject.activeInHierarchy))
            {
                var material = text.GetComponent<Renderer>().sharedMaterial;
                Check(material.shader.name == "PurpleGlove/World Document Text", "Missing world ink shader");
                Check(material.mainTexture == text.font.material.mainTexture, "Stale dynamic font atlas");
            }
            Finish(true, "PASS: " + (checks + SessionState.GetInt(Key + ".Checks", 0)) + " assertions, Unity Play Mode.\n"
                + "Font assignments, unchanged ciphertext and hidden records; repeat install + scene reload; Chinese glyph coverage.\n"
                + "All 34 original papers and six records fit physical pages; notebook pages/turning faces; dynamic atlas refresh.\n"
                + "Nine actual inspection renders across handwritten, printed and official typography.\n");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Finish(bool success, string message)
    {
        if (done) return; done = true; SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Output + "/validation.txt", message);
        if (success) Debug.Log("CHINESE_FONT_VALIDATION_PASS"); else Debug.LogError(message);
        EditorApplication.Exit(success ? 0 : 1);
    }
    static void Capture(string name)
    {
        var target = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32);
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally { camera.targetTexture = oldTarget; RenderTexture.active = oldActive; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(image); }
    }
}
