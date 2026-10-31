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
public static class DeliveryValidation
{
    const string Key = "DeliveryValidation.Running";
    static readonly string Output = Path.GetFullPath("../../output/delivery-validation");
    static double started, lastTick;
    static int checks, stage, lastFrame = -1, act = 1, completed, cancelled, rollbackTrial;
    static int[] originals;
    static SealedDeliveryController delivery;
    static SceneInputGate gate;
    static DeskScenePreview view;
    static SceneObjectInteractor interactor;
    static DialogueTestConsole console;
    static DialogueController dialogue;
    static ActArchiveController archive;
    static Camera camera;
    static Vector3 hoverOrigin;
    static float hoverTime;
    static bool capturedFlight;
    static DeliveryValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    static void Invoke(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);

    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            var before = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            string documents = string.Join("|", before.acts.SelectMany(a => a.documents).Select(d => d.name));
            DeskDeliveryBuilder.Install(); DeskDeliveryBuilder.Install();
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            var installed = Camera.main.GetComponent<SealedDeliveryController>();
            Check(installed && installed.transferDrum && installed.packet && installed.deskLanding, "Missing saved references");
            Check(installed.pickupSound && installed.landingSound, "Missing packet sounds");
            Check(installed.archive.requireDelivery && !installed.archive.previewAllActs, "Delivery availability not enabled");
            Check(!installed.packet.gameObject.activeSelf && !installed.transferDrum.Cargo, "Idle packet/cargo state invalid");
            Check(UnityEngine.Object.FindObjectsOfType<SealedPacketAction>(true).Length == 1, "Reinstall duplicated packet");
            Check(documents == string.Join("|", installed.archive.acts.SelectMany(a => a.documents).Select(d => d.name)), "Installer changed documents");
            Check(installed.archive.acts.Sum(a => a.documents.Length) == 38, "Expected 38 original papers");
            SessionState.SetInt(Key + ".Checks", checks);
            SessionState.SetBool(Key, true); Install(); EditorApplication.isPlaying = true;
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Install()
    {
        started = lastTick = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
    }
    static void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Finish(false, message + "\n" + stack);
    }
    static void Next() { stage++; lastFrame = Time.frameCount; }
    static void Pointer(SceneObject item, bool click)
    {
        var box = item.GetComponent<BoxCollider>();
        Vector3 point = camera.WorldToScreenPoint(item.transform.TransformPoint(box.center));
        Check(point.z > 0 && camera.pixelRect.Contains(point), "Packet not visible in doorway viewport: " + point);
        Invoke(interactor, "ProcessInput", (Vector2)point, true, click, false);
    }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup - started > 90) { Finish(false, "Timeout at stage " + stage + " act " + act); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isPaused || Time.frameCount <= lastFrame) return;
        lastFrame = Time.frameCount;
        double now = EditorApplication.timeSinceStartup;
        float dt = Mathf.Min(0.05f, (float)(now - lastTick)); lastTick = now;
        try
        {
            // Exercise the same runtime clock while batch-mode editor focus is absent.
            if (delivery && delivery.IsActive && !gate.ConsoleOpen && !Application.isFocused) Invoke(delivery, "AdvanceTime", dt);
            switch (stage)
            {
                case 0:
                    camera = Camera.main;
                    if (!camera || !camera.GetComponent<SealedDeliveryController>()) return;
                    delivery = camera.GetComponent<SealedDeliveryController>(); gate = camera.GetComponent<SceneInputGate>();
                    view = camera.GetComponent<DeskScenePreview>(); view.followMouse = false;
                    interactor = camera.GetComponent<SceneObjectInteractor>(); console = camera.GetComponent<DialogueTestConsole>();
                    dialogue = camera.GetComponent<DialogueController>(); archive = delivery.archive;
                    delivery.deliveryCompleted.AddListener(_ => completed++);
                    delivery.deliveryCancelled.AddListener(_ => cancelled++);
                    originals = archive.acts.SelectMany(a => a.documents).Select(d => d.GetInstanceID()).ToArray();
                    Check(archive.DeskAct == 0 && Enumerable.Range(1, 5).All(a => !archive.IsAvailable(a)), "Acts unlocked before delivery");
                    Check(archive.acts.All(a => !a.bundle.gameObject.activeSelf && a.documents.All(d => !d.gameObject.activeSelf)), "Locked props visible");
                    Check(!delivery.BeginDelivery(0) && !delivery.BeginDelivery(6), "Invalid act accepted");
                    Check(console.Execute("deliver nope").Contains("用法"), "Invalid console input accepted");
                    Check(dialogue.Play("arrival"), "Dialogue regression");
                    Check(!delivery.BeginDelivery(1), "Delivery interrupted dialogue");
                    dialogue.Stop(); Next(); break;
                case 1:
                    Check(view.SetView(DeskScenePreview.View.MatFocus), "Cannot enter prior view");
                    // Cancel while loading; disabling must perform the same rollback.
                    Check(delivery.BeginDelivery(5), "Could not start cancellation test");
                    delivery.enabled = false;
                    Check(!delivery.IsActive && !gate.DeliveryActive && !view.DeliveryLocked && view.CurrentView == DeskScenePreview.View.MatFocus,
                        "Disable did not restore camera/input");
                    Check(!archive.IsAvailable(5) && !delivery.packet.gameObject.activeSelf && !delivery.transferDrum.Cargo, "Disable leaked packet/unlock");
                    delivery.enabled = true;
                    delivery.transferDrum.SetPoseImmediately(true);
                    Check(delivery.BeginDelivery(5) && delivery.State == SealedDeliveryController.Phase.TurningOutside, "Inside drum did not reset outside");
                    delivery.Stop();
                    Check(delivery.transferDrum.AtSecondStop, "Cancellation did not restore drum stop");
                    delivery.transferDrum.SetPoseImmediately(false);
                    Next(); break;
                case 2:
                    console.SetOpen(true);
                    Check(console.Execute("deliver " + act).Contains("开始"), "Console did not start delivery " + act);
                    Check(!console.IsOpen && view.DeliveryLocked && view.CurrentView == DeskScenePreview.View.Doorway && gate.DeliveryActive, "Modal entry failed");
                    Check(!delivery.BeginDelivery(act) && !dialogue.Play("arrival"), "Concurrent event accepted");
                    Check(!view.SetView(DeskScenePreview.View.Desktop) && !view.SetView(DeskScenePreview.View.Bookshelf)
                        && !view.SetView(DeskScenePreview.View.MatFocus) && !view.SetView(DeskScenePreview.View.DecodeFocus), "View switch escaped lock");
                    Next(); break;
                case 3:
                    Check(!archive.IsAvailable(act), "Unlocked during drum turn");
                    if (delivery.State != SealedDeliveryController.Phase.AwaitingPickup) break;
                    hoverOrigin = delivery.packet.transform.position; hoverTime = 0;
                    Next(); break;
                case 4:
                    Check(gate.CanInteract(delivery.packet) && !gate.CanInteract(archive.sharedDocuments[0]), "Exclusive target gate failed");
                    interactor.Inspect(archive.sharedDocuments[0]);
                    interactor.Inspect(delivery.transferDrum.GetComponent<SceneObject>());
                    Check(!interactor.InspectedObject && !delivery.transferDrum.IsMoving, "Other object interacted during delivery");
                    Pointer(delivery.packet, false);
                    Check(delivery.packet.State == SceneObject.ObjectState.Hovered, "Real ray cannot hover packet");
                    // The hidden batch editor has no OS pointer focus; drive the ordinary prop animation explicitly.
                    Invoke(delivery.packet, "Animate", dt);
                    hoverTime += dt;
                    if (hoverTime < 0.4f) break;
                    if (act == 1) Capture("01-packet-hover");
                    Check(Vector3.Distance(hoverOrigin, delivery.packet.transform.position) > 0.025f, "Packet hover did not move");
                    if (act == 1 && rollbackTrial == 0)
                    {
                        Check(console.Execute("stop").Contains("收件已中止"), "Console stop did not cancel waiting packet");
                        Check(!archive.IsAvailable(1) && !gate.DeliveryActive && !delivery.packet.gameObject.activeSelf
                            && view.CurrentView == DeskScenePreview.View.MatFocus, "Waiting cancellation did not roll back");
                        rollbackTrial++; stage = 2; lastFrame = Time.frameCount; break;
                    }
                    console.SetOpen(true);
                    Check(!gate.CanInteract(delivery.packet) && !delivery.CollectPacket(), "Console did not block packet");
                    console.SetOpen(false);
                    Check(!delivery.CollectPacket(), "Closing click leaked to packet");
                    Next(); break;
                case 5:
                    Pointer(delivery.packet, true);
                    Check(delivery.State == SealedDeliveryController.Phase.FlyingToDesk && !interactor.InspectedObject, "Click inspected instead of collecting");
                    Check(view.CurrentView == DeskScenePreview.View.Desktop && view.DeliveryLocked, "Camera not following toward desktop");
                    Check(!delivery.CollectPacket() && !archive.IsAvailable(act), "Duplicate collect/unlock before landing");
                    if (act == 1 && rollbackTrial == 1)
                    {
                        Invoke(delivery, "AdvanceTime", 0.45f);
                        delivery.Stop();
                        Check(!archive.IsAvailable(1) && !gate.DeliveryActive && !delivery.packet.gameObject.activeSelf
                            && view.CurrentView == DeskScenePreview.View.MatFocus, "Mid-flight cancellation did not roll back");
                        rollbackTrial++; stage = 2; lastFrame = Time.frameCount; break;
                    }
                    capturedFlight = false; Next(); break;
                case 6:
                    if (!capturedFlight && delivery.State == SealedDeliveryController.Phase.FlyingToDesk
                        && Vector3.Distance(delivery.packet.transform.position, hoverOrigin) > 0.8f)
                    { if (act == 1) Capture("02-packet-flight"); capturedFlight = true; }
                    if (delivery.IsActive)
                    {
                        Check(!archive.IsAvailable(act), "Unlocked before packet disappearance");
                        if (delivery.State == SealedDeliveryController.Phase.Landing && act == 1) Capture("03-packet-landed");
                        break;
                    }
                    Check(archive.IsAvailable(act) && archive.DeskAct == act, "Received files not on desk");
                    Check(!delivery.packet.gameObject.activeSelf && !gate.DeliveryActive && !view.DeliveryLocked, "Finish leaked packet/lock");
                    Check(view.CurrentView == DeskScenePreview.View.Desktop && completed == act, "Wrong completed view/event count");
                    Check(archive.acts.SelectMany((a, i) => a.documents.Select(d => d.gameObject.activeSelf == (i + 1 == act))).All(v => v), "Wrong act papers visible");
                    Check(originals.SequenceEqual(archive.acts.SelectMany(a => a.documents).Select(d => d.GetInstanceID())), "Delivery replaced original evidence");
                    Check(!delivery.BeginDelivery(act) && console.Execute("deliver " + act).Contains("已解锁"), "Duplicate delivery accepted");
                    if (act == 1) Capture("04-act1-desktop");
                    if (act < 5)
                    {
                        Check(archive.CompleteAct(act) && !archive.IsAvailable(act + 1), "Act completion bypassed delivery lock");
                        act++; stage = 2; lastFrame = Time.frameCount;
                    }
                    else Next();
                    break;
                case 7:
                    Check(archive.ShowActOnDesk(1) && archive.DeskAct == 1, "Received act cannot be revisited");
                    interactor.Inspect(archive.acts[0].documents[0]);
                    Check(interactor.InspectedObject, "Regular document inspection not restored");
                    interactor.CancelInteraction();
                    Check(dialogue.Play("arrival"), "Dialogue cannot play after delivery"); dialogue.Stop();
                    Check(cancelled == 4, "Cancellation count incorrect");
                    Finish(true, "PASS: " + (checks + SessionState.GetInt(Key + ".Checks", 0)) + " assertions, Unity 2022.3.62f2 Play Mode / Direct3D 11.\n"
                        + "Idempotent installer + scene reload; five console deliveries; real packet ray hover/click; exclusive input + camera lock.\n"
                        + "No early unlock; original 38 documents preserved; prior act stored; completion cannot bypass delivery; received act review.\n"
                        + "Invalid/repeated events rejected; dialogue mutual exclusion; console closing click consumed; cancellation during turn, wait and flight.\n");
                    break;
            }
        }
        catch (Exception e) { Finish(false, "Stage " + stage + ", act " + act + "\n" + e); }
    }
    static void Finish(bool success, string message)
    {
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/validation.txt", message);
        if (success) Debug.Log("SEALED_DELIVERY_VALIDATION_PASS"); else Debug.LogError(message);
        EditorApplication.Exit(success ? 0 : 1);
    }
    static void Capture(string name)
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
