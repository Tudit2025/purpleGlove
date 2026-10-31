using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PurpleGlove;
using PurpleGlove.Editor;

[InitializeOnLoad]
public static class PlaintextKeyboardRegression
{
    const string Key = "PlaintextKeyboardRegression.Play";
    const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    static readonly string Output = Path.GetFullPath("../../output/plaintext-archive-validation/keyboard-regression");
    static double started;
    static bool running;
    static int checks;
    static PlaintextKeyboardRegression() { if (SessionState.GetBool(Key, false)) Install(); }
    static object Call(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(target);
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static void Ungate(SceneInputGate gate) => typeof(SceneInputGate).GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gate, -1);
    static Event Press(char c) => new Event { type = EventType.KeyDown, character = c,
        keyCode = c == ' ' ? KeyCode.Space : c == '\n' ? KeyCode.Return : (KeyCode)char.ToLowerInvariant(c) };
    static bool KeyPress(CipherWorkbench work, Event e) => (bool)Call(work, "HandleDecodeKey", e);
    static void Type(CipherWorkbench work, string text) { foreach (char c in text) Check(KeyPress(work, Press(c)), "Ignored configured key " + c); }
    static void Settle(CipherWorkbench work)
    {
        Call(work, "AnimateToolTransfer", 1f); Call(work, "AnimateToolTransfer", 1f);
        Ungate(work.GetComponent<SceneInputGate>()); Call(work, "Update"); Ungate(work.GetComponent<SceneInputGate>());
        Call(work.GetComponent<DeskScenePreview>(), "AnimateView", 10f, Vector2.zero);
        Call(work, "AnimateKeyboardTray", 10f); Physics.SyncTransforms();
    }
    static void Select(CipherWorkbench work, CipherWorkbench.Tool tool) { Check(work.SelectTool(tool), "Cannot select " + tool); Settle(work); }
    static void VerifyTray(CipherWorkbench work, Vector3[] originalPositions)
    {
        Call(work, "AnimateKeyboardTray", 10f); Physics.SyncTransforms();
        Check(work.KeyboardMapping.Distinct().Count() == 26, "Mapping is not a permutation");
        for (int key = 0; key < 26; key++)
        {
            var sample = work.letterSamples[work.KeyboardMapping[key] - 'A'];
            Check((sample.transform.parent.localPosition - originalPositions[key]).sqrMagnitude < 0.000001f, "Stack not at its ciphertext key position");
            Check(sample.Letter == work.KeyboardMapping[key], "Tile identity changed");
            Check(sample.transform.parent.GetComponentsInChildren<CipherLetterTile>().All(t => t.Letter == sample.Letter), "Three-tile stack split");
            Check(sample.GetComponentInChildren<TextMesh>().text == sample.Letter.ToString(), "Tile glyph differs from identity");
        }
    }
    static CipherWorkbenchTarget Part(Transform root, CipherWorkbenchTarget.Kind kind) => root.GetComponentsInChildren<CipherWorkbenchTarget>().First(t => t.kind == kind);
    static Vector2 Point(Camera camera, CipherWorkbenchTarget target) => camera.WorldToScreenPoint(target.transform.TransformPoint(target.GetComponent<BoxCollider>().center));
    public static void Run()
    {
        Directory.CreateDirectory(Output); EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
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
        if (running) return;
        if (EditorApplication.timeSinceStartup - started > 90) { Finish(false, "Play mode timeout"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || !Camera.main || Time.frameCount < 12) return;
        running = true;
        try
        {
            var camera = Camera.main; camera.aspect = 16f / 9;
            var work = camera.GetComponent<CipherWorkbench>(); var view = camera.GetComponent<DeskScenePreview>();
            var gate = camera.GetComponent<SceneInputGate>(); view.followMouse = false; Ungate(gate);
            // This regression exercises decoder input directly; object introductions have their own suite.
            foreach (var memory in UnityEngine.Object.FindObjectsOfType<FirstInteractionMemory>(true)) memory.enabled = false;
            var original = work.letterSamples.Select(t => t.transform.parent.localPosition).ToArray();
            Check(string.Concat(work.Affine.Take(26)) == Alphabet && work.Affine.Skip(26).All(string.IsNullOrEmpty), "Affine defaults must contain only the plaintext alphabet");
            Check(string.Concat(work.affineLetters.Take(26).Select(t => t.text)) == Alphabet, "Affine default ink missing");
            Check(string.Concat(Enumerable.Range(0, 26).Select(i => work.storedTools[3].transform.Find("Cell 0 " + i).GetComponent<TextMesh>().text)) == Alphabet, "Stored affine preview missing A-Z");
            Check(work.Rails.All(string.IsNullOrEmpty), "Affine defaults changed rail paper");
            Check(!KeyPress(work, Press('a')) && work.Plaintext == "", "Desktop keyboard writes text");
            Select(work, CipherWorkbench.Tool.Caesar);
            Check(!work.KeyboardDecodeReady && !KeyPress(work, Press('a')), "Default unconfigured Caesar accepts input");
            var paper = Part(work.mat, CipherWorkbenchTarget.Kind.Plaintext);
            Vector3 quillRest = work.writingQuill.position;
            Call(work, "ActivateTarget", paper, Point(camera, paper)); Ungate(gate);
            Call(work, "HandleWritingInput", Press('x')); Call(work, "UpdateQuill");
            Check(!gate.WorkbenchTyping && work.Plaintext == "" && work.writingQuill.position == quillRest, "Paper click starts quill writing");
            Check(!KeyPress(work, Press('x')), "Paper click bypasses unconfigured decoder");
            work.SetCaesarShift(3); VerifyTray(work, original);
            Check(work.KeyboardMapping == "XYZABCDEFGHIJKLMNOPQRSTUVW", "Caesar mapping direction wrong");
            Type(work, "KHOOR ZRUOG");
            Check(work.Plaintext == "HELLO WORLD", "Caesar known vector failed");
            Type(work, "DDD"); Check(work.Plaintext.EndsWith("AAA"), "Rapid repeated key dropped");
            var stamps = (IList)Field(work, "letterStamps");
            Check(stamps.Count == 13, "Each letter should produce one flight, spaces none");
            foreach (var stamp in stamps)
            {
                var visual = (GameObject)Field(stamp, "visual");
                Check(visual.GetComponentsInChildren<Collider>().All(c => !c.enabled), "Flying tile steals pointer input");
                Check(!visual.GetComponent<CipherLetterTile>().enabled, "Flying tile runs physical return controller");
            }
            Check(work.plaintextInk.text == "", "Ink appeared before the tiles reached paper");
            Call(work, "AnimateLetterStamps", 0.075f);
            Check(work.plaintextInk.text == "", "Mid-flight tile stamped early");
            Call(work, "AnimateLetterStamps", 0.08f);
            Check(work.plaintextInk.text == "HELLO WORLDAAA", "Landing did not stamp complete text");
            foreach (var stamp in stamps)
                Check(Vector3.Distance(((GameObject)Field(stamp, "visual")).transform.position, (Vector3)Field(stamp, "destination")) < 0.00001f, "Stamp did not touch its paper position");
            Call(work, "AnimateLetterStamps", 1f); Check(stamps.Count == 0, "Letter flight not cleaned up");
            Check(work.letterSamples.All(t => !t.IsReturning && Vector3.Distance(t.transform.position, t.homeSocket.position) < 0.00001f), "Keyboard consumed physical drag samples");
            work.SetPlaintext("");
            foreach (char c in "KHOOR") { Type(work, c.ToString()); Call(work, "AnimateLetterStamps", 0.065f); }
            Check(work.Plaintext == "HELLO" && work.plaintextInk.text == "HEL", "Staggered typing stamped out of order");
            Call(view, "AdjustFocusZoom", 10f); Call(view, "AnimateView", 10f, Vector2.zero);
            Capture(camera, "caesar-flying-letters");
            Call(view, "AdjustFocusZoom", -10f); Call(view, "AnimateView", 10f, Vector2.zero);
            Call(work, "AnimateLetterStamps", 1f); Check(work.plaintextInk.text == "HELLO", "Final queued letters lost");
            work.SetPlaintext(""); Type(work, new string('D', 40));
            Check(stamps.Count == 32 && work.Plaintext.Length == 40, "Rapid input cap lost text or leaked flights");
            // Enlarged letters wrap before column 40; both rendered rows must retain every stamp.
            Call(work, "AnimateLetterStamps", 1f); Check(work.plaintextInk.text.Replace("\n", "") == new string('A', 40), "Capped flights lost ink");
            work.SetPlaintext(""); Type(work, "SDVVZRUG"); Check(work.Plaintext == "PASSWORD", "WASD/P letters intercepted");
            Check(work.UsesDecodeKeyboard, "Configured keyboard does not own P shortcut");
            work.SetCaesarShift(0); Check(work.KeyboardDecodeReady && work.KeyboardMapping == Alphabet, "Returning dial to zero should keep it configured");

            Select(work, CipherWorkbench.Tool.SaintCyr);
            Check(!work.KeyboardDecodeReady, "Empty keyword accepted");
            var keyword = Part(work.tools[2].transform, CipherWorkbenchTarget.Kind.Keyword);
            Call(work, "ActivateTarget", keyword, Point(camera, keyword)); Ungate(gate);
            Call(work, "UpdateQuill");
            Check(gate.WorkbenchTyping && Vector3.Distance(work.writingQuill.position, quillRest) > 0.01f, "Keyword field no longer uses quill");
            foreach (char c in "LEMON") Call(work, "HandleWritingInput", Press(c));
            Check(work.Keyword == "LEMON" && !KeyPress(work, Press('a')), "Keyword editor was intercepted by decoding");
            Call(work, "HandleWritingInput", new Event { type = EventType.KeyDown, keyCode = KeyCode.Return }); Ungate(gate);
            work.SetPlaintext(""); VerifyTray(work, original);
            Type(work, "LXFOPV EF RNHR");
            Check(work.Plaintext == "ATTACK AT DAWN" && work.KeywordIndex == 2 && work.SlideShift == 12, "Repeating-key Vigenere vector failed");
            Check(KeyPress(work, new Event { type = EventType.KeyDown, keyCode = KeyCode.Backspace }), "Backspace ignored");
            Check(work.Plaintext == "ATTACK AT DAW" && work.KeywordIndex == 1, "Backspace did not rewind keyword");
            Type(work, "R \n"); Check(work.KeywordIndex == 2, "Whitespace advances keyword");
            KeyPress(work, new Event { type = EventType.KeyDown, keyCode = KeyCode.Backspace });
            KeyPress(work, new Event { type = EventType.KeyDown, keyCode = KeyCode.Backspace });
            Check(work.KeywordIndex == 2 && work.Plaintext == "ATTACK AT DAWN", "Deleting whitespace changed key position");
            VerifyTray(work, original); Capture(camera, "saint-cyr-keyboard");
            work.SetPlaintext(new string('A', 600)); int before = work.KeywordIndex;
            Type(work, "A"); Check(work.Plaintext.Length == 600 && work.KeywordIndex == before, "Full paper advanced key/lost text");
            work.SetPlaintext(""); work.SetKeyword("A"); Type(work, "abc");
            Check(work.Plaintext == "ABC" && work.KeywordIndex == 0, "Identity/single-letter keyword failed");
            work.SetKeyword(""); VerifyTray(work, original); Check(!work.KeyboardDecodeReady && work.KeyboardMapping == Alphabet, "Removing key did not reset tray");

            Select(work, CipherWorkbench.Tool.Affine); work.SetPlaintext("");
            Check(!work.KeyboardDecodeReady, "Empty affine table accepted");
            // Fill just the ciphertext row against the preset A-Z plaintext row.
            for (int i = 0; i < 26; i++) work.SetCell(CipherWorkbench.Tool.Affine, 26 + i, Alphabet[(5 * i + 8) % 26].ToString());
            Type(work, "IHHWVCSWFRCP");
            Check(work.Plaintext == "AFFINECIPHER", "Preset alphabet does not decode with a completed ciphertext row");
            work.SetPlaintext(""); work.SetCell(CipherWorkbench.Tool.Affine, 51, "");
            for (int i = 0; i < 26; i++)
            {
                int plain = (i * 7 + 5) % 26; // Deliberately reorder columns: use pairs, not their indices.
                work.SetCell(CipherWorkbench.Tool.Affine, i, Alphabet[plain].ToString());
                if (i < 25) work.SetCell(CipherWorkbench.Tool.Affine, 26 + i, Alphabet[(5 * plain + 8) % 26].ToString());
            }
            Check(!work.KeyboardDecodeReady && !KeyPress(work, Press('a')), "Partial affine table accepted");
            work.SetCell(CipherWorkbench.Tool.Affine, 51, Alphabet[(5 * ((25 * 7 + 5) % 26) + 8) % 26].ToString());
            VerifyTray(work, original); Type(work, "IHHWVCSWFRCP");
            Check(work.Plaintext == "AFFINECIPHER", "Affine inverse/pair mapping failed");
            Call(work, "AnimateLetterStamps", 1f);
            Capture(camera, "affine-keyboard");
            string cipherCell = work.Affine[51], plainCell = work.Affine[25];
            work.SetCell(CipherWorkbench.Tool.Affine, 51, work.Affine[26]);
            Check(!work.KeyboardDecodeReady && !KeyPress(work, Press('a')), "Duplicate ciphertext row accepted");
            work.SetCell(CipherWorkbench.Tool.Affine, 51, cipherCell);
            work.SetCell(CipherWorkbench.Tool.Affine, 25, work.Affine[0]);
            Check(!work.KeyboardDecodeReady, "Duplicate plaintext row accepted");
            work.SetCell(CipherWorkbench.Tool.Affine, 25, plainCell);
            Check(work.KeyboardDecodeReady, "Corrected table not accepted");

            // Plaintext clicks must not enter quill writing or intercept the configured decoder.
            Call(work, "ActivateTarget", paper, Point(camera, paper)); Ungate(gate); work.SetPlaintext("");
            Call(work, "UpdateQuill");
            Check(!gate.WorkbenchTyping && work.writingQuill.position == quillRest, "Paper click still owns quill input");
            Call(work, "HandleWritingInput", Press('x'));
            Check(work.Plaintext == "", "Plaintext still accepts raw quill writing");
            Type(work, "x"); Check(work.Plaintext == work.KeyboardMapping['X' - 'A'].ToString(), "Paper click disabled keyboard decoding");
            Call(work, "EndWriting"); Ungate(gate);
            Check(!KeyPress(work, new Event { type = EventType.KeyDown, keyCode = KeyCode.V, control = true }), "Ctrl shortcut decoded");
            gate.ConsumeFrame(); Check(!KeyPress(work, Press('x')), "Closing frame leaked text"); Ungate(gate);
            gate.SetConsole(true); Check(!KeyPress(work, Press('x')), "Console leaked keyboard input");
            Call(work, "Update"); gate.SetConsole(false); Ungate(gate);
            view.SetDialogueLock(true); Check(!KeyPress(work, Press('x')), "Dialogue leaked keyboard input"); view.SetDialogueLock(false);
            view.BeginDeliveryView(); Check(!KeyPress(work, Press('x')), "Delivery leaked keyboard input"); view.EndDeliveryView(false);
            work.OpenToolbox(); Check(!KeyPress(work, Press('x')), "Open toolbox leaked keyboard input"); work.CloseToolbox(); Ungate(gate);
            Select(work, CipherWorkbench.Tool.Caesar); work.SetCaesarShift(3); VerifyTray(work, original); work.SetPlaintext("");
            var tile = work.letterSamples[0]; var hit = tile.GetComponent<CipherWorkbenchTarget>();
            Check((CipherWorkbenchTarget)Call(work, "PickTarget", Point(camera, hit), false) == hit, "Remapped tile no longer pickable");
            Call(work, "ActivateTarget", hit, Point(camera, hit)); Ungate(gate);
            Check(!KeyPress(work, Press('x')), "Dragging leaked keyboard input");
            Call(work, "EndDrag", Point(camera, paper)); Ungate(gate);
            Check(work.Plaintext == "A", "Physical tile letter was decoded twice"); tile.ReturnHome(true);
            Vector3 loose = work.mat.TransformPoint(new Vector3(0, 0.013f, -0.32f)); tile.transform.position = loose;
            work.SetCaesarShift(7); Call(work, "AnimateKeyboardTray", 10f);
            Check(Vector3.Distance(tile.transform.position, loose) < 0.00001f, "Rearranging tray moved a manually composed tile"); tile.ReturnHome(true);
            view.SetView(DeskScenePreview.View.Desktop); Check(!KeyPress(work, Press('x')), "Left workspace still accepts typing");
            Call(work, "UpdateDecodeKeyboard"); Check(((IList)Field(work, "letterStamps")).Count == 0, "Leaving workspace left flying tiles");
            Select(work, CipherWorkbench.Tool.Pigpen); Check(!work.KeyboardDecodeReady && work.KeyboardMapping == Alphabet, "Pigpen invented a letter mapping");
            Select(work, CipherWorkbench.Tool.RailFence); Check(!work.KeyboardDecodeReady, "Rail fence invented a substitution mapping");
            Select(work, CipherWorkbench.Tool.None); VerifyTray(work, original);
            Finish(true, "PASS: " + checks + " assertions in Unity 2022.3.62f2 Play Mode.\n"
                + "Caesar HELLO WORLD/PASSWORD, repeating-key LEMON/ATTACK AT DAWN, reordered affine columns/AFFINECIPHER.\n"
                + "26 stable letter identities and all three tiles per stack; configured readiness, reset, actual remapped collider picking.\n"
                + "Continuous/staggered letter flights, stamping only on landing, cleanup, repeated keys, 32-flight cap, keyword undo, whitespace, 600-character limit.\n"
                + "Preset editable A-Z affine plaintext row; ciphertext-row-only setup; quill keyword input; plaintext clicks cannot enter raw writing.\n"
                + "Physical tile drops, console/dialogue/delivery/toolbox/drag and close-frame gates.\n"
                + "Synthetic key/pointer replay; OS keyboard and IME not manually tested.\n");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Finish(bool success, string message)
    {
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Output + "/validation.txt", message);
        if (success) Debug.Log("KEYBOARD_DECODE_VALIDATION_PASS"); else Debug.LogError(message);
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
