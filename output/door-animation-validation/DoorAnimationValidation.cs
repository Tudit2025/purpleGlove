using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PurpleGlove;
using PurpleGlove.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run in an isolated project with -executeMethod DoorAnimationValidation.Run.
[InitializeOnLoad]
public static class DoorAnimationValidation
{
    const string Key = "DoorAnimationValidation.Play";
    static readonly string Output = Path.GetFullPath("../../output/door-animation-validation");
    static double started;
    static bool done;
    static int checks, completed, cancelled;
    static Camera camera;
    static DeskScenePreview view;
    static DialogueController dialogue;
    static DoorMechanism shutter;
    static SceneInputGate gate;
    static DialogueEvent visit;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static object Call(object target, string name, params object[] args) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    static void Ungate() => typeof(SceneInputGate).GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gate, -1);
    static void CameraStep(float seconds) => Call(view, "AnimateView", seconds, Vector2.zero);
    static void DoorStep(float seconds) => Call(shutter, "Advance", seconds);
    static void DialogueStep(float seconds) => Call(dialogue, "AdvanceTime", seconds);
    static float DoorAngle => Quaternion.Angle(Quaternion.identity, shutter.pivot.localRotation);
    static void SamePose(Vector3 position, Quaternion rotation, float fov, string message)
    {
        Check(Vector3.Distance(camera.transform.position, position) < 0.00001f
            && Quaternion.Angle(camera.transform.rotation, rotation) < 0.05f
            && Mathf.Abs(camera.fieldOfView - fov) < 0.0001f, message);
    }
    static void Select(DeskScenePreview.View target)
    {
        Ungate(); Check(view.SetView(target), "Cannot select " + target); CameraStep(100);
    }
    static void BeginSpeech()
    {
        CameraStep(10); DialogueStep(10); DoorStep(10); DialogueStep(10);
        Check(dialogue.LineIndex == 0, "First line did not wait for completed entry/opening");
    }
    static void ReadToEnd()
    {
        for (int n = 0; dialogue.IsActive && !dialogue.IsClosing && n < 100; n++) dialogue.Advance();
    }
    static DoorAnimationValidation() { if (SessionState.GetBool(Key, false)) Install(); }
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
        if (done) return;
        if (EditorApplication.timeSinceStartup - started > 120) { Finish(false, "Timeout"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.frameCount < 5) return;
        try
        {
            camera = Camera.main; view = camera.GetComponent<DeskScenePreview>();
            dialogue = camera.GetComponent<DialogueController>(); shutter = dialogue.eyeShutter;
            gate = camera.GetComponent<SceneInputGate>();
            camera.GetComponent<DialogueTestConsole>().SetOpen(false);
            view.followMouse = false; camera.aspect = 16f / 9;
            visit = dialogue.events.First(e => e.id == "arrival");
            dialogue.eventCompleted.AddListener(_ => completed++);
            dialogue.eventCancelled.AddListener(_ => cancelled++);
            var sources = new[] { DeskScenePreview.View.Desktop, DeskScenePreview.View.Bookshelf,
                DeskScenePreview.View.MatFocus, DeskScenePreview.View.DecodeFocus };

            foreach (var source in sources)
            {
                Select(source); shutter.SetPoseImmediately(false);
                Vector3 startPosition = camera.transform.position;
                Quaternion startRotation = camera.transform.rotation;
                float startFov = camera.fieldOfView;
                int eventCount = completed;
                if (source == DeskScenePreview.View.Desktop) Capture("01-desktop");
                Check(dialogue.Play(visit), "Cannot start visit from " + source);
                SamePose(startPosition, startRotation, startFov, "Entry snapped from " + source);
                Check(view.IsEnteringDoorway && !view.CanNavigate && gate.DialogueActive, "Entry not locked");
                Check(!shutter.IsMoving && !shutter.AtSecondStop, "Shutter opened before camera arrival");
                DialogueStep(10); dialogue.Advance();
                Check(dialogue.LineIndex == -1 && !shutter.IsMoving, "Arrival input skipped entry");
                CameraStep(0);
                SamePose(startPosition, startRotation, startFov, "Zero-time entry moved camera");
                CameraStep(view.doorwayTransitionSeconds * 0.5f);
                float initialAngle = Quaternion.Angle(startRotation, Quaternion.Euler(view.doorwayEuler));
                float remainingAngle = Quaternion.Angle(camera.transform.rotation, Quaternion.Euler(view.doorwayEuler));
                Check(remainingAngle > 0.1f && remainingAngle < initialAngle - 0.1f, "No intermediate camera rotation");
                Check(camera.fieldOfView < startFov && camera.fieldOfView > DeskScenePreview.DoorwayFieldOfView,
                    "FOV did not animate with rotation");
                if (source == DeskScenePreview.View.Desktop) Capture("02-camera-halfway");
                DialogueStep(10); Check(!shutter.IsMoving && dialogue.LineIndex == -1, "Early opening mid-camera");
                CameraStep(view.doorwayTransitionSeconds * 0.5f + 0.0001f);
                Check(!view.IsEnteringDoorway && Quaternion.Angle(camera.transform.rotation, Quaternion.Euler(view.doorwayEuler)) < 0.05f,
                    "Door camera did not reach authored endpoint");
                Check(Mathf.Abs(camera.fieldOfView - DeskScenePreview.DoorwayFieldOfView) < 0.0001f, "Wrong endpoint FOV");
                if (source == DeskScenePreview.View.Desktop) Capture("03-door-closed");
                DialogueStep(0);
                Check(shutter.IsMoving && DoorAngle < 0.05f && dialogue.LineIndex == -1, "Opening skipped initial pose");
                DoorStep(shutter.travelSeconds * 0.5f);
                Check(DoorAngle > 1 && DoorAngle < shutter.travelAngle - 1, "No intermediate opening pose");
                shutter.Activate(); Check(shutter.IsMoving, "Duplicate click interrupted opening");
                DialogueStep(10); Check(dialogue.LineIndex == -1, "Dialogue shown before shutter finished opening");
                if (source == DeskScenePreview.View.Desktop) Capture("04-shutter-half-open");
                DoorStep(shutter.travelSeconds); DialogueStep(0);
                Check(shutter.AtSecondStop && dialogue.LineIndex == 0 && dialogue.visitor.CurrentVisitor, "Opening never reached dialogue");
                if (source == DeskScenePreview.View.Desktop) Capture("05-door-open");
                ReadToEnd();
                Check(dialogue.IsClosing && dialogue.IsActive && gate.DialogueActive && view.DialogueLocked,
                    "Closing released input/camera early");
                Check(completed == eventCount && shutter.IsMoving && Mathf.Abs(DoorAngle - shutter.travelAngle) < 0.05f,
                    "Closing snapped or completion fired early");
                dialogue.Advance(); dialogue.Advance(); DialogueStep(20);
                Check(dialogue.IsClosing && completed == eventCount, "Repeated advance bypassed closing");
                DoorStep(shutter.travelSeconds * 0.5f);
                Check(DoorAngle > 1 && DoorAngle < shutter.travelAngle - 1 && dialogue.visitor.CurrentVisitor,
                    "Closing pose/visitor did not persist");
                if (source == DeskScenePreview.View.Desktop) Capture("06-shutter-half-closed");
                DoorStep(shutter.travelSeconds); DialogueStep(0); DialogueStep(10); dialogue.Advance();
                Check(!dialogue.IsActive && !dialogue.IsClosing && !view.DialogueLocked && !gate.DialogueActive,
                    "Finished closing leaked locks");
                Check(!shutter.AtSecondStop && DoorAngle < 0.05f && !dialogue.visitor.CurrentVisitor, "Wrong restored shutter/visitor");
                Check(completed == eventCount + 1 && view.CurrentView == source, "Completion or source view restoration incorrect");
                Quaternion doorRotation = camera.transform.rotation;
                CameraStep(0.1f);
                Check(Quaternion.Angle(camera.transform.rotation, doorRotation) > 0.1f
                    && Quaternion.Angle(camera.transform.rotation, startRotation) > 0.1f, "Return camera snapped");
                CameraStep(100); SamePose(startPosition, startRotation, startFov, "Return did not restore source view");
            }

            // Camera movement remains smooth at different frame rates, even with mouse smoothing disabled.
            float oldFollow = view.cameraFollowSpeed; view.cameraFollowSpeed = 0;
            foreach (int fps in new[] { 30, 60, 144 })
            {
                Select(DeskScenePreview.View.Bookshelf);
                view.SetDialogueLock(true);
                float previous = Quaternion.Angle(camera.transform.rotation, Quaternion.Euler(view.doorwayEuler));
                int frames = 0;
                while (view.IsEnteringDoorway && frames++ < fps * 2)
                {
                    CameraStep(1f / fps);
                    float angle = Quaternion.Angle(camera.transform.rotation, Quaternion.Euler(view.doorwayEuler));
                    Check(angle <= previous + 0.05f && previous - angle < 8, "Discontinuous or reversed camera step at " + fps);
                    previous = angle;
                }
                Check(!view.IsEnteringDoorway && frames >= fps * view.doorwayTransitionSeconds - 1,
                    "Frame-rate-dependent transition duration");
                view.SetDialogueLock(false);
            }
            view.cameraFollowSpeed = oldFollow;

            // Cancel before arrival, during opening, and during closing; no teleport or stale completion.
            for (int phase = 0; phase < 3; phase++)
            {
                Select(DeskScenePreview.View.Desktop); shutter.SetPoseImmediately(false);
                Check(dialogue.Play(visit), "Cancellation setup failed");
                if (phase == 0) CameraStep(view.doorwayTransitionSeconds * 0.4f);
                else if (phase == 1)
                { CameraStep(10); DialogueStep(10); DoorStep(shutter.travelSeconds * 0.4f); }
                else { BeginSpeech(); ReadToEnd(); DoorStep(shutter.travelSeconds * 0.4f); }
                var position = camera.transform.position; var rotation = camera.transform.rotation; float fov = camera.fieldOfView;
                float angle = DoorAngle; int beforeCancel = cancelled, beforeComplete = completed;
                dialogue.Stop(); dialogue.Stop();
                SamePose(position, rotation, fov, "Cancellation snapped camera");
                Check(Mathf.Abs(DoorAngle - angle) < 0.05f, "Cancellation snapped shutter");
                Check(!dialogue.IsActive && !gate.DialogueActive && !view.DialogueLocked && !view.IsEnteringDoorway,
                    "Cancellation leaked modal state");
                Check(cancelled == beforeCancel + 1 && completed == beforeComplete, "Cancellation fired duplicate/wrong event");
                if (phase > 0)
                {
                    Check(shutter.IsMoving && !dialogue.Play(visit), "New dialogue interrupted shutter restore");
                    DoorStep(0.05f); Check(DoorAngle < angle && DoorAngle > 0, "Cancel did not animate back from partial pose");
                }
                DoorStep(10); DialogueStep(10);
                Check(!shutter.IsMoving && !shutter.AtSecondStop && completed == beforeComplete, "Cancel left stale completion/door motion");
            }

            Select(DeskScenePreview.View.MatFocus); shutter.SetPoseImmediately(true);
            Check(dialogue.Play(visit), "Cannot enter with shutter already open"); BeginSpeech(); ReadToEnd();
            Check(!dialogue.IsActive && shutter.AtSecondStop && !shutter.IsMoving, "Pre-opened shutter was incorrectly closed");
            shutter.SetPoseImmediately(false);
            Select(DeskScenePreview.View.Desktop);
            Check(dialogue.Play(visit), "Disable setup failed"); BeginSpeech(); ReadToEnd();
            shutter.enabled = false; DialogueStep(0);
            Check(!dialogue.IsActive && !gate.DialogueActive, "Disabled shutter left closing stuck"); shutter.enabled = true;
            Select(DeskScenePreview.View.Desktop);
            Check(dialogue.Play(visit), "Controller disable setup failed"); BeginSpeech();
            dialogue.enabled = false;
            Check(!dialogue.IsActive && !gate.DialogueActive && !view.DialogueLocked, "Disabled controller leaked locks");
            DoorStep(10); dialogue.enabled = true;

            // In-place monologues must keep owning their original camera/door behavior.
            Select(DeskScenePreview.View.DecodeFocus);
            var inPlacePosition = camera.transform.position; var inPlaceRotation = camera.transform.rotation; float inPlaceFov = camera.fieldOfView;
            var monologue = AssetDatabase.LoadAssetAtPath<DialogueEvent>("Assets/Data/Dialogue/Events/memory_toolbox.asset");
            Check(dialogue.PlayInPlace(monologue), "Cannot start in-place monologue"); DialogueStep(10); ReadToEnd();
            Check(!dialogue.IsActive && !shutter.IsMoving && !shutter.AtSecondStop, "In-place monologue changed door lifecycle");
            SamePose(inPlacePosition, inPlaceRotation, inPlaceFov, "In-place monologue moved camera");

            var delivery = camera.GetComponent<SealedDeliveryController>();
            foreach (var source in sources)
            {
                Select(source);
                var p = camera.transform.position; var r = camera.transform.rotation; float f = camera.fieldOfView;
                Check(delivery.BeginDelivery(1), "Delivery entry rejected");
                SamePose(p, r, f, "Delivery snapped camera");
                Check(view.IsEnteringDoorway && !view.CanNavigate, "Delivery entry not animated/locked");
                CameraStep(view.doorwayTransitionSeconds * 0.5f);
                Check(view.IsEnteringDoorway && camera.fieldOfView < f, "Delivery has no intermediate frame");
                delivery.Stop(); CameraStep(100);
                Check(!view.IsEnteringDoorway && view.CurrentView == source && !gate.DeliveryActive, "Cancelled delivery lost source view");
                SamePose(p, r, f, "Delivery return failed");
            }
            Finish(true, "PASS: " + checks + " assertions. Unity " + Application.unityVersion + " Play Mode.\n"
                + "Real saved scene: animated camera position/rotation/FOV from desktop, bookshelf and both top-down views; 30/60/144 fps.\n"
                + "Camera arrival -> complete shutter opening -> dialogue -> complete closing -> exactly-once completion and smooth return.\n"
                + "Duplicate advance/click guards, cancellation in all animation phases, pre-opened window, disable cleanup, in-place monologues and delivery entry.\n"
                + "Deterministic clock replay and camera renders; no manual OS input.\n");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Capture(string name)
    {
        var target = RenderTexture.GetTemporary(1280, 720, 24); var active = RenderTexture.active; var previous = camera.targetTexture;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally { camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(image); }
    }
    static void Finish(bool success, string message)
    {
        if (done) return; done = true; SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/validation.txt", message);
        if (success) Debug.Log(message); else Debug.LogError(message);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
