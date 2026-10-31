using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PurpleGlove;
using PurpleGlove.Editor;

// Run from a throwaway project under Temp; never enters Play mode in the user's editor.
[InitializeOnLoad]
public static class CipherWorkbenchValidation
{
    const string Key = "CipherWorkbenchValidation.Play";
    static readonly string Output = Path.GetFullPath("../../output/cipher-workbench-validation");
    static int checks;
    static bool running;
    static double started;
    static CipherWorkbenchValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static object Call(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    static void Settle(DeskScenePreview view) => Call(view, "AnimateView", 10f, Vector2.zero);
    static void Ungate(SceneInputGate gate) => typeof(SceneInputGate).GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gate, -1);

    public static void Run()
    {
        Directory.CreateDirectory(Output);
        try
        {
            DeskSceneBuilder.InstallCipherToolbox();
            DeskSceneBuilder.InstallCipherToolbox();
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            var work = Camera.main.GetComponent<CipherWorkbench>();
            Check(work && work.tools.Length == 5 && work.tools.All(t => t), "Five saved tools required");
            Check(UnityEngine.Object.FindObjectsOfType<CipherWorkbench>().Length == 1, "Duplicate workbench");
            Check(work.toolbox.GetComponents<CipherWorkbenchAction>().Length == 1, "Duplicate box action after reinstall");
            Check(work.storedTools.Length == 5 && work.storedTools.All(t => t.activeSelf), "Stored tools missing");
            Check(work.affineLetters.Length == 52 && work.railLetters.Length == 82, "Wrong grid capacity");
            Check(work.tools.All(t => !t.activeSelf), "Tools must start inside box");
            Check(work.saintCyrSlide.GetComponentsInChildren<TextMesh>(true).Length == 52, "Saint-Cyr needs two alphabets");
            Check((int)DeskScenePreview.View.MatFocus == 3 && (int)DeskScenePreview.View.Doorway == 1, "Existing view enum changed");
            SessionState.SetInt(Key + ".Build", checks);
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
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Finish(false, message + "\n" + stack);
    }
    static void Tick()
    {
        if (running) return;
        if (EditorApplication.timeSinceStartup - started > 80) { Finish(false, "Play mode timeout"); return; }
        var work = Camera.main ? Camera.main.GetComponent<CipherWorkbench>() : null;
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || !work || Time.frameCount < 8) return;
        running = true;
        try
        {
            var camera = Camera.main; var view = camera.GetComponent<DeskScenePreview>();
            var gate = camera.GetComponent<SceneInputGate>(); var interactor = camera.GetComponent<SceneObjectInteractor>();
            view.followMouse = false; camera.aspect = 16f / 9;
            Vector3 seat = camera.transform.position; Capture(camera, "desktop");
            Check(work.ActiveTool == CipherWorkbench.Tool.None && work.tools.All(t => !t.activeSelf), "Initial selection");
            // Pick the original box through the actual scene collider.
            var box = work.toolbox.GetComponent<BoxCollider>();
            Vector2 point = camera.WorldToScreenPoint(work.toolbox.transform.TransformPoint(box.center));
            Call(interactor, "ProcessPointer", point, true, true);
            Check(work.ToolboxOpen && gate.WorkbenchModal, "Original box cannot be clicked");
            Check(!view.SetView(DeskScenePreview.View.Bookshelf), "Toolbox fails to lock scene keys");
            Call(work.toolbox, "Animate", 5f); work.lid.localRotation = Quaternion.Euler(108, 0, 0);
            Check(work.lid.TransformPoint(new Vector3(0, 0, -0.3f)).y > work.lid.position.y, "Lid opens downward");
            Capture(camera, "open-toolbox");
            work.CloseToolbox(); Ungate(gate);
            Check(work.toolbox.State == SceneObject.ObjectState.Resting && !gate.WorkbenchModal, "Closing box did not restore pose");
            Check((bool)Call(view, "HandleViewKey", KeyCode.D), "D rejected"); Settle(view);
            Check(view.CurrentView == DeskScenePreview.View.DecodeFocus, "D selects wrong view");
            Check(Vector3.Dot(camera.transform.forward, Vector3.down) > 0.9999f, "D must be exactly top-down");
            Check(view.decodeFocusHeight < view.matFocusOffset.y, "D not closer than W");
            Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
            Call(view, "AnimateView", 10f, new Vector2(6, 4));
            Check(camera.transform.position == position && Quaternion.Angle(camera.transform.rotation, rotation) < 0.001f, "D mouse sway moves small cells");
            foreach (float aspect in new[] { 16f / 9, 4f / 3, 21f / 9 })
            {
                camera.aspect = aspect;
                foreach (float x in new[] { -0.38f, 0.38f }) foreach (float z in new[] { -0.34f, 0.34f })
                {
                    var p = camera.WorldToViewportPoint(work.mat.TransformPoint(new Vector3(x, 0.01f, z)));
                    Check(p.x > 0.01f && p.x < 0.99f && p.y > 0.01f && p.y < 0.99f, "Mat clipped at " + aspect);
                }
            }
            camera.aspect = 16f / 9;
            for (int repeat = 0; repeat < 3; repeat++)
            for (int tool = 0; tool < 5; tool++)
            {
                Check(work.SelectTool((CipherWorkbench.Tool)tool), "Select failed"); Ungate(gate); Call(work, "Update"); Settle(view);
                Check(work.tools.Count(t => t.activeSelf) == 1 && work.tools[tool].activeSelf, "Multiple tools on mat");
                Check(work.storedTools.Count(t => t.activeSelf) == 4 && !work.storedTools[tool].activeSelf, "Stored display not reciprocal");
                if (repeat == 0) Capture(camera, "tool-" + (CipherWorkbench.Tool)tool);
            }
            Check(!work.SelectTool((CipherWorkbench.Tool)99), "Invalid selection accepted");
            work.SetCaesarShift(-1); Check(work.CaesarShift == 25, "Negative Caesar wrap");
            work.SetCaesarShift(26); Check(work.CaesarShift == 0, "Caesar wrap");
            work.SelectTool(CipherWorkbench.Tool.Caesar); work.SetCaesarShift(3);
            work.caesarRotor.localRotation = Quaternion.Euler(0, 3 * 360f / 26, 0);
            var innerA = work.caesarRotor.Find("Plain 0"); var outerD = work.tools[0].transform.Find("Cipher 3");
            var origin = work.tools[0].transform.position;
            Vector3 ia = innerA.position - origin, od = outerD.position - origin; ia.y = od.y = 0;
            Check(Vector3.Angle(ia, od) < 0.01f, "Caesar rotates in wrong direction");
            work.SelectTool(CipherWorkbench.Tool.SaintCyr); work.SetKeyword(" s-o i e! ");
            Check(work.Keyword == "SOIE" && work.SlideShift == 18, "Keyword filtering/alignment");
            string cipher = "XFIRUSEMDZKVGGAELRIAF", expected = "FRANCEWILLCROSSATDAWN";
            for (int i = 0; i < cipher.Length; i++)
            {
                Check((char)('A' + CipherWorkbench.Wrap(cipher[i] - 'A' - work.SlideShift)) == expected[i], "Vigenere alignment at " + i);
                work.StepKeyword(1);
            }
            work.SetSlideShift(18); work.saintCyrSlide.localPosition = new Vector3(-18 * work.slidePitch, 0, 0);
            var fixedA = work.tools[2].transform.Find("Plain 0"); var slidingS = work.saintCyrSlide.Find("Cipher 18");
            Check(Mathf.Abs(fixedA.position.x - slidingS.position.x) < 0.0001f, "Saint-Cyr physical alignment reversed");
            Ungate(gate); Call(work, "Update"); Settle(view); Capture(camera, "saint-cyr-key-S");
            for (int i = 0; i < 52; i++) work.SetCell(CipherWorkbench.Tool.Affine, i, ((char)('A' + i % 26)).ToString());
            for (int i = 0; i < 82; i++) work.SetCell(CipherWorkbench.Tool.RailFence, i, ((char)('A' + i % 26)).ToString());
            work.SetCell(CipherWorkbench.Tool.Affine, 0, "!a1");
            work.SetPlaintext("KEEP THE ORIGINAL\n保留原件；核验来源。");
            foreach (var tool in new[] { CipherWorkbench.Tool.Affine, CipherWorkbench.Tool.RailFence, CipherWorkbench.Tool.None, CipherWorkbench.Tool.Affine })
            {
                work.SelectTool(tool); Ungate(gate); Call(work, "Update"); Settle(view);
                Check(work.Affine[0] == "A" && work.Rails[81] == "D" && work.Plaintext.Contains("保留原件"), "Switching erases notes");
                if (tool != CipherWorkbench.Tool.None) Capture(camera, "filled-" + tool);
            }
            Check(work.affineLetters.All(t => t.text.Length == 1) && work.railLetters.All(t => t.text.Length == 1), "Physical cells not updated");
            Check(work.plaintextInk.text.Contains("保留原件"), "Physical draft missing");
            Call(work, "BeginTyping");
            foreach (KeyCode key in new[] { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D })
                Check(!(bool)Call(view, "HandleViewKey", key), "Typing leaks to view key " + key);
            Call(work, "EndWriting"); Ungate(gate);
            work.OpenToolbox(); gate.SetConsole(true); Call(work, "Update");
            Check(!work.ToolboxOpen && !gate.WorkbenchModal && !gate.WorkbenchTyping, "Console leaves toolbox modal stuck");
            gate.SetConsole(false); Ungate(gate); Call(work, "BeginTyping");
            view.SetDialogueLock(true); Call(work, "Update");
            Check(view.CurrentView == DeskScenePreview.View.Doorway && !gate.WorkbenchTyping, "Dialogue fails to suspend workbench");
            Check(!work.SelectTool(CipherWorkbench.Tool.Caesar), "Can change tool during dialogue");
            view.SetDialogueLock(false); Ungate(gate);
            Check(view.CurrentView == DeskScenePreview.View.DecodeFocus && work.Affine[0] == "A", "Dialogue loses view or notes");
            Check((bool)Call(view, "HandleViewKey", KeyCode.W), "W regression"); Settle(view);
            Check(view.CurrentView == DeskScenePreview.View.MatFocus, "W changed");
            Check((bool)Call(view, "HandleViewKey", KeyCode.S), "S regression"); Settle(view);
            Check(Vector3.Distance(camera.transform.position, seat) < 0.001f, "S no longer seated");
            ValidateGridInput(work, view, gate);
            work.SelectTool(CipherWorkbench.Tool.None);
            Check(work.tools.All(t => !t.activeSelf) && work.storedTools.All(t => t.activeSelf), "Return all failed");
            Finish(true, "PASS: " + (checks + SessionState.GetInt(Key + ".Build", 0)) + " assertions; Unity 2022.3.62f2 Play Mode / Direct3D 11.\n"
                + "Saved scene reload and idempotent installer; actual original-box ray click; physical lid/preview; five mutually exclusive tools.\n"
                + "D is closer, exactly top-down, stable under mouse motion; 4:3, 16:9 and 21:9 framing; W/S and dialogue restoration.\n"
                + "Caesar physical alignment and wrap; Saint-Cyr two-alphabet alignment and full script ciphertext with SOIE.\n"
                + "52 affine cells, 82 rail cells, independent plaintext; contents survive repeated tool switches; typing blocks WASD.\n"
                + "Console/dialogue interruption releases modal and writing locks without losing work.\nKeyboard event handler: letters, auto-advance, deletion, arrows, reverse Tab, last-cell bounds.\n"
                + "Limit: camera captures exclude IMGUI overlays. Batch EditorWindow pointer replay was inconclusive; full Game view mouse interaction was not manually verified.\n");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void ValidateGridInput(CipherWorkbench work, DeskScenePreview view, SceneInputGate gate)
    {
        work.SelectTool(CipherWorkbench.Tool.Affine); Ungate(gate); Call(work, "Update"); Settle(view);
        var index = typeof(CipherWorkbench).GetField("editingCell", BindingFlags.Instance | BindingFlags.NonPublic);
        index.SetValue(work, 0); Call(work, "BeginTyping");
        void Key(KeyCode code, char c = '\0', bool shift = false) => Call(work, "HandleGridInput",
            new Event { type = EventType.KeyDown, keyCode = code, character = c, shift = shift },
            work.ActiveTool == CipherWorkbench.Tool.Affine ? 52 : 82);
        Key(KeyCode.Z, 'z'); Key(KeyCode.Y, 'y');
        Check(work.Affine[0] == "Z" && work.Affine[1] == "Y" && (int)index.GetValue(work) == 2, "Grid event typing/advance failed");
        Key(KeyCode.LeftArrow); Key(KeyCode.Delete);
        Check(work.Affine[1] == "", "Grid event erase failed");
        Key(KeyCode.DownArrow); Check((int)index.GetValue(work) == 27, "Grid vertical navigation");
        Key(KeyCode.Tab, '\0', true); Check((int)index.GetValue(work) == 26, "Reverse tab");
        Call(work, "EndWriting");
        work.SelectTool(CipherWorkbench.Tool.RailFence); Ungate(gate); Call(work, "Update"); Settle(view);
        index.SetValue(work, 81); Call(work, "BeginTyping"); Key(KeyCode.Q, 'q');
        Check(work.Rails[81] == "Q" && (int)index.GetValue(work) == 81, "Last rail cell overflow");
        Key(KeyCode.Backspace); Check(work.Rails[81] == "" && (int)index.GetValue(work) == 80, "Rail backspace");
        Call(work, "EndWriting"); Ungate(gate);
        Check(!gate.WorkbenchTyping, "Finish typing leaves lock");
    }
    static void Finish(bool success, string message)
    {
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Output + "/validation.txt", message);
        if (success) Debug.Log("CIPHER_WORKBENCH_VALIDATION_PASS"); else Debug.LogError(message);
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
