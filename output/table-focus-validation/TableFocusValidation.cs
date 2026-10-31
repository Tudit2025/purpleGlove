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
public static class TableFocusValidation
{
    const string Key = "TableFocusValidation.Play";
    static readonly string Output = Path.GetFullPath("../../output/table-focus-validation");
    static int checks;
    static bool running;
    static double started;
    static TableFocusValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static object Call(object target, string name, params object[] args) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    static bool KeyPress(DeskScenePreview view, KeyCode key) => (bool)Call(view, "HandleViewKey", key);
    static void Settle(DeskScenePreview view) => Call(view, "AnimateView", 10f, Vector2.zero);

    public static void Run()
    {
        Directory.CreateDirectory(Output);
        try
        {
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            Vector3 seat = Camera.main.transform.position;
            DeskSceneBuilder.ConfigureTableFocusView();
            DeskSceneBuilder.ConfigureTableFocusView();
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            Check(Camera.main.transform.position == seat, "Authored desktop camera moved");
            var view = Camera.main.GetComponent<DeskScenePreview>();
            Check(view.matFocusAnchor && view.matFocusAnchor.name == "WritingMat", "Missing mat anchor");
            Check((int)DeskScenePreview.View.Doorway == 1 && (int)DeskScenePreview.View.Bookshelf == 2, "Existing view enum values changed");
            Check(view.CurrentView == DeskScenePreview.View.Desktop, "Saved entry view changed");
            SessionState.SetInt("TableFocusValidation.BuildChecks", checks);
            SessionState.SetBool(Key, true); Install(); EditorApplication.isPlaying = true;
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
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
    static void CheckMatFraming(Camera camera, Transform mat)
    {
        foreach (float x in new[] { -0.51f, 0.51f })
        foreach (float z in new[] { -0.56f, 0.56f })
        {
            var p = camera.WorldToViewportPoint(mat.TransformPoint(new Vector3(x, 0.01f, z)));
            Check(p.z > 0 && p.x > 0.01f && p.x < 0.99f && p.y > 0.01f && p.y < 0.99f, "Mat corner outside view: " + p);
        }
    }
    static void Tick()
    {
        if (running) return;
        if (EditorApplication.timeSinceStartup - started > 65) { Finish(false, "Play Mode timeout"); return; }
        var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || !archive || !archive.IsAvailable(5)) return;
        running = true;
        try
        {
            var camera = Camera.main;
            var view = camera.GetComponent<DeskScenePreview>();
            var interactor = camera.GetComponent<SceneObjectInteractor>();
            view.followMouse = false; camera.aspect = 16f / 9;
            Vector3 seat = camera.transform.position;
            Check(!view.SetView(DeskScenePreview.View.Doorway), "Door accessible through ordinary SetView");
            Check(!view.SetView((DeskScenePreview.View)99), "Invalid view accepted");
            archive.ShowActOnDesk(4); Settle(view); Capture(camera, "desktop");
            Check(KeyPress(view, KeyCode.W) && view.CurrentView == DeskScenePreview.View.MatFocus, "W did not enter mat focus");
            Call(view, "AnimateView", 0.05f, Vector2.zero);
            Vector3 focusPosition = view.matFocusAnchor.TransformPoint(view.matFocusOffset);
            Check(camera.transform.position != seat && Vector3.Distance(camera.transform.position, focusPosition) > 0.1f, "Camera did not interpolate");
            Settle(view);
            Check(Vector3.Distance(camera.transform.position, focusPosition) < 0.0001f, "Wrong focus camera position");
            Check(camera.transform.eulerAngles.x > 65 && camera.transform.eulerAngles.x < 85, "Camera is not top-down enough");
            var center = camera.WorldToViewportPoint(view.matFocusAnchor.TransformPoint(new Vector3(0, 0.01f, 0)));
            Check(Vector2.Distance(new Vector2(center.x, center.y), Vector2.one * 0.5f) < 0.001f, "Mat not centered");
            CheckMatFraming(camera, view.matFocusAnchor); Capture(camera, "mat-focus");
            foreach (float x in new[] { -view.horizontalMouseAngle, view.horizontalMouseAngle })
            foreach (float y in new[] { -view.verticalMouseAngle, view.verticalMouseAngle })
            {
                Call(view, "AnimateView", 10f, new Vector2(x, y));
                CheckMatFraming(camera, view.matFocusAnchor);
            }
            Settle(view);
            for (int act = 1; act <= 5; act++)
            {
                archive.ShowActOnDesk(act); KeyPress(view, KeyCode.W); Settle(view);
                foreach (var document in archive.acts[act - 1].documents)
                {
                    var box = document.GetComponent<BoxCollider>();
                    Vector2 point = camera.WorldToScreenPoint(document.transform.TransformPoint(box.center));
                    Call(interactor, "ProcessPointer", point, true, true);
                    Check(interactor.InspectedObject == document, "Paper cannot be clicked in focus view: " + document.name);
                    Call(document, "Animate", 2f);
                    Check(!KeyPress(view, KeyCode.A) && !KeyPress(view, KeyCode.S), "Inspection allowed a view switch");
                    Vector3 lockedPosition = camera.transform.position;
                    Call(view, "LateUpdate");
                    Check(camera.transform.position == lockedPosition, "Inspection moved the camera");
                    interactor.CancelInteraction();
                }
            }
            foreach (var previous in new[] { DeskScenePreview.View.Desktop, DeskScenePreview.View.Bookshelf, DeskScenePreview.View.MatFocus })
            {
                Check(view.SetView(previous), "Could not select pre-event view"); Settle(view);
                view.SetDialogueLock(true); view.SetDialogueLock(true);
                Check(view.DialogueLocked && view.CurrentView == DeskScenePreview.View.Doorway, "Story event did not enter doorway");
                Check(camera.transform.position == seat && camera.fieldOfView == DeskScenePreview.DoorwayFieldOfView, "Door view retained overhead camera position");
                Check(Quaternion.Angle(camera.transform.rotation, Quaternion.Euler(view.doorwayEuler)) < 0.01f, "Wrong door rotation");
                foreach (var key in new[] { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.Escape })
                    Check(!KeyPress(view, key), "Door event escaped through player key " + key);
                if (previous == DeskScenePreview.View.MatFocus) Capture(camera, "event-doorway");
                view.SetDialogueLock(false); Settle(view);
                Check(view.CurrentView == previous, "Event did not restore previous view");
                Check(Vector3.Distance(camera.transform.position, previous == DeskScenePreview.View.MatFocus ? focusPosition : seat) < 0.0001f, "Event return position incorrect");
            }
            interactor.Inspect(archive.acts[4].documents[0]);
            view.SetDialogueLock(true);
            Check(!interactor.InspectedObject, "Door event left document in inspection");
            view.SetDialogueLock(false); Settle(view);
            Check(KeyPress(view, KeyCode.A), "A shortcut failed"); Settle(view);
            Check(view.CurrentView == DeskScenePreview.View.Bookshelf && camera.transform.position == seat, "Shelf did not return to seated origin");
            Capture(camera, "bookshelf");
            foreach (var key in new[] { KeyCode.S, KeyCode.D, KeyCode.Escape })
            {
                KeyPress(view, KeyCode.W); Settle(view); Check(KeyPress(view, key), "Desktop shortcut failed"); Settle(view);
                Check(view.CurrentView == DeskScenePreview.View.Desktop && camera.transform.position == seat && camera.fieldOfView == 60, "Wrong desktop return");
            }
            Finish(true, "PASS: " + (checks + SessionState.GetInt("TableFocusValidation.BuildChecks", 0)) + " assertions; Unity 2022.3.62f2 Play Mode / Direct3D 11.\n"
                + "W enters a centered ~73-degree mat view; smooth movement; all mat corners visible at maximum mouse offsets.\n"
                + "All 38 papers remain ray-clickable and inspectable in mat focus; inspection locks camera.\n"
                + "Ordinary doorway/invalid view requests rejected; event lock enters seated doorway and blocks WASD/Esc.\n"
                + "Event completion restores desktop, bookshelf or mat focus; entry cancels active inspection.\n"
                + "A returns to seated shelf; S/D/Esc return to seated desktop; repeat configuration and saved-scene reload verified.\n");
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }
    static void Finish(bool success, string message)
    {
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Output + "/validation.txt", message);
        if (success) Debug.Log("TABLE_FOCUS_VALIDATION_PASS"); else Debug.LogError(message);
        EditorApplication.Exit(success ? 0 : 1);
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
        finally { camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(image); }
    }
}
