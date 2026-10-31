using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PurpleGlove;
using PurpleGlove.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class NightSkyValidation
{
    static readonly string Output = Path.GetFullPath("../../output/night-sky-validation");
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            string originalLights = Lights();
            int originalAudioSources = UnityEngine.Object.FindObjectsOfType<AudioSource>().Length;
            var originalPosition = Camera.main.transform.position;
            var originalRotation = Camera.main.transform.rotation;
            float originalFov = Camera.main.fieldOfView;
            float originalFarClip = Camera.main.farClipPlane;
            DeskSceneBuilder.AddNightSkyToSavedScene();
            DeskSceneBuilder.AddNightSkyToSavedScene();
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            Check(Lights() == originalLights, "Room lighting changed");
            Check(UnityEngine.Object.FindObjectsOfType<AudioSource>().Length == originalAudioSources, "Audio sources changed");
            var camera = Camera.main;
            Check(camera.transform.position == originalPosition && camera.transform.rotation == originalRotation
                && camera.fieldOfView == originalFov && camera.farClipPlane == originalFarClip, "Gameplay camera changed");
            var skyFilters = UnityEngine.Object.FindObjectsOfType<MeshFilter>().Where(f => f.name == "NightSky_Sphere").ToArray();
            Check(skyFilters.Length == 1, "Sky update is not idempotent");
            var sky = skyFilters[0];
            Check(!sky.GetComponent<Collider>(), "Sky intercepts object picking");
            var renderer = sky.GetComponent<MeshRenderer>();
            Check(renderer.shadowCastingMode == ShadowCastingMode.Off && !renderer.receiveShadows, "Sky casts/receives shadows");
            foreach (string shaderName in new[] { "PurpleGlove/Night Sky", "PurpleGlove/Window Glass" })
            {
                var shader = Shader.Find(shaderName);
                Check(shader && shader.isSupported && !ShaderUtil.ShaderHasError(shader), "Shader error: " + shaderName);
            }
            var validateMesh = typeof(DeskSceneBuilder).GetMethod("ValidateMeshMetric", BindingFlags.NonPublic | BindingFlags.Static);
            validateMesh.Invoke(null, new object[] { sky.sharedMesh, false });
            var glass = AssetDatabase.LoadAssetAtPath<Material>(DeskSceneBuilder.Root + "/Materials/M_Desk_WindowNight.mat");
            Check(glass.shader.name == "PurpleGlove/Window Glass" && glass.renderQueue >= 3000 && glass.color.a < 0.15f, "Window is opaque");
            foreach (var filter in UnityEngine.Object.FindObjectsOfType<MeshFilter>())
                Check(filter.sharedMesh && filter.GetComponent<Renderer>().sharedMaterials.All(m => m && m.shader), "Missing mesh/material");

            Capture(camera, "desktop");
            var preview = camera.GetComponent<DeskScenePreview>();
            camera.transform.rotation = Quaternion.Euler(preview.bookshelfEuler);
            camera.fieldOfView = preview.bookshelfFieldOfView;
            Capture(camera, "bookshelf");
            camera.transform.LookAt(new Vector3(-1.85f, 1.72f, 0.55f));
            camera.fieldOfView = 34;
            Capture(camera, "window");
            camera.transform.rotation = Quaternion.Euler(preview.doorwayEuler);
            camera.fieldOfView = DeskScenePreview.DoorwayFieldOfView;
            Capture(camera, "doorway");

            sky.gameObject.layer = 31;
            camera.cullingMask = 1 << 31;
            camera.transform.rotation = Quaternion.LookRotation(renderer.sharedMaterial.GetVector("_MoonDirection"));
            camera.fieldOfView = 55;
            Capture(camera, "sky");
            Check(!ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader) && !ShaderUtil.ShaderHasError(glass.shader), "Shader failed on render");
            File.WriteAllText(Output + "/validation.txt", "PASS: Unity 2022.3.62f2 / Built-in / Direct3D 11.\n"
                + "Saved scene reopened; sky installed twice with exactly one sphere.\n"
                + "Night sky and glass shaders supported, compiled and rendered.\n"
                + "Sphere triangle winding and metric UV verified; no collider or shadows.\n"
                + "Original lights, audio source count and gameplay camera preserved.\n"
                + "Captured desktop, bookshelf, doorway, window close-up and sky.\n");
            Debug.Log("NIGHT_SKY_VALIDATION_PASS");
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    static string Lights() => string.Join("|", UnityEngine.Object.FindObjectsOfType<Light>()
        .Select(l => l.name + ":" + l.transform.position + ":" + l.type + ":" + l.intensity + ":" + l.color + ":" + l.cullingMask)
        .OrderBy(value => value));

    static void Capture(Camera camera, string name)
    {
        var target = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            image.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
