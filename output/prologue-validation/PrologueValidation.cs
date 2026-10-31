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
public static class PrologueValidation
{
    const string Key = "PrologueValidation.Play";
    static readonly string Output = Path.GetFullPath("../../output/prologue-validation");
    static bool running;
    static double started;
    static int checks;
    static PrologueValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    static object Field(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static void Ungate(SceneInputGate gate) => gate.GetType().GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gate, -1);
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        DeskPrologueBuilder.Install(); DeskPrologueBuilder.Install();
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
    static void ReadAll(DialogueController dialogue)
    {
        Call(dialogue, "AdvanceTime", 10f);
        int safety = 40;
        while (dialogue.IsActive && safety-- > 0) dialogue.Advance();
        Check(!dialogue.IsActive, "Monologue did not end");
    }
    static void Tick()
    {
        if (running) return;
        if (EditorApplication.timeSinceStartup - started > 120) { Finish(false, "Play mode timeout"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || !Camera.main || Time.frameCount < 12) return;
        running = true;
        try
        {
            var camera = Camera.main; camera.aspect = 16f / 9;
            var story = camera.GetComponent<PrologueSequence>();
            var curtain = camera.GetComponent<ActTransitionOverlay>();
            var dialogue = camera.GetComponent<DialogueController>();
            var console = camera.GetComponent<DialogueTestConsole>();
            var view = camera.GetComponent<DeskScenePreview>(); view.followMouse = false;
            var gate = camera.GetComponent<SceneInputGate>();
            var delivery = camera.GetComponent<SealedDeliveryController>();
            var work = camera.GetComponent<CipherWorkbench>();
            Check(camera.GetComponents<PrologueSequence>().Length == 1 && camera.GetComponents<ActTransitionOverlay>().Length == 1, "Duplicate installation");
            Check(story.prologue.TryValidate(out _) && story.prologue.lines.Length == 11, "Missing authored threshold dialogue");
            Check(story.prologue.lines.Count(l => l.speaker.isProtagonist) == 9, "Wrong protagonist lines");
            Check(story.doorOpeningSound && story.doorClosingSound && curtain.font, "Missing presentation resources");
            Quaternion doorRest = story.doorLeaf.localRotation;
            Vector3 initialPosition = camera.transform.position;
            Quaternion initialRotation = camera.transform.rotation;
            float initialFov = camera.fieldOfView;
            int completed = 0, cancelled = 0;
            story.actEntered.AddListener(act => completed++); story.sequenceCancelled.AddListener(() => cancelled++);
            console.SetOpen(true); Check(console.Execute("prologue").StartsWith("开始"), "Console cannot start prologue");
            Check(!console.IsOpen && story.IsActive && dialogue.IsActive && gate.StoryActive && view.StoryLocked, "Story did not take input/camera ownership");
            Check(story.exteriorLight && story.exteriorLight.enabled, "Exterior presentation light missing");
            Vector3 exteriorPosition = camera.transform.position;
            Quaternion exteriorRotation = camera.transform.rotation;
            Check(story.doorLeaf.InverseTransformPoint(exteriorPosition).z > 1, "Camera is not outside the door");
            Check(!view.CanNavigate && !view.SetView(DeskScenePreview.View.Desktop), "Navigation leaked into monologue");
            Check(!delivery.CanDeliver(1) && !dialogue.Play("arrival") && !story.PlayPrologue(), "Concurrent event allowed");
            Check(!work.SelectTool(CipherWorkbench.Tool.Caesar), "Cipher tools work during story");
            Check(!dialogue.eyeShutter.AtSecondStop && !dialogue.visitor.portrait.enabled, "In-place dialogue opened the window/showed a face");
            Call(dialogue, "AdvanceTime", 1f); dialogue.Advance();
            foreach (float aspect in new[] { 4f / 3, 16f / 9, 21f / 9 })
            {
                camera.aspect = aspect;
                foreach (var point in new[] { new Vector3(0.462f, 0.05f, 0), new Vector3(0.462f, 2.05f, 0), new Vector3(0.01f, 1.1f, 0), new Vector3(0.91f, 1.1f, 0) })
                {
                    Vector3 screen = camera.WorldToViewportPoint(story.doorLeaf.TransformPoint(point));
                    Check(screen.z > 0 && screen.x > 0.03f && screen.x < 0.97f && screen.y > 0.03f && screen.y < 0.97f, "Door frame clipped " + aspect);
                }
            }
            camera.aspect = 16f / 9;
            Capture(camera, dialogue, curtain, "01-exterior-monologue");
            Call(view, "LateUpdate");
            Check(camera.transform.position == exteriorPosition && camera.transform.rotation == exteriorRotation, "Normal view overrides exterior camera");
            ReadAll(dialogue); Check(story.State == PrologueSequence.Phase.OpeningDoor && gate.StoryActive, "Dialogue did not open door or released input early");
            Call(story, "AdvanceTime", story.doorOpenSeconds * 0.6f);
            Check(Quaternion.Angle(doorRest, story.doorLeaf.localRotation) > 20, "Door leaf did not rotate about its hinge");
            Capture(camera, dialogue, curtain, "02-opening-door");
            console.SetOpen(true);
            Quaternion pausedDoor = story.doorLeaf.localRotation; Call(story, "Update");
            Check(story.doorLeaf.localRotation == pausedDoor, "Console failed to pause door");
            console.SetOpen(false); Ungate(gate);
            Call(story, "AdvanceTime", 2f);
            Check(curtain.State == ActTransitionOverlay.Phase.FadeOut, "Missing fade out");
            Call(story, "AdvanceTime", curtain.fadeOutSeconds * 0.5f);
            Check(curtain.Opacity > 0 && curtain.Opacity < 1 && camera.transform.position == exteriorPosition, "Camera swapped before black");
            Call(story, "AdvanceTime", curtain.fadeOutSeconds);
            Check(curtain.State == ActTransitionOverlay.Phase.Black && curtain.Opacity == 1 && curtain.TitleOpacity == 0, "Missing clean black beat");
            Check(camera.transform.position != exteriorPosition && view.CurrentView == DeskScenePreview.View.Desktop && story.doorLeaf.localRotation == doorRest, "Scene was not prepared under black");
            Call(story, "AdvanceTime", curtain.blackSeconds);
            Call(story, "AdvanceTime", curtain.titleFadeSeconds);
            Check(curtain.State == ActTransitionOverlay.Phase.TitleHold && curtain.Title == "第一幕" && curtain.TitleOpacity == 1 && curtain.Opacity == 1, "Chapter title not shown on opaque black");
            Capture(camera, dialogue, curtain, "03-first-act-title");
            Check(!story.exteriorLight.enabled, "Exterior light leaked into indoor scene");
            console.SetOpen(true); Call(story, "Update");
            Check(!((Canvas)Field(curtain, "canvas")).enabled && curtain.State == ActTransitionOverlay.Phase.TitleHold, "Black curtain hides console or fails to pause");
            console.SetOpen(false); Call(story, "Update");
            Check(((Canvas)Field(curtain, "canvas")).enabled, "Closing console did not restore curtain");
            Call(story, "AdvanceTime", curtain.titleHoldSeconds);
            Call(story, "AdvanceTime", curtain.titleFadeSeconds);
            Check(curtain.State == ActTransitionOverlay.Phase.FadeIn && curtain.Opacity == 1 && curtain.TitleOpacity == 0, "Title did not clear before reveal");
            Call(story, "AdvanceTime", curtain.fadeInSeconds * 0.5f);
            Check(curtain.Opacity > 0 && curtain.Opacity < 1 && gate.StoryActive, "Missing fade in/input lock");
            Call(story, "AdvanceTime", curtain.fadeInSeconds); Ungate(gate);
            Check(!story.IsActive && !curtain.IsActive && !gate.StoryActive && !view.StoryLocked && view.CanNavigate && completed == 1 && story.PresentedAct == 1, "Story did not cleanly finish at act one");
            Check(!delivery.archive.IsAvailable(1), "Presentation bypassed the registered delivery");
            Capture(camera, dialogue, curtain, "04-first-act-desktop");
            // Cancellation at each phase, replay, title reuse, and disabled-component cleanup.
            for (int phase = 0; phase < 4; phase++)
            {
                Vector3 before = camera.transform.position; Quaternion beforeRotation = camera.transform.rotation;
                Check(story.PlayPrologue(), "Cannot replay prologue");
                if (phase > 0) ReadAll(dialogue);
                if (phase > 1) { Call(story, "AdvanceTime", 2f); Call(story, "AdvanceTime", 2f); }
                if (phase > 2) { Call(story, "AdvanceTime", 2f); Call(story, "AdvanceTime", 2f); }
                console.Execute("stop"); Ungate(gate);
                Check(!story.IsActive && !dialogue.IsActive && !curtain.IsActive && !gate.StoryActive && !gate.DialogueActive && !view.StoryLocked, "Stop left a modal/curtain behind");
                Check(camera.transform.position == before && camera.transform.rotation == beforeRotation && story.doorLeaf.localRotation == doorRest, "Stop did not restore camera/door");
                Check(!story.exteriorLight.enabled, "Cancelled story left light on");
            }
            for (int act = 1; act <= 5; act++)
            {
                Check(story.PlayActTransition(act), "Cannot present chapter " + act);
                Check(curtain.Title == ActTransitionOverlay.ActTitle(act), "Wrong chapter title");
                for (int phase = 0; phase < 6; phase++) Call(story, "AdvanceTime", 10f);
                Check(!story.IsActive && story.PresentedAct == act, "Reusable transition did not finish"); Ungate(gate);
            }
            Check(!story.PlayActTransition(0) && !story.PlayActTransition(6), "Invalid chapter accepted");
            Check(story.PlayPrologue(), "Cannot start disable test"); story.enabled = false;
            Check(!story.IsActive && !gate.StoryActive && !dialogue.IsActive && !curtain.IsActive, "Disable leaked state"); story.enabled = true; Ungate(gate);
            Check(dialogue.Play("arrival"), "Normal visitor dialogue broken after prologue");
            Check(view.DialogueLocked && !view.StoryLocked, "Normal dialogue camera lock broken"); dialogue.Stop(); Ungate(gate);
            Check(delivery.CanDeliver(1) && view.CanNavigate, "Normal input/delivery not restored");
            Check(completed == 6 && cancelled == 5, "Completion/cancel events fired incorrectly");
            Check(console.Execute("help").Contains("prologue") && console.Execute("list").Contains("transition"), "Missing console discovery");
            Finish(true, "PASS: " + checks + " assertions in Unity 2022.3.62f2 Play Mode.\n"
                + "11 authored lines; exterior door framing at 4:3, 16:9, 21:9; in-place dialogue/voice; hinge animation; console pause.\n"
                + "Fade out / opaque black scene swap / chapter title / fade in; chapters 1-5; all phase cancellation, replay, disable cleanup.\n"
                + "Story input exclusivity, normal visitor/delivery recovery, preserved archive availability, console commands, idempotent saved-scene installer.\n"
                + "Synthetic input/timing and rendered frames; audio output and OS keyboard not manually tested.\n");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Finish(bool success, string message)
    {
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Output + "/validation.txt", message);
        if (success) Debug.Log("PROLOGUE_VALIDATION_PASS"); else Debug.LogError(message);
        EditorApplication.Exit(success ? 0 : 1);
    }
    static void Capture(Camera camera, DialogueController dialogue, ActTransitionOverlay transition, string name)
    {
        var target = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32);
        var previous = camera.targetTexture; var active = RenderTexture.active;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        var overlay = dialogue.Overlay.OverlayCamera; var previousOverlay = overlay.targetTexture;
        var canvas = (Canvas)Field(transition, "canvas");
        Camera titleCamera = null;
        try
        {
            camera.targetTexture = target; camera.Render();
            if (overlay.enabled) { overlay.targetTexture = target; Canvas.ForceUpdateCanvases(); overlay.Render(); }
            if (canvas.enabled)
            {
                titleCamera = new GameObject("Validation title camera").AddComponent<Camera>();
                titleCamera.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
                titleCamera.cullingMask = 1 << 5; titleCamera.clearFlags = CameraClearFlags.Depth;
                titleCamera.nearClipPlane = 0.01f; titleCamera.farClipPlane = 1; titleCamera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = titleCamera; canvas.planeDistance = 0.1f;
                Canvas.ForceUpdateCanvases(); titleCamera.Render();
            }
            RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            if (titleCamera) UnityEngine.Object.DestroyImmediate(titleCamera.gameObject);
            camera.targetTexture = previous; overlay.targetTexture = previousOverlay; RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
