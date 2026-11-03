using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using PurpleGlove;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Render the saved layout in an isolated project; never rewrite the authored scene.
[InitializeOnLoad]
public static class CipherVisibilityReview
{
    const string Key = "CipherVisibilityReview.Play";
    static readonly string Output = Path.GetFullPath("../../output/cipher-visibility-review");
    static readonly StringBuilder Report = new StringBuilder();
    static bool done;
    static double started;
    static Camera camera;
    static DeskScenePreview view;
    static CipherWorkbench work;
    static SceneInputGate gate;
    static object Call(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    static void Ungate() => typeof(SceneInputGate).GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gate, -1);
    static CipherVisibilityReview() { if (SessionState.GetBool(Key, false)) Install(); }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene("Assets/Art/DeskSceneV1/Scenes/PH_DeskRoom.unity");
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
    static void Pose(DeskScenePreview.View target, bool detail)
    {
        Ungate(); view.SetView(target);
        Call(view, "AdjustFocusZoom", detail ? 10f : -10f);
        Call(view, "AnimateView", 10f, Vector2.zero);
        Physics.SyncTransforms();
    }
    static void Select(CipherWorkbench.Tool tool)
    {
        Ungate(); work.SelectTool(tool);
        Call(work, "AnimateToolTransfer", 1f); Call(work, "AnimateToolTransfer", 1f);
        Ungate(); Call(work, "Update"); Call(work, "AnimateKeyboardTray", 10f); Ungate();
    }
    static void Tick()
    {
        if (done) return;
        if (EditorApplication.timeSinceStartup - started > 150) { Finish(false, "Play mode timeout"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.frameCount < 8) return;
        try
        {
            camera = Camera.main; view = camera.GetComponent<DeskScenePreview>();
            work = camera.GetComponent<CipherWorkbench>(); gate = camera.GetComponent<SceneInputGate>();
            camera.GetComponent<PrologueSequence>().Stop(); camera.GetComponent<DialogueController>().Stop();
            camera.GetComponent<DialogueTestConsole>().SetOpen(false); view.followMouse = false;
            foreach (var behaviour in camera.GetComponents<MonoBehaviour>())
                if (behaviour.GetType().Name.EndsWith("Flow") || behaviour.GetType().Name.EndsWith("Opening")) behaviour.enabled = false;
            foreach (var memory in UnityEngine.Object.FindObjectsOfType<FirstInteractionMemory>(true)) memory.enabled = false;
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            Report.AppendLine("Unity 2022.3.62f2 Play Mode, saved scene, 1920x1080 and 1440x1080.");
            Report.AppendLine("Tool camera horizontal offset: " + view.decodeFocusHorizontalOffset);
            var tools = new[] { CipherWorkbench.Tool.Caesar, CipherWorkbench.Tool.Pigpen, CipherWorkbench.Tool.SaintCyr, CipherWorkbench.Tool.Affine };
            int[] cipherIndex = { 1, 2, 1, 2 };
            var mat = GameObject.Find("WritingMat").transform;
            for (int act = 1; act <= 4; act++)
            {
                Ungate(); archive.ReceiveAct(act); archive.ReceiveSupplement(act);
                var paper = archive.acts[act - 1].documents[cipherIndex[act - 1]];
                Report.AppendLine(paper.name + " mat-local center=" + mat.InverseTransformPoint(paper.transform.position).ToString("F4"));
                camera.aspect = 16f / 9;
                Pose(DeskScenePreview.View.MatFocus, false); Capture("act" + act + "-desk", 1920, 1080);
                Select(tools[act - 1]);
                Pose(DeskScenePreview.View.DecodeFocus, false);
                Capture("act" + act + "-decode", 1920, 1080); Describe(paper, "full 16:9");
                camera.aspect = 4f / 3;
                Capture("act" + act + "-decode-4x3", 1440, 1080); Describe(paper, "full 4:3");
                camera.aspect = 16f / 9;
                Pose(DeskScenePreview.View.DecodeFocus, true);
                Capture("act" + act + "-detail", 1920, 1080); Describe(paper, "detail 16:9");
                if (act == 2)
                {
                    var book = camera.GetComponent<SecondActFlow>().books.First(b => b.sevenPetal);
                    book.enabled = true; Ungate(); book.TryOpen();
                    Call(view, "AnimateView", 10f, Vector2.zero);
                    Capture("act2-book", 1920, 1080); Describe(book.note, "book 16:9");
                    work.CloseBook();
                }
                if (act == 4)
                {
                    Select(CipherWorkbench.Tool.RailFence); Pose(DeskScenePreview.View.DecodeFocus, true);
                    Capture("act4-rail-detail", 1920, 1080);
                }
                Select(CipherWorkbench.Tool.None);
                archive.StoreDeskAct(); archive.ShowActOnDesk(act);
                Report.AppendLine("After archive recall: " + mat.InverseTransformPoint(paper.transform.position).ToString("F4"));
                archive.CompleteAct(act);
            }
            Finish(true, Report.ToString());
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Describe(SceneObject paper, string label)
    {
        var box = paper.GetComponent<BoxCollider>(); var min = Vector2.one * float.PositiveInfinity; var max = Vector2.one * float.NegativeInfinity;
        foreach (int x in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
        {
            Vector2 point = camera.WorldToViewportPoint(paper.transform.TransformPoint(box.center + Vector3.Scale(box.size * 0.5f, new Vector3(x, 1, z))));
            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
        }
        Report.AppendLine(label + " paper viewport: " + min.ToString("F4") + " .. " + max.ToString("F4"));
    }
    static void Capture(string name, int width, int height)
    {
        var target = RenderTexture.GetTemporary(width, height, 24); var active = RenderTexture.active; var previous = camera.targetTexture;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply(); File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally { camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(image); }
    }
    static void Finish(bool success, string message)
    {
        if (done) return; done = true; SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/review.txt", (success ? "RENDERED\n" : "FAILED\n") + message);
        if (success) Debug.Log("CIPHER_VISIBILITY_RENDERED"); else Debug.LogError(message);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
