using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PurpleGlove;
using PurpleGlove.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run in an isolated project copy with -executeMethod ReadingZoomValidation.Run.
[InitializeOnLoad]
public static class ReadingZoomValidation
{
    const string Key = "ReadingZoomValidation.Play";
    static readonly string Output = Path.GetFullPath("../../output/reading-zoom-validation");
    static Camera camera;
    static SceneObjectInteractor interactor;
    static SceneInputGate gate;
    static DeskScenePreview view;
    static int checks;
    static bool finished;
    static double started;
    static ReadingZoomValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    static object Call(object target, string name, params object[] args) => target.GetType()
        .GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    static void Near(float a, float b, string message) => Check(Mathf.Abs(a - b) < 0.001f, message);
    static void Ungate() => typeof(SceneInputGate).GetField("ignoreThroughFrame", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(gate, -1);
    static void Scroll(float amount, bool canPoint = true) => Call(interactor, "ProcessScroll", camera.pixelRect.center, canPoint, amount);
    static void Settle(SceneObject item) => Call(item, "Animate", 3f);
    static float Height(SceneObject item)
    {
        var bounds = item.GetComponent<BoxCollider>();
        Vector3 min = Vector3.one * float.PositiveInfinity, max = Vector3.one * float.NegativeInfinity;
        for (int i = 0; i < 8; i++)
        {
            var corner = bounds.center + Vector3.Scale(bounds.size * 0.5f,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            var projected = camera.WorldToViewportPoint(item.transform.TransformPoint(corner));
            Check(projected.z > camera.nearClipPlane, item.name + " intersects near clip plane");
            min = Vector3.Min(min, projected); max = Vector3.Max(max, projected);
        }
        return max.y - min.y;
    }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
        // Keep the real desk, documents and input components; stop story autoplay in this copy only.
        foreach (var behaviour in Camera.main.GetComponents<MonoBehaviour>())
            if (!(behaviour is DeskScenePreview) && !(behaviour is SceneObjectInteractor)
                && !(behaviour is SceneInputGate) && !(behaviour is DeskViewHud) && !(behaviour is PixelSceneView))
                behaviour.enabled = false;
        foreach (var memory in UnityEngine.Object.FindObjectsOfType<FirstInteractionMemory>(true)) memory.enabled = false;
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
        if (finished) return;
        if (EditorApplication.timeSinceStartup - started > 90) { Finish(false, "Timed out"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.frameCount < 10) return;
        try
        {
            camera = Camera.main; interactor = camera.GetComponent<SceneObjectInteractor>();
            gate = camera.GetComponent<SceneInputGate>(); view = camera.GetComponent<DeskScenePreview>();
            view.followMouse = false;
            gate.SetStory(false); gate.SetDialogue(false); gate.SetConsole(false); gate.SetPropAction(false);
            gate.SetDelivery(false); gate.SetWorkbenchModal(false); gate.SetWorkbenchTyping(false); Ungate();
            var archive = UnityEngine.Object.FindObjectOfType<ActArchiveController>();
            archive.previewAllActs = true;
            Check(!interactor.CanZoomInspection, "Zoom available without a document"); Scroll(0.1f);
            for (int act = 1; act <= 5; act++)
            {
                if (!archive.IsAvailable(act)) Check(archive.ReceiveAct(act), "Cannot receive act " + act);
                Check(archive.ShowActOnDesk(act), "Cannot show act " + act);
                Call(view, "AnimateView", 3f, Vector2.zero);
                foreach (var item in archive.acts[act - 1].documents.Where(d => !d.GetComponent<DeskOperationalDocument>() && !d.GetComponent<SceneObjectAction>()))
                {
                    Vector3 rest = item.transform.localPosition, scale = item.transform.localScale;
                    interactor.Inspect(item); Settle(item);
                    Check(interactor.InspectedObject == item && interactor.CanZoomInspection, "Cannot read " + item.name);
                    float originalHeight = Height(item);
                    Vector3 cameraPosition = camera.transform.position; float fov = camera.fieldOfView;
                    Scroll(0.1f); Settle(item);
                    Check(item.InspectionZoom > 1 && Height(item) > originalHeight, "Wheel up does not enlarge " + item.name);
                    Scroll(-0.1f); Settle(item); Near(item.InspectionZoom, 1, "Opposite steps do not cancel");
                    Scroll(100); Settle(item); Near(item.InspectionZoom, 2, "Upper limit"); Height(item);
                    Scroll(-100); Settle(item); Near(item.InspectionZoom, 0.75f, "Lower limit");
                    Check(Height(item) < originalHeight, "Wheel down does not shrink " + item.name);
                    Check(camera.transform.position == cameraPosition && camera.fieldOfView == fov, "Reading moved scene camera");
                    interactor.CloseInspection(); Scroll(100); Near(item.InspectionZoom, 1, "Returning document accepts zoom");
                    Settle(item); interactor.CancelInteraction();
                    Check(item.State == SceneObject.ObjectState.Resting && item.transform.localPosition == rest
                        && item.transform.localScale == scale, "Return lost rest pose");
                }
            }
            var paper = archive.acts[4].documents.First(d => !d.GetComponent<DeskOperationalDocument>() && !d.GetComponent<SceneObjectAction>());
            interactor.Inspect(paper); Settle(paper); Capture("paper-100");
            Scroll(100); Settle(paper); Capture("paper-200");
            Scroll(-100); Settle(paper); Capture("paper-75");
            interactor.CancelInteraction(); interactor.Inspect(paper); Settle(paper);
            Scroll(1, false); Near(paper.InspectionZoom, 1, "Invalid pointer accepts zoom");
            foreach (string modal in new[] { "SetDialogue", "SetConsole", "SetStory", "SetPropAction", "SetWorkbenchTyping", "SetWorkbenchModal" })
            {
                Call(gate, modal, true); Ungate(); Scroll(1);
                Check(!interactor.CanZoomInspection && paper.InspectionZoom == 1, modal + " leaked zoom");
                Call(gate, modal, false); Scroll(1); Near(paper.InspectionZoom, 1, modal + " closing frame leaked zoom"); Ungate();
            }
            gate.SetDelivery(true); Ungate(); Scroll(1); Near(paper.InspectionZoom, 1, "Delivery leaked zoom");
            gate.SetDelivery(false); Ungate();
            Scroll(0.1f); Check(paper.InspectionZoom > 1, "Zoom did not recover after modal");
            paper.enabled = false; Near(paper.InspectionZoom, 1, "Disabling paper retained zoom");
            paper.enabled = true; interactor.CancelInteraction();

            var book = UnityEngine.Object.FindObjectOfType<NotebookBook>();
            var bookItem = book.GetComponent<SceneObject>();
            interactor.Inspect(bookItem); Settle(bookItem); Scroll(0.1f); Settle(bookItem);
            int spread = book.CurrentSpread; float zoom = bookItem.InspectionZoom;
            Check(book.TurnPage(1), "Zoom blocked page turning"); Call(book, "AdvanceTurn", 2f); Ungate();
            Check(book.CurrentSpread == spread + 1 && bookItem.InspectionZoom == zoom, "Turn reset page or zoom");
            var point = (Vector2)camera.WorldToScreenPoint(book.transform.TransformPoint(new Vector3(0.19f, 0.075f, 0)));
            Call(interactor, "ProcessInput", point, true, true, false);
            Check(book.IsTurning, "Zoom displaced physical page hit area"); Call(book, "AdvanceTurn", 2f); Ungate();
            var hud = camera.GetComponent<DeskViewHud>();
            Rect arrow = (Rect)Call(hud, "ArrowBounds", DeskScenePreview.Direction.Left);
            Call(interactor, "ProcessScroll", new Vector2(arrow.center.x, Screen.height - arrow.center.y), true, 1f);
            Near(bookItem.InspectionZoom, zoom, "Book arrow accepts wheel zoom");
            foreach (float aspect in new[] { 4f / 3, 16f / 9, 21f / 9 })
            {
                camera.aspect = aspect; Scroll(100); Settle(bookItem); Height(bookItem);
                Scroll(-100); Settle(bookItem); Height(bookItem);
            }
            camera.ResetAspect();
            int savedSpread = book.CurrentSpread;
            interactor.CancelInteraction(); interactor.Inspect(bookItem); Settle(bookItem);
            Near(bookItem.InspectionZoom, 1, "Reopen retained zoom"); Check(book.CurrentSpread == savedSpread, "Reopen lost page");
            interactor.CancelInteraction();

            while (archive.CurrentAct < 5) Check(archive.CompleteAct(archive.CurrentAct), "Cannot prepare rack");
            Check(archive.ShowActOnDesk(5), "Cannot reopen act five");
            foreach (var form in UnityEngine.Object.FindObjectsOfType<DeskOperationalDocument>().Where(f => f.Available))
            {
                Ungate(); Call(view, "AnimateView", 3f, Vector2.zero);
                Vector3 before = camera.transform.position; Quaternion rotation = camera.transform.rotation; float fov = camera.fieldOfView;
                var item = form.GetComponent<SceneObject>(); interactor.Inspect(item); Ungate();
                Scroll(100); Near(item.InspectionZoom, 1, "Form zoomed during opening");
                Call(form, "AdvanceTime", 2f); Settle(item);
                Check(form.IsReady && interactor.CanZoomInspection, "Ready form cannot zoom");
                float originalHeight = Height(item);
                Scroll(0.1f); Call(form, "AdvanceTime", 0.01f);
                float partial = Height(item); Call(form, "AdvanceTime", 2f);
                Check(Height(item) > partial && partial > originalHeight, "Form zoom is not smooth");
                Scroll(100); Call(form, "AdvanceTime", 2f); Height(item);
                var choices = item.GetComponent<FifthActDocument>();
                if (choices && choices.areas.Length > 0)
                {
                    var area = choices.areas[0]; Scroll(-100); Call(form, "AdvanceTime", 2f);
                    Vector2 pointer = camera.WorldToScreenPoint(item.transform.TransformPoint(new Vector3(area.center.x, 0.011f, area.center.y)));
                    Check(choices.ChoiceAtPointer(camera, pointer) == 0, "Form choice hit area moved after zoom");
                }
                Capture("form-" + form.name);
                interactor.CancelInteraction();
                Check(!form.IsOpen && !view.StoryLocked && camera.transform.position == before
                    && Quaternion.Angle(camera.transform.rotation, rotation) < 0.01f && camera.fieldOfView == fov,
                    "Closing form failed to restore camera");
                Near(item.InspectionZoom, 1, "Closing form retained zoom");
            }
            Check(view.SetView(DeskScenePreview.View.MatFocus), "Cannot restore mat navigation");
            Call(view, "AdjustFocusZoom", 0.1f); Call(view, "AnimateView", 3f, Vector2.zero);
            Check(!interactor.CanZoomInspection && view.CanNavigate, "Reading zoom leaked into desk navigation");
            Finish(true, "PASS: " + checks + " assertions; Unity 2022.3.62f2 Play Mode.\n"
                + "Real scene documents: wheel direction, 75%-200% bounds, smooth rack camera zoom, near clipping, rest poses and reset.\n"
                + "Books: page turns, physical clicks, arrow input, retained page; 4:3, 16:9 and 21:9 framing.\n"
                + "Modal/consumed-frame/pointer guards, rack choice hits, camera restoration and desk navigation.\n"
                + "Synthetic input replay; OS mouse wheel was not exercised. Rendered screenshots exclude IMGUI hints.\n");
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }
    static void Capture(string name)
    {
        var target = RenderTexture.GetTemporary(1600, 900, 24);
        var previous = camera.targetTexture; var active = RenderTexture.active;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(Output, name + ".png"), image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous; RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(image);
        }
    }
    static void Finish(bool success, string message)
    {
        if (finished) return; finished = true; SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Path.Combine(Output, "validation.txt"), message);
        if (success) Debug.Log("READING_ZOOM_VALIDATION_PASS"); else Debug.LogError(message);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
