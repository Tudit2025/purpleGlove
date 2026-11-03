using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PurpleGlove;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Run only in an isolated copy: -executeMethod PixelTextValidation.Run.
[InitializeOnLoad]
public static class PixelTextValidation
{
    const string Key = "PixelTextValidation.Play";
    static readonly string Output = Path.GetFullPath("../../output/pixel-text-validation");
    static int phase, checks, wait;
    static bool done;
    static double started;
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method,
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    static PixelTextValidation() { if (SessionState.GetBool(Key, false)) Install(); }

    public static void Run()
    {
        Directory.CreateDirectory(Output);
        SessionState.SetInt(Key + ".pixelation", PlayerPrefs.GetInt("PurpleGlove.Pixelation", -1));
        SessionState.SetInt(Key + ".language", PlayerPrefs.GetInt("PurpleGlove.Language", -1));
        PlayerPrefs.SetInt("PurpleGlove.Pixelation", 1); PlayerPrefs.SetInt("PurpleGlove.Language", 0);
        EditorSceneManager.OpenScene(MainMenuController.ScenePath);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuController.ScenePath);
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
        if (done) return;
        if (EditorApplication.timeSinceStartup - started > 150) { Finish(false, "Timeout phase " + phase); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.frameCount < 10) return;
        try
        {
            if (phase == 0)
            {
                var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
                Check(menu, "Main menu missing");
                AssertEffect(menu.title, true); AssertEffect(menu.subtitle, true);
                Check(!menu.startLabel.GetComponent<PixelText>(), "Menu controls unexpectedly styled");
                Check(menu.title.material.shader.isSupported && !ShaderUtil.ShaderHasError(menu.title.material.shader), "Pixel text shader failed");
                var canvas = menu.title.canvas;
                Capture("menu-zh-on", menu.sceneCamera, canvas.worldCamera, canvas);
                menu.OpenSettings(); menu.pixelationButton.onClick.Invoke();
                AssertEffect(menu.title, false); AssertEffect(menu.subtitle, false);
                menu.CloseSettings(); Capture("menu-zh-off", menu.sceneCamera, canvas.worldCamera, canvas);
                menu.OpenSettings(); menu.pixelationButton.onClick.Invoke(); menu.CloseSettings();
                AssertEffect(menu.title, true);
                Localization.SetLanguage(Localization.Language.English);
                Check(menu.title.font == menu.englishFont, "English title font changed");
                AssertEffect(menu.title, true); Capture("menu-en-on", menu.sceneCamera, canvas.worldCamera, canvas);
                Localization.SetLanguage(Localization.Language.Chinese);
                Capture("menu-720p", menu.sceneCamera, canvas.worldCamera, canvas, 1280, 720);
                Check(menu.title.cachedTextGenerator.characterCountVisible == menu.title.text.Length, "Title clipped at 720p");
                SceneManager.LoadSceneAsync(MainMenuController.GameScenePath); phase = 1; wait = 0; return;
            }
            if (phase == 1)
            {
                if (SceneManager.GetActiveScene().path != MainMenuController.GameScenePath || ++wait < 8) return;
                var camera = Camera.main;
                camera.GetComponent<PrologueSequence>().Stop();
                camera.GetComponent<DialogueController>().Stop();
                foreach (var b in camera.GetComponents<MonoBehaviour>())
                    if (b.GetType().Name.EndsWith("Flow") || b.GetType().Name.EndsWith("Opening")) b.enabled = false;
                camera.GetComponent<DeskScenePreview>().followMouse = false;
                var overlay = camera.GetComponent<DialogueOverlay>();
                var speaker = camera.GetComponent<DialogueController>().speakers.First(s => s.isProtagonist);
                const string line = "卢卡，三方的见证封条已经贴好。今夜送进来的每份文件，都要登记来路。";
                overlay.Show(speaker, line); overlay.Reveal(line.Length);
                var canvas = overlay.Body.canvas;
                AssertEffect(overlay.Body, true);
                Check(overlay.Body.font.name == "YinPinZhuSong", "Dialogue font replaced");
                Canvas.ForceUpdateCanvases();
                int lines = overlay.Body.cachedTextGenerator.lineCount;
                overlay.Reveal(5); Canvas.ForceUpdateCanvases();
                Check(overlay.Body.text.Contains("<color=#00000000>"), "Typewriter hidden suffix missing");
                Check(overlay.Body.cachedTextGenerator.lineCount == lines, "Typewriter line wrapping changed");
                Capture("dialogue-typing", camera, overlay.OverlayCamera, canvas);
                overlay.Reveal(line.Length);
                Capture("dialogue-zh-on", camera, overlay.OverlayCamera, canvas);
                GameSettings.SetPixelation(false); AssertEffect(overlay.Body, false);
                Capture("dialogue-zh-off", camera, overlay.OverlayCamera, canvas);
                GameSettings.SetPixelation(true);
                overlay.Show(speaker, "Every document delivered tonight must have its origin recorded."); overlay.Reveal(999);
                Capture("dialogue-en-on", camera, overlay.OverlayCamera, canvas);
                var choices = new[] {
                    new DialogueEvent.Choice { id = "one", text = "请把编号留下，我会先检查封口。" },
                    new DialogueEvent.Choice { id = "two", text = "我会按正式手续调取原件。" },
                    new DialogueEvent.Choice { id = "three", text = "再检查一下。" }
                };
                overlay.ShowChoices(speaker, "现在要摇铃交卷吗？", choices);
                Capture("choices-zh", camera, overlay.OverlayCamera, canvas);
                Capture("choices-4x3", camera, overlay.OverlayCamera, canvas, 1024, 768);
                Capture("choices-ultrawide", camera, overlay.OverlayCamera, canvas, 2560, 1080);
                for (int i = 0; i < choices.Length; i++)
                {
                    var row = overlay.Panel.Find("Player response " + (i + 1)).GetComponent<RectTransform>();
                    var text = row.GetComponentInChildren<Text>(); AssertEffect(text, true);
                    Canvas.ForceUpdateCanvases();
                    Check(overlay.ChoiceAtPointer(RectTransformUtility.WorldToScreenPoint(overlay.OverlayCamera, row.TransformPoint(row.rect.center))) == i,
                        "Choice hit target changed: " + i);
                }
                // Newly created and reactivated rows must inherit the current preference.
                GameSettings.SetPixelation(false); overlay.Hide(); overlay.ShowChoices(speaker, "", choices);
                foreach (var text in overlay.Panel.GetComponentsInChildren<Text>()) AssertEffect(text, false);
                GameSettings.SetPixelation(true);
                foreach (var text in overlay.Panel.GetComponentsInChildren<Text>()) AssertEffect(text, true);
                var transition = camera.GetComponent<ActTransitionOverlay>();
                transition.BeginFinalFade("第六幕 · 尾声", null); transition.Advance(10);
                var chapter = camera.GetComponentsInChildren<Text>(true).Single(t => t.name == "Chapter title");
                AssertEffect(chapter, true);
                Check(transition.TitleOpacity == 1, "Chapter fade did not finish");
                // Camera capture of the otherwise screen-space-overlay chapter canvas.
                var chapterCanvas = chapter.canvas;
                chapterCanvas.renderMode = RenderMode.ScreenSpaceCamera; chapterCanvas.worldCamera = overlay.OverlayCamera; chapterCanvas.planeDistance = .11f;
                overlay.Panel.gameObject.SetActive(false);
                Capture("chapter-zh", camera, overlay.OverlayCamera, chapterCanvas);
                chapterCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                transition.Cancel();
                Check(camera.GetComponent<PixelSceneView>().fullResolutionDocuments, "Document reading resolution changed");
                Check(!UnityEngine.Object.FindObjectsByType<PixelText>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Any(t => t.GetComponent<TextMesh>()), "Document text was styled");
                GameSettings.SetPixelation(false);
                SceneManager.LoadSceneAsync(MainMenuController.ScenePath); phase = 2; wait = 0; return;
            }
            if (phase == 2)
            {
                if (SceneManager.GetActiveScene().path != MainMenuController.ScenePath || ++wait < 8) return;
                var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
                AssertEffect(menu.title, false); AssertEffect(menu.subtitle, false);
                GameSettings.SetPixelation(true); AssertEffect(menu.title, true); AssertEffect(menu.subtitle, true);
                Finish(true, "PASS: " + checks + " checks. Unity " + Application.unityVersion
                    + ". Shader compile/render; Chinese/English title and dialogue; 720p, 4:3 and ultrawide captures; typewriter wrapping; choice hit targets; inactive row reactivation; chapter/end title; setting off/on and scene reload; document resolution preserved.\n");
            }
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void AssertEffect(Text text, bool active)
    {
        Check(text.GetComponent<PixelText>(), "Missing text effect on " + text.name);
        Check((text.material.shader.name == "PurpleGlove/UI/Pixel Text") == active, "Wrong pixel material on " + text.name);
    }
    static void Capture(string name, Camera scene, Camera ui, Canvas canvas, int width = 1920, int height = 1080)
    {
        var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        var active = RenderTexture.active; var scaler = canvas.GetComponent<CanvasScaler>();
        var scaleMode = scaler.uiScaleMode; float scale = scaler.scaleFactor;
        var oldScene = scene.targetTexture; var oldUI = ui.targetTexture;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            scene.targetTexture = ui.targetTexture = rt;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = Mathf.Min(width / 1920f, height / 1080f);
            Call(scaler, "Handle"); Canvas.ForceUpdateCanvases();
            foreach (var text in canvas.GetComponentsInChildren<Text>().Where(t => t.isActiveAndEnabled && t.text.Length > 0))
                Check(text.cachedTextGenerator.characterCountVisible > 0, "Empty rendering: " + text.name);
            scene.Render(); ui.Render(); RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(Output, name + ".png"), image.EncodeToPNG());
        }
        finally
        {
            scene.targetTexture = oldScene; ui.targetTexture = oldUI;
            scaler.uiScaleMode = scaleMode; scaler.scaleFactor = scale; Call(scaler, "Handle"); Canvas.ForceUpdateCanvases();
            RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(image);
        }
    }
    static void Finish(bool success, string report)
    {
        if (done) return; done = true;
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        foreach (var entry in new[] { ("pixelation", "PurpleGlove.Pixelation"), ("language", "PurpleGlove.Language") })
        {
            int value = SessionState.GetInt(Key + "." + entry.Item1, -1);
            if (value < 0) PlayerPrefs.DeleteKey(entry.Item2); else PlayerPrefs.SetInt(entry.Item2, value);
        }
        PlayerPrefs.Save(); File.WriteAllText(Path.Combine(Output, "validation.txt"), report);
        if (success) Debug.Log(report); else Debug.LogError(report);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
