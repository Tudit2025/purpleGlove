using System;
using System.IO;
using System.Linq;
using PurpleGlove;
using PurpleGlove.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run in the isolated Temp/guild-letter-layout project.
public static class ThirdActWorkPaperReview
{
    static readonly string Output = Path.GetFullPath("../../output/third-act-work-paper");
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static void Run()
    {
        try
        {
            Require(Path.GetFullPath(".").Replace('\\', '/').EndsWith("/Temp/guild-letter-layout"), "Use the isolated project.");
            Directory.CreateDirectory(Output);
            DeskSceneBuilder.InstallThirdActWorkPaper();
            DeskSceneBuilder.InstallThirdActWorkPaper();
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            var act = archive.acts[2];
            Require(act.documents.Length == 6, "Expected five initial papers plus Charles's deferred receipt.");
            var note = act.documents[5];
            var ink = note.GetComponentsInChildren<TextMesh>(true);
            Require(ink.Length == 1 && ink[0].text == "SOIE", "The work paper must contain only SOIE.");
            Require(ink[0].font == act.documents[0].transform.Find("Matteo signature").GetComponent<TextMesh>().font,
                "The work paper must use Matteo's handwriting.");
            Require(act.deferredDocuments.Length == 1 && act.deferredDocuments[0] == act.documents[3], "Only Charles's receipt is deferred.");
            Require(!note.gameObject.activeSelf && note.GetComponent<Collider>(), "Work paper must be hidden until delivery and inspectable.");
            var records = Camera.main.GetComponent<PlaintextArchiveController>();
            var answer = records.entries.Single(e => e.id == "vigenere");
            string cipher = act.documents[1].transform.Find("Cipher").GetComponent<TextMesh>().text;
            string plain = new string(cipher.Select((c, i) => (char)('A' + (c - ink[0].text[i % 4] + 26) % 26)).ToArray());
            Require(plain == new string(answer.answer.Where(char.IsLetter).ToArray()), "SOIE does not decode the military cipher.");
            Require(answer.sourceDocument == act.documents[1], "The cipher's source reference changed.");
            Capture(note.transform, "work-paper");
            archive.previewAllActs = false;
            archive.actReceived.RemoveAllListeners();
            Require(!archive.IsDocumentAvailable(3, note), "Work paper unlocked before receipt.");
            Require(archive.ReceiveAct(3), "Could not receive third-act packet.");
            Require(archive.IsDocumentAvailable(3, note) && note.gameObject.activeSelf, "Work paper missing from first packet.");
            Require(!archive.IsDocumentAvailable(3, act.documents[3]), "Charles's receipt arrived early.");
            var pose = note.transform.position;
            Require(archive.StoreDeskAct() && !note.gameObject.activeSelf, "Work paper did not archive.");
            Require(archive.ShowActOnDesk(3) && note.gameObject.activeSelf && Vector3.Distance(pose, note.transform.position) < 0.001f,
                "Work paper did not return to its original desk pose.");
            File.WriteAllText(Output + "/validation.txt", "PASS: Unity compilation and saved scene update.\n"
                + "Work paper contains only SOIE in Matteo's handwriting; keyword decodes the existing military cipher.\n"
                + "Five initial papers plus one deferred receipt; delivery, archive recall and repeat installation verified.\n");
            DocumentTextExport.Run();
        }
        catch (Exception error)
        {
            File.WriteAllText(Output + "/validation.txt", "FAIL: " + error);
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
    static void Capture(Transform source, string name)
    {
        var subject = UnityEngine.Object.Instantiate(source.gameObject);
        subject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        subject.SetActive(true);
        foreach (var item in subject.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 31;
        var rig = new GameObject("Letter review camera");
        var lens = rig.AddComponent<Camera>();
        lens.orthographic = true; lens.orthographicSize = 0.10f;
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
