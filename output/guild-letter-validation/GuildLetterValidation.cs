using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using PurpleGlove;
using PurpleGlove.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Copy into an isolated validation project's Assets/Editor, then execute Run in batch mode.
[InitializeOnLoad]
public static class GuildLetterValidation
{
    const string Key = "GuildLetterValidation.Play";
    static readonly string Output = Path.GetFullPath("../../output/guild-letter-validation");
    static int checks;
    static bool done;
    static double started;
    static Camera camera;
    static SceneInputGate gate;
    static GuildLetterValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    static object Call(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    static void Ungate() => typeof(SceneInputGate).GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gate, -1);
    static string Compact(string text) => Regex.Replace(text, @"\s+", "");

    public static void Run() => Validate(true);
    public static void ValidateSavedScene() => Validate(false);
    static void Validate(bool rebuild)
    {
        try
        {
            Directory.CreateDirectory(Output);
            if (rebuild)
            {
                DeskSceneBuilder.InstallFirstActDetails();
                DeskSceneBuilder.InstallFirstActDetails();
            }
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            var papers = archive.acts[0].documents;
            Check(papers.Length == 3 && papers.All(p => p), "First act must reference three documents after save/reload");
            Check(archive.GetComponentsInChildren<SceneObject>(true).Count(p => p.name.StartsWith("Act1_0")) == 3, "Reinstall must not duplicate papers");
            Check(papers[1].GetComponent<FirstInteractionMemory>() && !papers[2].GetComponent<FirstInteractionMemory>(), "Only cipher slip owns recollection");
            Check(papers[1].transform.Find("Cipher").GetComponent<TextMesh>().text == "NHHS WKH RULJLQDO", "Cipher preserved");
            Check(archive.acts[0].deferredDocuments.Length == 0, "Guild letter must arrive in first batch");
            var guild = papers[2];
            var text = guild.transform.Find("Body").GetComponent<TextMesh>();
            string script = File.ReadAllText("../../doc/ph-revised.md");
            string section = script.Split(new[] { "### 行会会长的信" }, StringSplitOptions.None)[1]
                .Split(new[] { "### 谜题一" }, StringSplitOptions.None)[0];
            string expected = string.Join("", section.Split('\n').Where(l => l.StartsWith("> ")).Select(l => l.Substring(2)));
            string actual = text.text + guild.transform.Find("Signature").GetComponent<TextMesh>().text;
            Check(Compact(actual) == Compact(expected), "Full letter must match script including salutation and signature");
            Check(text.font.name.Contains("LXGW"), "Guild letter uses manuscript font");
            Check(text.text.Split('\n').All(l => l.Length == 0 || "，。；：！？、）”’".IndexOf(l[0]) < 0), "No closing punctuation stranded at line start");
            Check(Camera.main.GetComponent<FirstActFlow>().openingVisit.lines.Any(l => l.text.Contains("会长今晚写给你的信") && l.text.Contains("原封未动")), "Saved arrival dialogue mentions both brown-packet contents");
            var mat = GameObject.Find("WritingMat").transform;
            foreach (var paper in papers)
            {
                var box = paper.GetComponent<BoxCollider>();
                for (int i = 0; i < 4; i++)
                {
                    var corner = new Vector3((i % 2 == 0 ? -1 : 1) * box.size.x / 2, 0, (i < 2 ? -1 : 1) * box.size.z / 2);
                    var p = mat.InverseTransformPoint(paper.transform.TransformPoint(corner));
                    Check(Mathf.Abs(p.x) < 0.50f && Mathf.Abs(p.z) < 0.54f, "Paper stays within mat stitching: " + paper.name);
                }
                foreach (var ink in paper.GetComponentsInChildren<TextMesh>(true))
                {
                    var bounds = ink.GetComponent<Renderer>().localBounds;
                    for (int i = 0; i < 8; i++)
                    {
                        var local = bounds.center + Vector3.Scale(bounds.extents,
                            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        var p = paper.transform.InverseTransformPoint(ink.transform.TransformPoint(local));
                        Check(Mathf.Abs(p.x) < box.size.x / 2 && Mathf.Abs(p.z) < box.size.z / 2, "Ink contained: " + paper.name + "/" + ink.name);
                    }
                    foreach (char c in ink.text.Where(c => !char.IsWhiteSpace(c)).Distinct())
                        Check(ink.font.HasCharacter(c), "Missing letter glyph: " + c);
                }
            }
            SessionState.SetInt(Key + ".Checks", checks); SessionState.SetBool(Key, true);
            Install(); EditorApplication.isPlaying = true;
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }

    static void Install()
    {
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup - started > 60) { Finish(false, "Play Mode timed out"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.frameCount < 3) return;
        try
        {
            camera = Camera.main; camera.aspect = 16f / 9;
            gate = camera.GetComponent<SceneInputGate>();
            var view = camera.GetComponent<DeskScenePreview>(); view.followMouse = false;
            var interactor = camera.GetComponent<SceneObjectInteractor>();
            var delivery = camera.GetComponent<SealedDeliveryController>();
            var archive = delivery.archive; var papers = archive.acts[0].documents; var guild = papers[2];
            var originalIds = papers.Select(p => p.GetInstanceID()).ToArray();
            Check(!archive.IsAvailable(1) && papers.All(p => !p.gameObject.activeSelf), "No papers visible before receipt");
            Check(delivery.BeginDelivery(1), "First delivery begins");
            Check(delivery.firstActPacketsVisual.activeSelf, "Two-packet first-act variant selected");
            delivery.Stop(); Ungate();
            Check(!archive.IsAvailable(1) && !guild.gameObject.activeSelf, "Cancelled delivery must not expose letter");
            Check(delivery.BeginDelivery(1), "Delivery restarts after cancellation");
            Call(delivery.transferDrum, "Advance", 5f); Call(delivery, "AdvanceTime", 5f);
            Check(delivery.State == SealedDeliveryController.Phase.AwaitingPickup && !guild.gameObject.activeSelf, "Guild letter waits for collection");
            Call(view, "AnimateView", 5f, Vector2.zero); Ungate();
            var packetPoint = camera.WorldToScreenPoint(delivery.packet.transform.TransformPoint(delivery.packet.GetComponent<BoxCollider>().center));
            Call(interactor, "ProcessInput", (Vector2)packetPoint, true, true, false);
            Check(delivery.State == SealedDeliveryController.Phase.FlyingToDesk, "Pointer click collects packet");
            Check(!guild.gameObject.activeSelf, "Letter stays hidden during flight");
            Call(delivery, "AdvanceTime", 5f);
            Check(delivery.State == SealedDeliveryController.Phase.Landing && !guild.gameObject.activeSelf, "Letter stays hidden until landing completes");
            Call(delivery, "AdvanceTime", 5f); Ungate();
            Check(archive.IsAvailable(1) && archive.DeskAct == 1 && papers.All(p => p.gameObject.activeInHierarchy), "Landing reveals all three papers");
            Check(!delivery.BeginDelivery(1), "First batch cannot be duplicated");
            view.SetView(DeskScenePreview.View.MatFocus); Call(view, "AnimateView", 5f, Vector2.zero);
            Capture("01-three-documents");
            foreach (var paper in papers)
            {
                Physics.SyncTransforms();
                var point = camera.WorldToScreenPoint(paper.transform.TransformPoint(paper.GetComponent<BoxCollider>().center));
                Check(Physics.Raycast(camera.ScreenPointToRay(point), out var hit) && hit.collider.GetComponentInParent<SceneObject>() == paper,
                    "Paper independently pickable: " + paper.name);
            }
            Ungate();
            var guildPoint = camera.WorldToScreenPoint(guild.transform.TransformPoint(guild.GetComponent<BoxCollider>().center));
            Call(interactor, "ProcessInput", (Vector2)guildPoint, true, true, false);
            Check(interactor.InspectedObject == guild && !camera.GetComponent<DialogueController>().IsActive, "Letter readable by normal click without cipher or dialogue");
            Call(guild, "Animate", 3f); Capture("02-guild-letter"); interactor.CancelInteraction(); Ungate();
            Check(archive.StoreDeskAct() && papers.All(p => !p.gameObject.activeSelf), "All three papers shelved");
            Check(archive.ShowActOnDesk(1) && guild.gameObject.activeInHierarchy, "Guild letter retrievable from archive");
            Check(originalIds.SequenceEqual(papers.Select(p => p.GetInstanceID())), "Archive retains original instances");
            Check(archive.CompleteAct(1) && !guild.gameObject.activeSelf, "Act completion archives letter");
            Check(archive.ShowActOnDesk(1) && guild.gameObject.activeInHierarchy && archive.CurrentAct == 2, "Letter retrievable after advancing act");
            Finish(true, "PASS: " + (checks + SessionState.GetInt(Key + ".Checks", 0)) + " checks, Unity 2022.3.62f2 Play Mode.\n"
                + "Saved three-document references; repeated installer; full script text; arrival wording; glyphs and text/mat bounds.\n"
                + "Cancelled delivery; actual packet pointer collection; no early reveal; independent paper picking; plaintext reading; original-instance archive/retrieval.\n"
                + "Synthetic pointer and frame stepping; captures at 1920x1080.\n");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Capture(string name)
    {
        var target = RenderTexture.GetTemporary(1920, 1080, 24); var active = RenderTexture.active;
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false); var previous = camera.targetTexture;
        camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply(); File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(image);
    }
    static void Finish(bool success, string message)
    {
        if (done) return; done = true; SessionState.SetBool(Key, false); EditorApplication.update -= Tick;
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/validation.txt", message);
        if (success) Debug.Log(message); else Debug.LogError(message); EditorApplication.Exit(success ? 0 : 1);
    }
}
