using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PurpleGlove;
using PurpleGlove.Editor;

[InitializeOnLoad]
public static class ActDocumentsValidation
{
    const string Key = "ActDocumentsValidation.Play";
    static readonly string Output = Path.GetFullPath("../../output/act-documents-validation");
    static int checks;
    static double started;
    static bool testing;
    static ActDocumentsValidation()
    {
        if (SessionState.GetBool(Key, false)) Install();
    }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
    static void Invoke(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    static ActArchiveController Archive() => UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();

    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            var common = GameObject.Find("03 Reference books and notebook").transform;
            string originalCommon = JsonUtility.ToJson(common.GetChild(0).GetComponent<SceneObject>())
                + string.Join("|", common.Cast<Transform>().Where(t => t.name.StartsWith("Book_")).Select(t => t.name + t.localPosition + t.localRotation + t.localScale));
            string sky = GameObject.Find("08 Exterior night sky").name;
            DeskSceneBuilder.AddActDocumentsToSavedScene();
            DeskSceneBuilder.AddActDocumentsToSavedScene();
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            var archive = Archive();
            common = GameObject.Find("03 Reference books and notebook").transform;
            Check(originalCommon == JsonUtility.ToJson(common.GetChild(0).GetComponent<SceneObject>())
                + string.Join("|", common.Cast<Transform>().Where(t => t.name.StartsWith("Book_")).Select(t => t.name + t.localPosition + t.localRotation + t.localScale)), "Prayer books changed during build");
            Check(GameObject.Find(sky), "Night sky lost");
            Check(archive.acts.SelectMany(a => a.documents).Distinct().Count() == 38, "Expected 38 distinct documents");
            Check(!archive.transform.Find("Return current act"), "Removed return button still exists");
            Check(archive.acts.All(a => a.bundle.gameObject.activeSelf && a.documents.All(d => !d.gameObject.activeSelf)), "Saved default is not fully shelved");
            foreach (var text in archive.GetComponentsInChildren<TextMesh>(true))
            {
                text.font.RequestCharactersInTexture(text.text, text.fontSize);
                foreach (char c in text.text.Where(c => !char.IsWhiteSpace(c)))
                    Check(text.font.HasCharacter(c), "Missing glyph: " + c + " in " + text.transform.parent.name);
                if (text.name.EndsWith("Body"))
                    Check(text.GetComponent<MeshRenderer>().localBounds.size.y <= 0.206f, "Body exceeds paper: " + text.transform.parent.name);
            }
            Capture(Camera.main, "default-desktop");
            var view = Camera.main.GetComponent<DeskScenePreview>();
            Camera.main.transform.rotation = Quaternion.Euler(view.bookshelfEuler);
            Camera.main.fieldOfView = view.bookshelfFieldOfView;
            Capture(Camera.main, "default-bookshelf");
            archive.previewAllActs = false;
            Invoke(archive, "Awake");
            Check(archive.IsAvailable(1) && !archive.IsAvailable(2) && !archive.ShowActOnDesk(2), "Story availability gate failed");
            Check(archive.CompleteAct(1) && archive.IsAvailable(2) && archive.DeskAct == 0, "Next story act did not stay on shelf");
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            SessionState.SetBool(Key, true);
            SessionState.SetInt("ActDocumentsValidation.BuildChecks", checks);
            Install();
            EditorApplication.isPlaying = true;
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }
    static void Install()
    {
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
    }
    static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            Finish(false, message + "\n" + stack);
    }
    static void Click(SceneObject item, Camera camera, SceneObjectInteractor interactor)
    {
        var box = item.GetComponent<BoxCollider>();
        Vector3 point = camera.WorldToScreenPoint(item.transform.TransformPoint(box.center));
        Invoke(interactor, "ProcessPointer", (Vector2)point, true, true);
    }
    static void DeskCamera(Camera camera, DeskScenePreview view)
    {
        camera.transform.rotation = Quaternion.Euler(view.desktopEuler);
        camera.fieldOfView = 60;
    }
    static void Tick()
    {
        if (testing) return;
        if (EditorApplication.timeSinceStartup - started > 60) { Finish(false, "Play Mode timed out"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || !Archive() || !Archive().IsAvailable(5)) return;
        testing = true;
        try
        {
            var archive = Archive();
            var camera = Camera.main;
            var interactor = archive.interactor;
            var view = archive.view;
            view.followMouse = false;
            var shared = archive.sharedDocuments.Select(d => new { item = d, parent = d.transform.parent,
                position = d.transform.localPosition, rotation = d.transform.localRotation, scale = d.transform.localScale }).ToArray();
            Check(shared.Length == 3 && archive.DeskAct == 0, "Wrong initial shared/desk state");
            Check(archive.acts.All(a => a.bundle.gameObject.activeSelf && a.documents.All(d => !d.gameObject.activeSelf)), "Awake did not shelve all acts");
            Check(!archive.ShowActOnDesk(0) && !archive.ShowActOnDesk(6) && !archive.StoreDeskAct(), "Invalid actions accepted");
            foreach (var item in shared) Check(!archive.RegisterDocument(1, item.item), "Shared item accepted by archive");
            var originalIds = archive.acts.SelectMany(a => a.documents).Select(d => d.GetInstanceID()).ToArray();

            for (int act = 1; act <= 5; act++)
            {
                view.SetView(DeskScenePreview.View.Bookshelf);
                camera.transform.rotation = Quaternion.Euler(view.bookshelfEuler);
                camera.fieldOfView = view.bookshelfFieldOfView;
                Click(archive.acts[act - 1].bundle.GetComponent<SceneObject>(), camera, interactor);
                Check(archive.DeskAct == act && view.CurrentView == DeskScenePreview.View.Desktop, "Shelf click failed for act " + act);
                Check(!interactor.BlocksSceneInput, "Bundle entered inspection");
                for (int other = 1; other <= 5; other++)
                    Check(archive.acts[other - 1].documents.All(d => d.gameObject.activeInHierarchy == (other == act)), "Mixed desktop acts");
                DeskCamera(camera, view);
                Capture(camera, "act-" + act + "-desktop");
                foreach (var document in archive.acts[act - 1].documents)
                {
                    Click(document, camera, interactor);
                    Check(interactor.InspectedObject == document, "Paper unreachable by ray: " + document.name);
                    Invoke(document, "Animate", 2f);
                    Check(document.State == SceneObject.ObjectState.Inspecting, "Inspection failed");
                    if (document == archive.acts[act - 1].documents[1] || document.name.Contains("双栏") || document.name.Contains("裁纸"))
                        Capture(camera, document.name.Split('_')[0] + "-" + document.name.Split('_')[1] + "-read");
                    interactor.CancelInteraction();
                }
                foreach (var item in shared)
                    Check(item.item.gameObject.activeInHierarchy && item.item.transform.parent == item.parent
                        && item.item.transform.localPosition == item.position && item.item.transform.localRotation == item.rotation
                        && item.item.transform.localScale == item.scale, "Common reference moved");
            }

            var witness = archive.acts[4].documents[0];
            var ink = witness.transform.Find("Body").GetComponent<TextMesh>();
            ink.text += "\n保存批注";
            interactor.Inspect(witness);
            Invoke(witness, "Animate", 2f);
            Check(archive.ShowActOnDesk(2) && !interactor.BlocksSceneInput, "Switch did not cancel inspection");
            Check(archive.ShowActOnDesk(5) && ink.text.EndsWith("保存批注"), "Switch lost original content");
            DeskCamera(camera, view);
            Check(archive.StoreDeskAct() && archive.DeskAct == 0, "Store API failed");
            Check(archive.acts.All(a => a.bundle.gameObject.activeSelf && a.documents.All(d => !d.gameObject.activeSelf)), "Return did not shelve all papers");
            Check(originalIds.SequenceEqual(archive.acts.SelectMany(a => a.documents).Select(d => d.GetInstanceID())), "Original document identities changed");

            // A later delivery is stored with its own intended pose and restored on the next unfold.
            var delivery = new GameObject("Validation delivered document").AddComponent<SceneObject>();
            delivery.transform.position = new Vector3(0.2f, 0.82f, -0.1f);
            Check(archive.RegisterDocument(2, delivery) && !delivery.gameObject.activeSelf, "Delivery did not stay shelved");
            Check(!archive.RegisterDocument(3, delivery), "Duplicate membership accepted");
            archive.ShowActOnDesk(2);
            Check(delivery.gameObject.activeSelf && delivery.transform.position == new Vector3(0.2f, 0.82f, -0.1f), "Delivered document lost intended pose");
            int events = 0;
            archive.actCompleted.AddListener(_ => events++);
            Check(archive.CompleteAct(1) && archive.DeskAct == 0 && archive.CurrentAct == 2, "Completion did not leave papers shelved");
            Check(!archive.CompleteAct(1) && events == 1, "Duplicate act completion");
            for (int act = 2; act <= 5; act++) Check(archive.CompleteAct(act), "Completion failed");
            Check(archive.AllActsCompleted && events == 5 && archive.ShowActOnDesk(1), "Completed act review failed");
            Finish(true, "PASS: " + (checks + SessionState.GetInt("ActDocumentsValidation.BuildChecks", 0))
                + " assertions; Unity 2022.3.62f2 Play Mode / Direct3D 11.\n"
                + "38 distinct papers; repeat build and saved-scene reopen; Chinese glyphs and text bounds.\n"
                + "Default five shelf dossiers; ray-click every shelf and all 38 papers; one act at a time.\n"
                + "Inspect/return; store API; original instances/content retained; shared notebook and books protected.\n"
                + "Late delivery, invalid/duplicate registrations, act completion and completed-act review.\n");
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }
    static void Finish(bool success, string message)
    {
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        File.WriteAllText(Output + "/validation.txt", message);
        if (success) Debug.Log("ACT_DOCUMENTS_VALIDATION_PASS");
        else Debug.LogError(message);
        EditorApplication.Exit(success ? 0 : 1);
    }
    static void Capture(Camera camera, string name)
    {
        var target = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
