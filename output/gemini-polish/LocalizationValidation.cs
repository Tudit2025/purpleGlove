using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using PurpleGlove;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Run only in the isolated project copy under Temp: -executeMethod LocalizationValidation.Run
// Never switches the user's editor to Play mode.
[InitializeOnLoad]
public static class LocalizationValidation
{
    const string Key = "LocalizationValidation.Play";
    const string Scene = "Assets/Art/DeskSceneV1/Scenes/PH_DeskRoom.unity";
    static readonly string Output = Path.GetFullPath("../../output/gemini-polish/validation");
    static readonly string[] Legacy = { "arrival", "charles", "pressure", "archive", "matteo", "bartolo", "act3_ring_hint", "act3_request" };
    // Papers, tools and books rendered in both languages for review.
    static readonly string[] Reviewed = { "Act1_01_公爵的核验委托", "Act1_03_行会会长的信", "Act2_02_印务台账摘要", "Act2_03_猪圈密文碎纸",
        "Act3_03_军情核验表", "Act3_04_夏尔的补充凭条", "Act4_01_商业文件委托签收页", "Act4_04_师父的明文书信", "Act5_01_米兰代表的书信",
        "Act4_Plaintext_rail", "Epilogue result receipt", "Epilogue reunion receipt", "Stored Caesar", "Stored Affine", "Stored RailFence" };
    static int checks, phase, reloadFrames;
    static bool done;
    static double started;
    static Camera camera, firstCamera;
    static DialogueController dialogue;
    static SceneInputGate gate;
    static readonly List<string> failures = new List<string>();
    static readonly StringBuilder notes = new StringBuilder();
    static readonly Dictionary<TextMesh, (string text, float size)> baseline = new Dictionary<TextMesh, (string, float)>();

    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    static object Field(object target, string name) => target.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
    static void SetProperty(object target, string name, object value) => target.GetType().GetProperty(name,
        BindingFlags.Instance | BindingFlags.Public).GetSetMethod(true).Invoke(target, new[] { value });
    static void Check(bool ok, string why) { checks++; if (!ok) failures.Add(why); }
    static void Ungate() => typeof(SceneInputGate).GetField("ignoreThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gate, -1);
    static string PathOf(Transform item) => item.parent ? PathOf(item.parent) + "/" + item.name : item.name;
    static IEnumerable<Transform> SceneTransforms() => SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true));

    static LocalizationValidation() { if (SessionState.GetBool(Key, false)) Install(); }

    public static void Run()
    {
        Directory.CreateDirectory(Output);
        PlayerPrefs.SetInt("PurpleGlove.Language", 0); PlayerPrefs.Save();
        EditorSceneManager.OpenScene(Scene);
        SessionState.SetBool(Key, true); Install(); EditorApplication.isPlaying = true;
    }

    static void Install()
    {
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
    }

    static void Log(string text, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Finish(false, text + "\n" + stack); }

    static void Tick()
    {
        if (done) return;
        if (EditorApplication.timeSinceStartup - started > 420) { Finish(false, "Timed out in phase " + phase); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating || Time.frameCount < 5) return;
        try
        {
            if (phase == 0)
            {
                Prepare(); WorldText(); Dialogue(); Composed(); Restore(); BeginReload();
                phase = 1; return;
            }
            if (Camera.main == firstCamera || !Camera.main || ++reloadFrames < 10) return;
            AfterReload();
            Finish(failures.Count == 0, failures.Count == 0 ? Summary() : "FAIL: " + failures.Count + " of " + checks + " checks\n" + string.Join("\n", failures));
        }
        catch (Exception error) { Finish(false, error + (failures.Count > 0 ? "\n" + string.Join("\n", failures) : "")); }
    }

    static void Prepare()
    {
        camera = firstCamera = Camera.main;
        dialogue = camera.GetComponent<DialogueController>(); gate = camera.GetComponent<SceneInputGate>();
        camera.GetComponent<DeskScenePreview>().followMouse = false;
        camera.GetComponent<PrologueSequence>().Stop(); dialogue.Stop();
        foreach (var behaviour in camera.GetComponents<MonoBehaviour>())
            if (behaviour.GetType().Name.EndsWith("Flow") || behaviour.GetType().Name.EndsWith("Opening")) behaviour.enabled = false;
        camera.GetComponent<DialogueTestConsole>().SetOpen(false); Ungate();
        Check(Resources.Load<TextAsset>(Localization.TablePath), "English table missing from Resources");
        Check(!Localization.IsEnglish, "Validation must start in Chinese");
    }

    // ------------------------------------------------------------------ world ink

    static void WorldText()
    {
        var notebook = UnityEngine.Object.FindObjectsByType<NotebookBook>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
        var pages = new[] { notebook.leftPage, notebook.rightPage, notebook.leafFront, notebook.leafBack };
        foreach (var text in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (Localization.HasChinese(text.text) && !pages.Contains(text)) baseline[text] = (text.text, text.characterSize);
        Check(baseline.Count > 100, "Too few Chinese world texts found: " + baseline.Count);
        var targets = Reviewed.Select(name => SceneTransforms().FirstOrDefault(t => t.name == name)).ToArray();
        for (int i = 0; i < Reviewed.Length; i++) Check(targets[i], "Review target missing: " + Reviewed[i]);
        foreach (var target in targets.Where(t => t))
            for (var item = target; item; item = item.parent) item.gameObject.SetActive(true);
        Capture(targets, "zh"); CaptureNotebook(notebook, "zh");

        Localization.SetLanguage(Localization.Language.English);
        Check(Localization.IsEnglish, "Language switch failed");
        var scales = new List<(float scale, string where)>();
        var widthModel = new List<float>();
        foreach (var pair in baseline)
        {
            var text = pair.Key; string where = PathOf(text.transform);
            Check(!Localization.HasChinese(text.text), "Untranslated world text: " + where + " | " + pair.Value.text.Replace("\n", "⏎"));
            Vector2 zh = Localization.Measure(text, pair.Value.text), en = Localization.Measure(text, text.text);
            float allowed = zh.x;
            if (zh.y == 1 && en.y == 1) allowed = Mathf.Max(allowed, Localization.FreeWidth(text, pair.Value.size));
            Check(en.x * text.characterSize <= allowed * pair.Value.size * 1.001f && en.y * text.characterSize <= zh.y * pair.Value.size * 1.001f,
                "English exceeds the Chinese footprint or free sheet width: " + where);
            if (text.gameObject.activeInHierarchy && en.x > 0)
                widthModel.Add(text.GetComponent<MeshRenderer>().localBounds.size.x / (en.x * text.characterSize * 0.1f));
            foreach (char c in text.text.Where(c => !char.IsWhiteSpace(c)).Distinct())
                Check(text.font && text.font.HasCharacter(c), $"Missing glyph '{c}' in {(text.font ? text.font.name : "no font")}: {where}");
            scales.Add((text.characterSize / pair.Value.size, where + " | " + text.text.Split('\n')[0]));
        }
        foreach (var spread in notebook.spreads)
            foreach (var page in new[] { spread.left, spread.right })
            {
                string shown = Localization.Text(page);
                Check(!Localization.HasChinese(shown), "Untranslated notebook page: " + page.Split('\n')[0]);
                foreach (char c in shown.Where(c => !char.IsWhiteSpace(c)).Distinct())
                    Check(notebook.leftPage.font.HasCharacter(c), $"Missing notebook glyph '{c}'");
            }
        Check(!Localization.HasChinese(notebook.leftPage.text) && !Localization.HasChinese(notebook.rightPage.text), "Notebook did not refresh on switch");
        // The free-width rule converts font advances to local units; confirm it against generated meshes.
        Check(widthModel.Count > 20 && widthModel.All(r => r > 0.8f && r < 1.2f),
            $"TextMesh width model off: {widthModel.Count} samples, {widthModel.DefaultIfEmpty().Min():0.00}–{widthModel.DefaultIfEmpty().Max():0.00}");
        notes.AppendLine($"Width model: mesh width / predicted width over {widthModel.Count} visible texts = {widthModel.DefaultIfEmpty().Min():0.00}–{widthModel.DefaultIfEmpty().Max():0.00}.");
        notes.AppendLine("Most reduced English world texts (character size relative to Chinese):");
        foreach (var item in scales.OrderBy(s => s.scale).Take(12)) notes.AppendLine($"  {item.scale:0.00}  {item.where}");
        Capture(targets, "en"); CaptureNotebook(notebook, "en");
    }

    static void Capture(Transform[] targets, string language)
    {
        foreach (var target in targets.Where(t => t))
        {
            var renderers = target.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
            var reference = target.GetComponentsInChildren<TextMesh>().OrderByDescending(t => t.text.Length).FirstOrDefault();
            if (renderers.Length == 0 || !reference) { Check(false, "Nothing to render: " + target.name); continue; }
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            Render(target.name + "-" + language, target, bounds, reference.transform.forward, reference.transform.up);
        }
    }

    static void CaptureNotebook(NotebookBook notebook, string language)
    {
        var field = typeof(NotebookBook).GetField("currentSpread", BindingFlags.Instance | BindingFlags.NonPublic);
        for (var item = notebook.transform; item; item = item.parent) item.gameObject.SetActive(true);
        foreach (int spread in new[] { 0, 4 })
        {
            field.SetValue(notebook, spread); notebook.RefreshPages();
            var bounds = notebook.leftPage.GetComponent<Renderer>().bounds;
            bounds.Encapsulate(notebook.rightPage.GetComponent<Renderer>().bounds);
            Render($"notebook-spread{spread}-{language}", notebook.transform, bounds, notebook.leftPage.transform.forward, notebook.leftPage.transform.up);
        }
        field.SetValue(notebook, 0); notebook.RefreshPages();
    }

    // The subject moves to a spare layer so shelves, lids and stored stacks cannot hide it.
    static void Render(string name, Transform subject, Bounds bounds, Vector3 forward, Vector3 up)
    {
        const int width = 1600, height = 1200, isolated = 31;
        var layers = subject.GetComponentsInChildren<Transform>(true).Select(t => (t, t.gameObject.layer)).ToArray();
        foreach (var (item, _) in layers) item.gameObject.layer = isolated;
        var rig = new GameObject("Localization review camera");
        var lens = rig.AddComponent<Camera>();
        lens.fieldOfView = 24; lens.nearClipPlane = 0.005f; lens.farClipPlane = 20; lens.aspect = (float)width / height;
        lens.clearFlags = CameraClearFlags.SolidColor; lens.backgroundColor = new Color(0.16f, 0.15f, 0.14f); lens.cullingMask = 1 << isolated;
        var right = Vector3.Cross(up, forward).normalized;
        float halfWidth = 0, halfHeight = 0, depth = 0;
        foreach (var corner in Corners(bounds))
        {
            var offset = corner - bounds.center;
            halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector3.Dot(offset, right)));
            halfHeight = Mathf.Max(halfHeight, Mathf.Abs(Vector3.Dot(offset, up)));
            depth = Mathf.Max(depth, Mathf.Abs(Vector3.Dot(offset, forward)));
        }
        float half = Mathf.Max(halfHeight, halfWidth / lens.aspect) * 1.08f;
        float distance = half / Mathf.Tan(lens.fieldOfView * 0.5f * Mathf.Deg2Rad) + depth;
        rig.transform.SetPositionAndRotation(bounds.center - forward * distance, Quaternion.LookRotation(forward, up));
        var lamp = rig.AddComponent<Light>(); lamp.type = LightType.Directional; lamp.intensity = 1.1f; lamp.cullingMask = 1 << isolated;
        var texture = RenderTexture.GetTemporary(width, height, 24);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        var active = RenderTexture.active;
        try
        {
            lens.targetTexture = texture; lens.Render(); RenderTexture.active = texture;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            lens.targetTexture = null; RenderTexture.active = active; RenderTexture.ReleaseTemporary(texture);
            UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(rig);
            foreach (var (item, layer) in layers) item.gameObject.layer = layer;
        }
    }

    static IEnumerable<Vector3> Corners(Bounds b)
    {
        for (int i = 0; i < 8; i++)
            yield return b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
    }

    // -------------------------------------------------------------------- dialogue

    static void Dialogue()
    {
        var overlay = dialogue.Overlay;
        var fifth = camera.GetComponent<FifthActFlow>();
        Call(fifth, "EnsureChoices");
        // Flows reference events the controller list does not register, so read every event asset.
        var events = AssetDatabase.FindAssets("t:DialogueEvent", new[] { "Assets/Data/Dialogue/Events" })
            .Select(g => AssetDatabase.LoadAssetAtPath<DialogueEvent>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(e => e && !Legacy.Contains(e.id))
            .Concat(new[] { (DialogueEvent)Field(fifth, "ringChoice"), (DialogueEvent)Field(fifth, "burnChoice") }).ToArray();
        Check(events.Length >= 39, "Too few story dialogue events: " + events.Length);
        var protagonist = dialogue.speakers.First(s => s && s.isProtagonist);
        var target = RenderTexture.GetTemporary(1600, 900, 24);
        var previous = overlay.OverlayCamera.targetTexture;
        overlay.OverlayCamera.targetTexture = target;
        int lines = 0;
        try
        {
            foreach (var sequence in events)
                foreach (var line in sequence.lines)
                {
                    if (line.text.Trim() == "{去向后果}") continue;  // Filled by EpilogueFlow; checked in Composed().
                    string shown = Localization.Text(line.text);
                    Check(!Localization.HasChinese(shown), $"Untranslated line {sequence.id}: {line.text}");
                    Fits(overlay, line.speaker, shown, sequence.id);
                    foreach (var choice in line.choices)
                        Check(!Localization.HasChinese(Localization.Text(choice.text)), $"Untranslated choice {sequence.id}.{choice.id}");
                    if (line.HasChoices)
                    {
                        overlay.ShowChoices(protagonist, line.speaker.isProtagonist ? shown : string.Empty, line.choices);
                        Canvas.ForceUpdateCanvases();
                        foreach (var label in overlay.Panel.GetComponentsInChildren<Text>().Where(t => t.transform.parent != overlay.Panel && t.gameObject.activeInHierarchy))
                            Check(label.cachedTextGenerator.characterCountVisible >= label.text.Length, $"English choice clipped in {sequence.id}: {label.text}");
                    }
                    lines++;
                }
        }
        finally { overlay.OverlayCamera.targetTexture = previous; RenderTexture.ReleaseTemporary(target); overlay.Hide(); }
        notes.AppendLine($"Dialogue: {lines} story lines in {events.Length} events translated, glyph-complete and unclipped at 1600x900.");

        // Typing, a language switch mid-line, and the choice panel, in the real controller.
        var summary = events.First(e => e.id == "act1_summary");
        Check(dialogue.PlayInPlace(summary), "Could not play act1_summary in place");
        for (int i = 0; i < 40 && dialogue.CurrentLine == null; i++) { Ungate(); Call(dialogue, "AdvanceTime", 0.1f); }
        Ungate(); Call(dialogue, "AdvanceTime", 0.2f);
        Check(dialogue.IsTyping && !Localization.HasChinese(dialogue.VisibleText), "English line is not typing");
        Localization.SetLanguage(Localization.Language.Chinese);
        Check(dialogue.VisibleText == dialogue.CurrentLine.text && !dialogue.IsTyping, "Switch to Chinese did not show the full Chinese line");
        Localization.SetLanguage(Localization.Language.English);
        Check(dialogue.VisibleText == Localization.Text(dialogue.CurrentLine.text), "Switch back did not show the full English line");
        CaptureDialogue("dialogue-en");
        dialogue.Stop();
        var secretary = events.First(e => e.id == "act1_secretary");
        Check(dialogue.PlayInPlace(secretary), "Could not play act1_secretary in place");
        for (int i = 0; i < 40 && dialogue.CurrentLine == null; i++) { Ungate(); Call(dialogue, "AdvanceTime", 0.1f); }
        Ungate(); dialogue.Advance(); Ungate(); dialogue.Advance();
        Check(dialogue.IsChoosing, "Secretary choices did not open");
        CaptureDialogue("choices-en");
        dialogue.Stop();
    }

    static void Fits(DialogueOverlay overlay, DialogueSpeaker speaker, string shown, string where)
    {
        overlay.Show(speaker, shown); overlay.Reveal(shown.Length); Canvas.ForceUpdateCanvases();
        Check(overlay.Body.cachedTextGenerator.characterCountVisible >= overlay.Body.text.Length, $"English line clipped in {where}: {shown}");
        foreach (char c in shown.Where(c => !char.IsWhiteSpace(c)).Distinct())
            Check(overlay.font.HasCharacter(c), $"Dialogue font lacks '{c}' ({where})");
    }

    static void CaptureDialogue(string name)
    {
        const int width = 1600, height = 900;
        var overlay = dialogue.Overlay.OverlayCamera;
        var texture = RenderTexture.GetTemporary(width, height, 24);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        var oldScene = camera.targetTexture; var oldOverlay = overlay.targetTexture; var active = RenderTexture.active;
        try
        {
            camera.targetTexture = overlay.targetTexture = texture;
            Call(dialogue.Overlay, "LateUpdate"); Canvas.ForceUpdateCanvases(); camera.Render(); overlay.Render();
            RenderTexture.active = texture; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldScene; overlay.targetTexture = oldOverlay; RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(texture); UnityEngine.Object.DestroyImmediate(image);
        }
    }

    // ------------------------------------------------------- composed at runtime

    static void Composed()
    {
        for (int act = 0; act <= 6; act++)
            Check(!Localization.HasChinese(Localization.Text(ActTransitionOverlay.ActTitle(act))), "Untranslated act caption " + act);
        var fifth = camera.GetComponent<FifthActFlow>();
        var epilogue = camera.GetComponent<EpilogueFlow>();
        foreach (bool guarantee in new[] { false, true })
            foreach (FifthActFlow.Custody custody in Enum.GetValues(typeof(FifthActFlow.Custody)))
            {
                SetProperty(fifth, "GuaranteesSource", guarantee); SetProperty(fifth, "OriginalCustody", custody);
                string receipt = (string)Call(epilogue, "ReportText");
                Check(!Localization.HasChinese(Localization.Text(receipt)), $"Untranslated receipt {guarantee}/{custody}: {receipt}");
                foreach (var ending in new[] { epilogue.endorsedEnding, epilogue.transferredEnding })
                {
                    var copy = UnityEngine.Object.Instantiate(ending);
                    Call(epilogue, "FillCustody", copy);
                    foreach (var line in copy.lines)
                        Check(!Localization.HasChinese(Localization.Text(line.text)), $"Untranslated epilogue line {ending.id}/{custody}: {line.text}");
                    UnityEngine.Object.DestroyImmediate(copy);
                }
                Check(!Localization.HasChinese(Localization.Text("尾声 · " + fifth.Ending + "\n终")), "Untranslated final card " + fifth.Ending);
            }
        SetProperty(fifth, "GuaranteesSource", false); SetProperty(fifth, "OriginalCustody", FifthActFlow.Custody.Guild);
        var hints = new DeskCommandHints(camera.gameObject).Resolve();
        Check(!Localization.HasChinese(hints.task + hints.detail), "Desk task hint still Chinese: " + hints.task);
    }

    static void Restore()
    {
        Localization.SetLanguage(Localization.Language.Chinese);
        foreach (var pair in baseline)
            Check(pair.Key.text == pair.Value.text && pair.Key.characterSize == pair.Value.size,
                "Chinese not restored exactly: " + PathOf(pair.Key.transform));
        Check(Localization.HasChinese(UnityEngine.Object.FindObjectsByType<NotebookBook>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Single().leftPage.text), "Notebook did not return to Chinese");
    }

    // A chapter jump reloads the scene; the English choice must carry over.
    static void BeginReload()
    {
        Localization.SetLanguage(Localization.Language.English);
        Check(camera.GetComponent<DialogueTestConsole>().JumpToAct(1), "Chapter jump refused");
    }

    static void AfterReload()
    {
        Check(Localization.IsEnglish, "English lost across the scene reload");
        var title = (Text)Field(Camera.main.GetComponent<ActTransitionOverlay>(), "title");
        Check(title.text == "Act I", "Act caption after reload: " + title.text);
        var heading = SceneTransforms().First(t => t.name == "Act1_01_公爵的核验委托").Find("Title").GetComponent<TextMesh>();
        Check(!Localization.HasChinese(heading.text), "Reloaded scene paper still Chinese: " + heading.text);
        Localization.SetLanguage(Localization.Language.Chinese);
        Check(Localization.HasChinese(heading.text), "Reloaded scene did not return to Chinese");
    }

    static string Summary() => "PASS: " + checks + " checks in Unity 2022.3.62f2 Play Mode.\n"
        + $"{baseline.Count} Chinese world texts switch to English inside their Chinese footprint, with every glyph present, and restore exactly.\n"
        + "Notebook pages, act captions, 8 receipts, 7 custody consequences, final cards and the desk-task hint resolve to English.\n"
        + "Mid-line language switch, choice panel, chapter-jump reload with English kept, and return to Chinese.\n"
        + "Screenshots: <name>-zh.png / <name>-en.png per reviewed paper or tool, notebook spreads, dialogue-en.png, choices-en.png.\n\n" + notes;

    static void Finish(bool success, string message)
    {
        if (done) return;
        done = true; SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        PlayerPrefs.SetInt("PurpleGlove.Language", 0); PlayerPrefs.Save();
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/validation.txt", message);
        if (success) Debug.Log("LOCALIZATION_VALIDATION_PASS"); else Debug.LogError(message);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
