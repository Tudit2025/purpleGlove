using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PurpleGlove;
using PurpleGlove.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run in the isolated Temp/guild-letter-layout project.
[InitializeOnLoad]
public static class AffineParametersValidation
{
    const string Key = "AffineParametersValidation.Play";
    const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    static readonly string Output = Path.GetFullPath("../../output/affine-parameters");
    static double started;
    static bool finished;
    static int checks;
    static AffineParametersValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    static object Call(object obj, string method, params object[] args) => obj.GetType()
        .GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(obj, args);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    static void Ungate(CipherWorkbench work) => typeof(SceneInputGate)
        .GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(work.GetComponent<SceneInputGate>(), -1);
    static Event Press(KeyCode code, char c = '\0', bool ctrl = false) => new Event { type = EventType.KeyDown, keyCode = code, character = c, control = ctrl };
    static void Input(CipherWorkbench work, Event e) => Call(work, "HandleWritingInput", e);
    static void Select(CipherWorkbench work, CipherWorkbench.Tool tool)
    {
        Check(work.SelectTool(tool), "Cannot select " + tool);
        Call(work, "AnimateToolTransfer", 1f); Call(work, "AnimateToolTransfer", 1f);
        Ungate(work); Call(work, "Update"); Ungate(work);
        Call(work.GetComponent<DeskScenePreview>(), "AnimateView", 10f, Vector2.zero);
        Physics.SyncTransforms();
    }
    static void Click(CipherWorkbench work, int index)
    {
        Ungate(work);
        var target = work.tools[3].GetComponentsInChildren<CipherWorkbenchTarget>()
            .Single(t => t.kind == CipherWorkbenchTarget.Kind.AffineParameter && t.index == index);
        var box = target.GetComponent<BoxCollider>();
        Vector2 point = Camera.main.WorldToScreenPoint(target.transform.TransformPoint(box.center));
        Check((CipherWorkbenchTarget)Call(work, "PickTarget", point, false) == target, "Parameter hit area is obscured.");
        Call(work, "ActivateTarget", target, point);
    }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        try
        {
            Check(Path.GetFullPath(".").Replace('\\', '/').EndsWith("/Temp/guild-letter-layout"), "Use the isolated project.");
            DeskSceneBuilder.InstallAffineParameters(); DeskSceneBuilder.InstallAffineParameters();
            var work = Camera.main.GetComponent<CipherWorkbench>();
            Check(work.affineAInk && work.affineBInk, "Missing A/B fields.");
            Check(work.tools[3].GetComponentsInChildren<CipherWorkbenchTarget>(true).Count(t => t.kind == CipherWorkbenchTarget.Kind.AffineParameter) == 2, "Repeated install duplicates fields.");
            Check(!work.tools[3].GetComponentsInChildren<CipherWorkbenchTarget>(true).Any(t => t.kind == CipherWorkbenchTarget.Kind.Cell), "Affine grid is still editable.");
            Check(work.tools[4].GetComponentsInChildren<CipherWorkbenchTarget>(true).Count(t => t.kind == CipherWorkbenchTarget.Kind.Cell) == 82, "Rail grid was changed.");
            foreach (var component in Camera.main.GetComponents<MonoBehaviour>())
                if (!(component is CipherWorkbench) && !(component is SceneInputGate) && !(component is DeskScenePreview)
                    && !(component is SceneObjectInteractor) && !(component is DeskViewHud) && !(component is PixelSceneView)) component.enabled = false;
            foreach (var memory in UnityEngine.Object.FindObjectsOfType<FirstInteractionMemory>(true)) memory.enabled = false;
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
    { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) Finish(false, message + "\n" + stack); }
    static void Tick()
    {
        if (finished) return;
        if (EditorApplication.timeSinceStartup - started > 90) { Finish(false, "Timed out."); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.frameCount < 10) return;
        try
        {
            checks = SessionState.GetInt(Key + ".Checks", 0);
            var work = Camera.main.GetComponent<CipherWorkbench>();
            var gate = work.GetComponent<SceneInputGate>();
            gate.SetStory(false); gate.SetDialogue(false); gate.SetConsole(false); gate.SetDelivery(false); gate.SetPropAction(false);
            work.GetComponent<DeskScenePreview>().followMouse = false;
            Select(work, CipherWorkbench.Tool.Affine);
            Check(work.AffineA == "" && work.AffineB == "" && !work.AffineParametersValid, "Parameters should start blank.");
            Check(work.KeyboardDecodeReady && work.KeyboardMapping == Alphabet, "Blank parameters must preserve direct plaintext input.");
            Check(string.Concat(work.Affine.Take(26)) == Alphabet && work.Affine.Skip(26).All(string.IsNullOrEmpty), "Initial table contents.");
            Capture("empty");
            Click(work, 0); Input(work, Press(KeyCode.Alpha5, '5'));
            Check(work.AffineA == "5" && !work.KeyboardDecodeReady, "Partial parameters must not decode.");
            Check(!(bool)Call(work, "HandleDecodeKey", Press(KeyCode.I, 'i')), "Parameter typing leaked into plaintext.");
            Input(work, Press(KeyCode.Return)); Input(work, Press(KeyCode.Keypad8, '8')); Input(work, Press(KeyCode.KeypadEnter)); Ungate(work);
            Check(work.AffineParametersValid && work.KeyboardDecodeReady, "A=5 B=8 did not configure decoding.");
            Check(string.Concat(work.Affine.Skip(26)) == "INSXCHMRWBGLQVAFKPUZEJOTYD", "Wrong generated cipher row.");
            Check(work.affineLetters.Select(t => t.text).SequenceEqual(work.Affine), "Displayed table differs from decoding table.");
            work.SetPlaintext("");
            foreach (char c in "VAPZRMIZCUIWVZQIPZWV")
                Check((bool)Call(work, "HandleDecodeKey", Press((KeyCode)char.ToLowerInvariant(c), char.ToLowerInvariant(c))), "Configured input ignored.");
            Check(work.Plaintext == "NORTHGATESAINTMARTIN", "Story cipher did not decode.");
            Call(work, "AnimateLetterStamps", 1f); Call(work, "AnimateKeyboardTray", 10f);
            Capture("configured");
            string cells = string.Concat(work.Affine);
            work.SetCell(CipherWorkbench.Tool.Affine, 0, "Z"); work.SetCell(CipherWorkbench.Tool.Affine, 26, "Z");
            Check(string.Concat(work.Affine) == cells, "Manual cell writes changed derived mapping.");
            var stale = new GameObject("Legacy grid hit").AddComponent<CipherWorkbenchTarget>(); stale.kind = CipherWorkbenchTarget.Kind.Cell;
            Call(work, "ActivateTarget", stale, Vector2.zero); UnityEngine.Object.Destroy(stale.gameObject); Ungate(work);
            Check(!gate.WorkbenchTyping, "Legacy grid hit enters writing mode.");
            string draft = work.Plaintext;
            Click(work, 1); Input(work, Press(KeyCode.Alpha0, '0')); Input(work, Press(KeyCode.Return)); Ungate(work);
            Check(work.AffineB == "0" && work.Affine[26] == "A" && work.Plaintext == draft, "Changing B must replace the old value and refresh mapping only.");
            Click(work, 0); Input(work, Press(KeyCode.Alpha1, '1')); Input(work, Press(KeyCode.Alpha3, '3')); Input(work, Press(KeyCode.Escape)); Ungate(work);
            Check(work.AffineA == "13" && !work.AffineParametersValid && !work.KeyboardDecodeReady && work.Affine.Skip(26).All(string.IsNullOrEmpty), "Noninvertible A left stale mapping.");
            foreach (string invalid in new[] { "0", "2", "26", "99" })
            { work.SetAffineParameter(0, invalid); Check(!work.AffineParametersValid && !work.KeyboardDecodeReady, "Accepted invalid A=" + invalid); }
            work.SetAffineParameter(0, "5"); work.SetAffineParameter(1, "26");
            Check(!work.AffineParametersValid, "Accepted invalid B.");
            work.SetAffineParameter(1, "8");
            Click(work, 0); Input(work, Press(KeyCode.A, 'a')); Check(work.AffineA == "5", "Letter entered numeric field.");
            string clipboard = GUIUtility.systemCopyBuffer;
            try
            {
                GUIUtility.systemCopyBuffer = "25"; Input(work, Press(KeyCode.V, '\0', true));
                Check(work.AffineA == "25", "Two-digit paste failed.");
                GUIUtility.systemCopyBuffer = "-1"; Input(work, Press(KeyCode.A, '\0', true)); Input(work, Press(KeyCode.V, '\0', true));
                Check(work.AffineA == "25", "Invalid paste changed the parameter.");
            }
            finally { GUIUtility.systemCopyBuffer = clipboard; }
            Input(work, Press(KeyCode.Tab)); Input(work, Press(KeyCode.Alpha0, '0')); Input(work, Press(KeyCode.Return)); Ungate(work);
            foreach (int a in new[] { 1, 3, 5, 11, 15, 25 })
            foreach (int b in new[] { 0, 8, 25 })
            {
                work.SetAffineParameter(0, a.ToString()); work.SetAffineParameter(1, b.ToString());
                Check(work.AffineParametersValid, "Valid parameters rejected.");
                for (int x = 0; x < 26; x++)
                    Check(work.TryDecodeLetter((char)('A' + (a * x + b) % 26), out char p) && p == 'A' + x, "Affine inverse is wrong.");
            }
            work.SetAffineParameter(0, "5"); work.SetAffineParameter(1, "8");
            Select(work, CipherWorkbench.Tool.RailFence); work.SetCell(CipherWorkbench.Tool.RailFence, 0, "H");
            Check(work.Rails[0] == "H", "Rail grid no longer accepts manual writes.");
            Select(work, CipherWorkbench.Tool.Affine);
            Check(work.AffineA == "5" && work.AffineB == "8" && work.Plaintext == draft, "Tool switch lost parameters or draft.");
            Click(work, 0); Input(work, Press(KeyCode.Delete)); Input(work, Press(KeyCode.Tab)); Input(work, Press(KeyCode.Backspace)); Input(work, Press(KeyCode.Return)); Ungate(work);
            Check(work.AffineA == "" && work.AffineB == "" && work.KeyboardDecodeReady && work.KeyboardMapping == Alphabet, "Clearing both parameters did not reset mapping.");
            Click(work, 0); work.SelectTool(CipherWorkbench.Tool.RailFence);
            Check(!gate.WorkbenchTyping, "Tool switch did not release parameter input.");
            Finish(true, "PASS: " + checks + " checks in Unity Play Mode.\n"
                + "Actual parameter collider picking, keyboard/keypad input, Tab/Enter/Escape, overwrite, paste and deletion.\n"
                + "Automatic forward/inverse tables, story cipher, invalid/partial/reset states, read-only grid, tool switching and rail editing.\n"
                + "Synthetic pointer/key events; screenshots use the actual scene camera.\n");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Finish(bool ok, string message)
    {
        if (finished) return; finished = true;
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Output + "/validation.txt", message); if (!ok) Debug.LogError(message);
        EditorApplication.Exit(ok ? 0 : 1);
    }
    static void Capture(string name)
    {
        var camera = Camera.main;
        var texture = RenderTexture.GetTemporary(1600, 900, 24);
        var previous = camera.targetTexture; var active = RenderTexture.active;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally { camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(texture); UnityEngine.Object.DestroyImmediate(image); }
    }
}
