using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using PurpleGlove;
using PurpleGlove.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class HandbellValidation
{
    const string Key = "HandbellValidation.Running";
    static readonly string Output = Path.GetFullPath("../../output/handbell-validation");
    static bool finished;
    static double started;
    static int checks, calls, returns;
    static Camera camera;
    static DeskHandbell bell;
    static SceneInputGate gate;
    static SceneObjectInteractor interactor;
    static DialogueController dialogue;
    static DeskScenePreview view;
    static Vector3 rest;
    static Quaternion restRotation;
    static HandbellValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(obj, args);
    static void ReleaseGate() => typeof(SceneInputGate).GetField("ignoreThroughFrame", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(gate, -1);
    static void Step(float delta) => Call(bell, "AdvanceTime", delta);
    static void Restored(string label)
    {
        Check(!bell.IsBusy && !gate.PropActionActive && !dialogue.IsActive, label + ": modal stuck");
        Check(bell.transform.localPosition == rest && bell.transform.localRotation == restRotation
            && bell.transform.localScale == Vector3.one, label + ": home pose drift");
        Check(bell.GetComponent<SceneObject>().enabled && bell.gripPivot.localRotation == Quaternion.identity
            && bell.clapperPivot.localRotation == Quaternion.identity, label + ": rig not restored");
        ReleaseGate();
    }
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            int lights = UnityEngine.Object.FindObjectsOfType<Light>(true).Length;
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            string papers = string.Join("|", archive.acts.SelectMany(a => a.documents).Select(d => d.name));
            DeskSceneBuilder.InstallDeskHandbell();
            var original = UnityEngine.Object.FindFirstObjectByType<DeskHandbell>();
            var pose = original.transform.position;
            original.SetRingAllowed(true); original.transform.position += Vector3.right * 0.01f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(original);
            PrefabUtility.RecordPrefabInstancePropertyModifications(original.transform);
            EditorSceneManager.SaveScene(original.gameObject.scene);
            DeskSceneBuilder.InstallDeskHandbell();
            var all = UnityEngine.Object.FindObjectsOfType<DeskHandbell>();
            Check(all.Length == 1, "Duplicate handbells"); bell = all[0];
            Check(bell.ringAllowed && Vector3.Distance(bell.transform.position, pose + Vector3.right * 0.01f) < 0.00001f, "Installer overwrites authored permission/placement");
            bell.SetRingAllowed(false); bell.transform.position = pose;
            PrefabUtility.RecordPrefabInstancePropertyModifications(bell);
            PrefabUtility.RecordPrefabInstancePropertyModifications(bell.transform);
            EditorSceneManager.SaveScene(bell.gameObject.scene);
            Check(bell.dialogue && bell.notNeededLine && bell.pickupSound && bell.putdownSound && bell.strikes.All(s => s), "Unwired saved references");
            Check(UnityEngine.Object.FindObjectsOfType<Light>(true).Length == lights, "Bell added dedicated light");
            archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            Check(papers == string.Join("|", archive.acts.SelectMany(a => a.documents).Select(d => d.name)), "Installer changed documents");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeskSceneBuilder.Root + "/Prefabs/PF_Handbell.prefab");
            Check(prefab && !prefab.GetComponent<DeskHandbell>().dialogue && !prefab.GetComponent<DeskHandbell>().ringAllowed, "Prefab carries scene references or rings by default");
            Check(bell.GetComponentsInChildren<Collider>(true).Length == 1, "Expected single picking box");
            foreach (var mesh in bell.GetComponentsInChildren<MeshFilter>())
            {
                var normals = new List<Vector3>(); mesh.sharedMesh.GetUVs(2, normals);
                Check(normals.Count == mesh.sharedMesh.vertexCount && mesh.sharedMesh.uv.Length == normals.Count, "Missing metric UV/outline normals");
                Check(mesh.transform.localScale == Vector3.one && mesh.GetComponent<Renderer>().sharedMaterials.All(m =>
                    m && m.shader.name == "PurpleGlove/Pixel Outlined" && m.GetFloat("_PixelSize") == 4), "Inconsistent prop materials");
            }
            Check(bell.GetComponentsInChildren<Renderer>().Min(r => r.bounds.min.y) >= 0.8009f, "Bell penetrates tabletop");
            var bellBounds = bell.GetComponent<BoxCollider>().bounds;
            foreach (string neighbor in new[] { "Notebook_Open", "Book_Charcoal", "Book_SevenPetal", "Candleholder_Lit", "WritingMat", "CandlePile_Unlit" })
                foreach (var surface in GameObject.Find(neighbor).GetComponentsInChildren<MeshRenderer>())
                    Check(!bellBounds.Intersects(surface.bounds), "Bell " + bellBounds + " overlaps " + neighbor + "/" + surface.name + " " + surface.bounds);
            var shell = bell.GetComponentsInChildren<MeshFilter>().Single(f => f.name == "Handbell_BrassShell").sharedMesh;
            for (int i = 0; i < shell.vertexCount; i++)
            {
                var vertex = shell.vertices[i]; var normal = shell.normals[i];
                float radius = new Vector2(vertex.x, vertex.z).magnitude;
                if (Mathf.Abs(vertex.y - .055f) < .00001f && Mathf.Abs(radius - .054f) < .00001f)
                    Check(Vector3.Dot(new Vector3(vertex.x, 0, vertex.z), normal) > 0, "Bell exterior winding is inverted");
            }
            foreach (var clip in bell.strikes) Check(clip.channels == 1 && clip.frequency == 48000 && clip.length > .3f, "Invalid ring audio import");
            SessionState.SetInt(Key + ".Checks", checks); SessionState.SetBool(Key, true);
            Install(); EditorApplication.isPlaying = true;
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Install()
    {
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
    }
    static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Finish(false, message + "\n" + stack); }
    static void Click()
    {
        ReleaseGate(); Physics.SyncTransforms();
        var box = bell.GetComponent<BoxCollider>();
        Vector2 point = camera.WorldToScreenPoint(bell.transform.TransformPoint(box.center));
        Call(interactor, "ProcessInput", point, true, true, false);
    }
    static void FinishAction()
    {
        for (int i = 0; i < 250 && bell.IsBusy; i++) Step(.02f);
        Restored("Finish action");
    }
    static void Tick()
    {
        if (finished) return;
        if (EditorApplication.timeSinceStartup - started > 150) { Finish(false, "Timed out"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || !Camera.main || Time.frameCount < 8) return;
        try
        {
            checks = SessionState.GetInt(Key + ".Checks", 0);
            camera = Camera.main; camera.aspect = 16f / 9;
            view = camera.GetComponent<DeskScenePreview>(); view.followMouse = false;
            dialogue = camera.GetComponent<DialogueController>(); gate = camera.GetComponent<SceneInputGate>();
            interactor = camera.GetComponent<SceneObjectInteractor>();
            camera.GetComponent<DialogueTestConsole>().SetOpen(false); ReleaseGate();
            Check(view.SetView(DeskScenePreview.View.Desktop), "Cannot reach desktop");
            Call(view, "AnimateView", 10f, Vector2.zero);
            bell = UnityEngine.Object.FindFirstObjectByType<DeskHandbell>();
            rest = bell.transform.localPosition; restRotation = bell.transform.localRotation;
            var cameraPosition = camera.transform.position; var cameraRotation = camera.transform.rotation;
            bell.rang.AddListener(() => calls++); bell.returnedToDesk.AddListener(() => returns++);
            Capture("01-desktop");
            Detail();
            var box = bell.GetComponent<BoxCollider>();
            Vector2 point = camera.WorldToScreenPoint(bell.transform.TransformPoint(box.center));
            Call(interactor, "ProcessInput", point, true, false, false);
            Call(bell.GetComponent<SceneObject>(), "Animate", .15f);
            Check(!bell.IsBusy && !dialogue.IsActive && calls == 0, "Hover triggers bell");
            Click(); Check(bell.State == DeskHandbell.Phase.PickingUp && gate.PropActionActive,
                "Real collider click does not pick up; hits=" + string.Join(",", Physics.RaycastAll(camera.ScreenPointToRay(
                    camera.WorldToScreenPoint(bell.transform.TransformPoint(box.center))), 10).OrderBy(h => h.distance).Select(h => h.collider.name)));
            ReleaseGate(); // Exercise the lasting modal lock after the initiating frame's suppression.
            Check(!bell.TryInteract(), "Rapid repeat accepted");
            Check(!view.SetView(DeskScenePreview.View.Bookshelf), "Navigation during pickup");
            Step(.24f); Check(Vector3.Distance(bell.transform.localPosition, rest) > .03f, "Pickup does not move"); Capture("02-pickup");
            Step(.4f); Check(bell.State == DeskHandbell.Phase.Speaking && dialogue.ActiveEvent == bell.notNeededLine, "Default click should refuse");
            Call(dialogue, "AdvanceTime", .1f); dialogue.Advance();
            Check(dialogue.VisibleText == "现在不需要用这个" && dialogue.CurrentLine.speaker.isProtagonist, "Wrong Luca line");
            foreach (char ch in dialogue.VisibleText) Check(dialogue.Overlay.font.HasCharacter(ch), "Missing Chinese glyph " + ch);
            Check(calls == 0 && bell.StrikeCount == 0 && !dialogue.eyeShutter.AtSecondStop, "Refusal rang or opened door");
            Check(camera.transform.position == cameraPosition && camera.transform.rotation == cameraRotation, "Refusal changes viewpoint");
            Capture("03-not-needed");
            interactor.Inspect(GameObject.Find("Book_Charcoal").GetComponent<SceneObject>());
            Check(!interactor.InspectedObject && dialogue.ActiveEvent == bell.notNeededLine, "Click through refusal");
            dialogue.Advance(); Step(.02f);
            Check(bell.State == DeskHandbell.Phase.PuttingDown && gate.PropActionActive, "Did not put down after dialogue");
            Step(.3f); Capture("04-putdown"); FinishAction(); Check(calls == 0 && returns == 1, "Refusal completed with ringing");
            // Repeated refusal must remain repeatable, unlike first-inspection memories.
            Click(); Step(.6f); Check(dialogue.ActiveEvent == bell.notNeededLine, "Repeated refusal disappeared");
            dialogue.Stop(); Step(.02f); FinishAction();
            bell.SetRingAllowed(true); Click(); Step(.6f);
            Check(bell.State == DeskHandbell.Phase.Shaking && !dialogue.IsActive, "Allowed route did not shake");
            Step(.12f); Check(bell.StrikeCount == 1 && calls == 1 && Quaternion.Angle(bell.gripPivot.localRotation, Quaternion.identity) > 10, "Stroke/sound not synchronized");
            Check(bell.GetComponent<AudioSource>().isPlaying, "Bell AudioSource is silent");
            Capture("05-shaking"); FinishAction();
            Check(bell.StrikeCount == 6 && calls == 1, "Expected six strokes and one gameplay call");
            // One-shot permission lasts through this ring, then defaults to refusal next time.
            bell.consumePermissionOnRing = true; Click(); Step(.6f); Step(2f); FinishAction();
            Check(!bell.ringAllowed && bell.StrikeCount == 6 && calls == 2, "Permission consumption interrupted ring");
            Click(); Step(.6f); Check(dialogue.ActiveEvent == bell.notNeededLine, "Consumed permission rang again");
            bell.Cancel(); Restored("Cancel refusal"); bell.consumePermissionOnRing = false;
            // Permission may disappear between pickup and the first strike.
            bell.SetRingAllowed(true); Click(); Step(.6f); bell.SetRingAllowed(false); Step(.05f);
            Check(bell.State == DeskHandbell.Phase.Speaking && bell.StrikeCount == 0 && calls == 2, "Revoked permission still rang");
            bell.Cancel(); Restored("Revoked before stroke");
            bell.SetRingAllowed(true); Click(); Step(.6f); Step(.12f); int heard = calls;
            bell.SetRingAllowed(false); Step(.4f); FinishAction(); Check(bell.StrikeCount == 1 && calls == heard, "Revocation did not stop later strokes");
            foreach (var phase in new[] { DeskHandbell.Phase.PickingUp, DeskHandbell.Phase.Shaking, DeskHandbell.Phase.PuttingDown })
            {
                bell.SetRingAllowed(true); Click();
                if (phase != DeskHandbell.Phase.PickingUp) Step(.6f);
                if (phase == DeskHandbell.Phase.PuttingDown) bell.PutDown();
                Check(bell.State == phase, "Wrong cancellation phase");
                interactor.CancelInteraction(); Restored("Interactor cancel " + phase);
                Check(!bell.GetComponent<AudioSource>().isPlaying, "Cancelled ring continues audio");
            }
            bell.SetRingAllowed(true); Click(); Step(.6f); bell.enabled = false;
            Restored("Disable"); bell.enabled = true;
            // Modal blockers, direct scripted entry, and a missing sound all fail safely.
            gate.SetStory(true); Check(!bell.TryInteract(), "Started during story"); gate.SetStory(false); ReleaseGate();
            gate.SetDelivery(true); Check(!bell.TryInteract(), "Started during delivery"); gate.SetDelivery(false); ReleaseGate();
            gate.SetWorkbenchTyping(true); Check(!bell.TryInteract(), "Started during typing"); gate.SetWorkbenchTyping(false); ReleaseGate();
            var clips = bell.strikes; bell.strikes = new AudioClip[0];
            Check(bell.TryInteract(), "Cannot start with missing optional strike"); Step(.6f);
            Check(dialogue.ActiveEvent == bell.notNeededLine && bell.StrikeCount == 0, "Missing audio emitted gameplay call");
            interactor.CancelInteraction(); bell.strikes = clips; Restored("Direct scripted cancellation with missing ring clip");
            // Paused clock and audio resume from the same point.
            Click(); Step(.22f); var pausePose = bell.transform.position;
            typeof(DeskHandbell).GetField("paused", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(bell, true);
            Step(4); Check(bell.transform.position == pausePose && bell.State == DeskHandbell.Phase.PickingUp, "Paused timeline advanced");
            typeof(DeskHandbell).GetField("paused", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(bell, false);
            bell.Cancel(); Restored("Pause/cancel");
            // Old inspection still works after all completion/cancel paths.
            var ordinary = GameObject.Find("InkBottle").GetComponent<SceneObject>();
            interactor.Inspect(ordinary); Check(interactor.InspectedObject == ordinary, "Existing desk inspection broken");
            interactor.CancelInteraction(); ReleaseGate();
            Capture("06-returned");
            // Render real rig frames for an inspectable animation preview (clock stepped at 30 FPS).
            Directory.CreateDirectory(Output + "/frames");
            bell.SetRingAllowed(true); Click();
            for (int frame = 0; frame < 100; frame++) { Step(1f / 30); Capture("frames/ring-" + frame.ToString("D3"), 960, 540); }
            Restored("Preview sequence");
            Finish(true, "PASS: " + checks + " assertions. Unity 2022.3.62f2 / Built-in / Direct3D 11.\n"
                + "Saved prefab, idempotent installation preserving placement/permission, three metric UV meshes with outline normals and standard desk materials; no extra lights; imported 48 kHz mono audio.\n"
                + "Actual collider picking; hover does not ring; pickup, Luca refusal, automatic return; repeat refusal; allowed six-strike shake, one gameplay call; one-shot permission and revoked permission.\n"
                + "Persistent navigation/click blockers, exact restoration, cancellation in all motion phases, disable/re-enable, paused clock, missing audio, direct script cancellation, other desk inspection.\n"
                + "Screenshots and 30 FPS animation frames rendered in Play Mode. Synthetic timing/pointer checks; device listening and OS mouse input are not asserted.\n");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Detail()
    {
        var p = camera.transform.position; var q = camera.transform.rotation; float fov = camera.fieldOfView;
        camera.transform.position = bell.transform.position + new Vector3(.30f, .30f, -.43f);
        camera.transform.LookAt(bell.transform.position + Vector3.up * .14f); camera.fieldOfView = 40;
        Capture("00-detail"); camera.transform.SetPositionAndRotation(p, q); camera.fieldOfView = fov;
    }
    static void Capture(string name, int width = 1600, int height = 900)
    {
        var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        var previous = camera.targetTexture; var active = RenderTexture.active;
        var overlay = dialogue.Overlay.OverlayCamera; var previousOverlay = overlay.targetTexture;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = overlay.targetTexture = target;
            Canvas.ForceUpdateCanvases(); camera.Render(); if (overlay.enabled) overlay.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous; overlay.targetTexture = previousOverlay; RenderTexture.active = active;
            UnityEngine.Object.DestroyImmediate(image); RenderTexture.ReleaseTemporary(target);
        }
    }
    static void Finish(bool success, string message)
    {
        if (finished) return; finished = true;
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log; SessionState.SetBool(Key, false);
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/validation.txt", message);
        if (success) Debug.Log("HANDBELL_VALIDATION_PASS"); else Debug.LogError(message);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
