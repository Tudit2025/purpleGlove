using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PurpleGlove;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class PixelationMenuValidation
{
    const string Key = "PixelationMenuValidation.Play";
    static readonly string Output = Path.GetFullPath("../../output/pixelation-menu-validation");
    static int phase, checks, waitFrames;
    static double started;
    static bool finished;
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method,
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
    static void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
    static PixelationMenuValidation() { if (SessionState.GetBool(Key, false)) Install(); }

    public static void Run()
    {
        Directory.CreateDirectory(Output);
        PixelationMenuInstall.Install();
        PlayerPrefs.SetInt("PurpleGlove.Language", 0); PlayerPrefs.SetFloat("PurpleGlove.MasterVolume", 1); PlayerPrefs.Save();
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
        Check(scenes.Length >= 2 && scenes[0].path == MainMenuController.ScenePath && scenes[1].path == MainMenuController.GameScenePath,
            "Main menu is not first in the build scene list");
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
    static void Log(string text, string stack, LogType type)
    { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) Finish(false, text + "\n" + stack); }
    static void Tick()
    {
        if (finished) return;
        if (EditorApplication.timeSinceStartup - started > 150) { Finish(false, "Timeout in phase " + phase); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.frameCount < 10) return;
        try
        {
            if (phase == 0)
            {
                var menu = UnityEngine.Object.FindObjectOfType<MainMenuController>();
                Check(menu, "Menu controller missing");
                Check(menu.title.text == "紫手套" && menu.subtitle.text == "明文的谎言", "Chinese title/subtitle differ");
                Check(!menu.SettingsOpen && menu.homePanel.activeSelf && !menu.IsStarting, "Menu opens in wrong state");
                Check(UnityEngine.Object.FindObjectsOfType<AudioListener>().Count(l => l.enabled) == 1, "Menu has multiple audio listeners");
                Check(!UnityEngine.Object.FindObjectOfType<PrologueSequence>() && !UnityEngine.Object.FindObjectOfType<DialogueController>()
                    && !UnityEngine.Object.FindObjectOfType<DoorMechanism>(), "Menu is running gameplay components");
                Check(menu.ambience.GetComponent<AudioSource>().isPlaying, "Menu rain did not start");
                Check(EventSystem.current.currentSelectedGameObject == menu.startButton.gameObject, "Keyboard focus does not start on Begin");
                AssertPointer(menu.settingsButton);
                Capture(menu, "menu-zh", 1920, 1080);
                Capture(menu, "menu-4x3", 1280, 960);
                Capture(menu, "menu-ultrawide", 2560, 1080);
                Click(menu.settingsButton);
                Check(menu.SettingsOpen && !menu.homePanel.activeSelf, "Settings button does not open settings");

                Check(GameSettings.Pixelation && menu.sceneCamera.GetComponent<PixelSceneView>().pixelate && menu.pixelationLabel.text == "像素化：开", "Default pixelation is not enabled");
                Click(menu.pixelationButton);
                Check(!GameSettings.Pixelation && !menu.sceneCamera.GetComponent<PixelSceneView>().pixelate && menu.pixelationLabel.text == "像素化：关", "Pixelation off does not apply immediately");
                Check(PlayerPrefs.GetInt("PurpleGlove.Pixelation", -1) == 0, "Pixelation choice was not stored");
                Click(menu.pixelationButton);
                Check(GameSettings.Pixelation && menu.sceneCamera.GetComponent<PixelSceneView>().pixelate && menu.pixelationLabel.text == "像素化：开", "Pixelation on does not apply immediately");
                menu.volumeSlider.value = 0.37f;
                Check(Mathf.Abs(AudioListener.volume - .37f) < .001f && Mathf.Abs(GameSettings.Volume - .37f) < .001f, "Volume slider does not change/store master volume");
                Check(menu.volumeLabel.text.Contains("37%"), "Volume percentage did not refresh");
                Check(Mathf.Abs(menu.volumeSlider.fillRect.rect.width - 420 * .37f) < .01f
                    && Mathf.Abs(menu.volumeSlider.handleRect.rect.height - 24) < .01f, "Slider geometry does not match its value");
                Capture(menu, "settings-zh", 1920, 1080);

                AssertPointer(menu.pixelationButton);
                Capture(menu, "settings-4x3", 1280, 960);
                Capture(menu, "settings-ultrawide", 2560, 1080);
                Click(menu.englishButton);
                Check(Localization.IsEnglish && menu.subtitle.text == "Lies in Plaintext" && menu.backLabel.text == "Back", "Language switch did not refresh settings and title");

                Check(menu.pixelationLabel.text == "Pixelation: On", "English pixelation on label missing");
                Click(menu.pixelationButton);
                Check(menu.pixelationLabel.text == "Pixelation: Off" && !GameSettings.Pixelation, "English pixelation off label missing");
                Capture(menu, "settings-en", 1920, 1080);
                Click(menu.backButton);
                Check(!menu.SettingsOpen && EventSystem.current.currentSelectedGameObject == menu.settingsButton.gameObject, "Back failed to restore menu/focus");
                Check(menu.startLabel.text == "Begin", "Begin did not translate");
                Capture(menu, "menu-en", 1920, 1080);
                AssertPointer(menu.startButton);
                // Use the same submit handler as the keyboard, then test double activation.
                ExecuteEvents.Execute(menu.startButton.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                Check(menu.IsStarting && !menu.controls.interactable, "Submit does not start the fade/input lock");
                menu.StartGame(); Check(menu.IsStarting, "Repeated start cancels the pending load");

                menu.TogglePixelation();
                Check(!GameSettings.Pixelation, "Pixelation can change during scene transition");
                phase = 1; return;
            }
            if (phase == 1)
            {
                if (SceneManager.GetActiveScene().path != MainMenuController.GameScenePath || ++waitFrames < 8) return;
                var story = Camera.main.GetComponent<PrologueSequence>();
                var dialogue = Camera.main.GetComponent<DialogueController>();
                Check(story.State == PrologueSequence.Phase.Monologue && dialogue.IsActive, "Begin did not enter the actual prologue");
                Check(!UnityEngine.Object.FindObjectOfType<MainMenuController>(), "Menu survived the gameplay load");

                Check(!Camera.main.GetComponent<PixelSceneView>().pixelate && !GameSettings.Pixelation, "Gameplay lost pixelation choice");
                Check(Camera.main.GetComponent<PixelSceneView>().fullResolutionDocuments, "Full resolution reading was disabled");
                Check(Localization.IsEnglish && Mathf.Abs(AudioListener.volume - .37f) < .001f, "Language/volume lost during scene load");
                Check(UnityEngine.Object.FindObjectsOfType<AudioListener>().Count(l => l.enabled) == 1, "Scene load duplicated audio listeners");
                for (int i = 0; i < 10 && dialogue.CurrentLine == null; i++) Call(dialogue, "AdvanceTime", 10f);
                Check(dialogue.LineIndex == 0, "Prologue did not open on its first line");
                if (dialogue.IsTyping) dialogue.Advance();
                Check(dialogue.VisibleText == Localization.Text(story.prologue.lines[0].text), "Prologue did not use selected language: " + dialogue.VisibleText);
                for (int i = 0; i < 40 && dialogue.IsActive; i++) { Call(dialogue, "AdvanceTime", 10f); dialogue.Advance(); }
                Check(story.State == PrologueSequence.Phase.OpeningDoor, "Prologue no longer opens the door");
                for (int i = 0; i < 8; i++) Call(story, "AdvanceTime", 10f);
                Check(!story.IsActive && story.PresentedAct == 1, "Prologue did not enter act one");
                Check(Camera.main.GetComponent<FirstActFlow>().State == FirstActFlow.Phase.OpeningDelay, "Act one arrival was not scheduled");
                Check(Camera.main.GetComponent<DialogueTestConsole>().JumpToAct(2), "Chapter jump did not accept act two");
                phase = 2; waitFrames = 0; return;
            }
            if (phase == 2)
            {
                if (++waitFrames < 8 || !Camera.main || Camera.main.GetComponent<DialogueTestConsole>().IsJumping) return;
                Check(Camera.main.GetComponent<DialogueTestConsole>().LastStartedAct == 2, "Chapter reload did not enter act two");

                Check(!Camera.main.GetComponent<PixelSceneView>().pixelate, "Chapter reload lost pixelation choice");
                Check(Camera.main.GetComponent<PrologueSequence>().State != PrologueSequence.Phase.Monologue, "Menu launch callback replayed prologue on chapter jump");
                SceneManager.LoadSceneAsync(MainMenuController.ScenePath); phase = 3; waitFrames = 0; return;
            }
            if (phase == 3)
            {
                if (SceneManager.GetActiveScene().path != MainMenuController.ScenePath || ++waitFrames < 8) return;
                var menu = UnityEngine.Object.FindObjectOfType<MainMenuController>();
                Check(menu && !menu.IsStarting && !menu.SettingsOpen, "Fresh menu retained its loading/settings state");
                Check(Localization.IsEnglish && menu.startLabel.text == "Begin" && Mathf.Abs(menu.volumeSlider.value - .37f) < .001f,
                    "Menu reload did not restore preferences");
                Click(menu.chineseButton); Check(menu.subtitle.text == "明文的谎言", "Chinese subtitle did not restore");
                menu.SetVolume(1); GameSettings.Save();

                Check(!GameSettings.Pixelation && !menu.sceneCamera.GetComponent<PixelSceneView>().pixelate, "Returning to menu lost pixelation choice");
                Click(menu.settingsButton);
                Check(menu.pixelationLabel.text == "像素化：关", "Reopened settings show wrong choice");
                Click(menu.pixelationButton);
                Check(GameSettings.Pixelation && menu.sceneCamera.GetComponent<PixelSceneView>().pixelate, "Returning to pixelation on failed");
                Click(menu.backButton);
                SceneManager.LoadSceneAsync(MainMenuController.ScenePath); phase = 4; waitFrames = 0; return;
            }
            if (phase == 4)
            {
                if (SceneManager.GetActiveScene().path != MainMenuController.ScenePath || ++waitFrames < 8) return;
                var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
                Check(GameSettings.Pixelation && menu.sceneCamera.GetComponent<PixelSceneView>().pixelate, "Enabled choice did not survive scene reload");
                Finish(true, $"PASS: {checks} checks in Unity 2022.3.62f2 Play Mode.\n"
                    + "Pixelation: default on; real button turns rendering off/on; Chinese/English labels; choice survives menu, gameplay and chapter reloads.\n"
                    + "Main menu is build scene zero. Chinese/English titles, pointer hit targets, settings, volume persistence and keyboard submission.\n"
                    + "Begin fades, rejects duplicate activation, loads the real prologue, opens the door and schedules act one.\n"
                    + "No gameplay components in menu; one audio listener before/after load; language and preferences survive scene reload.\n"
                    + "Chapter jump still enters act two after starting from the menu, without replaying the prologue.\n"
                    + "Rendered 16:9, 4:3 and ultrawide compositions plus Chinese/English settings.\n");
            }
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    static void Click(Button button) => ExecuteEvents.Execute(button.gameObject,
        new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
    static void AssertPointer(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var canvas = button.GetComponentInParent<Canvas>();
        var rect = button.GetComponent<RectTransform>();
        var point = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rect.TransformPoint(rect.rect.center));
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
        Check(hits.Any(hit => ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject) == button.gameObject), "Button cannot be hit: " + button.name);
    }
    static void Capture(MainMenuController menu, string name, int width, int height)
    {
        var canvas = menu.title.GetComponentInParent<Canvas>(); var ui = canvas.worldCamera;
        var scaler = canvas.GetComponent<CanvasScaler>(); var camera = menu.sceneCamera;
        var texture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        var active = RenderTexture.active; float fov = camera.fieldOfView, aspect = camera.aspect;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        var previous = scaler.uiScaleMode; float scale = scaler.scaleFactor;
        try
        {
            camera.targetTexture = ui.targetTexture = texture;
            camera.aspect = ui.aspect = width / (float)height;
            camera.fieldOfView = 2 * Mathf.Atan(Mathf.Tan(31 * Mathf.Deg2Rad) * Mathf.Max(1, (16f / 9) / camera.aspect)) * Mathf.Rad2Deg;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; scaler.scaleFactor = Mathf.Min(width / 1920f, height / 1080f);
            Call(scaler, "Handle"); Canvas.ForceUpdateCanvases();
            foreach (var label in new[] { menu.title, menu.subtitle, menu.startLabel, menu.volumeLabel, menu.fullscreenLabel, menu.pixelationLabel, menu.backLabel })
            {
                if (!label.gameObject.activeInHierarchy) continue;
                var corners = new Vector3[4]; label.rectTransform.GetWorldCorners(corners);
                Check(corners.All(p => { var v = ui.WorldToViewportPoint(p); return v.x >= 0 && v.x <= 1 && v.y >= 0 && v.y <= 1; }), "Label leaves viewport: " + name + "/" + label.name);
                Check(label.cachedTextGenerator.characterCountVisible >= label.text.Length, "Clipped text: " + name + "/" + label.name);
            }
            camera.Render(); ui.Render(); RenderTexture.active = texture;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(Output, name + ".png"), image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = ui.targetTexture = null; camera.aspect = aspect; ui.ResetAspect(); camera.fieldOfView = fov;
            scaler.uiScaleMode = previous; scaler.scaleFactor = scale; Call(scaler, "Handle"); Canvas.ForceUpdateCanvases();
            RenderTexture.active = active; RenderTexture.ReleaseTemporary(texture); UnityEngine.Object.DestroyImmediate(image);
        }
    }
    static void Finish(bool success, string report)
    {
        if (finished) return; finished = true;
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Path.Combine(Output, "validation.txt"), report);
        if (success) Debug.Log("MAIN_MENU_VALIDATION_PASS"); else Debug.LogError(report);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
