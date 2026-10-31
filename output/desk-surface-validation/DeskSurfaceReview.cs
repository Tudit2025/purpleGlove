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

// Visual review of the saved scene in an isolated project under Temp.
[InitializeOnLoad]
public static class DeskSurfaceReview
{
    const string Key = "DeskSurfaceReview.Play";
    static readonly string Output = Path.GetFullPath("../../output/desk-surface-validation");
    static double started;
    static bool running;
    static DeskSurfaceReview() { if (SessionState.GetBool(Key, false)) Install(); }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
        SessionState.SetBool(Key, true);
        Install(); EditorApplication.isPlaying = true;
    }
    static void Install()
    {
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
    }
    static void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            Finish(false, message + "\n" + stack);
    }
    static void Settle(DeskScenePreview view) => typeof(DeskScenePreview)
        .GetMethod("AnimateView", BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(view, new object[] { 10f, Vector2.zero });
    static void Tick()
    {
        if (running) return;
        if (EditorApplication.timeSinceStartup - started > 90) { Finish(false, "Play mode timeout"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || !Camera.main || Time.frameCount < 12) return;
        running = true;
        try
        {
            var camera = Camera.main;
            var view = camera.GetComponent<DeskScenePreview>();
            var work = camera.GetComponent<CipherWorkbench>();
            view.followMouse = false; camera.aspect = 16f / 9;
            view.SetView(DeskScenePreview.View.Desktop); Settle(view);
            Capture(camera, "desktop");
            var report = new StringBuilder("Unity 2022.3.62f2 / Play Mode / saved scene / Direct3D 11\n");
            foreach (var renderer in work.mat.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!renderer.GetComponent<MeshFilter>()) continue;
                report.AppendLine(renderer.name + " | layer=" + renderer.gameObject.layer
                    + " | cast=" + renderer.shadowCastingMode + " | receive=" + renderer.receiveShadows);
                foreach (var material in renderer.sharedMaterials)
                    report.AppendLine("  " + material.name + " | texture=" + (material.mainTexture ? material.mainTexture.name : "none")
                        + " | fill=" + material.GetColor("_InspectionFill") + " | pixelSize=" + material.GetFloat("_PixelSize"));
            }
            work.SetPlaintext("保留原件，等待回信。\nKEEP THE ORIGINAL");
            view.SetView(DeskScenePreview.View.DecodeFocus); Settle(view);
            Capture(camera, "writing-area");
            foreach (CipherWorkbench.Tool tool in Enum.GetValues(typeof(CipherWorkbench.Tool)))
            {
                if (tool == CipherWorkbench.Tool.None) continue;
                work.SelectTool(tool);
                if (tool == CipherWorkbench.Tool.Affine || tool == CipherWorkbench.Tool.RailFence)
                {
                    work.SetCell(tool, 0, "A"); work.SetCell(tool, 1, "B"); work.SetCell(tool, 2, "C");
                }
                view.SetView(DeskScenePreview.View.DecodeFocus); Settle(view);
                Capture(camera, "tool-" + tool);
            }
            work.SelectTool(CipherWorkbench.Tool.None);
            var lights = UnityEngine.Object.FindObjectsOfType<Light>();
            float[] intensities = lights.Select(light => light.intensity).ToArray();
            for (int i = 0; i < lights.Length; i++) lights[i].intensity = intensities[i] * 0.5f;
            Capture(camera, "half-light");
            for (int i = 0; i < lights.Length; i++) lights[i].intensity = intensities[i];
            Finish(true, report + "\nRendered desktop, handwritten slip, five tools, and half-light comparison.\n");
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
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
        finally
        {
            camera.targetTexture = previous; RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(image);
        }
    }
    static void Finish(bool success, string message)
    {
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Output + "/review.txt", message);
        if (success) Debug.Log("DESK_SURFACE_REVIEW_CAPTURED"); else Debug.LogError(message);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
