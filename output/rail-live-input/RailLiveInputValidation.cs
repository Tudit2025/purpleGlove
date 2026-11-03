using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PurpleGlove;
using PurpleGlove.Editor;
using UnityEditor;
using UnityEngine;

// Run in the isolated Temp/guild-letter-layout project.
[InitializeOnLoad]
public static class RailLiveInputValidation
{
    const string Key = "RailLiveInputValidation.Play";
    static readonly string Output = Path.GetFullPath("../../output/rail-live-input");
    static double started;
    static bool finished;
    static int checks;
    static RailLiveInputValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    static object Call(object obj, string method, params object[] args) => obj.GetType()
        .GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(obj, args);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    static void Ungate(CipherWorkbench work) => typeof(SceneInputGate)
        .GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(work.GetComponent<SceneInputGate>(), -1);
    static Event Press(KeyCode code, char c = '\0', bool ctrl = false) => new Event { type = EventType.KeyDown, keyCode = code, character = c, control = ctrl };
    static void Input(CipherWorkbench work, Event e) => Call(work, "HandleWritingInput", e);
    static string Row(CipherWorkbench work, int row) => string.Concat(work.Rails.Skip(row * 41).Take(41));
    static string Read(CipherWorkbench work) => string.Concat(Enumerable.Range(0, 41).Select(i => work.Rails[i] + work.Rails[41 + i]));
    static void Select(CipherWorkbench work, CipherWorkbench.Tool tool)
    {
        Check(work.SelectTool(tool), "Cannot select " + tool);
        Call(work, "AnimateToolTransfer", 1f); Call(work, "AnimateToolTransfer", 1f);
        Ungate(work); Call(work, "Update"); Ungate(work);
        Call(work.GetComponent<DeskScenePreview>(), "AnimateView", 10f, Vector2.zero);
        Physics.SyncTransforms();
    }
    static void Click(CipherWorkbench work, string name)
    {
        Ungate(work);
        var target = work.tools[4].GetComponentsInChildren<CipherWorkbenchTarget>().Single(t => t.name == name);
        var box = target.GetComponent<BoxCollider>();
        Vector2 point = Camera.main.WorldToScreenPoint(target.transform.TransformPoint(box.center));
        Check((CipherWorkbenchTarget)Call(work, "PickTarget", point, false) == target, "Rail input hit area is obscured.");
        Call(work, "ActivateTarget", target, point);
    }
    static void Type(CipherWorkbench work, string text)
    {
        foreach (char c in text) Input(work, Press((KeyCode)char.ToLowerInvariant(c), c));
    }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        try
        {
            Check(Path.GetFullPath(".").Replace('\\', '/').EndsWith("/Temp/guild-letter-layout"), "Use the isolated project.");
            DeskSceneBuilder.InstallRailCipherInput(); DeskSceneBuilder.InstallRailCipherInput();
            var work = Camera.main.GetComponent<CipherWorkbench>();
            Check(work.railInputInk && work.railLetters.Length == 82, "Missing rail input or cells.");
            Check(work.tools[4].GetComponentsInChildren<CipherWorkbenchTarget>(true).Count(t => t.kind == CipherWorkbenchTarget.Kind.RailCipher) == 2, "Duplicate rail input targets.");
            Check(!work.tools[4].GetComponentsInChildren<CipherWorkbenchTarget>(true).Any(t => t.kind == CipherWorkbenchTarget.Kind.Cell), "Old per-cell inputs remain.");
            Check(work.affineAInk && work.affineBInk, "Affine parameter fields were lost.");
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
            Select(work, CipherWorkbench.Tool.RailFence);
            Check(gate.WorkbenchTyping, "Taking the rail tool should start ciphertext input.");
            work.SetPlaintext("DRAFT");
            Type(work, "h"); Check(Row(work, 0) == "H" && Row(work, 1) == "", "First letter did not appear immediately.");
            Type(work, "l"); Check(Row(work, 0) == "H" && Row(work, 1) == "L", "Second letter did not rebalance rows.");
            Type(work, "o"); Check(Row(work, 0) == "HL" && Row(work, 1) == "O", "Odd-length input should leave one extra letter above.");
            Type(work, "olelwrd");
            Check(Row(work, 0) == "HLOOL" && Row(work, 1) == "ELWRD" && Read(work) == "HELLOWORLD", "Two-rail example is incorrect.");
            Check(work.Plaintext == "DRAFT", "Cipher input changed the plaintext draft.");
            Input(work, Press(KeyCode.Backspace));
            Check(work.RailCiphertext == "HLOOLELWR" && Row(work, 0) == "HLOOL" && Row(work, 1) == "ELWR", "Backspace did not rearrange immediately.");
            Input(work, Press(KeyCode.A, '\0', true)); Type(work, "hloel");
            Check(Read(work) == "HELLO" && Row(work, 0) == "HLO" && Row(work, 1) == "EL", "Odd-length replacement failed.");
            Input(work, Press(KeyCode.Delete));
            Check(work.RailCiphertext == "" && work.Rails.All(string.IsNullOrEmpty), "Clearing left old letters.");
            Type(work, "123 !"); Check(work.RailCiphertext == "", "Nonletters entered the rail grid.");
            string clipboard = GUIUtility.systemCopyBuffer;
            try
            {
                GUIUtility.systemCopyBuffer = "hlool\n elwrd";
                Input(work, Press(KeyCode.V, '\0', true));
                Check(work.RailCiphertext == "HLOOLELWRD" && Read(work) == "HELLOWORLD", "Paste failed to normalize and arrange.");
            }
            finally { GUIUtility.systemCopyBuffer = clipboard; }
            Input(work, Press(KeyCode.Delete));
            var entry = Camera.main.GetComponent<PlaintextArchiveController>().entries.Single(e => e.id == "rail");
            string cipher = CipherWorkbench.LettersOnly(entry.sourceDocument.GetComponentsInChildren<TextMesh>(true).Single(t => t.name == "Rail cipher").text, 82);
            Check(cipher.Length == 82, "Expected the current full story ciphertext.");
            foreach (char c in cipher)
            {
                Type(work, c.ToString());
                Check(Row(work, 0) + Row(work, 1) == work.RailCiphertext, "A keystroke lost or reordered a ciphertext letter.");
                Check(work.railLetters.Select(t => t.text).SequenceEqual(work.Rails), "Visible cells are not updated on each keystroke.");
            }
            Check(Row(work, 0) == cipher.Substring(0, 41) && Row(work, 1) == cipher.Substring(41), "Full story split is incorrect.");
            Check(Read(work) == PlaintextArchiveController.Normalize(entry.answer), "Displayed columns do not read as the story plaintext.");
            Check(work.Plaintext == "DRAFT" && !entry.saved, "Arranging ciphertext must not save the answer.");
            Capture("ciphertext-entered");
            Type(work, "X"); Check(work.RailCiphertext == cipher, "Input exceeded the physical 82-cell capacity.");
            Input(work, Press(KeyCode.KeypadEnter)); Ungate(work);
            Check(!gate.WorkbenchTyping && work.UsesDecodeKeyboard, "Enter did not return to plaintext transcription.");
            work.SetPlaintext("");
            foreach (char c in "HELLO") Check((bool)Call(work, "HandleDecodeKey", Press((KeyCode)char.ToLowerInvariant(c), c)), "Plaintext key ignored.");
            Check(work.Plaintext == "HELLO" && work.RailCiphertext == cipher, "Transcribing changed the arranged ciphertext.");
            Call(work, "AnimateLetterStamps", 1f);
            Click(work, "Type ciphertext on grid"); Input(work, Press(KeyCode.Backspace));
            Check(work.RailCiphertext.Length == 81 && Row(work, 0).Length == 41 && Row(work, 1).Length == 40, "Grid click did not resume live input.");
            Input(work, Press(KeyCode.Escape)); Ungate(work);
            Click(work, "Type ciphertext"); Type(work, cipher[81].ToString());
            Select(work, CipherWorkbench.Tool.Affine);
            Check(!gate.WorkbenchTyping && work.RailCiphertext == cipher, "Switching tools lost ciphertext or focus.");
            work.SetAffineParameter(0, "5"); work.SetAffineParameter(1, "8");
            Check(work.TryDecodeLetter('I', out char a) && a == 'A', "Affine decoding regressed.");
            Select(work, CipherWorkbench.Tool.RailFence);
            Check(gate.WorkbenchTyping && work.RailCiphertext == cipher && work.Plaintext == "HELLO", "Retaking rail tool lost notes or did not resume input.");
            gate.SetDialogue(true); Call(work, "Update");
            Check(!gate.WorkbenchTyping && work.RailCiphertext == cipher, "Dialogue did not release writing focus safely.");
            gate.SetDialogue(false); Ungate(work); Click(work, "Type ciphertext");
            Input(work, Press(KeyCode.A, '\0', true)); Input(work, Press(KeyCode.Backspace));
            Check(work.RailCiphertext == "" && work.Rails.All(string.IsNullOrEmpty), "Select-all deletion did not clear both rows.");
            Finish(true, "PASS: " + checks + " checks in Unity Play Mode.\n"
                + "Each typed/deleted letter immediately reflows both rails; even/odd examples and full story cipher verified.\n"
                + "Auto input on takeout, real collider picking, normalization/paste, capacity, manual plaintext, tool switching and dialogue interruption.\n"
                + "Affine decoding preserved. Synthetic pointer/key events; screenshots use the actual scene camera.\n");
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
