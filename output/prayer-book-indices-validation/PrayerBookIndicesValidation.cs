using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using PurpleGlove;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Deterministic checks and captures in the isolated Unity project.
[InitializeOnLoad]
public static class PrayerBookIndicesValidation
{
    const string Key = "PrayerBookIndicesValidation.Play";
    static readonly string Output = Path.GetFullPath("../../output/prayer-book-indices-validation");
    static Camera camera;
    static SceneInputGate gate;
    static CipherWorkbench work;
    static DeskScenePreview view;
    static bool done;
    static int checks;
    static double started;
    static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method,
        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(obj, args);
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); checks++; }
    static void Ungate() => typeof(SceneInputGate).GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gate, -1);
    static PrayerBookIndicesValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene("Assets/Art/DeskSceneV1/Scenes/PH_DeskRoom.unity");
        SessionState.SetBool(Key, true); Install(); EditorApplication.isPlaying = true;
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
        if (done) return;
        if (EditorApplication.timeSinceStartup - started > 150) { Finish(false, "Timeout"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating || Time.frameCount < 5) return;
        try
        {
            camera = Camera.main; gate = camera.GetComponent<SceneInputGate>(); work = camera.GetComponent<CipherWorkbench>(); view = camera.GetComponent<DeskScenePreview>();
            camera.GetComponent<PrologueSequence>().Stop(); camera.GetComponent<DialogueController>().Stop();
            camera.GetComponent<DialogueTestConsole>().SetOpen(false); view.followMouse = false;
            var flow = camera.GetComponent<SecondActFlow>(); var archive = flow.archive;
            foreach (var behaviour in camera.GetComponents<MonoBehaviour>())
                if (behaviour.GetType().Name.EndsWith("Flow") || behaviour.GetType().Name.EndsWith("Opening")) behaviour.enabled = false;
            foreach (var memory in UnityEngine.Object.FindObjectsOfType<FirstInteractionMemory>(true)) memory.enabled = false;
            Check(archive.ReceiveAct(1) && archive.CompleteAct(1) && archive.ReceiveAct(2), "Cannot prepare book receipt");
            foreach (var book in flow.books)
            {
                var pose = book.transform.localPosition; var scale = book.transform.localScale; var notePose = book.note.transform.localPosition;
                Check(book.letterIndexFont && book.letterIndexFont.name.Contains("Courgette"), "Saved book lacks handwritten font");
                book.enabled = true; Ungate(); Check(book.TryOpen(), "Book cannot open"); Call(view, "AnimateView", 5f, Vector2.zero);
                Check(work.BookTool == book && book.spread.activeSelf && work.UsesDecodeKeyboard, "Book interaction changed");
                ValidateRows(book); ValidateCipher(book);
                DescribeRows(book);
                Capture(book.sevenPetal ? "flower-1920" : "ordinary-1920", 1920, 1080);
                Capture(book.sevenPetal ? "flower-1280" : "ordinary-1280", 1280, 720);
                int before = book.spread.GetComponentsInChildren<TextMesh>(true).Length;
                work.CloseBook(); Check(!book.spread.activeSelf && !work.BookTool && book.transform.localPosition == pose && book.transform.localScale == scale && book.note.transform.localPosition == notePose, "Closing did not restore book/note");
                Ungate(); Check(book.TryOpen(), "Cannot reopen book");
                Check(book.spread.GetComponentsInChildren<TextMesh>(true).Length == before, "Reopening duplicated indices");
                work.CloseBook();
            }
            Finish(true, "PASS: " + checks + " checks. Both authored editions; per-row letter-only indexing; exact case-57 coordinate lookup; smaller handwritten lighter ink; labels above body and separated horizontally; font material/depth; book open-close-reopen restores desk poses and creates no duplicates; rendered 1920x1080 and 1280x720. Unity 2022.3.62f2 Play Mode.");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void ValidateRows(PrayerBookTool book)
    {
        var source = book.sevenPetal ? BookCipherText.Flower : BookCipherText.Ordinary;
        for (int i = 0; i < source.Length; i++)
        {
            var row = book.spread.transform.Find("Page " + (12 + i / 4) + " line " + (1 + i % 4));
            var body = row.GetComponent<TextMesh>(); var numbers = row.Find("Letter indices");
            Check(body.text == source[i], "Printed line changed");
            var letters = Regex.Replace(body.text, "[^A-Z]", "");
            Check(numbers && numbers.childCount == letters.Length, "Spaces counted or letters omitted");
            float previousRight = float.NegativeInfinity;
            foreach (Transform child in numbers)
            {
                var ink = child.GetComponent<TextMesh>(); var renderer = child.GetComponent<Renderer>();
                Check(ink.text == (child.GetSiblingIndex() + 1).ToString(), "Wrong visible ordinal");
                Check(ink.font == book.letterIndexFont && ink.characterSize < body.characterSize && ink.color.r > body.color.r, "Number typography mismatch");
                var bounds = renderer.localBounds;
                var left = child.localPosition.x + bounds.min.x * child.localScale.x; var right = child.localPosition.x + bounds.max.x * child.localScale.x;
                Check(bounds.size.x > 0 && bounds.size.y > 0 && left > previousRight, "Numbers overlap or missing geometry"); previousRight = right;
                Check(child.localPosition.y + bounds.min.y > body.GetComponent<Renderer>().localBounds.center.y, "Number below body");
                Check(renderer.sharedMaterial.shader.name == "PurpleGlove/World Document Text", "Wrong depth material");
            }
        }
    }
    static void DescribeRows(PrayerBookTool book)
    {
        var report = new StringBuilder();
        foreach (Transform row in book.spread.transform)
        {
            if (!row.name.StartsWith("Page ")) continue;
            var text = row.GetComponent<TextMesh>();
            var number = row.Find("Letter indices/Letter 12").GetComponent<TextMesh>();
            var renderer = text.GetComponent<Renderer>(); var numberRenderer = number.GetComponent<Renderer>();
            var generator = new TextGenerator();
            generator.Populate(text.text, new TextGenerationSettings { font = text.font, fontSize = text.fontSize, scaleFactor = 1,
                color = Color.white, textAnchor = text.anchor, lineSpacing = text.lineSpacing,
                horizontalOverflow = HorizontalWrapMode.Overflow, verticalOverflow = VerticalWrapMode.Overflow });
            report.AppendLine(row.name + ": " + text.text + " font=" + text.font.name + " charSize=" + text.characterSize
                + " bounds=" + renderer.localBounds + " fontSize=" + text.font.fontSize + " ascent=" + text.font.ascent
                + " indexSize=" + number.characterSize + " indexBounds=" + numberRenderer.localBounds
                + " vertices=" + string.Join(";", generator.verts.Take(4).Select(v => v.position.ToString())));
        }
        File.WriteAllText(Output + (book.sevenPetal ? "/flower-layout.txt" : "/ordinary-layout.txt"), report.ToString());
    }
    static void ValidateCipher(PrayerBookTool book)
    {
        var answer = new StringBuilder();
        foreach (Match match in Regex.Matches(BookCipherText.Cipher, @"(\d+)-(\d+)-(\d+)"))
        {
            int page = int.Parse(match.Groups[1].Value), line = int.Parse(match.Groups[2].Value), index = int.Parse(match.Groups[3].Value);
            var row = book.spread.transform.Find("Page " + page + " line " + line);
            var number = row.Find("Letter indices/Letter " + index).GetComponent<TextMesh>();
            var letters = Regex.Replace(row.GetComponent<TextMesh>().text, "[^A-Z]", "");
            Check(int.Parse(number.text) == index, "Coordinate points to wrong number"); answer.Append(letters[index - 1]);
        }
        Check((answer.ToString() == "REQUESTCASEFIFTYSEVEN") == book.sevenPetal, "Cipher edition/numbering mismatch: " + answer);
    }
    static void Capture(string name, int width, int height)
    {
        var target = RenderTexture.GetTemporary(width, height, 24); var active = RenderTexture.active; var previous = camera.targetTexture;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply(); File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally { camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(image); }
    }
    static void Finish(bool success, string message)
    {
        if (done) return; done = true; SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/validation.txt", message);
        if (success) Debug.Log(message); else Debug.LogError(message); EditorApplication.Exit(success ? 0 : 1);
    }
}
