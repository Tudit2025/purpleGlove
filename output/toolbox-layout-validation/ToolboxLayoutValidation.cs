using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PurpleGlove;
using PurpleGlove.Editor;

// Copy into Assets/Editor in an isolated project; run ToolboxLayoutValidation.Run.
[InitializeOnLoad]
public static class ToolboxLayoutValidation
{
    const string Key = "ToolboxLayoutValidation.Play";
    static readonly string Output = Path.GetFullPath("../../output/toolbox-layout-validation");
    static double started;
    static bool running;
    static int checks;
    static ToolboxLayoutValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    static object Call(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static void Ungate(SceneInputGate gate) => typeof(SceneInputGate).GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gate, -1);
    static void Settle(CipherWorkbench work)
    {
        Call(work, "AnimateToolTransfer", 1f); Call(work, "AnimateToolTransfer", 1f);
        Ungate(work.GetComponent<SceneInputGate>()); Call(work, "Update");
        Ungate(work.GetComponent<SceneInputGate>()); Call(work.GetComponent<DeskScenePreview>(), "AnimateView", 10f, Vector2.zero);
        Physics.SyncTransforms();
    }
    static string Pose(Transform item) => item.name + item.localPosition.ToString("F6") + item.localRotation.ToString("F6") + item.localScale.ToString("F6");
    static string Surroundings()
    {
        var work = Camera.main.GetComponent<CipherWorkbench>();
        return Pose(Camera.main.transform) + Pose(work.toolbox.transform) + Pose(work.mat) + Pose(work.letterTray)
            + Pose(GameObject.Find("PaperKnife").transform) + Pose(GameObject.Find("CorrespondenceRack").transform)
            + string.Join("|", work.tools.Select(t => Pose(t.transform)));
    }
    static string Layout(CipherWorkbench work) => string.Join("|", work.storedTools.Select(t => Pose(t.transform)))
        + string.Join("|", work.storedTools.SelectMany(t => t.GetComponentsInChildren<MeshFilter>(true))
            .Select(f => f.sharedMesh.name + string.Join(";", f.sharedMesh.uv.Select(uv => uv.ToString("F6")))));

