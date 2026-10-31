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
public static class VisitationDoorPlaySmoke
{
    const string Key = "VisitationDoorPlaySmoke.Running";
    static double started, stageStarted;
    static int stage, openEvents, deliveryEvents;
    static DoorMechanism eye, drum;
    static SceneObjectInteractor interactor;
    static DeskScenePreview view;
    static Vector3 cargoStart;
    static int cargoId;
    static MethodInfo pointer = typeof(SceneObjectInteractor).GetMethod("ProcessPointer", BindingFlags.Instance | BindingFlags.NonPublic);
    static VisitationDoorPlaySmoke() { if (SessionState.GetBool(Key, false)) Install(); }
    public static void Run()
    {
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
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Finish(false, message + "\n" + stack);
    }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Next() { stage++; stageStarted = EditorApplication.timeSinceStartup; }
    static void Click(DoorMechanism action, Vector3 localPoint)
    {
        Vector3 point = Camera.main.WorldToScreenPoint(action.transform.TransformPoint(localPoint));
        pointer.Invoke(interactor, new object[] { (Vector2)point, true, true });
    }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup - started > 55) { Finish(false, "Timeout stage " + stage); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isPaused) return;
        try
        {
            if (stage == 0)
            {
                var actions = UnityEngine.Object.FindObjectsOfType<DoorMechanism>();
                if (actions.Length != 2) return;
                eye = actions.Single(a => a.mechanism == DoorMechanism.Mechanism.EyeShutter);
                drum = actions.Single(a => a.mechanism == DoorMechanism.Mechanism.TransferDrum);
                view = Camera.main.GetComponent<DeskScenePreview>(); view.followMouse = false;
                interactor = Camera.main.GetComponent<SceneObjectInteractor>();
                cargoStart = drum.Cargo.position; cargoId = drum.Cargo.GetInstanceID();
                eye.reachedSecondStop.AddListener(() => openEvents++);
                drum.reachedSecondStop.AddListener(() => deliveryEvents++);
                Check(!eye.AtSecondStop && !drum.CargoIsInside, "Wrong runtime initial state");
                view.SetView(DeskScenePreview.View.Doorway); Next();
            }
            else if (EditorApplication.timeSinceStartup - stageStarted > 1.7)
            {
                if (stage == 1)
                {
                    Click(eye, new Vector3(0, 0, -0.10f));
                    Check(eye.IsMoving && !interactor.BlocksSceneInput, "Eye click became inspection instead of action");
                    Check(eye.GetComponent<AudioSource>().isPlaying, "Eye opening audio did not play");
                    eye.Activate(); // Duplicate click must not reverse an in-flight action.
                }
                else if (stage == 2)
                {
                    Check(eye.AtSecondStop && openEvents == 1, "Eye animation/event failed in Play Mode");
                    Click(eye, new Vector3(0, -0.111f, -0.11f));
                    Check(eye.IsMoving, "Open eye window cannot be closed by pointer");
                    Click(drum, new Vector3(0, -0.23f, -0.31f));
                    Check(drum.IsMoving && !interactor.BlocksSceneInput, "Transfer click failed");
                    drum.Activate();
                    Check(!drum.ReleaseCargo(), "Released cargo mid-turn");
                    view.SetView(DeskScenePreview.View.Desktop);
                }
                else if (stage == 3)
                {
                    Check(!eye.AtSecondStop && !eye.IsMoving, "Eye did not close");
                    Check(drum.CargoIsInside && deliveryEvents == 1 && drum.Cargo.position.z < 3.2f, "Delivery did not finish across camera switch");
                    Check(drum.Cargo.GetInstanceID() == cargoId, "Cargo was cloned/replaced");
                    var delivered = drum.ReleaseCargo();
                    Check(delivered && !drum.Cargo && delivered.parent == null && delivered.GetInstanceID() == cargoId, "Runtime unload failed");
                    Check(drum.TryLoadCargo(delivered), "Runtime reload failed");
                    drum.Activate();
                    view.SetView(DeskScenePreview.View.Doorway);
                }
                else if (stage == 4)
                {
                    Check(!drum.IsMoving && !drum.AtSecondStop && (drum.Cargo.position - cargoStart).sqrMagnitude < 0.0000001f, "Return trip or pivot drift");
                    for (int i = 0; i < 2; i++)
                    {
                        eye.Activate();
                        Check(eye.IsMoving, "Repeat cycle failed");
                        eye.SetPoseImmediately(false);
                    }
                    var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
                    Check(archive.CompleteAct(1), "Archive regression");
                    var book = GameObject.Find("Book_SevenPetal").GetComponent<SceneObject>();
                    interactor.Inspect(book);
                    Check(interactor.BlocksSceneInput, "Book inspection regression");
                    Finish(true, "PASS: Unity Play Mode; pointer ray opens and closes shutter; pointer rotates cargo; animation and positional audio; duplicate-click guard; completion events once; camera switch during delivery; cargo identity and return pose; no inspection lock for door actions; existing archive and book inspection; no runtime errors.");
                    return;
                }
                Next();
            }
            EditorApplication.QueuePlayerLoopUpdate();
        }
        catch (Exception ex) { Finish(false, ex.ToString()); }
    }
    static void Finish(bool success, string message)
    {
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Path.GetFullPath("../../output/door-mechanism-validation/play-mode.txt"), message);
        if (success) Debug.Log("VISITATION_DOOR_PLAY_MODE_PASS");
        EditorApplication.Exit(success ? 0 : 1);
    }
}
