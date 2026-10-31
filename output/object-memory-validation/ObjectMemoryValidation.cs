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
public static class ObjectMemoryValidation
{
    const string Key = "ObjectMemoryValidation.Play";
    static readonly string Output = Path.GetFullPath("../../output/object-memory-validation");
    static readonly string[] Names = { "Book_Charcoal", "Book_SevenPetal", "Notebook_Open", "PreservationBox" };
    static int checks, index, stage, finishedFrame;
    static double started;
    static bool finished;
    static Camera camera;
    static DialogueController dialogue;
    static SceneInputGate gate;
    static DeskScenePreview view;
    static SceneObjectInteractor interactor;
    static CipherWorkbench work;
    static FirstInteractionMemory[] memories;
    static Vector3 cameraBefore;
    static Quaternion rotationBefore;
    static ObjectMemoryValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    static void Ungate() => gate.GetType().GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gate, -1);
    static void Desktop()
    {
        work.CloseToolbox(); interactor.CancelInteraction(); Ungate();
        Check(view.SetView(DeskScenePreview.View.Desktop), "Cannot restore desktop");
        Call(view, "AnimateView", 10f, Vector2.zero); Physics.SyncTransforms();
    }
    static void Click(SceneObject item)
    {
        var box = item.GetComponent<BoxCollider>();
        Vector2 point = camera.WorldToScreenPoint(box.transform.TransformPoint(box.center));
        Call(interactor, "ProcessInput", point, true, true, false);
    }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        DeskObjectMemoryBuilder.Install(); DeskObjectMemoryBuilder.Install();
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
        if (finished) return;
        if (EditorApplication.timeSinceStartup - started > 90) { Finish(false, "Timeout stage=" + stage + " object=" + index); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || !Camera.main || Time.frameCount < 12) return;
        try
        {
            if (stage == 0)
            {
                camera = Camera.main; camera.aspect = 16f / 9;
                dialogue = camera.GetComponent<DialogueController>(); gate = camera.GetComponent<SceneInputGate>();
                view = camera.GetComponent<DeskScenePreview>(); view.followMouse = false;
                interactor = camera.GetComponent<SceneObjectInteractor>(); work = camera.GetComponent<CipherWorkbench>();
                memories = Names.Select(n => GameObject.Find(n).GetComponent<FirstInteractionMemory>()).ToArray();
                Check(UnityEngine.Object.FindObjectsOfType<FirstInteractionMemory>().Length == 4, "Installer duplicate/missing memories");
                Check(memories.Select(m => m.memory.id).Distinct().Count() == 4, "Objects share a memory identity");
                foreach (var memory in memories)
                {
                    Check(memory.dialogue == dialogue && !memory.HasRemembered, "Bad controller or prematurely remembered");
                    Check(memory.memory.TryValidate(out _) && memory.memory.lines.Length == 1 && memory.memory.lines[0].speaker.isProtagonist, "Memory must be one Luca line");
                    Check(!memory.memory.arrivalSound && !memory.memory.initialVisitor, "Memory has visitor presentation");
                    foreach (char c in memory.memory.lines[0].text) Check(dialogue.Overlay.font.HasCharacter(c), "Missing memory glyph " + c);
                }
                stage = 1;
            }
            if (stage == 1)
            {
                Desktop();
                var memory = memories[index]; var item = memory.GetComponent<SceneObject>();
                cameraBefore = camera.transform.position; rotationBefore = camera.transform.rotation;
                var box = item.GetComponent<BoxCollider>();
                Vector2 pointer = camera.WorldToScreenPoint(box.transform.TransformPoint(box.center));
                Call(interactor, "ProcessInput", pointer, true, false, false);
                Check(!dialogue.IsActive && !memory.HasRemembered, "Hover consumed first interaction");
                if (index == 3)
                {
                    interactor.CancelInteraction(); Ungate(); view.SetView(DeskScenePreview.View.DecodeFocus);
                    Call(view, "AnimateView", 10f, Vector2.zero);
                    work.OpenToolbox();
                    Check(dialogue.ActiveEvent == memory.memory && !work.ToolboxOpen, "Decode-view toolbox bypasses memory");
                    dialogue.Stop(); Check(!memory.HasRemembered, "Cancelled memory marked complete");
                    Desktop(); cameraBefore = camera.transform.position; rotationBefore = camera.transform.rotation;
                }
                Click(item);
                Check(memory.IsRemembering && dialogue.ActiveEvent == memory.memory, "Click did not start " + Names[index]);
                Check(!interactor.InspectedObject && !work.ToolboxOpen, "Interaction ran before memory");
                Call(dialogue, "AdvanceTime", 1f);
                Check(dialogue.LineIndex == 0 && dialogue.CurrentLine.speaker.isProtagonist, "Not a Luca memory");
                dialogue.Advance();
                Check(dialogue.VisibleText == memory.memory.lines[0].text, "Memory text truncated");
                Call(view, "LateUpdate");
                Check(camera.transform.position == cameraBefore && camera.transform.rotation == rotationBefore && !view.DialogueLocked, "Memory switched camera toward door");
                Check(!dialogue.visitor.portrait.enabled && !dialogue.eyeShutter.AtSecondStop, "Memory opened visitor presentation");
                interactor.Inspect(memories[(index + 1) % 4].GetComponent<SceneObject>());
                Check(dialogue.ActiveEvent == memory.memory && !interactor.InspectedObject, "Overlapping click interrupted memory");
                Capture("memory-" + Names[index]);
                dialogue.Advance(); finishedFrame = Time.frameCount;
                Check(memory.HasRemembered && !dialogue.IsActive, "Completion not remembered");
                Call(memory, "Update");
                Check(!interactor.InspectedObject && !work.ToolboxOpen && gate.FrameConsumed, "Finishing click leaked into prop");
                stage = 2; return;
            }
            if (stage == 2)
            {
                if (Time.frameCount <= finishedFrame) return;
                var memory = memories[index]; var item = memory.GetComponent<SceneObject>();
                Call(memory, "Update");
                Check(index == 3 ? work.ToolboxOpen : interactor.InspectedObject == item, "Original interaction did not resume");
                Check(!dialogue.IsActive && memory.HasRemembered, "Memory repeated on continuation");
                if (index == 2)
                {
                    var notebook = item.GetComponent<NotebookBook>();
                    int before = notebook.CurrentSpread;
                    Check(notebook.TurnPage(1), "Notebook no longer turns pages"); Call(notebook, "AdvanceTurn", 1f);
                    Check(notebook.CurrentSpread == before + 1, "Notebook page not advanced");
                }
                Desktop(); Click(item);
                Check(!dialogue.IsActive && (index == 3 ? work.ToolboxOpen : interactor.InspectedObject == item), "Second click did not directly use prop");
                if (index == 3)
                {
                    Desktop(); view.SetView(DeskScenePreview.View.DecodeFocus); Call(view, "AnimateView", 10f, Vector2.zero);
                    work.OpenToolbox(); Check(work.ToolboxOpen && !dialogue.IsActive, "Repeated decode-view opening replayed memory");
                    var slot = work.toolbox.GetComponentsInChildren<CipherWorkbenchTarget>().First(t => t.kind == CipherWorkbenchTarget.Kind.StoredTool);
                    Call(work, "ActivateTarget", slot, Vector2.zero);
                    Call(work, "AnimateToolTransfer", 1f);
                    Check(work.ActiveTool == slot.tool && !dialogue.IsActive, "Tool selection broken after memory");
                    work.SelectTool(CipherWorkbench.Tool.None); Call(work, "AnimateToolTransfer", 1f);
                }
                Desktop(); index++;
                if (index < 4) { stage = 1; return; }
                stage = 3;
            }
            if (stage == 3)
            {
                var ordinary = GameObject.Find("InkBottle").GetComponent<SceneObject>();
                interactor.Inspect(ordinary); Check(interactor.InspectedObject == ordinary && !dialogue.IsActive, "Unconfigured prop affected");
                interactor.CancelInteraction(); Ungate();
                // A fresh prop lets us verify cancellation/disable without rewriting remembered flags.
                var root = new GameObject("Memory cancellation test", typeof(SceneObject), typeof(FirstInteractionMemory));
                var temp = root.GetComponent<FirstInteractionMemory>(); temp.dialogue = dialogue; temp.memory = memories[0].memory;
                int resumes = 0;
                Check(temp.TryBegin(() => resumes++), "Cannot begin cancellation test");
                dialogue.Stop(); Call(temp, "Update");
                Check(!temp.HasRemembered && !temp.IsRemembering && resumes == 0, "Cancelled line resumed action/consumed memory"); Ungate();
                Check(temp.TryBegin(() => resumes++), "Cancelled memory cannot retry");
                temp.enabled = false;
                Check(!dialogue.IsActive && !gate.DialogueActive && resumes == 0, "Disabled memory left active dialogue/action");
                temp.enabled = true; Ungate();
                Check(temp.TryBegin(() => resumes++), "Memory cannot restart after enabling");
                Call(dialogue, "AdvanceTime", 1f); dialogue.Advance(); dialogue.Advance();
                Check(temp.HasRemembered, "Completed memory not marked"); temp.enabled = false;
                Check(resumes == 0, "Disable did not discard deferred action");
                UnityEngine.Object.DestroyImmediate(root); Ungate();
                Check(dialogue.Play("arrival"), "Visitor dialogue broken after memory"); dialogue.Stop(); Ungate();
                Check(camera.GetComponent<PrologueSequence>().PlayPrologue(), "Prologue broken after memory");
                interactor.Inspect(memories[0].GetComponent<SceneObject>());
                Check(!interactor.InspectedObject && dialogue.ActiveEvent.id == "prologue", "Remembered prop bypassed story lock");
                camera.GetComponent<PrologueSequence>().Stop(); Ungate();
                Check(view.CanNavigate && memories.All(m => m.HasRemembered), "Story reset per-play memory state");
                Finish(true, "PASS: " + checks + " assertions in Unity 2022.3.62f2 Play Mode.\n"
                    + "Four distinct single-line Luca memories; hover ignored; actual collider clicks; no door/camera cut; Chinese glyphs verified.\n"
                    + "Original inspect/open action resumes on a later frame; repeat clicks bypass memory; notebook page turn and tool pickup preserved.\n"
                    + "Desktop and decode-view toolbox entry; cancel retries; disable clears dialogue/deferred action; other props, visitor dialogue and prologue preserved.\n"
                    + "State lasts for the current play session; synthetic pointer/timing replay, not OS input testing.\n");
            }
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Finish(bool success, string message)
    {
        if (finished) return; finished = true;
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Output + "/validation.txt", message);
        if (success) Debug.Log("OBJECT_MEMORY_VALIDATION_PASS"); else Debug.LogError(message);
        EditorApplication.Exit(success ? 0 : 1);
    }
    static void Capture(string name)
    {
        var target = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32);
        var oldTarget = camera.targetTexture; var active = RenderTexture.active;
        var overlay = dialogue.Overlay.OverlayCamera; var oldOverlay = overlay.targetTexture;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = overlay.targetTexture = target;
            Canvas.ForceUpdateCanvases(); camera.Render(); overlay.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget; overlay.targetTexture = oldOverlay; RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
