using System;
using System.IO;
using System.Text;
using PurpleGlove;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VignetteValidation
{
    static readonly string Output = Path.GetFullPath("../../output/vignette-validation");
    static readonly StringBuilder Report = new StringBuilder();

    public static void Run()
    {
        try
        {
            var shader = Resources.Load<Shader>("PHVignette");
            if (!shader || !shader.isSupported || ShaderUtil.ShaderHasError(shader))
                throw new Exception("Vignette shader failed to load or compile.");
            foreach (var scene in new[] { "PH_DeskRoom", "PH_MainMenu" })
            {
                EditorSceneManager.OpenScene("Assets/Art/DeskSceneV1/Scenes/" + scene + ".unity");
                var camera = Camera.main;
                var effect = camera.GetComponent<PixelSceneView>();
                if (!effect.vignette || Mathf.Abs(effect.vignetteIntensity - .45f) > .001f)
                    throw new Exception("Existing scene did not receive enabled defaults: " + scene);
                Capture(camera, scene + "-before", false, 1920, 1080);
                Capture(camera, scene + "-after", true, 1920, 1080);
                int mask = camera.cullingMask;
                var clear = camera.clearFlags;
                var background = camera.backgroundColor;
                camera.cullingMask = 0;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.8f, .8f, .8f, .6f);
                foreach (var size in new[] { new Vector2Int(960, 540), new Vector2Int(800, 600), new Vector2Int(1260, 540) })
                foreach (bool pixelate in new[] { true, false })
                {
                    effect.pixelate = pixelate;
                    var before = Render(camera, false, size.x, size.y);
                    var after = Render(camera, true, size.x, size.y);
                    var center = new Vector2Int(size.x / 2, size.y / 2);
                    if (Mathf.Abs(after.GetPixel(center.x, center.y).r - before.GetPixel(center.x, center.y).r) > .005f)
                        throw new Exception("Center brightness changed.");
                    float corner = after.GetPixel(1, 1).r;
                    foreach (var point in new[] { new Vector2Int(1, 1), new Vector2Int(size.x - 2, 1),
                        new Vector2Int(1, size.y - 2), new Vector2Int(size.x - 2, size.y - 2) })
                    {
                        var a = after.GetPixel(point.x, point.y);
                        var b = before.GetPixel(point.x, point.y);
                        if (a.r > b.r * .8f || Mathf.Abs(a.r - corner) > .005f || Mathf.Abs(a.a - b.a) > .005f)
                            throw new Exception("Corner darkening, symmetry or alpha failed.");
                    }
                    Report.AppendLine($"PASS {scene}, {size.x}x{size.y}, pixelate={pixelate}: center unchanged, four corners darkened equally, alpha preserved.");
                    UnityEngine.Object.DestroyImmediate(before);
                    UnityEngine.Object.DestroyImmediate(after);
                }
                camera.cullingMask = mask; camera.clearFlags = clear; camera.backgroundColor = background;
                effect.enabled = false;
                effect.enabled = true;
                Capture(camera, scene + "-reenabled", true, 960, 540);
            }
            File.WriteAllText(Path.Combine(Output, "validation.txt"), "Unity " + Application.unityVersion + ": shader compiled and both saved scenes render with enabled defaults.\n" + Report);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(Output, "validation.txt"), "FAIL\n" + error);
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    static void Capture(Camera camera, string name, bool enabled, int width, int height)
    {
        var image = Render(camera, enabled, width, height);
        File.WriteAllBytes(Path.Combine(Output, name + ".png"), image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
    }

    static Texture2D Render(Camera camera, bool enabled, int width, int height)
    {
        camera.GetComponent<PixelSceneView>().vignette = enabled;
        var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            return image;
        }
        finally
        {
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            RenderTexture.ReleaseTemporary(target);
        }
    }
}
