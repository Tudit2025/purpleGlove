using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PurpleGlove;
using PurpleGlove.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Copy to Assets/Editor in an isolated project and run -executeMethod CipherReadabilityReview.Run.
[InitializeOnLoad]
public static class CipherReadabilityReview
{
    const string Key = "CipherReadabilityReview.Play";
    static readonly string Output = Path.GetFullPath("../../output/cipher-readability");
    static int checks;
    static bool done;
    static double started;
    static CipherReadabilityReview() { if (SessionState.GetBool(Key, false)) Watch(); }
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static Vector3[] Corners(Bounds b) => Enumerable.Range(0, 8).Select(i => b.center + Vector3.Scale(b.extents,
        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))).ToArray();
    static void InkFits(SceneObject paper, Renderer ink)
    {
        var box = paper.GetComponent<BoxCollider>();
        foreach (var p in Corners(ink.localBounds).Select(v => paper.transform.InverseTransformPoint(ink.transform.TransformPoint(v))))
            Check(Mathf.Abs(p.x) < box.size.x / 2 - 0.003f && Mathf.Abs(p.z) < box.size.z / 2 - 0.003f,
                paper.name + "/" + ink.name + " ink outside paper: " + p);
    }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        try
        {
            Check(Path.GetFullPath(".").Replace('\\', '/').EndsWith("/Temp/guild-letter-layout"), "Use the isolated project.");
            DeskSceneBuilder.ApplyUnifiedCipherTypography();
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            var papers = new[] { archive.acts[0].documents[1], archive.acts[1].documents[0], archive.acts[2].documents[1],
                archive.acts[3].documents[2], archive.acts[3].documents[4] };
            string[] names = { "Cipher", "Coordinates", "Cipher", "Affine cipher", "Rail cipher" };
            Font font = null;
            for (int i = 0; i < papers.Length; i++)
            {
                var ink = papers[i].transform.Find(names[i]).GetComponent<TextMesh>();
                if (!font) font = ink.font;
                Check(ink.font == font && ink.fontSize == 96 && Mathf.Abs(ink.characterSize - 0.002201835f * 1.2f) < 0.0000001f,
                    "Nonuniform cipher typography: " + papers[i].name);
                InkFits(papers[i], ink.GetComponent<Renderer>());
            }
            InkFits(archive.acts[1].documents[2], archive.acts[1].documents[2].transform.Find("Act2_PigpenCipher").GetComponent<Renderer>());
            var affine = papers[3].GetComponentInChildren<TextMesh>(true);
            var rail = papers[4].GetComponentInChildren<TextMesh>(true);
            Check(affine.text == "VAPZR  MIZC  UIWVZ  QIPZWV", "Affine spaces changed.");
            Check(rail.text.Length == 82 && !rail.text.Contains("\n"), "Rail must retain 82 letters in one line.");
            var records = Camera.main.GetComponent<PlaintextArchiveController>();
            var entry = records.entries.Single(e => e.id == "rail");
            Check(entry.sourceDocument == papers[4], "Lost rail source reference.");
            string plain = new string(Enumerable.Range(0, 82).Select(i => rail.text[i % 2 == 0 ? i / 2 : 41 + i / 2]).ToArray());
            Check(plain == PlaintextArchiveController.Normalize(entry.answer), "Rail plaintext changed.");
            var desk = GameObject.Find("WritingDesk").transform;
            var box = papers[4].GetComponent<BoxCollider>();
            foreach (var v in Corners(new Bounds(box.center, box.size)))
            {
                var p = desk.InverseTransformPoint(papers[4].transform.TransformPoint(v));
                Check(Mathf.Abs(p.x) < 1.425f && Mathf.Abs(p.z) < 0.9f, "Long strip extends past desktop.");
            }
            File.WriteAllText(Output + "/layout.txt", string.Join("\n", papers.Select((p, i) => p.name + ": "
                + p.transform.Find(names[i]).GetComponent<TextMesh>().characterSize + "; "
                + p.transform.Find(names[i]).GetComponent<TextMesh>().text)));
            // Save only the generated changes, then exercise actual scene input/archives in Play Mode.
            foreach (var behaviour in Camera.main.GetComponents<MonoBehaviour>())
                if (!(behaviour is DeskScenePreview) && !(behaviour is SceneObjectInteractor) && !(behaviour is CipherWorkbench)
                    && !(behaviour is SceneInputGate) && !(behaviour is PixelSceneView)) behaviour.enabled = false;
            foreach (var memory in UnityEngine.Object.FindObjectsOfType<FirstInteractionMemory>(true)) memory.enabled = false;
            SessionState.SetInt(Key + ".checks", checks);
            SessionState.SetBool(Key, true); Watch();
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(DeskSceneStartup.ScenePath);
            EditorApplication.isPlaying = true;
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Watch() { started = EditorApplication.timeSinceStartup; EditorApplication.update -= Tick; EditorApplication.update += Tick; }
    static void Tick()
    {
        if (done) return;
        if (EditorApplication.timeSinceStartup - started > 100) { Finish(false, "Timed out."); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || !Camera.main || Time.frameCount < 15) return;
        try
        {
            checks = SessionState.GetInt(Key + ".checks", 0);
            var camera = Camera.main;
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            var view = camera.GetComponent<DeskScenePreview>();
            var gate = camera.GetComponent<SceneInputGate>();
            var work = camera.GetComponent<CipherWorkbench>();
            var interactor = camera.GetComponent<SceneObjectInteractor>();
            view.followMouse = false;
            camera.GetComponent<PrologueSequence>().enabled = false;
            gate.SetStory(false); gate.SetDialogue(false); gate.SetConsole(false); gate.SetPropAction(false);
            gate.SetDelivery(false); gate.SetWorkbenchModal(false); gate.SetWorkbenchTyping(false);
            Ungate(gate);
            Check(!archive.IsAvailable(4), "Papers unlocked before delivery.");
            Check(archive.ReceiveAct(4), "Cannot receive fourth act.");
            var paper = archive.acts[3].documents[4];
            Vector3 rest = paper.transform.position;
            Check(paper.gameObject.activeInHierarchy && view.decodeReferencePaper == paper.GetComponent<BoxCollider>(), "Long strip missing after delivery.");
            Check(archive.StoreDeskAct() && archive.ShowActOnDesk(4), "Archive recall failed.");
            Check(Vector3.Distance(rest, paper.transform.position) < 0.0001f, "Recall changed long strip pose.");
            Ungate(gate);
            Check(work.SelectTool(CipherWorkbench.Tool.RailFence), "Cannot take rail tool.");
            Call(work, "AnimateToolTransfer", 1f);
            foreach (var size in new[] { new Vector2Int(1600, 900), new Vector2Int(1280, 720), new Vector2Int(1200, 900), new Vector2Int(2100, 900) })
            {
                camera.aspect = (float)size.x / size.y;
                Ungate(gate); view.SetView(DeskScenePreview.View.Desktop);
                Check(view.SetView(DeskScenePreview.View.DecodeFocus), "Cannot enter decoding view.");
                foreach (bool zoom in new[] { false, true })
                {
                    if (zoom) Call(view, "AdjustFocusZoom", 100f);
                    Call(view, "AnimateView", 10f, Vector2.zero);
                    Visible(camera, paper.GetComponent<BoxCollider>(), 0.06f);
                    Capture(camera, size.x, size.y, "decode-" + size.x + "-" + (zoom ? "zoom" : "full"));
                }
                // Read and put the same physical strip back, with normal hit bounds.
                work.SelectTool(CipherWorkbench.Tool.None); Call(work, "AnimateToolTransfer", 1f);
                Ungate(gate); view.SetView(DeskScenePreview.View.Desktop); Call(view, "AnimateView", 10f, Vector2.zero);
                Ungate(gate); interactor.Inspect(paper); Call(paper, "Animate", 10f);
                Check(interactor.InspectedObject == paper, "Cannot inspect long strip.");
                Visible(camera, paper.GetComponent<BoxCollider>());
                interactor.CancelInteraction();
                Check(Vector3.Distance(rest, paper.transform.position) < 0.0001f, "Putting back changed strip pose.");
                Ungate(gate); work.SelectTool(CipherWorkbench.Tool.RailFence); Call(work, "AnimateToolTransfer", 1f);
            }
            work.SelectTool(CipherWorkbench.Tool.None); Call(work, "AnimateToolTransfer", 1f);
            camera.aspect = 16f / 9;
            archive.previewAllActs = true;
            var cases = new[] { new[] { 1, 1 }, new[] { 2, 0 }, new[] { 2, 2 }, new[] { 3, 1 }, new[] { 4, 2 }, new[] { 4, 4 } };
            foreach (var sample in cases)
            {
                if (!archive.IsAvailable(sample[0])) archive.ReceiveAct(sample[0]);
                archive.ShowActOnDesk(sample[0]);
                var document = archive.acts[sample[0] - 1].documents[sample[1]];
                Ungate(gate); view.SetView(DeskScenePreview.View.Desktop); Call(view, "AnimateView", 10f, Vector2.zero);
                Ungate(gate); interactor.Inspect(document); Call(document, "Animate", 10f);
                Check(interactor.InspectedObject == document, "Cannot inspect enlarged cipher.");
                Visible(camera, document.GetComponent<BoxCollider>());
                Capture(camera, 1600, 900, "paper-" + sample[0] + "-" + sample[1]);
                interactor.CancelInteraction();
            }
            Finish(true, "PASS: " + checks + " assertions in Unity 2022.3.62f2.\n"
                + "Uniform cipher font at 120% of previous maximum; text and pigpen ink inside paper; exact cipher content.\n"
                + "82-letter single-line strip within desktop. Delivery, archive recall, inspection/return and rail-tool interaction.\n"
                + "Full/maximum-zoom decoding viewport checked at 16:9 (900p/720p), 4:3 and 21:9. Synthetic Play Mode validation.\n");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Ungate(SceneInputGate gate) => typeof(SceneInputGate).GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gate, -1);
    static void Visible(Camera camera, BoxCollider box, float bottomMargin = 0)
    {
        foreach (var v in Corners(new Bounds(box.center, box.size)))
        {
            var p = camera.WorldToViewportPoint(box.transform.TransformPoint(v));
            Check(p.z > camera.nearClipPlane && p.x >= 0 && p.x <= 1 && p.y >= bottomMargin && p.y <= 1, "Paper clipped or in HUD: " + p);
        }
    }
    static void Capture(Camera camera, int width, int height, string name)
    {
        var texture = RenderTexture.GetTemporary(width, height, 24);
        var active = RenderTexture.active; var previous = camera.targetTexture;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(texture);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }
    static void Finish(bool success, string message)
    {
        if (done) return; done = true;
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick;
        File.WriteAllText(Output + "/validation.txt", message);
        if (!success) Debug.LogError(message);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
