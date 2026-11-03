using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PurpleGlove.Editor
{
    public static class MainMenuBuilder
    {
        static readonly Color Cream = new Color(0.86f, 0.79f, 0.65f);
        static readonly Color Quiet = new Color(0.65f, 0.58f, 0.48f);
        static Font chinese, english;

        [MenuItem("Purple Glove/Scene/Build Main Menu")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var source = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            var camera = Camera.main;
            var story = camera.GetComponent<PrologueSequence>();
            var architecture = story.doorLeaf.parent;
            var rain = UnityEngine.Object.FindObjectOfType<SceneAmbience>();
            var rainClip = rain.GetComponent<AudioSource>().clip;
            float rainVolume = rain.volume;
            var sky = RenderSettings.ambientSkyColor; var equator = RenderSettings.ambientEquatorColor;
            var ground = RenderSettings.ambientGroundColor; var reflection = RenderSettings.customReflectionTexture;
            var fog = RenderSettings.fogColor; float fogDensity = RenderSettings.fogDensity;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground; RenderSettings.reflectionIntensity = 0.7f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = reflection;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = fogDensity; RenderSettings.fogColor = fog;
            // Copy only visual components. No puzzle, dialogue, delivery or door interaction runs here.
            CopyVisual(architecture.Find("Door exterior - dim corridor"), null);
            CopyVisual(story.doorLeaf, null);
            CopyVisual(architecture.Find("DoorFrame"), null);
            var bounce = CopyVisual(story.exteriorLight.transform, null).GetComponent<Light>();
            bounce.enabled = true; bounce.intensity = 0.95f; bounce.range = 2.4f;
            bounce.color = new Color(1f, 0.75f, 0.48f);
            bounce.transform.position = story.doorLeaf.TransformPoint(new Vector3(0.06f, 1.76f, 0.73f));

            var shot = new GameObject("Main Camera").AddComponent<Camera>(); shot.CopyFrom(camera);
            shot.tag = "MainCamera"; shot.enabled = true; shot.cullingMask &= ~(1 << 5); shot.depth = 0;
            shot.fieldOfView = 62; shot.nearClipPlane = 0.035f;
            Vector3 position = story.doorLeaf.TransformPoint(new Vector3(1.567f, 1.31f, 1.73f));
            Vector3 target = story.doorLeaf.TransformPoint(new Vector3(1.267f, 1.21f, 0.08f));
            shot.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
            shot.gameObject.AddComponent<AudioListener>();
            var pixels = shot.gameObject.AddComponent<PixelSceneView>(); pixels.verticalResolution = 540;

            var controller = new GameObject("Main Menu").AddComponent<MainMenuController>();
            controller.sceneCamera = shot; controller.candleLight = bounce;
            chinese = AssetDatabase.LoadAssetAtPath<Font>(DeskSceneBuilder.Root + "/Fonts/YinPinZhuSong.ttf");
            english = AssetDatabase.LoadAssetAtPath<Font>(DeskSceneBuilder.Root + "/Fonts/EBGaramond-Medium.otf");
            controller.chineseFont = chinese; controller.englishFont = english;
            var sound = new GameObject("Corridor rain").AddComponent<AudioSource>();
            sound.clip = rainClip; sound.loop = true; sound.playOnAwake = false; sound.spatialBlend = 0;
            sound.dopplerLevel = 0;
            controller.ambience = sound.gameObject.AddComponent<SceneAmbience>();
            controller.ambience.volume = rainVolume * 0.65f;
            BuildInterface(controller);
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            EditorSceneManager.CloseScene(source, true);
            EditorSceneManager.SaveScene(scene, MainMenuController.ScenePath);
            DeskSceneStartup.Configure();
            AssetDatabase.SaveAssets();
            Debug.Log("MAIN_MENU_BUILT");
        }

        public static void Run()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        static Transform CopyVisual(Transform source, Transform parent)
        {
            if (!source) throw new InvalidOperationException("Missing corridor source object.");
            var copy = new GameObject(source.name).transform; copy.gameObject.layer = source.gameObject.layer;
            copy.SetParent(parent, false);
            copy.localPosition = parent ? source.localPosition : source.position;
            copy.localRotation = parent ? source.localRotation : source.rotation;
            copy.localScale = parent ? source.localScale : source.lossyScale;
            foreach (var type in new[] { typeof(MeshFilter), typeof(MeshRenderer), typeof(Light) })
            {
                var component = source.GetComponent(type);
                if (component) EditorUtility.CopySerialized(component, copy.gameObject.AddComponent(type));
            }
            foreach (Transform child in source) CopyVisual(child, copy);
            copy.gameObject.SetActive(source.gameObject.activeSelf);
            return copy;
        }

        static void BuildInterface(MainMenuController menu)
        {
            var camera = new GameObject("Menu UI Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Depth; camera.cullingMask = 1 << 5; camera.depth = 10;
            camera.orthographic = true; camera.orthographicSize = 540; camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100; camera.transform.position = new Vector3(0, 0, -20);
            var root = new GameObject("Main Menu Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.layer = 5;
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var layout = Rect("Title and controls", root.transform, 0, 0, 1920, 1080);
            layout.anchorMin = layout.anchorMax = layout.pivot = new Vector2(0.5f, 0.5f);
            menu.controls = layout.gameObject.AddComponent<CanvasGroup>();
            menu.title = Label("Title", layout, "紫手套", 180, 265, 650, 220, 176, Cream);
            menu.title.lineSpacing = 0.85f;
            menu.subtitle = Label("Subtitle", layout, "明文的谎言", 198, 505, 630, 66, 44, Cream);
            Line("Title rule", layout, 196, 586, 440, 1, new Color(0.51f, 0.43f, 0.33f, 0.45f));
            Line("Purple accent", layout, 196, 583, 44, 5, new Color(0.42f, 0.27f, 0.40f));

            menu.homePanel = Rect("Home", layout, 0, 0, 1920, 1080).gameObject;
            menu.startButton = Button("Start game", menu.homePanel.transform, "开始游戏", 196, 631, 480, 64, out menu.startLabel);
            menu.settingsButton = Button("Settings", menu.homePanel.transform, "设置", 196, 713, 480, 64, out menu.settingsLabel);
            menu.quitButton = Button("Quit", menu.homePanel.transform, "退出", 196, 795, 480, 64, out menu.quitLabel);

            menu.settingsPanel = Rect("Settings panel", layout, 0, 0, 1920, 1080).gameObject;
            var settings = menu.settingsPanel.transform;
            menu.volumeLabel = Label("Volume label", settings, "总音量：100%", 208, 633, 500, 50, 28, Cream);
            menu.volumeSlider = Slider(settings);
            menu.fullscreenButton = Button("Fullscreen", settings, "全屏：开", 196, 735, 500, 50, out menu.fullscreenLabel);
            menu.pixelationButton = Button("Pixelation", settings, "像素化：开", 196, 795, 500, 50, out menu.pixelationLabel);
            menu.backButton = Button("Back", settings, "返回", 196, 855, 500, 50, out menu.backLabel);
            menu.settingsPanel.SetActive(false);
            menu.chineseButton = Button("Chinese", layout, "中文", 183, 968, 90, 44, out var zh);
            menu.englishButton = Button("English", layout, "ENGLISH", 291, 968, 180, 44, out var en);
            zh.fontSize = en.fontSize = 22; en.font = english;
            menu.chineseButton.transition = menu.englishButton.transition = Selectable.Transition.None;
            zh.color = Cream; en.color = Quiet;
            Label("Language divider", layout, "/", 277, 973, 20, 40, 22, Quiet).font = english;
            menu.status = Label("Status", layout, "", 196, 906, 700, 42, 24, Cream);
            menu.fade = Line("Fade", root.transform, 0, 0, 1920, 1080, Color.black);
            var curtain = menu.fade.rectTransform;
            curtain.anchorMin = Vector2.zero; curtain.anchorMax = Vector2.one;
            curtain.offsetMin = curtain.offsetMax = Vector2.zero;
            menu.fade.raycastTarget = true; menu.fade.gameObject.SetActive(false);
        }

        static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5; rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
            return rect;
        }
        static Text Label(string name, Transform parent, string value, float x, float y, float width, float height, int size, Color color)
        {
            var text = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Text>();
            text.font = chinese; text.text = value; text.fontSize = size; text.color = color;
            text.alignment = TextAnchor.UpperLeft; text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow; text.raycastTarget = false;
            return text;
        }
        static Image Line(string name, Transform parent, float x, float y, float width, float height, Color color)
        {
            var image = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false; return image;
        }
        static Button Button(string name, Transform parent, string label, float x, float y, float width, float height, out Text text)
        {
            var rect = Rect(name, parent, x, y, width, height);
            text = Label("Label", rect, label, 12, 4, width - 12, height - 4, 32, Color.white);
            text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = text;
            var colors = button.colors; colors.normalColor = Quiet; colors.highlightedColor = Cream;
            colors.selectedColor = Cream; colors.pressedColor = new Color(0.67f, 0.47f, 0.63f);
            colors.fadeDuration = 0.13f; button.colors = colors; return button;
        }
        static Slider Slider(Transform parent)
        {
            var root = Rect("Master volume", parent, 208, 689, 420, 42);
            // Keep the full row draggable, including the space around the thin track.
            root.gameObject.AddComponent<Image>().color = Color.clear;
            var track = Line("Track", root, 0, 17, 420, 5, new Color(0.27f, 0.23f, 0.18f)); track.raycastTarget = true;
            var fillArea = Rect("Fill area", root, 0, 17, 420, 5);
            var fill = Line("Fill", fillArea, 0, 0, 420, 5, Quiet);
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.sizeDelta = Vector2.zero;
            var handleArea = Rect("Handle area", root, 0, 9, 420, 24);
            var handle = Line("Handle", handleArea, 0, 0, 12, 0, Cream);
            handle.rectTransform.pivot = new Vector2(0.5f, 0.5f); handle.rectTransform.anchoredPosition = Vector2.zero;
            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight; slider.minValue = 0; slider.maxValue = 1; slider.value = 1;
            return slider;
        }
    }
}
