using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PurpleGlove;
using PurpleGlove.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run with -executeMethod FourthActStripReview.Run in Temp/guild-letter-layout.
[InitializeOnLoad]
public static class FourthActStripReview
{
    const string Key = "FourthActStripReview.Play";
    static readonly string Output = Path.GetFullPath("../../output/fourth-act-strips");
    static double started;
    static bool done;
    static int checks;
    static FourthActStripReview() { if (SessionState.GetBool(Key, false)) Watch(); }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    static void Ungate(SceneInputGate gate) => typeof(SceneInputGate).GetField("ignoreThroughFrame",
        BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gate, -1);
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        try
        {
            Check(Path.GetFullPath(".").Replace('\\', '/').EndsWith("/Temp/guild-letter-layout"), "Use the isolated project.");
            DeskSceneBuilder.InstallFourthActOpening();
            DeskSceneBuilder.InstallFourthActOpening();
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            var papers = archive.acts[3].documents;
            Check(papers.Length == 5 && papers.Distinct().Count() == 5, "Expected five separate originals.");
            foreach (int index in new[] { 2, 4 })
            {
                var paper = papers[index];
                var texts = paper.GetComponentsInChildren<TextMesh>(true);
                var box = paper.GetComponent<BoxCollider>();
                Check(texts.Length == 1 && !texts[0].text.Contains("\n"), "Cipher strip must contain only one line.");
                Check(box.size.x / box.size.z > 5, "Cipher paper is not a horizontal strip.");
                Check(texts[0].GetComponent<Renderer>().localBounds.size.x <= box.size.x - 0.02f, "Cipher spills off strip.");
                Check(!paper.gameObject.activeSelf, "Paper visible before delivery.");
                Capture(paper.transform, index == 2 ? "affine" : "rail", true);
            }
            string affine = papers[2].GetComponentInChildren<TextMesh>(true).text;
            Check(affine == "VAPZR  MIZC  UIWVZ  QIPZWV", "Affine words must use exactly two spaces.");
            var records = Camera.main.GetComponent<PlaintextArchiveController>();
            var a = records.entries.Single(e => e.id == "affine");
            var r = records.entries.Single(e => e.id == "rail");
            Check(a.sourceDocument == papers[2] && r.sourceDocument == papers[4], "Separate source references missing.");
            string plain = new string(affine.Where(char.IsLetter).Select(c => (char)('A' + ((21 * (c - 'A' - 8)) % 26 + 26) % 26)).ToArray());
            Check(plain == PlaintextArchiveController.Normalize(a.answer), "A=5, B=8 no longer decode the affine text.");
            string rail = papers[4].GetComponentInChildren<TextMesh>(true).text;
            int split = (rail.Length + 1) / 2;
            string railPlain = new string(Enumerable.Range(0, rail.Length).Select(i => rail[i % 2 == 0 ? i / 2 : split + i / 2]).ToArray());
            Check(railPlain == PlaintextArchiveController.Normalize(r.answer), "Rail-fence text no longer decodes.");
            var letter = papers[3];
            string body = letter.transform.Find("Body").GetComponent<TextMesh>().text;
            Check(!letter.transform.Find("Title") && !letter.transform.Find("Folio"), "Letter retains title or footer.");
            Check(!body.Contains("我原以为") && !body.Contains("伪案拆穿") && body.Contains("是我高估了自己"), "Wrong letter passage removed.");
            Check(body.Contains("A = 5    B = 8"), "Letter is missing clear A/B values.");
            var memory = papers[2].GetComponent<FirstInteractionMemory>();
            Check(memory && memory.duringInspection && memory.memory.TryValidate(out _) && memory.memory.lines.Length == 1,
                "Missing in-inspection first-pickup memory.");
            Check(memory.memory.lines[0].speaker.id == "luca" && memory.memory.lines[0].text.Contains("仿射密码")
                && memory.memory.lines[0].text.Contains("师父的信"), "Incorrect pickup hint.");
            Check(!papers[4].GetComponent<FirstInteractionMemory>(), "Rail strip must not trigger affine hint.");
            Capture(letter.transform, "letter", false);
            var mat = GameObject.Find("WritingMat").transform;
            foreach (var paper in papers)
            {
                var box = paper.GetComponent<BoxCollider>();
                foreach (float x in new[] { -box.size.x / 2, box.size.x / 2 })
                    foreach (float z in new[] { -box.size.z / 2, box.size.z / 2 })
                    {
                        var p = mat.InverseTransformPoint(paper.transform.TransformPoint(new Vector3(x, 0, z)));
                        Check(Mathf.Abs(p.x) < 0.49f && Mathf.Abs(p.z) < 0.54f, "Paper outside writing mat.");
                    }
            }
            SessionState.SetInt(Key + ".checks", checks);
            SessionState.SetInt(Key + ".language", PlayerPrefs.GetInt("PurpleGlove.Language", -1));
            SessionState.SetBool(Key, true); Watch();
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(DeskSceneStartup.ScenePath);
            EditorApplication.isPlaying = true;
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Watch()
    {
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    static void Tick()
    {
        if (done) return;
        if (EditorApplication.timeSinceStartup - started > 90) { Finish(false, "Play Mode timeout."); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || !Camera.main || Time.frameCount < 12) return;
        try
        {
            checks = SessionState.GetInt(Key + ".checks", 0);
            Localization.SetLanguage(Localization.Language.Chinese);
            var camera = Camera.main;
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            var papers = archive.acts[3].documents.ToArray();
            var dialogue = camera.GetComponent<DialogueController>();
            var gate = camera.GetComponent<SceneInputGate>();
            var interactor = camera.GetComponent<SceneObjectInteractor>();
            var view = camera.GetComponent<DeskScenePreview>();
            var records = camera.GetComponent<PlaintextArchiveController>();
            Check(!archive.IsAvailable(4) && papers.All(p => !p.gameObject.activeSelf), "Fourth-act papers unlocked early.");
            Check(archive.ReceiveAct(4) && papers.All(p => p.gameObject.activeSelf), "Delivery did not reveal five originals.");
            Ungate(gate); view.SetView(DeskScenePreview.View.Desktop); Call(view, "AnimateView", 5f, Vector2.zero);
            var memory = papers[2].GetComponent<FirstInteractionMemory>();
            interactor.Inspect(papers[4]);
            Check(interactor.InspectedObject == papers[4] && !dialogue.IsActive && !memory.HasRemembered, "Rail pickup consumed affine hint.");
            interactor.CancelInteraction(); Ungate(gate);
            interactor.Inspect(papers[2]);
            Check(interactor.InspectedObject == papers[2] && dialogue.ActiveEvent == memory.memory, "Affine hint did not preserve inspection.");
            dialogue.Stop(); Check(!memory.HasRemembered, "Cancelled hint was marked complete.");
            interactor.CancelInteraction(); Ungate(gate); interactor.Inspect(papers[2]);
            Check(dialogue.ActiveEvent == memory.memory, "Cancelled hint did not retry.");
            Call(dialogue, "AdvanceTime", 1f); dialogue.Advance(); dialogue.Advance();
            Check(memory.HasRemembered && !dialogue.IsActive && interactor.InspectedObject == papers[2], "Completed hint lost inspection.");
            interactor.CancelInteraction(); Ungate(gate); interactor.Inspect(papers[2]);
            Check(!dialogue.IsActive && interactor.InspectedObject == papers[2], "Hint repeated on second pickup.");
            interactor.CancelInteraction(); Ungate(gate);
            Check(archive.StoreDeskAct() && archive.ShowActOnDesk(4) && papers.All(p => p.gameObject.activeSelf), "Archive recall lost a paper.");
            Ungate(gate); interactor.Inspect(papers[2]);
            Check(!dialogue.IsActive && memory.HasRemembered, "Archive recall replayed hint.");
            interactor.CancelInteraction(); Ungate(gate);
            Localization.SetLanguage(Localization.Language.English);
            Check(papers[2].GetComponentInChildren<TextMesh>(true).text == "VAPZR  MIZC  UIWVZ  QIPZWV", "English changed cipher spacing.");
            string body = papers[3].transform.Find("Body").GetComponent<TextMesh>().text;
            Check(body.StartsWith("Luca,") && body.Contains("A = 5    B = 8"), "English letter missing translation or keys.");
            Check(Localization.Text(memory.memory.lines[0].text).Contains("affine cipher"), "Pickup hint missing English translation.");
            Capture(papers[3].transform, "letter-en", false);
            Localization.SetLanguage(Localization.Language.Chinese);
            // Save rail first, then affine: both must still register against their own delivered source.
            Call(records, "AdvanceTime", 1f);
            foreach (string id in new[] { "rail", "affine" })
            {
                Ungate(gate);
                var entry = records.entries.Single(e => e.id == id);
                records.workbench.SetPlaintext(entry.answer);
                Call(records, "AdvanceTime", 0f); Call(records, "AdvanceTime", 1f);
                Check(entry.saved, "Could not preserve " + id);
                if (dialogue.IsActive) { Call(dialogue, "AdvanceTime", 1f); dialogue.Advance(); dialogue.Advance(); }
            }
            Check(archive.acts[3].documents.Length == 7, "Expected five originals plus two plaintext records.");
            Finish(true, "PASS: " + checks + " assertions in Unity 2022.3.62f2.\n"
                + "Single-line separate strips, exact double spaces, A/B decryption, no titles/footers/explanation, mat bounds, repeat installation.\n"
                + "Play Mode: delivery locks, rail pickup, affine pickup with inspection, cancellation retry, no repeat after pickup or archive recall, both plaintext records.\n"
                + "Chinese and English letter/key/hint checked; rendered previews included. Synthetic input/timing replay.\n");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Finish(bool success, string report)
    {
        if (done) return; done = true;
        if (SessionState.GetBool(Key, false))
        {
            int language = SessionState.GetInt(Key + ".language", -1);
            if (language < 0) PlayerPrefs.DeleteKey("PurpleGlove.Language"); else PlayerPrefs.SetInt("PurpleGlove.Language", language);
            PlayerPrefs.Save();
        }
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick;
        File.WriteAllText(Output + "/validation.txt", report);
        if (!success) Debug.LogError(report);
        EditorApplication.Exit(success ? 0 : 1);
    }
    static void Capture(Transform source, string name, bool strip)
    {
        int width = strip ? 1800 : 1200, height = strip ? 450 : 1200;
        var subject = UnityEngine.Object.Instantiate(source.gameObject);
        subject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); subject.transform.localScale = Vector3.one;
        subject.SetActive(true);
        foreach (var t in subject.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
        var rig = new GameObject("Paper review camera"); var lens = rig.AddComponent<Camera>();
        lens.orthographic = true; lens.orthographicSize = strip ? 0.065f : 0.24f; lens.aspect = (float)width / height;
        lens.nearClipPlane = 0.01f; lens.farClipPlane = 5; lens.cullingMask = 1 << 31;
        lens.clearFlags = CameraClearFlags.SolidColor; lens.backgroundColor = new Color(0.16f, 0.15f, 0.14f);
        rig.transform.SetPositionAndRotation(Vector3.up, Quaternion.Euler(90, 0, 0));
        var lamp = rig.AddComponent<Light>(); lamp.type = LightType.Directional; lamp.intensity = 1.1f; lamp.cullingMask = 1 << 31;
        var texture = RenderTexture.GetTemporary(width, height, 24); var active = RenderTexture.active;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            lens.targetTexture = texture; lens.Render(); RenderTexture.active = texture;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            lens.targetTexture = null; RenderTexture.active = active; RenderTexture.ReleaseTemporary(texture);
            UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(rig); UnityEngine.Object.DestroyImmediate(subject);
        }
    }
}
