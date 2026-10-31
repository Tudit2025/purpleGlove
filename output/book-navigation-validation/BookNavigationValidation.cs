using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PurpleGlove;
using PurpleGlove.Editor;

[InitializeOnLoad]
public static class BookNavigationValidation
{
    const string Key = "BookNavigationValidation.Play";
    static readonly string Output = Path.GetFullPath("../../output/book-navigation-validation");
    static int checks, stage;
    static double started, captureStarted;
    static bool done;
    static Camera camera;
    static DeskViewHud hud;
    static DeskScenePreview view;
    static SceneInputGate gate;
    static SceneObjectInteractor interactor;
    static NotebookBook book;
    static BookNavigationValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    static object Call(object target, string name, params object[] args) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    static void Ungate() => typeof(SceneInputGate).GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gate, -1);
    static Vector2 Arrow(DeskScenePreview.Direction direction)
    {
        var rect = (Rect)Call(hud, "ArrowBounds", direction);
        return new Vector2(rect.center.x, Screen.height - rect.center.y);
    }
    static bool KeyPress(KeyCode key) => (bool)Call(book, "HandlePageKey", key);
    static void Settle()
    {
        Call(book, "AdvanceTurn", 1f); Ungate();
        Check(!book.IsTurning && !book.turningLeaf.gameObject.activeSelf, "Page animation did not settle");
        Check(book.leftPage.text == book.spreads[book.CurrentSpread].left && book.rightPage.text == book.spreads[book.CurrentSpread].right, "Wrong page content");
    }
    static void Inspect()
    {
        interactor.CancelInteraction(); Ungate();
        interactor.Inspect(book.GetComponent<SceneObject>()); Call(book.GetComponent<SceneObject>(), "Animate", 2f);
        Check(book.CanTurnPages && !view.CanNavigate, "Book/camera input ownership incorrect");
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
        if (EditorApplication.timeSinceStartup - started > 90) { Finish(false, "Timeout"); return; }
        if (done || !EditorApplication.isPlaying || EditorApplication.isCompiling || Time.frameCount < 12) return;
        try
        {
            if (stage == 0)
            {
                camera = Camera.main; hud = camera.GetComponent<DeskViewHud>(); view = camera.GetComponent<DeskScenePreview>();
                gate = camera.GetComponent<SceneInputGate>(); interactor = camera.GetComponent<SceneObjectInteractor>();
                book = UnityEngine.Object.FindFirstObjectByType<NotebookBook>(); view.followMouse = false;
                foreach (var memory in UnityEngine.Object.FindObjectsOfType<FirstInteractionMemory>(true)) memory.enabled = false;
                Ungate(); Check(!book.CanTurnPages && !KeyPress(KeyCode.D), "Resting book accepts keys");
                Check(hud.arrowOpacity > 0 && hud.arrowOpacity < hud.arrowHoverOpacity, "No hover contrast");
                foreach (char c in "翻页←→Esc放回书本第–/共0123456789中…") Check(hud.labelFont.HasCharacter(c), "Missing hint glyph " + c);
                Inspect();
                foreach (Rect viewport in new[] { new Rect(0, 0, 1, 1), new Rect(0.12f, 0.18f, 0.76f, 0.64f) })
                {
                    camera.rect = viewport;
                    Check(!hud.BlocksPointer(Arrow(DeskScenePreview.Direction.Left)), "First page has a back arrow");
                    var pointer = Arrow(DeskScenePreview.Direction.Right);
                    Check(camera.pixelRect.Contains(pointer) && hud.BlocksPointer(pointer), "Next arrow not inside viewport");
                    Check(!hud.BlocksPointer(Arrow(DeskScenePreview.Direction.Up)) && !hud.BlocksPointer(Arrow(DeskScenePreview.Direction.Down)), "Book exposed camera arrows");
                    var originalView = view.CurrentView;
                    Check((bool)Call(hud, "ProcessPointer", pointer, true), "Edge click missed book");
                    Check(book.IsTurning && book.pageAudio.isPlaying && gate.FrameConsumed, "Edge click did not animate/play sound/consume frame");
                    Call(interactor, "ProcessInput", pointer, true, true, false);
                    Check(!KeyPress(KeyCode.D), "Consumed click also accepted a key");
                    Ungate(); Check(!KeyPress(KeyCode.D) && !KeyPress(KeyCode.A), "Repeated input interrupted turn");
                    Call(book, "AdvanceTurn", book.turnSeconds * 0.5f);
                    Check(Quaternion.Angle(book.turningLeaf.localRotation, Quaternion.Euler(0, 0, 90)) < 0.01f, "No physical leaf turn");
                    Settle();
                    Check(book.CurrentSpread == 1 && view.CurrentView == originalView, "Arrow skipped pages or switched camera");
                    Check(hud.BlocksPointer(Arrow(DeskScenePreview.Direction.Left)), "Middle page missing back arrow");
                    Check(!(bool)Call(view, "HandleViewKey", KeyCode.D), "D moved camera while reading");
                    Check((bool)Call(hud, "ProcessPointer", Arrow(DeskScenePreview.Direction.Left), true), "Back arrow missed");
                    Settle(); Check(book.CurrentSpread == 0, "Back arrow failed");
                }
                camera.rect = new Rect(0, 0, 1, 1);
                foreach (KeyCode key in new[] { KeyCode.D, KeyCode.A, KeyCode.RightArrow, KeyCode.LeftArrow })
                { Check(KeyPress(key), "Page shortcut failed: " + key); Settle(); }
                Check(!KeyPress(KeyCode.W) && !KeyPress(KeyCode.S) && !KeyPress(KeyCode.LeftArrow), "Unexpected shortcut or first-page wrap");
                // Existing page surface clicks still work, independently of the new edge UI.
                var pagePointer = (Vector2)camera.WorldToScreenPoint(book.transform.TransformPoint(new Vector3(0.19f, 0.075f, 0)));
                Call(interactor, "ProcessInput", pagePointer, true, true, false); Check(book.IsTurning, "Physical page click stopped working"); Settle();
                foreach (string modal in new[] { "SetConsole", "SetDialogue", "SetStory", "SetWorkbenchTyping", "SetWorkbenchModal" })
                {
                    Call(gate, modal, true); Ungate();
                    Check(!book.CanTurnPages && !KeyPress(KeyCode.D) && !KeyPress(KeyCode.LeftArrow), modal + " permits keyboard turning");
                    Check(!(bool)Call(hud, "ProcessPointer", Arrow(DeskScenePreview.Direction.Right), true), modal + " permits arrow click");
                    Check(!book.TryTurnAtPointer(camera, pagePointer), modal + " permits paper click");
                    Call(gate, modal, false);
                    Check(!KeyPress(KeyCode.D), modal + " closing key leaked"); Ungate();
                }
                gate.SetDelivery(true); Ungate(); Check(!book.CanTurnPages && !KeyPress(KeyCode.D), "Delivery permits turning"); gate.SetDelivery(false); Ungate();
                Check(KeyPress(KeyCode.D), "Turning did not recover after modal"); Settle();
                while (book.CurrentSpread + 1 < book.SpreadCount) { Check(KeyPress(KeyCode.D), "Cannot reach last page"); Settle(); }
                Check(!hud.BlocksPointer(Arrow(DeskScenePreview.Direction.Right)) && !KeyPress(KeyCode.D), "Last page wraps or shows next arrow");
                Check(hud.BlocksPointer(Arrow(DeskScenePreview.Direction.Left)), "Last page missing back arrow");
                Check(KeyPress(KeyCode.A), "Cannot turn back before closing");
                Call(interactor, "ProcessInput", Vector2.zero, false, false, true); Call(book, "Update");
                Check(!book.CanTurnPages && !book.IsTurning, "Esc did not close/finish moving sheet");
                int savedPage = book.CurrentSpread; interactor.CancelInteraction(); Inspect();
                Check(book.CurrentSpread == savedPage, "Reopening reset current page");
                // Camera arrows keep all original routes after returning the book.
                interactor.CancelInteraction();
                var sources = new[] { DeskScenePreview.View.Desktop, DeskScenePreview.View.Bookshelf, DeskScenePreview.View.DecodeFocus, DeskScenePreview.View.MatFocus };
                int[][] targets = { new[] { 3, 2, 4, -1 }, new[] { -1, -1, 0, -1 }, new[] { -1, 3, -1, -1 }, new[] { -1, -1, 4, 0 } };
                for (int s = 0; s < sources.Length; s++)
                for (int d = 0; d < 4; d++)
                {
                    Ungate(); Check(view.SetView(sources[s]), "Cannot enter camera source");
                    var pointer = Arrow((DeskScenePreview.Direction)d); bool available = targets[s][d] >= 0;
                    Check(hud.BlocksPointer(pointer) == available, "Camera arrow visibility changed");
                    Call(hud, "ProcessPointer", pointer, true);
                    Check(view.CurrentView == (available ? (DeskScenePreview.View)targets[s][d] : sources[s]), "Camera route changed");
                }
                Ungate(); view.SetView(DeskScenePreview.View.Desktop); Call(view, "AnimateView", 10f, Vector2.zero); Inspect();
                captureStarted = EditorApplication.timeSinceStartup;
                ScreenCapture.CaptureScreenshot(Output + "/book-navigation.png");
                stage = 1;
            }
            else if (File.Exists(Output + "/book-navigation.png") || EditorApplication.timeSinceStartup - captureStarted > 5)
                Finish(true, "PASS: " + checks + " assertions, Unity Play Mode.\n"
                    + "Book edge hit areas at full/offset viewports; first/last boundaries; A/D and arrow keys; original physical-page clicks.\n"
                    + "Animation/audio, duplicate input, consumed clicks; dialogue/console/story/delivery/typing gates; Esc and retained page.\n"
                    + "All camera arrow routes recover after putting book away. Synthetic pointer/key replay, not OS input testing.\n"
                    + "Game-view screenshot: " + File.Exists(Output + "/book-navigation.png") + "\n");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Finish(bool success, string message)
    {
        if (done) return; done = true; SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Output + "/validation.txt", message);
        if (success) Debug.Log("BOOK_NAVIGATION_VALIDATION_PASS"); else Debug.LogError(message);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
