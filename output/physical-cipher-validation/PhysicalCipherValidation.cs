using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PurpleGlove;
using PurpleGlove.Editor;

// Run only in Temp/physical-cipher-review. Never switches the user's editor to Play mode.
[InitializeOnLoad]
public static class PhysicalCipherValidation
{
    const string Key = "PhysicalCipherValidation.Play";
    static readonly string Output = Path.GetFullPath("../../output/physical-cipher-validation");
    static double started;
    static bool running;
    static int checks;
    static PhysicalCipherValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    static object Call(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, args);
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static void Ungate(SceneInputGate gate) => typeof(SceneInputGate).GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gate, -1);
    static void Settle(CipherWorkbench work)
    {
        Call(work, "AnimateToolTransfer", 1f); Call(work, "AnimateToolTransfer", 1f);
        var gate = work.GetComponent<SceneInputGate>(); Ungate(gate);
        Call(work, "Update"); Ungate(gate);
        Call(work.GetComponent<DeskScenePreview>(), "AnimateView", 10f, Vector2.zero);
        Physics.SyncTransforms();
    }
    static Vector2 Point(Camera camera, CipherWorkbenchTarget target)
    {
        var collider = target.GetComponent<BoxCollider>();
        return camera.WorldToScreenPoint(collider.transform.TransformPoint(collider.center));
    }
    static CipherWorkbenchTarget Part(Transform root, CipherWorkbenchTarget.Kind kind, int index = -1) =>
        root.GetComponentsInChildren<CipherWorkbenchTarget>().First(t => t.kind == kind && (index < 0 || t.index == index));
    static void Click(CipherWorkbench work, CipherWorkbenchTarget target)
    {
        var pointer = Point(work.GetComponent<Camera>(), target);
        Check((CipherWorkbenchTarget)Call(work, "PickTarget", pointer, false) == target, "Collider obscured: " + target.name);
        Call(work, "ActivateTarget", target, pointer);
    }

    public static void Run()
    {
        Directory.CreateDirectory(Output);
        try
        {
            DeskSceneBuilder.InstallCipherToolbox(); DeskSceneBuilder.InstallCipherToolbox();
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            var work = Camera.main.GetComponent<CipherWorkbench>();
            Check(UnityEngine.Object.FindObjectsOfType<CipherWorkbench>().Length == 1, "Duplicate controller");
            Check(work.letterSamples.Length == 26 && work.letterSamples.All(t => t), "Missing A-Z sample");
            Check(work.letterTray.GetComponentsInChildren<CipherLetterTile>().Length == 78, "Need 26 stacks of three");
            Check(work.affineLetters.Length == 52 && work.railLetters.Length == 82, "Grid sizes changed");
            string[] rows = { "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
            for (int r = 0; r < rows.Length; r++)
            for (int c = 0; c < rows[r].Length; c++)
            {
                var tile = work.letterSamples[rows[r][c] - 'A'];
                Check(tile.Letter == rows[r][c] && tile.animationPivot && tile.homeSocket, "Bad stable tile identity");
                Check(Mathf.Abs(tile.transform.parent.localPosition.z - (0.065f - r * 0.065f)) < 0.0001f, "QWERTY row wrong");
                Check(tile.transform.lossyScale == Vector3.one, "Tile UV density affected by scale");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/DeskSceneV1/Prefabs/PF_LetterTile_" + rows[r][c] + ".prefab");
                Check(prefab && prefab.GetComponent<CipherLetterTile>().Letter == rows[r][c], "Missing reusable letter prefab");
                Check(prefab.GetComponentsInChildren<TextMesh>().Single().text == rows[r][c].ToString(), "Wrong prefab glyph");
            }
            foreach (var renderer in work.letterTray.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.GetComponent<MeshFilter>()) continue;
                foreach (var material in renderer.sharedMaterials)
                {
                    Check(material.shader.name == "PurpleGlove/Pixel Outlined", "Nonstandard tray shader");
                    Check(material.mainTexture && material.mainTexture.filterMode == FilterMode.Point, "Missing point-filtered material");
                    Check(material.GetFloat("_PixelSize") == 4 && material.GetColor("_InspectionFill").maxColorComponent == 0, "Wrong density or self-illumination");
                }
            }
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
    static void Tick()
    {
        if (running) return;
        if (EditorApplication.timeSinceStartup - started > 90) { Finish(false, "Timeout"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || !Camera.main || Time.frameCount < 12) return;
        running = true;
        try
        {
            var camera = Camera.main; var work = camera.GetComponent<CipherWorkbench>();
            var view = camera.GetComponent<DeskScenePreview>(); var gate = camera.GetComponent<SceneInputGate>();
            var interactor = camera.GetComponent<SceneObjectInteractor>();
            view.followMouse = false; camera.aspect = 16f / 9;
            Ungate(gate);
            Check(!(bool)Call(work, "HandleEscape") && !gate.FrameConsumed, "Workbench steals desktop/inspection Escape");
            Capture(camera, "desktop");
            Check(work.ActiveTool == CipherWorkbench.Tool.None && work.tools.All(t => !t.activeSelf), "Tools should start stored");
            work.FocusWorkspace(); Settle(work);
            Capture(camera, "workspace");
            foreach (float aspect in new[] { 4f / 3, 16f / 9, 21f / 9 })
            {
                camera.aspect = aspect;
                foreach (float x in new[] { -0.38f, 0.38f }) foreach (float z in new[] { -0.34f, 0.34f })
                {
                    Vector3 p = camera.WorldToViewportPoint(work.mat.TransformPoint(new Vector3(x, 0.01f, z)));
                    Check(p.x > 0.01f && p.x < 0.99f && p.y > 0.01f && p.y < 0.99f, "Mat clipped " + aspect);
                }
                foreach (var tile in work.letterSamples)
                {
                    Vector3 p = camera.WorldToViewportPoint(tile.transform.position);
                    Check(p.x > 0.01f && p.x < 0.99f && p.y > 0.01f && p.y < 0.99f, "Letter clipped " + aspect);
                }
                Capture(camera, "workspace-" + aspect.ToString("0.00"));
            }
            camera.aspect = 16f / 9;
            for (int i = 0; i < 5; i++)
            {
                work.OpenToolbox(); Call(work.toolbox, "Animate", 10f); work.lid.localRotation = Quaternion.Euler(108, 0, 0);
                Check(work.ToolboxOpen && gate.WorkbenchModal, "Box must own input");
                var slot = work.toolbox.GetComponentsInChildren<CipherWorkbenchTarget>().Single(t => t.kind == CipherWorkbenchTarget.Kind.StoredTool && (int)t.tool == i);
                if (i == 0) Capture(camera, "open-toolbox");
                Click(work, slot); Settle(work);
                Check(work.ActiveTool == (CipherWorkbench.Tool)i && work.tools.Count(t => t.activeSelf) == 1, "Mutual exclusion/transfer");
                Check(work.storedTools.Count(t => t.activeSelf) == 4, "Storage reciprocal");
                Capture(camera, "tool-" + (CipherWorkbench.Tool)i);
            }
            work.SelectTool(CipherWorkbench.Tool.Caesar); Settle(work);
            Call(view, "AdjustDecodeZoom", 10f); Call(view, "AnimateView", 10f, Vector2.zero);
            Check(Mathf.Abs(camera.transform.position.y - (work.mat.position.y + view.decodeFocusHeight * 0.60f)) < 0.001f, "Detail zoom limit");
            Capture(camera, "tool-detail-Caesar");
            Call(view, "AdjustDecodeZoom", -10f); Call(view, "AnimateView", 10f, Vector2.zero);
            var rotor = Part(work.tools[0].transform, CipherWorkbenchTarget.Kind.Rotor);
            Vector3 center = work.tools[0].transform.position;
            Vector2 start = camera.WorldToScreenPoint(center + new Vector3(0, 0.035f, 0.07f));
            Vector2 end = camera.WorldToScreenPoint(center + new Vector3(0.07f, 0.035f, 0));
            Call(work, "BeginDrag", rotor, start); Call(work, "DragAt", end);
            Check(work.CaesarShift == 6 || work.CaesarShift == 7, "Rotor drag wrong direction");
            Check(!view.SetView(DeskScenePreview.View.Desktop), "Drag leaks navigation");
            Call(work, "EndDrag", end); Ungate(gate); Check(!gate.WorkbenchModal, "Drag release stuck");
            work.SetCaesarShift(-1); Check(work.CaesarShift == 25, "Negative wrap");
            work.SelectTool(CipherWorkbench.Tool.SaintCyr); Settle(work);
            var slide = Part(work.tools[2].transform, CipherWorkbenchTarget.Kind.Slide);
            start = Point(camera, slide); end = camera.WorldToScreenPoint(slide.transform.TransformPoint(slide.GetComponent<BoxCollider>().center) - Vector3.right * 0.10f);
            Call(work, "BeginDrag", slide, start); Call(work, "EndDrag", end);
            Check(work.SlideShift == 10, "Slide drag not quantized to letters");
            work.SetKeyword("s-o i e"); Check(work.Keyword == "SOIE" && work.SlideShift == 18, "Keyword filtering");
            Click(work, Part(work.tools[2].transform, CipherWorkbenchTarget.Kind.NextKey));
            Check(work.KeywordIndex == 1 && work.SlideShift == 14, "Physical keyword peg");
            Click(work, Part(work.tools[2].transform, CipherWorkbenchTarget.Kind.Keyword));
            Call(work, "HandleWritingInput", new Event { type = EventType.KeyDown, keyCode = KeyCode.Backspace });
            Check(work.Keyword == "SOI", "Physical keyword writing"); Call(work, "EndWriting");
            work.SelectTool(CipherWorkbench.Tool.Affine); Settle(work);
            Call(view, "AdjustDecodeZoom", 10f); Call(view, "AnimateView", 10f, Vector2.zero);
            Capture(camera, "tool-detail-Affine");
            Call(view, "AdjustDecodeZoom", -10f); Call(view, "AnimateView", 10f, Vector2.zero);
            var cell = Part(work.tools[3].transform, CipherWorkbenchTarget.Kind.Cell, 0);
            Click(work, cell);
            Check(gate.WorkbenchTyping && !view.SetView(DeskScenePreview.View.Bookshelf), "Typing leaks WASD");
            Call(work, "HandleWritingInput", new Event { type = EventType.KeyDown, character = 'a', keyCode = KeyCode.A });
            Check(work.Affine[0] == "A", "Physical cell typing");
            Check((bool)Call(work, "HandleEscape") && !gate.WorkbenchTyping && view.CurrentView == DeskScenePreview.View.DecodeFocus, "Escape must end writing first");
            var tileA = work.letterSamples[25]; var letterHit = tileA.GetComponent<CipherWorkbenchTarget>();
            Click(work, letterHit); Check(work.IsManipulating && gate.WorkbenchModal, "Letter not held");
            Call(work, "EndDrag", Point(camera, cell));
            Check(work.Affine[0] == "Z" && tileA.IsReturning, "Tile does not stamp physical cell/return");
            tileA.ReturnHome(true);
            Click(work, letterHit); Call(work, "EndDrag", new Vector2(-100, -100));
            Check(tileA.IsReturning && !gate.WorkbenchModal, "Invalid drop lost tile or lock"); tileA.ReturnHome(true);
            var slip = Part(work.mat, CipherWorkbenchTarget.Kind.Plaintext); Click(work, slip);
            Call(work, "HandleWritingInput", new Event { type = EventType.KeyDown, character = 'K', keyCode = KeyCode.K });
            Check(work.Plaintext == "K" && work.plaintextInk.text == "K", "Physical paper input");
            Call(work, "EndWriting"); work.SetPlaintext("KEEP THE ORIGINAL\n保留原件。");
            work.SelectTool(CipherWorkbench.Tool.RailFence); Settle(work);
            Click(work, Part(work.tools[4].transform, CipherWorkbenchTarget.Kind.Cell, 81));
            Call(work, "HandleWritingInput", new Event { type = EventType.KeyDown, character = 'z', keyCode = KeyCode.Z });
            Check(work.Rails[81] == "Z", "Last rail cell");
            gate.SetConsole(true); Call(work, "Update"); Check(!gate.WorkbenchTyping && !gate.WorkbenchModal, "Console leaves writing locked");
            gate.SetConsole(false); Ungate(gate);
            work.SelectTool(CipherWorkbench.Tool.Affine); Settle(work);
            Check(work.Affine[0] == "Z" && work.Plaintext.Contains("保留原件"), "Switch lost notes");
            Click(work, letterHit); view.SetDialogueLock(true); Call(work, "Update");
            Check(!work.IsManipulating && !gate.WorkbenchModal && tileA.IsReturning, "Dialogue loses held tile");
            Check(!work.SelectTool(CipherWorkbench.Tool.Caesar), "Dialogue permits tool exchange");
            view.SetDialogueLock(false); Ungate(gate); tileA.ReturnHome(true); Settle(work);
            Click(work, Part(work.tools[3].transform, CipherWorkbenchTarget.Kind.ReturnTool)); Settle(work);
            Check(work.ActiveTool == CipherWorkbench.Tool.None && work.tools.All(t => !t.activeSelf), "Physical return grip");
            Check(work.storedTools.All(t => t.activeSelf), "Storage missing returned prop");
            foreach (var tile in work.letterSamples)
            {
                var target = tile.GetComponent<CipherWorkbenchTarget>();
                Check((CipherWorkbenchTarget)Call(work, "PickTarget", Point(camera, target), false) == target, "Unreachable letter " + tile.Letter);
            }
            Capture(camera, "completed-workspace");
            work.SelectTool(CipherWorkbench.Tool.Pigpen); Settle(work);
            var lights = UnityEngine.Object.FindObjectsOfType<Light>(); var intensities = lights.Select(l => l.intensity).ToArray();
            for (int i = 0; i < lights.Length; i++) lights[i].intensity *= 0.5f;
            Capture(camera, "half-light");
            for (int i = 0; i < lights.Length; i++) lights[i].intensity = intensities[i];
            Finish(true, "PASS: " + (checks + SessionState.GetInt(Key + ".Checks", 0)) + " assertions in Unity 2022.3.62f2 Play Mode.\n"
                + "Idempotent scene generation; 78 individual wood tiles; 26 reusable prefabs; QWERTY identity and metre UV material standard.\n"
                + "Actual collider picking for toolbox slots, keyword pegs, all 26 letters, grid and writing paper; physical drag handlers for rotation, slide and letter drops.\n"
                + "Tool exclusivity/animated exchange, field input, persistence across switches, invalid drops, typing and drag input gates, console/dialogue interruption.\n"
                + "4:3 / 16:9 / 21:9 camera framing and real rendered screenshots.\n"
                + "Limit: synthetic pointer/key replay in Play Mode; no manual OS mouse or IME testing.\n");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Finish(bool success, string message)
    {
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Output + "/validation.txt", message);
        if (success) Debug.Log("PHYSICAL_CIPHER_VALIDATION_PASS"); else Debug.LogError(message);
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
