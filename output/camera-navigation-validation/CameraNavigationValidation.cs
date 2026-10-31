using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PurpleGlove;
using PurpleGlove.Editor;

// Copy into Assets/Editor in an isolated project; run with -executeMethod CameraNavigationValidation.Run.
[InitializeOnLoad]
public static class CameraNavigationValidation
{
    const string Key = "CameraNavigationValidation.Play";
    static readonly string Output = Path.GetFullPath("../../output/camera-navigation-validation");
    static double started;
    static bool running;
    static int checks;
    static CameraNavigationValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    static object Call(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, args);
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static void Ungate(SceneInputGate gate) => typeof(SceneInputGate).GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gate, -1);
    static void Settle(DeskScenePreview view) { Call(view, "AnimateView", 10f, new Vector2(5, 3)); Physics.SyncTransforms(); }
    static bool Press(DeskScenePreview view, KeyCode key) => (bool)Call(view, "HandleViewKey", key);
    static Vector2 ArrowPointer(DeskViewHud hud, DeskScenePreview.Direction direction)
    {
        var rect = (Rect)Call(hud, "ArrowBounds", direction);
        return new Vector2(rect.center.x, Screen.height - rect.center.y);
    }

    public static void Run()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
        SessionState.SetBool(Key, true); Install(); EditorApplication.isPlaying = true;
    }
    static void Install()
    {
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
    }
    static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Finish(false, message + "\n" + stack); }

    static void Tick()
    {
        if (running) return;
        if (EditorApplication.timeSinceStartup - started > 90) { Finish(false, "Play mode timeout"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || !Camera.main || Time.frameCount < 12) return;
        running = true;
        try
        {
            var camera = Camera.main;
            var view = camera.GetComponent<DeskScenePreview>();
            var gate = camera.GetComponent<SceneInputGate>();
            var hud = camera.GetComponent<DeskViewHud>();
            var interactor = camera.GetComponent<SceneObjectInteractor>();
            var work = camera.GetComponent<CipherWorkbench>();
            view.followMouse = false; camera.aspect = 16f / 9; Ungate(gate);
            Check(view.matFocusAnchor && hud && gate, "Missing saved scene references");
            Check(view.matFocusOffset.z == 0, "Saved mat camera is not centered");
            Check(hud.arrowOpacity > 0 && hud.arrowOpacity < hud.arrowHoverOpacity, "Arrow hover contrast missing");
            foreach (char c in "Esc 返回中桌面视角") Check(hud.labelFont.HasCharacter(c), "Missing hint glyph " + c);

            // Every visible arrow must lead to the requested destination; other edges stay non-interactive.
            var sources = new[] { DeskScenePreview.View.Desktop, DeskScenePreview.View.Bookshelf,
                DeskScenePreview.View.DecodeFocus, DeskScenePreview.View.MatFocus };
            var expected = new[] {
                new[] { 3, 2, 4, -1 }, new[] { -1, -1, 0, -1 },
                new[] { -1, 3, -1, -1 }, new[] { -1, -1, 4, 0 }
            };
            foreach (Rect viewport in new[] { new Rect(0, 0, 1, 1), new Rect(0.12f, 0.18f, 0.76f, 0.64f) })
            {
                camera.rect = viewport;
                for (int s = 0; s < sources.Length; s++)
                for (int d = 0; d < 4; d++)
                {
                    Ungate(gate); Check(view.SetView(sources[s]), "Cannot enter navigation source");
                    var direction = (DeskScenePreview.Direction)d;
                    bool available = view.TryGetNavigationTarget(direction, out var target);
                    Check(available == (expected[s][d] >= 0), "Wrong visible arrow " + sources[s] + "/" + direction);
                    Vector2 pointer = ArrowPointer(hud, direction);
                    Check(camera.pixelRect.Contains(pointer), "Arrow outside camera viewport");
                    Check(hud.BlocksPointer(pointer) == available, "Wrong pointer reservation");
                    Call(hud, "ProcessPointer", pointer, true);
                    Check(view.CurrentView == (available ? (DeskScenePreview.View)expected[s][d] : sources[s]), "Arrow switched to wrong camera");
                    Check(gate.FrameConsumed == available, "Navigation click leaked through input gate");
                    if (available)
                    {
                        Call(interactor, "ProcessInput", pointer, true, true, false);
                        Check(!interactor.InspectedObject, "Arrow click also picked a world object");
                    }
                }
            }
            camera.rect = new Rect(0, 0, 1, 1); Ungate(gate);
            // WASD follows the same adjacent routes, except the deliberate decode keyboard lock.
            var keys = new[] { KeyCode.W, KeyCode.A, KeyCode.D, KeyCode.S };
            for (int s = 0; s < sources.Length; s++)
            for (int d = 0; d < keys.Length; d++)
            {
                view.SetView(sources[s]);
                bool available = sources[s] != DeskScenePreview.View.DecodeFocus && expected[s][d] >= 0;
                Check(Press(view, keys[d]) == available, "Wrong keyboard route availability: " + sources[s] + "/" + keys[d]);
                Check(view.CurrentView == (available ? (DeskScenePreview.View)expected[s][d] : sources[s]),
                    "WASD did not follow the adjacent view: " + sources[s] + "/" + keys[d]);
            }
            view.SetView(DeskScenePreview.View.Bookshelf);
            Check(Press(view, KeyCode.D) && view.CurrentView == DeskScenePreview.View.Desktop, "First D skipped desktop from bookshelf");
            Check(Press(view, KeyCode.D) && view.CurrentView == DeskScenePreview.View.DecodeFocus, "Second D did not enter decode from desktop");
            foreach (var source in sources)
            {
                view.SetView(source);
                Check(Press(view, KeyCode.Escape) && view.CurrentView == DeskScenePreview.View.Desktop, "Esc cannot return from " + source);
            }

            // Use the saved collider and real ray picking, without adding a runtime click component.
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            Check(archive.ReceiveAct(1), "Could not display archive documents");
            Settle(view);
            var document = archive.acts[0].documents[0];
            var documentBox = document.GetComponent<BoxCollider>();
            Vector2 documentPointer = camera.WorldToScreenPoint(document.transform.TransformPoint(documentBox.center));
            Call(interactor, "ProcessInput", documentPointer, true, true, false);
            Check(interactor.InspectedObject == document && view.CurrentView == DeskScenePreview.View.Desktop,
                "Clicking a document should inspect it, not enter mat top-down");
            interactor.CancelInteraction(); Physics.SyncTransforms();
            var matCollider = view.matFocusAnchor.GetComponent<Collider>();
            bool found = false;
            for (float x = -0.48f; x <= 0.48f && !found; x += 0.08f)
            for (float z = -0.52f; z <= 0.52f && !found; z += 0.08f)
            {
                Vector3 point = camera.WorldToScreenPoint(view.matFocusAnchor.TransformPoint(new Vector3(x, 0.008f, z)));
                if (!camera.pixelRect.Contains(point) || hud.BlocksPointer(point)) continue;
                if (!Physics.Raycast(camera.ScreenPointToRay(point), out var hit, 10) || hit.collider != matCollider) continue;
                Call(interactor, "ProcessInput", (Vector2)point, true, true, false); found = true;
            }
            Check(found && view.CurrentView == DeskScenePreview.View.MatFocus, "Clicking exposed mat did not enter top-down");
            Check(gate.FrameConsumed && !interactor.InspectedObject, "Mat became inspected or click not consumed");
            Ungate(gate); Settle(view);
            foreach (float aspect in new[] { 4f / 3, 16f / 9, 21f / 9 })
            {
                camera.aspect = aspect;
                foreach (float x in new[] { -0.51f, 0.51f })
                foreach (float z in new[] { -0.56f, 0.56f })
                {
                    var p = camera.WorldToViewportPoint(view.matFocusAnchor.TransformPoint(new Vector3(x, 0.01f, z)));
                    Check(p.z > 0 && p.x > 0 && p.x < 1 && p.y > 0 && p.y < 1, "Full mat clipped at " + aspect);
                }
            }
            camera.aspect = 16f / 9;
            Capture(camera, "archive-topdown");
            float fullHeight = camera.transform.position.y;
            float fov = camera.fieldOfView;
            Call(view, "AdjustFocusZoom", 100f); Settle(view);
            Check(Mathf.Abs(camera.transform.position.y - view.matFocusAnchor.position.y - view.matFocusOffset.y * 0.6f) < 0.001f, "Mat close height or clamp wrong");
            Check(Vector3.Dot(camera.transform.forward, Vector3.down) > 0.9999f && Mathf.Abs(camera.fieldOfView - fov) < 0.001f, "Mat zoom changed orientation/FOV");
            Capture(camera, "archive-close");
            Call(view, "AdjustFocusZoom", -100f); Settle(view);
            Check(Mathf.Abs(camera.transform.position.y - fullHeight) < 0.001f, "Mat far clamp wrong");
            Call(view, "AdjustFocusZoom", 100f); view.SetView(DeskScenePreview.View.Desktop); view.SetView(DeskScenePreview.View.MatFocus); Settle(view);
            Check(Mathf.Abs(camera.transform.position.y - fullHeight) < 0.001f, "Reentry did not reset mat height");

            // Modal navigation/zoom stays blocked; decode's Esc still ends editing before leaving.
            gate.SetConsole(true); Ungate(gate);
            Check(!view.CanNavigate && !view.SetView(DeskScenePreview.View.Desktop), "Console permits navigation");
            Call(view, "AdjustFocusZoom", 100f); Settle(view);
            Check(Mathf.Abs(camera.transform.position.y - fullHeight) < 0.001f, "Console permits zoom");
            gate.SetConsole(false); Ungate(gate);
            view.SetDialogueLock(true);
            Check(!view.CanNavigate && !view.TryFocusMat(matCollider), "Dialogue permits mat navigation");
            view.SetDialogueLock(false);
            view.BeginDeliveryView(); Check(!view.CanNavigate, "Delivery permits navigation"); view.EndDeliveryView(false);
            view.SetView(DeskScenePreview.View.DecodeFocus); Settle(view);
            float decodeFull = camera.transform.position.y;
            Call(view, "AdjustFocusZoom", 100f); Settle(view);
            Check(camera.transform.position.y < decodeFull && Vector3.Dot(camera.transform.forward, Vector3.down) > 0.9999f, "Decode zoom regressed");
            Call(view, "AdjustFocusZoom", -100f); Settle(view);
            var paper = work.mat.GetComponentsInChildren<CipherWorkbenchTarget>().First(t => t.kind == CipherWorkbenchTarget.Kind.Plaintext);
            Call(work, "ActivateTarget", paper, (Vector2)camera.WorldToScreenPoint(paper.transform.position)); Ungate(gate);
            Check(gate.WorkbenchTyping && !view.CanNavigate, "Typing permits navigation");
            foreach (char c in "wasd") Call(work, "HandleWritingInput", new Event { type = EventType.KeyDown, character = c, keyCode = (KeyCode)c });
            Check(work.Plaintext == "wasd", "WASD text input intercepted");
            Call(view, "AdjustFocusZoom", 100f); Settle(view);
            Check(Mathf.Abs(camera.transform.position.y - decodeFull) < 0.001f, "Typing permits zoom");
            Check((bool)Call(work, "HandleEscape") && !gate.WorkbenchTyping, "Esc did not end typing");
            Ungate(gate); Call(work, "Update");
            Check(view.CurrentView == DeskScenePreview.View.DecodeFocus, "Typing Esc also left workspace");
            Check((bool)Call(work, "HandleEscape"), "Workspace Esc not handled");
            Ungate(gate); Call(work, "Update");
            Check(view.CurrentView == DeskScenePreview.View.Desktop && work.Plaintext == "wasd", "Workspace Esc failed or lost notes");

            // Moving-tool gate must not discard the requested camera entry.
            work.SelectTool(CipherWorkbench.Tool.Caesar); Ungate(gate); Call(work, "Update");
            Check(work.IsManipulating && !view.CanNavigate, "Transfer permits navigation");
            Call(work, "AnimateToolTransfer", 1f); Ungate(gate); Call(work, "Update");
            Check(view.CurrentView == DeskScenePreview.View.DecodeFocus && work.ActiveTool == CipherWorkbench.Tool.Caesar, "Transfer lost pending workspace entry");
            Settle(view); Capture(camera, "decode-topdown");
            var letter = work.letterSamples[0].GetComponent<CipherWorkbenchTarget>();
            Call(work, "ActivateTarget", letter, (Vector2)camera.WorldToScreenPoint(letter.transform.position)); Ungate(gate);
            Check(work.IsManipulating && !view.CanNavigate && !hud.BlocksPointer(ArrowPointer(hud, DeskScenePreview.Direction.Left)), "Dragging permits arrow navigation");
            Call(work, "HandleEscape"); Ungate(gate);
            Check(!work.IsManipulating && view.CurrentView == DeskScenePreview.View.DecodeFocus, "Drag Esc should cancel before leaving");
            view.SetView(DeskScenePreview.View.Desktop); Settle(view);
            var notebook = UnityEngine.Object.FindFirstObjectByType<NotebookBook>().GetComponent<SceneObject>();
            interactor.Inspect(notebook);
            Check(!view.CanNavigate && !view.TryFocusMat(matCollider), "Inspection permits navigation");
            interactor.CancelInteraction();
            Check(view.CanNavigate, "Navigation did not recover after inspection");
            Capture(camera, "desktop");
            Finish(true, "PASS: " + checks + " assertions in Unity 2022.3.62f2 Play Mode.\n"
                + "Saved scene: real mat collider click, 90-degree top-down, height limits/reset and 4:3/16:9/21:9 framing.\n"
                + "All edge routes including archive right-to-decode, inactive edges, offset viewport hit areas, click consumption.\n"
                + "Relative WASD navigation matches the arrows; bookshelf D returns to desktop; decode keyboard lock and Esc return retained.\n"
                + "Typing WASD, layered Escape, tool transfer, dragging, console, dialogue, delivery and inspection gates.\n"
                + "Rendered camera screenshots exclude IMGUI; pointer/key replay is synthetic, not manual OS input.\n");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }

    static void Finish(bool success, string message)
    {
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Output + "/validation.txt", message);
        if (success) Debug.Log("CAMERA_NAVIGATION_VALIDATION_PASS"); else Debug.LogError(message);
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