    public static void Run()
    {
        Directory.CreateDirectory(Output);
        try
        {
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            string surroundings = Surroundings();
            DeskSceneBuilder.ArrangeCipherToolboxContents();
            string layout = Layout(Camera.main.GetComponent<CipherWorkbench>());
            DeskSceneBuilder.ArrangeCipherToolboxContents();
            Check(surroundings == Surroundings(), "Toolbox arrangement moved another scene object");
            Check(layout == Layout(Camera.main.GetComponent<CipherWorkbench>()), "Repeated layout changes poses or texture scale");
            var work = Camera.main.GetComponent<CipherWorkbench>();
            var assets = work.storedTools.SelectMany(t => t.GetComponentsInChildren<MeshFilter>(true))
                .Select(f => AssetDatabase.GetAssetPath(f.sharedMesh)).Concat(new[] {
                    AssetDatabase.GetAssetPath(work.toolbox.transform.Find("Cipher tray/Cipher_BoxTray").GetComponent<MeshFilter>().sharedMesh),
                    DeskSceneStartup.ScenePath }).Distinct().ToArray();
            File.WriteAllLines(Output + "/updated-assets.txt", assets);
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
    static Bounds Geometry(Transform item, Transform tray)
    {
        var points = item.GetComponentsInChildren<MeshFilter>(true).SelectMany(f => f.sharedMesh.vertices
            .Select(p => tray.InverseTransformPoint(f.transform.TransformPoint(p)))).ToArray();
        var result = new Bounds(points[0], Vector3.zero);
        foreach (var point in points) result.Encapsulate(point);
        return result;
    }
    static CipherWorkbenchTarget Slot(CipherWorkbench work, int i) => work.toolbox
        .GetComponentsInChildren<CipherWorkbenchTarget>(true).Single(t => t.kind == CipherWorkbenchTarget.Kind.StoredTool && (int)t.tool == i);
    static void Open(CipherWorkbench work)
    {
        Ungate(work.GetComponent<SceneInputGate>()); work.OpenToolbox();
        Call(work.toolbox, "Animate", 10f); work.lid.localRotation = Quaternion.Euler(108, 0, 0); Physics.SyncTransforms();
    }
    static void Tick()
    {
        if (running) return;
        if (EditorApplication.timeSinceStartup - started > 90) { Finish(false, "Play mode timeout"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || !Camera.main || Time.frameCount < 12) return;
        running = true;
        try
        {
            var camera = Camera.main;
            var work = camera.GetComponent<CipherWorkbench>();
            var view = camera.GetComponent<DeskScenePreview>();
            var gate = camera.GetComponent<SceneInputGate>();
            var tray = work.toolbox.transform.Find("Cipher tray");
            var report = new StringBuilder();
            var bounds = new Bounds[5];
            view.followMouse = false; Ungate(gate); camera.aspect = 16f / 9;
            for (int i = 0; i < 5; i++)
            {
                var item = work.storedTools[i].transform;
                float scale = item.localScale.x;
                bounds[i] = Geometry(item, tray);
                Check(scale >= (i == 0 ? 0.20f : 0.13f) * 1.5f, "Tool was not substantially enlarged: " + i);
                Check(item.localScale == Vector3.one * scale && Vector3.Dot(item.up, tray.up) > 0.9999f, "Tool distorted or not flat");
                Check(bounds[i].min.x >= -0.22f && bounds[i].max.x <= 0.22f && bounds[i].min.z >= -0.1275f && bounds[i].max.z <= 0.1275f, "Tool outside box");
                Check(bounds[i].min.y >= 0.006f && bounds[i].max.y < 0.044f, "Tool intersects tray or closed lid");
                Check(work.tools[i].transform.localScale == Vector3.one, "Working tool size changed");
                var pick = Slot(work, i).GetComponent<BoxCollider>();
                Check(pick.bounds.Contains(tray.TransformPoint(bounds[i].center)), "Click area does not cover tool");
                foreach (var filter in item.GetComponentsInChildren<MeshFilter>(true))
                {
                    string path = AnimationUtility.CalculateTransformPath(filter.transform, item);
                    var source = work.tools[i].transform.Find(path).GetComponent<MeshFilter>().sharedMesh;
                    var actual = filter.sharedMesh.uv; var original = source.uv;
                    Check(filter.sharedMesh != source, "Stored mesh overwrote working tool");
                    Check(actual.Length == original.Length && actual.Where((uv, index) => Vector2.Distance(uv, original[index] * scale) > 0.00001f).Count() == 0, "Stored texture density changed");
                }
                report.AppendLine(((CipherWorkbench.Tool)i) + ": scale=" + scale.ToString("F3") + ", enlarged="
                    + (scale / (i == 0 ? 0.20f : 0.13f)).ToString("F2") + "x, tray bounds=" + bounds[i]);
            }
            for (int i = 0; i < 5; i++) for (int j = i + 1; j < 5; j++)
            {
                Rect a = new Rect(bounds[i].min.x, bounds[i].min.z, bounds[i].size.x, bounds[i].size.z);
                Rect b = new Rect(bounds[j].min.x, bounds[j].min.z, bounds[j].size.x, bounds[j].size.z);
                Check(!a.Overlaps(b), "Stored tools overlap");
            }
            foreach (var sourceView in new[] { DeskScenePreview.View.Desktop, DeskScenePreview.View.DecodeFocus })
            foreach (float aspect in new[] { 4f / 3, 16f / 9, 21f / 9 })
            {
                work.CloseToolbox(); Ungate(gate); view.SetView(sourceView); camera.aspect = aspect;
                Call(view, "AnimateView", 10f, Vector2.zero); Open(work);
                for (int i = 0; i < 5; i++)
                {
                    var slot = Slot(work, i);
                    // Pick the enlarged model itself at the center and near its four ends.
                    foreach (var offset in new[] { Vector3.zero, Vector3.left * 0.75f, Vector3.right * 0.75f, Vector3.forward * 0.75f, Vector3.back * 0.75f })
                    {
                        Vector3 point = tray.TransformPoint(bounds[i].center + Vector3.Scale(bounds[i].extents, offset));
                        Vector3 screen = camera.WorldToViewportPoint(point);
                        Check(screen.z > 0 && screen.x > 0 && screen.x < 1 && screen.y > 0 && screen.y < 1, "Tool clipped in open box");
                        Check((CipherWorkbenchTarget)Call(work, "PickTarget", (Vector2)camera.WorldToScreenPoint(point), false) == slot, "Cannot pick tool " + i);
                    }
                }
                if (Mathf.Abs(aspect - 16f / 9) < 0.001f) Capture(camera, "open-toolbox-" + sourceView);
            }
            camera.aspect = 16f / 9;
            for (int i = 0; i < 5; i++)
            {
                Open(work);
                var slot = Slot(work, i); var collider = slot.GetComponent<BoxCollider>();
                Call(work, "ActivateTarget", slot, (Vector2)camera.WorldToScreenPoint(slot.transform.TransformPoint(collider.center)));
                Settle(work);
                Check(work.ActiveTool == (CipherWorkbench.Tool)i && work.tools.Count(t => t.activeSelf) == 1, "Tool selection failed");
                Check(!work.storedTools[i].activeSelf && work.storedTools.Count(t => t.activeSelf) == 4, "Stored copy remained after pickup");
                work.SelectTool(CipherWorkbench.Tool.None); Settle(work);
                Check(work.ActiveTool == CipherWorkbench.Tool.None && work.storedTools.All(t => t.activeSelf), "Tool did not return to box");
            }
            Open(work); Capture(camera, "open-toolbox-returned"); work.CloseToolbox();
            Finish(true, "PASS: " + (checks + SessionState.GetInt(Key + ".Checks", 0)) + " assertions in Unity 2022.3.62f2 Play Mode.\n"
                + "Repeated layout is stable; surrounding props and working tools preserved.\n"
                + "Five enlarged flat tools fit below the lid without overlap; texture density and click regions checked.\n"
                + "Actual collider picking at five points per tool from desktop/decode at 4:3, 16:9 and 21:9; take/return all tools.\n"
                + report + "Synthetic pointer replay; screenshots rendered from the saved scene.\n");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Finish(bool success, string message)
    {
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Output + "/validation.txt", message);
        if (success) Debug.Log("TOOLBOX_LAYOUT_VALIDATION_PASS"); else Debug.LogError(message);
        EditorApplication.Exit(success ? 0 : 1);
    }
    static void Capture(Camera camera, string name)
    {
        var target = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32);
        var previous = camera.targetTexture; var active = RenderTexture.active;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally { camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(image); }
    }
}
