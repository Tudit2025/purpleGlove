using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PurpleGlove;
using PurpleGlove.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CandlePileValidation
{
    const string Key = "CandlePileValidation.Running";
    static readonly string Output = Path.GetFullPath("../../output/candle-pile-validation");
    static double started;
    static int checks;
    static bool done;
    static int reloadSourceInstance;
    static bool waitingForReload;
    static CandlePileValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    static void Call(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, args);

    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            var sourceArchive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            string papers = string.Join("|", sourceArchive.acts.SelectMany(a => a.documents).Select(d => d.name));
            int lights = UnityEngine.Object.FindObjectsOfType<Light>(true).Length;
            DeskSceneBuilder.InstallActCandlePile();
            DeskSceneBuilder.InstallActCandlePile();
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            var piles = UnityEngine.Object.FindObjectsOfType<ActCandlePile>(true);
            Check(piles.Length == 1, "Installer must be idempotent");
            var pile = piles[0];
            Check(pile.archive && pile.story && pile.candles.Length == 4, "Saved event sources/candles are missing");
            Check(papers == string.Join("|", pile.archive.acts.SelectMany(a => a.documents).Select(d => d.name)), "Installer changed documents");
            Check(UnityEngine.Object.FindObjectsOfType<Light>(true).Length == lights, "Decorations added a light");
            Check(pile.GetComponentsInChildren<Collider>(true).Length == 0 && pile.GetComponentsInChildren<SceneObject>(true).Length == 0, "Decorative pile is pickable");
            Check(pile.transform.localScale == Vector3.one, "Pile changes metre UV density through scaling");
            var holder = GameObject.Find("Candleholder_Lit");
            Check(Vector3.Distance(holder.transform.position, pile.transform.position) < 0.4f, "Pile is not beside the holder");
            foreach (var candle in pile.candles)
            {
                Check(candle.activeSelf && candle.transform.localScale == Vector3.one, "Invalid initial candle pose");
                var mesh = candle.GetComponent<MeshFilter>().sharedMesh;
                var normals = new List<Vector3>(); mesh.GetUVs(2, normals);
                Check(mesh && mesh.uv.Length == mesh.vertexCount && normals.Count == mesh.vertexCount, "Missing mesh, metre UVs or outline normals");
                var renderer = candle.GetComponent<Renderer>();
                Check(renderer.sharedMaterials.All(m => m && m.shader.name == "PurpleGlove/Pixel Outlined" && m.GetFloat("_PixelSize") == 4), "Candle style differs from desk props");
                var bounds = renderer.bounds;
                Check(bounds.min.y >= 0.800f && bounds.min.x > -1.425f && bounds.max.x < 1.425f && bounds.max.z < 0.775f, "Candle goes through or beyond the tabletop");
                Check(!bounds.Intersects(GameObject.Find("Book_Charcoal").GetComponentInChildren<Renderer>().bounds), "Pile intersects the reference book");
            }
            SessionState.SetInt(Key + ".Checks", checks);
            SessionState.SetBool(Key, true); Install(); EditorApplication.isPlaying = true;
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

    static void Count(ActCandlePile pile, int expected, string label)
    {
        Check(pile.RemainingCount == expected && pile.candles.Count(c => c.activeSelf) == expected, label);
        for (int i = 0; i < 4; i++) Check(pile.candles[i].activeSelf == (i < expected), "Top-first stable removal: " + label);
        Check(GameObject.Find("Candleholder_Lit").GetComponentInChildren<Light>().enabled, "Lit candle was extinguished");
    }

    static void ReleaseGate(SceneInputGate gate)
    { typeof(SceneInputGate).GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gate, -1); }

    static void Tick()
    {
        if (done) return;
        if (EditorApplication.timeSinceStartup - started > 120) { Finish(false, "Timed out"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.frameCount < 4) return;
        try
        {
            if (waitingForReload)
            {
                var fresh = UnityEngine.Object.FindFirstObjectByType<ActCandlePile>();
                if (!fresh || fresh.GetInstanceID() == reloadSourceInstance) return;
                Count(fresh, 4, "Reload resets initial reserves");
                var received = fresh.archive;
                Check(received.ReceiveAct(2), "Direct delivery two"); Count(fresh, 3, "Delivery-only act two");
                fresh.enabled = false; Check(received.ReceiveAct(4), "Direct delivery four while disabled"); fresh.enabled = true;
                Count(fresh, 1, "Re-enable catches up to received act");
                Check(received.ReceiveAct(1), "Late old delivery"); Count(fresh, 1, "Late old delivery cannot replenish");
                Check(received.ReceiveAct(5), "Direct delivery five"); Count(fresh, 0, "Delivery-only final act");
                Finish(true, "PASS: " + checks + " assertions. Unity 2022.3.62f2 / Built-in / Direct3D 11.\n"
                    + "Saved scene and idempotent installer; four reusable meshes, metre UVs, prop pixel shader and outline normals; tabletop placement, unchanged lights/documents, no pickup.\n"
                    + "Acts 1-5: 4/3/2/1/0; stable top-first removal; prepared under black; cancellation before/after blackout; delivery-only route; repeated/old delivery and dossier review; completion, disable/re-enable, late enable and scene reload.\n"
                    + "Desktop/detail renders for all five acts. No real-time countdown or extra light; lit candle remains enabled.\n");
                return;
            }
            checks = SessionState.GetInt(Key + ".Checks", 0);
            var pile = UnityEngine.Object.FindFirstObjectByType<ActCandlePile>();
            var story = pile.story; var archive = pile.archive; var camera = Camera.main;
            var curtain = camera.GetComponent<ActTransitionOverlay>();
            var view = camera.GetComponent<DeskScenePreview>();
            var gate = camera.GetComponent<SceneInputGate>();
            camera.GetComponent<DialogueTestConsole>().SetOpen(false); ReleaseGate(gate);
            view.followMouse = false;
            Count(pile, 4, "Initial act one has four reserves");
            Check(archive.ReceiveAct(1), "First packet cannot be received"); Count(pile, 4, "Act one delivery");
            Capture(camera, "act-1-desktop"); CaptureDetail(camera, pile, "act-1-detail");
            Check(story.PlayActTransition(2), "Cannot start transition two");
            Call(story, "AdvanceTime", curtain.fadeOutSeconds * 0.5f); Count(pile, 4, "No change before blackout");
            story.Stop(); ReleaseGate(gate); Count(pile, 4, "Cancel before blackout");
            Check(story.PlayActTransition(2), "Cannot restart transition two");
            Call(story, "AdvanceTime", 10f); Count(pile, 3, "Prepared under black");
            Check(curtain.Opacity == 1 && pile.EnteredAct == 1, "Preview committed too early");
            story.Stop(); ReleaseGate(gate); Count(pile, 4, "Cancel after blackout restores reserves");

            for (int act = 2; act <= 5; act++)
            {
                Check(story.PlayActTransition(act), "Cannot transition to " + act);
                Call(story, "AdvanceTime", 10f);
                Count(pile, 5 - act, "Act " + act + " prepared before reveal");
                for (int phase = 0; phase < 5; phase++) Call(story, "AdvanceTime", 10f);
                ReleaseGate(gate);
                Check(!story.IsActive && pile.EnteredAct == act, "Act entry not committed");
                Count(pile, 5 - act, "Entered act " + act);
                Check(archive.ReceiveAct(act), "Packet cannot be received for " + act);
                Check(!archive.ReceiveAct(act), "Repeated packet accepted"); Count(pile, 5 - act, "Delivery not double counted");
                Capture(camera, "act-" + act + "-desktop"); CaptureDetail(camera, pile, "act-" + act + "-detail");
                Check(archive.ShowActOnDesk(1), "Cannot retrieve old dossier"); Count(pile, 5 - act, "Old dossier cannot replenish");
                Check(archive.StoreDeskAct(), "Cannot store dossier"); Count(pile, 5 - act, "Storing dossier cannot change time");
                Check(archive.CompleteAct(act - 1), "Cannot complete prior current act"); Count(pile, 5 - act, "Completion does not consume again");
            }
            pile.EnterAct(1); pile.EnterAct(0); pile.EnterAct(6); Count(pile, 0, "Old and invalid act IDs cannot replenish");
            pile.enabled = false; pile.enabled = true; Count(pile, 0, "Re-enable retains committed act");

            // Independent runtime clone verifies receive-only progression and resync after missing events.
            var probeRoot = new GameObject("Disabled reserve probe"); probeRoot.SetActive(false);
            var probe = probeRoot.AddComponent<ActCandlePile>();
            probe.candles = Enumerable.Range(0, 4).Select(i => { var go = new GameObject("Candle " + i); go.transform.SetParent(probeRoot.transform); return go; }).ToArray();
            probe.archive = archive; probe.story = story; probeRoot.SetActive(true);
            Check(probe.EnteredAct == 5 && probe.RemainingCount == 0, "Late-enabled decoration did not catch up");
            UnityEngine.Object.DestroyImmediate(probeRoot);

            // Fresh scene: isolate delivery-only route from presentation events.
            reloadSourceInstance = pile.GetInstanceID(); waitingForReload = true;
            EditorSceneManager.LoadSceneInPlayMode(DeskSceneStartup.ScenePath, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }

    static void CaptureDetail(Camera camera, ActCandlePile pile, string name)
    {
        Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation; float fov = camera.fieldOfView;
        camera.transform.position = pile.transform.position + new Vector3(0.30f, 0.48f, -0.52f);
        camera.transform.LookAt(pile.transform.position + new Vector3(0.035f, 0.05f, -0.07f)); camera.fieldOfView = 48;
        Capture(camera, name);
        camera.transform.SetPositionAndRotation(position, rotation); camera.fieldOfView = fov;
    }
    static void Capture(Camera camera, string name)
    {
        var target = RenderTexture.GetTemporary(1600, 900, 24);
        var previous = camera.targetTexture; var active = RenderTexture.active;
        camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
        File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image); camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target);
    }
    static void Finish(bool success, string message)
    {
        if (done) return; done = true;
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        SessionState.SetBool(Key, false); Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/validation.txt", message);
        if (success) Debug.Log(message); else Debug.LogError(message);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
