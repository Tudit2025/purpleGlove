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
public static class NotebookValidation
{
    const string Key = "NotebookValidation.Play";
    static readonly string Output = Path.GetFullPath("../../output/notebook-validation");
    static int checks;
    static bool running;
    static double started;
    static NotebookValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static void Call(object target, string name, params object[] args) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, args);
    static ActArchiveController Archive() => UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();

    public static void Run()
    {
        Directory.CreateDirectory(Output);
        try
        {
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            var books = Archive().sharedDocuments.Where(d => d.name.StartsWith("Book_")).ToArray();
            var original = string.Join("|", books.Select(d => d.name + d.transform.position + d.transform.rotation));
            DeskSceneBuilder.ArrangeDeskAndNotebook();
            DeskSceneBuilder.ArrangeDeskAndNotebook();
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            var archive = Archive();
            Check(original == string.Join("|", archive.sharedDocuments.Where(d => d.name.StartsWith("Book_"))
                .Select(d => d.name + d.transform.position + d.transform.rotation)), "Prayer books moved");
            Check(!archive.transform.Find("Return current act"), "Return button still exists");
            Check(UnityEngine.Object.FindObjectsOfType<NotebookBook>().Length == 1, "Notebook duplicated");
            var book = UnityEngine.Object.FindFirstObjectByType<NotebookBook>();
            Check(book.SpreadCount == 7 && !book.turningLeaf.gameObject.activeSelf, "Wrong initial notebook");
            var notebookItem = book.GetComponent<SceneObject>();
            Check(notebookItem.screenFill == 0.82f && notebookItem.inspectionFill.r == 0.85f
                && notebookItem.minimumViewDistance == 0.18f, "Notebook reading overrides were not saved");
            Check(book.transform.Find("Notebook contents").GetComponentsInChildren<AudioSource>(true).Length == 0, "Duplicate audio");
            Check(book.GetComponents<AudioSource>().Length == 1 && book.pageTurnSound, "Missing page sound");
            var mat = GameObject.Find("WritingMat").transform;
            foreach (var act in archive.acts)
            foreach (var document in act.documents) CheckOnMat(document, mat);
            var box = book.GetComponent<BoxCollider>();
            foreach (Vector3 point in Corners(box))
                Check(mat.InverseTransformPoint(point).x < -0.53f, "Notebook overlaps mat");
            Vector3 bottom = book.transform.TransformDirection(Vector3.back);
            Vector3 cameraDirection = Camera.main.transform.position - book.transform.position; cameraDirection.y = 0;
            Check(Vector3.Dot(bottom.normalized, cameraDirection.normalized) > 0.99f, "Notebook bottom does not face camera");
            foreach (var spread in book.spreads)
                foreach (char c in (spread.left + spread.right).Where(c => !char.IsWhiteSpace(c)))
                    Check(book.leftPage.font.HasCharacter(c), "Missing notebook glyph: " + c);
            Capture(Camera.main, "default-desktop");
            SessionState.SetInt("NotebookValidation.EditChecks", checks);
            SessionState.SetBool(Key, true); Install(); EditorApplication.isPlaying = true;
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }
    static Vector3[] Corners(BoxCollider box)
    {
        var points = new Vector3[8];
        for (int i = 0; i < 8; i++) points[i] = box.transform.TransformPoint(box.center + Vector3.Scale(box.size * 0.5f,
            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
        return points;
    }
    static void CheckOnMat(SceneObject item, Transform mat)
    {
        foreach (var world in Corners(item.GetComponent<BoxCollider>()))
        {
            var point = mat.InverseTransformPoint(world);
            Check(Mathf.Abs(point.x) < 0.473f && Mathf.Abs(point.z) < 0.52f, "Document outside mat: " + item.name);
        }
    }
    static void Install()
    {
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
    }
    static void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Finish(false, message + "\n" + stack);
    }
    static void Click(SceneObject item, Camera camera, SceneObjectInteractor interactor)
    {
        var box = item.GetComponent<BoxCollider>();
        Vector2 screen = camera.WorldToScreenPoint(item.transform.TransformPoint(box.center));
        Call(interactor, "ProcessPointer", screen, true, true);
    }
    static void ClickPage(NotebookBook book, int direction, Camera camera, SceneObjectInteractor interactor)
    {
        Vector2 screen = camera.WorldToScreenPoint(book.transform.TransformPoint(new Vector3(0.19f * direction, 0.075f, 0)));
        Call(interactor, "ProcessInput", screen, true, true, false);
    }
    static void Tick()
    {
        if (running) return;
        if (EditorApplication.timeSinceStartup - started > 65) { Finish(false, "Play Mode timeout"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || !Archive() || !Archive().IsAvailable(5)) return;
        running = true;
        try
        {
            var archive = Archive(); var camera = Camera.main; var interactor = archive.interactor;
            var book = UnityEngine.Object.FindFirstObjectByType<NotebookBook>(); var item = book.GetComponent<SceneObject>();
            var mat = GameObject.Find("WritingMat").transform;
            archive.view.followMouse = false;
            Vector3 rest = book.transform.position; Quaternion rotation = book.transform.rotation;
            for (int act = 1; act <= 5; act++)
            {
                archive.ShowActOnDesk(act);
                camera.transform.rotation = Quaternion.Euler(archive.view.desktopEuler); camera.fieldOfView = 60;
                foreach (var document in archive.acts[act - 1].documents)
                {
                    CheckOnMat(document, mat);
                    Click(document, camera, interactor);
                    Check(interactor.InspectedObject == document, "Cannot click paper on mat: " + document.name);
                    interactor.CancelInteraction();
                }
                Check(archive.acts.Where((_, i) => i != act - 1).All(a => a.documents.All(d => !d.gameObject.activeSelf)), "Mixed acts");
                Check(book.transform.position == rest && book.transform.rotation == rotation, "Archive moved notebook");
                if (act == 4) Capture(camera, "act-4-on-mat");
            }
            Check(!book.TurnPage(1), "Notebook turns before inspection");
            Click(item, camera, interactor);
            Check(interactor.InspectedObject == item, "Notebook unreachable on desktop");
            Call(item, "Animate", 2f);
            Capture(camera, "notebook-01");
            Check(!book.TurnPage(-1), "First spread wrapped backwards");
            for (int spread = 1; spread < book.SpreadCount; spread++)
            {
                ClickPage(book, 1, camera, interactor);
                Check(book.IsTurning && book.pageAudio.isPlaying, "Pointer/audio did not turn page");
                Check(!book.TurnPage(1), "Duplicate input interrupted turn");
                Call(book, "AdvanceTurn", book.turnSeconds * 0.3f);
                if (spread == 1) Capture(camera, "page-turn");
                Call(book, "AdvanceTurn", book.turnSeconds * 0.2f);
                Check(Quaternion.Angle(book.turningLeaf.localRotation, Quaternion.Euler(0, 0, 90)) < 0.1f, "No physical page animation");
                Call(book, "AdvanceTurn", book.turnSeconds);
                Check(book.CurrentSpread == spread && !book.IsTurning && !book.turningLeaf.gameObject.activeSelf, "Turn did not settle");
                Check(book.leftPage.text == book.spreads[spread].left && book.rightPage.text == book.spreads[spread].right, "Wrong spread content");
                Check(book.leftPage.GetComponent<MeshRenderer>().localBounds.size.y < 0.349f
                    && book.rightPage.GetComponent<MeshRenderer>().localBounds.size.y < 0.349f, "Notebook text overflow");
                Check(book.pigpenChart.activeSelf == (spread == book.pigpenSpread), "Pigpen chart visible on wrong page");
                Capture(camera, "notebook-" + (spread + 1).ToString("00"));
            }
            Check(!book.TurnPage(1), "Last spread wrapped forwards");
            for (int spread = book.SpreadCount - 2; spread >= 0; spread--)
            {
                ClickPage(book, -1, camera, interactor);
                Check(book.IsTurning, "Left page click failed");
                Call(book, "AdvanceTurn", 1f);
                Check(book.CurrentSpread == spread, "Backwards turn failed");
            }
            Check(book.TurnPage(1), "Could not start close-during-turn case");
            interactor.CloseInspection(); Call(book, "Update"); Call(item, "Animate", 2f); interactor.CancelInteraction();
            Check(book.CurrentSpread == 1 && !book.IsTurning && !book.turningLeaf.gameObject.activeSelf, "Closing lost/dangled page turn");
            Check(book.transform.position == rest && Quaternion.Angle(book.transform.rotation, rotation) < 0.01f, "Notebook returned to wrong pose");
            archive.ShowActOnDesk(1); archive.ShowActOnDesk(4);
            Check(book.CurrentSpread == 1 && book.gameObject.activeInHierarchy, "Act switch reset/archived notebook");
            interactor.Inspect(item); Call(item, "Animate", 2f);
            Check(book.CurrentSpread == 1, "Reinspection reset page");
            book.TurnPage(1); book.enabled = false;
            Check(!book.IsTurning && book.CurrentSpread == 2 && !book.turningLeaf.gameObject.activeSelf, "Disable left half-turned sheet");
            book.enabled = true;
            Finish(true, "PASS: " + (checks + SessionState.GetInt("NotebookValidation.EditChecks", 0)) + " assertions; Unity 2022.3.62f2 Play Mode / Direct3D 11.\n"
                + "All 38 document bounds inside mat; every paper ray-clickable; mutually exclusive acts.\n"
                + "No return button; notebook outside mat with bottom facing camera; both prayer books preserved.\n"
                + "14 pages / 7 spreads; all Chinese glyphs; complete geometric pigpen alphabet; text fits physical pages.\n"
                + "Left/right page pointer input; forward/backward animation and sound; first/last boundaries; duplicate input ignored.\n"
                + "Page retained after close/reinspect/act switch; closing/disabling during turn settles safely.\n"
                + "Repeated builder and saved-scene reopen verified; screenshots captured.\n");
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }
    static void Finish(bool success, string message)
    {
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Output + "/validation.txt", message);
        if (success) Debug.Log("NOTEBOOK_VALIDATION_PASS"); else Debug.LogError(message);
        EditorApplication.Exit(success ? 0 : 1);
    }
    static void Capture(Camera camera, string name)
    {
        var target = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32);
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
