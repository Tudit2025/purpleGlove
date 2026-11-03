using System;
using System.IO;
using System.Linq;
using PurpleGlove;
using PurpleGlove.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ThirdActPlaintextLayoutReview
{
    static readonly string Output = Path.GetFullPath("../../output/third-act-plaintext-layout");
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static void Run()
    {
        try
        {
            Require(Path.GetFullPath(".").Replace('\\', '/').EndsWith("/Temp/guild-letter-layout"), "Use the isolated project.");
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            var records = Camera.main.GetComponent<PlaintextArchiveController>();
            var entry = records.entries.Single(e => e.id == "vigenere");
            Require(entry.plaintextInk.text == "", "Unsolved plaintext must remain hidden.");
            entry.plaintextInk.text = PlaintextArchiveController.WrapAnswer(entry.answer);
            Require(entry.plaintextInk.text == "FRANCE WILL CROSS\nON JUNE EIGHTEEN", "Expected two authored lines.");
            Require(PlaintextArchiveController.MatchesAnswer("FRANCE WILL CROSS ON JUNE EIGHTEEN", entry.answer), "The existing answer must still be accepted.");
            var translation = entry.document.transform.Find("Translation").GetComponent<TextMesh>();
            var englishBounds = InkBounds(entry.document.transform, entry.plaintextInk);
            var chineseBounds = InkBounds(entry.document.transform, translation);
            Require(englishBounds.min.z > chineseBounds.max.z + 0.008f, "Plaintext overlaps the Chinese translation.");
            Capture(entry.document.transform, "plaintext");
            File.WriteAllText(Output + "/validation.txt", "PASS: Unity compilation and saved-scene rendering.\n"
                + "Preserved plaintext has two lines, fits inside the paper and clears the Chinese translation.\n"
                + "Original unbroken answer accepted; unsolved plaintext stays blank.\n"
                + $"Plaintext size: {englishBounds.size.x:F4} x {englishBounds.size.z:F4} m; translation gap: {englishBounds.min.z - chineseBounds.max.z:F4} m.\n");
            DocumentTextExport.Run();
        }
        catch (Exception error)
        {
            File.WriteAllText(Output + "/validation.txt", "FAIL: " + error);
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
    static Bounds InkBounds(Transform paper, TextMesh ink)
    {
        ink.font.RequestCharactersInTexture(ink.text, ink.fontSize, ink.fontStyle);
        var bounds = ink.GetComponent<Renderer>().localBounds;
        var result = new Bounds(paper.InverseTransformPoint(ink.transform.TransformPoint(bounds.center)), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            var point = paper.InverseTransformPoint(ink.transform.TransformPoint(corner));
            Require(Mathf.Abs(point.x) < 0.29f && Mathf.Abs(point.z) < 0.16f, "Ink exceeds paper margins: " + ink.name);
            result.Encapsulate(point);
        }
        return result;
    }
    static void Capture(Transform source, string name)
    {
        var subject = UnityEngine.Object.Instantiate(source.gameObject);
        subject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        subject.transform.localScale = Vector3.one;
        subject.SetActive(true);
        foreach (var item in subject.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 31;
        var rig = new GameObject("Letter review camera");
        var lens = rig.AddComponent<Camera>();
        lens.orthographic = true; lens.orthographicSize = 0.24f;
        lens.aspect = 1200f / 800f; lens.nearClipPlane = 0.01f; lens.farClipPlane = 5;
        lens.clearFlags = CameraClearFlags.SolidColor; lens.backgroundColor = new Color(0.16f, 0.15f, 0.14f);
        lens.cullingMask = 1 << 31;
        rig.transform.SetPositionAndRotation(Vector3.up, Quaternion.Euler(90, 0, 0));
        var lamp = rig.AddComponent<Light>(); lamp.type = LightType.Directional; lamp.intensity = 1.1f; lamp.cullingMask = 1 << 31;
        var texture = RenderTexture.GetTemporary(1200, 800, 24);
        var image = new Texture2D(1200, 800, TextureFormat.RGB24, false);
        var active = RenderTexture.active;
        try
        {
            lens.targetTexture = texture; lens.Render(); RenderTexture.active = texture;
            image.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            lens.targetTexture = null; RenderTexture.active = active; RenderTexture.ReleaseTemporary(texture);
            UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(rig); UnityEngine.Object.DestroyImmediate(subject);
        }
    }
}
