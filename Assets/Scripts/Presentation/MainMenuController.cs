using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PurpleGlove
{
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        public const string ScenePath = "Assets/Art/DeskSceneV1/Scenes/PH_MainMenu.unity";
        public const string GameScenePath = "Assets/Art/DeskSceneV1/Scenes/PH_DeskRoom.unity";
        public Camera sceneCamera;
        public Font chineseFont, englishFont;
        public Text title, subtitle, startLabel, settingsLabel, quitLabel, volumeLabel, fullscreenLabel, pixelationLabel, backLabel, status;
        public Button startButton, settingsButton, quitButton, backButton, fullscreenButton, pixelationButton, chineseButton, englishButton;
        public Slider volumeSlider;
        public GameObject homePanel, settingsPanel;
        public CanvasGroup controls;
        public Image fade;
        public Light candleLight;
        public SceneAmbience ambience;
        public float fadeSeconds = 0.65f;
        public bool IsStarting { get; private set; }
        public bool SettingsOpen => settingsPanel && settingsPanel.activeSelf;
        float candleIntensity, ambienceVolume;
        int screenWidth, screenHeight;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLaunch() => SceneManager.sceneLoaded -= BeginPrologue;

        void Awake()
        {
            PixelText.Apply(title, 3);
            PixelText.Apply(subtitle);
            candleIntensity = candleLight ? candleLight.intensity : 0;
            ambienceVolume = ambience ? ambience.volume : 0;
            startButton.onClick.AddListener(StartGame);
            settingsButton.onClick.AddListener(OpenSettings);
            backButton.onClick.AddListener(CloseSettings);
            quitButton.onClick.AddListener(Quit);
            fullscreenButton.onClick.AddListener(ToggleFullscreen);
            pixelationButton.onClick.AddListener(TogglePixelation);
            chineseButton.onClick.AddListener(() => Localization.SetLanguage(Localization.Language.Chinese));
            englishButton.onClick.AddListener(() => Localization.SetLanguage(Localization.Language.English));
            volumeSlider.SetValueWithoutNotify(GameSettings.Volume);
            volumeSlider.onValueChanged.AddListener(SetVolume);
            settingsPanel.SetActive(false); homePanel.SetActive(true);
            fade.gameObject.SetActive(false); status.text = "";
            RefreshLanguage(); FitCamera();
        }
        void OnEnable() => Localization.Changed += RefreshLanguage;
        void Start() => Select(startButton);
        void OnDisable() { Localization.Changed -= RefreshLanguage; GameSettings.Save(); }

        void Update()
        {
            if (screenWidth != Screen.width || screenHeight != Screen.height) FitCamera();
            if (candleLight)
                candleLight.intensity = candleIntensity * (0.97f + Mathf.PerlinNoise(Time.unscaledTime * 1.7f, 0.31f) * 0.06f);
            if (SettingsOpen && !IsStarting && Input.GetKeyDown(KeyCode.Escape)) CloseSettings();
        }
        void FitCamera()
        {
            screenWidth = Screen.width; screenHeight = Screen.height;
            // Preserve the horizontal composition on narrower windows; UI uses the same fit.
            float aspect = screenWidth / (float)Mathf.Max(1, screenHeight);
            sceneCamera.fieldOfView = 2 * Mathf.Atan(Mathf.Tan(31 * Mathf.Deg2Rad) * Mathf.Max(1, (16f / 9) / aspect)) * Mathf.Rad2Deg;
        }
        void RefreshLanguage()
        {
            if (!title) return;
            bool english = Localization.IsEnglish;
            title.font = subtitle.font = english ? englishFont : chineseFont;
            title.fontSize = english ? 90 : 176;
            title.text = english ? "THE PURPLE\nGLOVE" : Localization.Text("紫手套");
            subtitle.fontSize = english ? 40 : 44;
            subtitle.text = Localization.Text("明文的谎言");
            startLabel.text = Localization.Text("开始游戏"); settingsLabel.text = Localization.Text("设置");
            quitLabel.text = Localization.Text("退出"); backLabel.text = Localization.Text("返回");
            RefreshSettings();
            chineseButton.GetComponentInChildren<Text>().color = english ? Muted : Cream;
            englishButton.GetComponentInChildren<Text>().color = english ? Cream : Muted;
        }
        static Color Cream => new Color(0.86f, 0.79f, 0.65f);
        static Color Muted => new Color(0.55f, 0.49f, 0.40f);
        void RefreshSettings()
        {
            volumeLabel.text = Localization.Format("总音量：{0}%", Mathf.RoundToInt(GameSettings.Volume * 100));
            fullscreenLabel.text = Localization.Text(Screen.fullScreen ? "全屏：开" : "全屏：关");
            pixelationLabel.text = Localization.Text(GameSettings.Pixelation ? "像素化：开" : "像素化：关");
        }
        public void SetVolume(float value) { GameSettings.SetVolume(value); RefreshSettings(); }
        public void OpenSettings()
        {
            if (IsStarting) return;
            homePanel.SetActive(false); settingsPanel.SetActive(true);
            RefreshSettings(); Select(volumeSlider);
        }
        public void CloseSettings()
        {
            if (IsStarting) return;
            settingsPanel.SetActive(false); homePanel.SetActive(true); GameSettings.Save(); Select(settingsButton);
        }
        public void ToggleFullscreen()
        {
            if (IsStarting) return;
            bool next = !Screen.fullScreen; GameSettings.SetFullscreen(next);
            fullscreenLabel.text = Localization.Text(next ? "全屏：开" : "全屏：关");
        }
        public void TogglePixelation()
        {
            if (IsStarting) return;
            GameSettings.SetPixelation(!GameSettings.Pixelation);
            RefreshSettings();
        }
        static void Select(Selectable control)
        {
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(control.gameObject);
        }

        public void StartGame()
        {
            if (IsStarting) return;
            if (!Application.CanStreamedLevelBeLoaded(GameScenePath))
            { status.text = Localization.Text("暂时无法开始，请重试。"); return; }
            IsStarting = true; controls.interactable = false; status.text = "";
            GameSettings.Save(); StartCoroutine(EnterGame());
        }
        IEnumerator EnterGame()
        {
            fade.gameObject.SetActive(true);
            float elapsed = 0;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeSeconds));
                fade.color = new Color(0, 0, 0, t);
                if (ambience) ambience.volume = ambienceVolume * (1 - t);
                yield return null;
            }
            fade.color = Color.black;
            SceneManager.sceneLoaded -= BeginPrologue; SceneManager.sceneLoaded += BeginPrologue;
            AsyncOperation operation = null;
            try { operation = SceneManager.LoadSceneAsync(GameScenePath, LoadSceneMode.Single); }
            catch (System.Exception error) { Debug.LogException(error, this); }
            if (operation == null)
            {
                SceneManager.sceneLoaded -= BeginPrologue;
                IsStarting = false; controls.interactable = true; fade.gameObject.SetActive(false);
                if (ambience) ambience.volume = ambienceVolume;
                status.text = Localization.Text("暂时无法开始，请重试。"); Select(startButton);
            }
        }
        static void BeginPrologue(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != GameScenePath) return;
            SceneManager.sceneLoaded -= BeginPrologue;
            foreach (var root in scene.GetRootGameObjects())
            {
                var story = root.GetComponentInChildren<PrologueSequence>();
                if (!story) continue;
                if (!story.PlayPrologue()) Debug.LogError("Main menu could not start the prologue.", story);
                return;
            }
            Debug.LogError("Main menu loaded a scene without its prologue.");
        }
        public void Quit()
        {
            if (IsStarting) return;
            GameSettings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
