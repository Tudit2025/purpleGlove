using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PurpleGlove;
using PurpleGlove.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run in Temp/guild-letter-layout. Only the letter's two text nodes are updated.
public static class GuildLetterLayoutReview
{
    static readonly string Output = Path.GetFullPath("../../output/guild-letter-layout");
    [Serializable] sealed class Entry { public string zh, text; }
    [Serializable] sealed class Table { public Entry[] entries; }
    [Serializable] sealed class Result
    {
        public string body, signature;
        public long[] ids;
        public float oldSize, newSize, width, height;
    }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

    public static void Run()
    {
        try
        {
            Require(Path.GetFullPath(".").Replace('\\', '/').EndsWith("/Temp/guild-letter-layout"), "Use the isolated project.");
            Directory.CreateDirectory(Output);
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            var letter = archive.acts[0].documents[2].transform;
            var body = letter.Find("Body").GetComponent<TextMesh>();
            var signature = letter.Find("Signature").GetComponent<TextMesh>();
            float oldSize = body.characterSize;
            Capture(letter, "before-zh");
            typeof(DeskSceneBuilder).GetMethod("LoadSavedSceneAssets", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            var sample = new GameObject("Temporary letter layout");
            typeof(DeskSceneBuilder).GetMethod("BuildGuildLetter", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { sample.transform });
            foreach (var target in new[] { body, signature })
            {
                var authored = sample.transform.Find(target.name).GetComponent<TextMesh>();
                target.text = authored.text;
                target.characterSize = authored.characterSize;
                target.lineSpacing = authored.lineSpacing;
                target.anchor = authored.anchor;
                target.alignment = authored.alignment;
                target.transform.localPosition = authored.transform.localPosition;
                EditorUtility.SetDirty(target);
                EditorUtility.SetDirty(target.transform);
            }
            UnityEngine.Object.DestroyImmediate(sample);
            Require(body.characterSize > oldSize * 1.15f, "Body should be visibly larger.");
            CheckPaper(letter);
            var bounds = body.GetComponent<Renderer>().localBounds;
            var result = new Result {
                body = body.text, signature = signature.text, oldSize = oldSize, newSize = body.characterSize,
                width = bounds.size.x, height = bounds.size.y,
                ids = new UnityEngine.Object[] { body, body.transform, signature, signature.transform }
                    .Select(o => (long)GlobalObjectId.GetGlobalObjectIdSlow(o).targetObjectId).ToArray()
            };
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            File.WriteAllText(Output + "/layout.json", JsonUtility.ToJson(result, true));
            Capture(letter, "after-zh");

            var table = JsonUtility.FromJson<Table>(File.ReadAllText("Assets/Resources/PHLocalization/English.json")).entries;
            foreach (var ink in new[] { body, signature })
            {
                var english = table.Single(e => e.zh == ink.text).text;
                ink.characterSize *= Localization.FitScale(ink, ink.text, english);
                ink.text = english;
            }
            CheckPaper(letter);
            Capture(letter, "after-en");
            File.WriteAllText(Output + "/validation.txt",
                $"PASS: Unity 2022.3.62f2 letter layout review.\nBody size: {oldSize:R} -> {result.newSize:R} ({result.newSize / oldSize:P1}).\n"
                + $"Body footprint: {result.width:F4} x {result.height:F4} m on a 0.42 x 0.60 m sheet.\n"
                + "Chinese and English glyphs present; text within paper bounds; signature below body.\n"
                + "Saved only Chinese. English capture uses the runtime translation table and FitScale.\n");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/validation.txt", "FAIL: " + error);
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    static void CheckPaper(Transform letter)
    {
        float bottom = float.MaxValue, signatureTop = float.MinValue;
        foreach (var text in letter.GetComponentsInChildren<TextMesh>(true))
        {
            Require(text.text.Split('\n').All(line => line.Length == 0 || "，。；：！？、）”’".IndexOf(line[0]) < 0), "Line starts with closing punctuation.");
            text.font.RequestCharactersInTexture(text.text, text.fontSize);
            foreach (char c in text.text.Where(c => !char.IsWhiteSpace(c)).Distinct())
                Require(text.font.HasCharacter(c), "Missing glyph: " + c);
            var b = text.GetComponent<Renderer>().localBounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var point = letter.InverseTransformPoint(text.transform.TransformPoint(corner));
                Require(Mathf.Abs(point.x) < 0.20f && Mathf.Abs(point.z) < 0.285f, "Ink outside paper margin: " + text.name);
                if (text.name == "Body") bottom = Mathf.Min(bottom, point.z);
                if (text.name == "Signature") signatureTop = Mathf.Max(signatureTop, point.z);
            }
        }
        Require(bottom - signatureTop > 0.012f, "Signature overlaps body.");
    }

    static void Capture(Transform source, string name)
    {
        var subject = UnityEngine.Object.Instantiate(source.gameObject);
        subject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        subject.SetActive(true);
        foreach (var item in subject.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 31;
        var rig = new GameObject("Letter review camera");
        var lens = rig.AddComponent<Camera>();
        lens.orthographic = true; lens.orthographicSize = 0.325f;
        lens.aspect = 1600f / 1200f; lens.nearClipPlane = 0.01f; lens.farClipPlane = 5;
        lens.clearFlags = CameraClearFlags.SolidColor; lens.backgroundColor = new Color(0.16f, 0.15f, 0.14f);
        lens.cullingMask = 1 << 31;
        rig.transform.SetPositionAndRotation(Vector3.up, Quaternion.Euler(90, 0, 0));
        var lamp = rig.AddComponent<Light>(); lamp.type = LightType.Directional; lamp.intensity = 1.1f; lamp.cullingMask = 1 << 31;
        var texture = RenderTexture.GetTemporary(1600, 1200, 24);
        var image = new Texture2D(1600, 1200, TextureFormat.RGB24, false);
        var active = RenderTexture.active;
        try
        {
            lens.targetTexture = texture; lens.Render(); RenderTexture.active = texture;
            image.ReadPixels(new Rect(0, 0, 1600, 1200), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            lens.targetTexture = null; RenderTexture.active = active; RenderTexture.ReleaseTemporary(texture);
            UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(rig); UnityEngine.Object.DestroyImmediate(subject);
        }
    }
}
