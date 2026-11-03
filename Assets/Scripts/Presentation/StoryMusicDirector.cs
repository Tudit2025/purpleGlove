using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PurpleGlove
{
    /// <summary>Scene-scoped music follows story progress, never the old dossier being inspected.</summary>
    [DefaultExecutionOrder(100), DisallowMultipleComponent]
    public sealed class StoryMusicDirector : MonoBehaviour
    {
        public enum Mood { Silence, Investigation, Pressure, Reflection, Reunion, Loss }
        public StoryMusicSettings settings;
        public bool muted;
        public Mood CurrentMood { get; private set; }
        public bool Paused { get; private set; }
        public bool OutroRequested => voices != null && voices[activeVoice].IsFinishing;
        public bool Finished => voices != null && voices[activeVoice].HasFinished;
        public float DuckGain { get; private set; } = 1;
        ActArchiveController archive;
        PrologueSequence story;
        PlaintextArchiveController records;
        FifthActFlow fifth;
        EpilogueFlow epilogue;
        DialogueController dialogue;
        SceneInputGate gate;
        StoryMusicVoice[] voices;
        StoryMusicSettings.Cue investigation;
        int activeVoice;
        bool appPaused;
        Canvas creditsCanvas;
        Font font;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= Attach;
            SceneManager.sceneLoaded += Attach;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InitialScene() => Attach(SceneManager.GetActiveScene(), LoadSceneMode.Single);

        static void Attach(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid() || scene.name != "PH_DeskRoom") return;
            var objects = scene.GetRootGameObjects();
            if (objects.Any(r => r.GetComponentInChildren<StoryMusicDirector>(true))) return;
            var settings = Resources.Load<StoryMusicSettings>("PHMusic/StoryMusic");
            var story = objects.SelectMany(r => r.GetComponentsInChildren<PrologueSequence>(true)).FirstOrDefault();
            if (!settings || !story) return;
            var root = new GameObject("Story music"); SceneManager.MoveGameObjectToScene(root, scene);
            var director = root.AddComponent<StoryMusicDirector>();
            director.Initialize(settings, story);
        }

        public void Initialize(StoryMusicSettings configuration, PrologueSequence sequence)
        {
            settings = configuration; story = sequence;
            var camera = story.gameObject;
            records = camera.GetComponent<PlaintextArchiveController>(); archive = records.archive;
            dialogue = camera.GetComponent<DialogueController>(); gate = camera.GetComponent<SceneInputGate>();
            fifth = camera.GetComponent<FifthActFlow>(); epilogue = camera.GetComponent<EpilogueFlow>();
            font = camera.GetComponent<DialogueOverlay>().font;
            voices = new[] { new StoryMusicVoice(transform, "Music A"), new StoryMusicVoice(transform, "Music B") };
            investigation = settings.candlelitMonastery ? new StoryMusicSettings.Cue { loop = settings.candlelitMonastery } : settings.investigation;
            BuildCredits();
        }

        public Mood ResolveMood()
        {
            if (!archive || !story) return Mood.Silence;
            if (archive.CurrentAct >= 6)
                return fifth && (fifth.Ending == "开门" || fifth.Ending == "缺页") ? Mood.Reunion : Mood.Loss;
            if (archive.CurrentAct >= 5) return Mood.Reflection;
            if (archive.CurrentAct == 4)
                return records.entries.Any(e => e.id == "rail" && e.saved) ? Mood.Reflection : Mood.Investigation;
            if (archive.CurrentAct == 3) return Mood.Pressure;
            return story.PresentedAct >= 1 || archive.LatestReceivedAct >= 1 || archive.CurrentAct >= 2 ? Mood.Investigation : Mood.Silence;
        }

        StoryMusicSettings.Cue Cue(Mood mood)
        {
            switch (mood)
            {
                case Mood.Investigation: return investigation;
                case Mood.Pressure: return settings.pressure;
                case Mood.Reflection: return settings.reflection;
                case Mood.Reunion: return settings.reunion;
                case Mood.Loss: return settings.loss;
                default: return null;
            }
        }

        void Update()
        {
            bool pause = appPaused || (!Application.isFocused && !Application.isBatchMode) || (gate && gate.ConsoleOpen);
            Advance(Time.unscaledDeltaTime, pause);
        }

        public void Advance(float seconds, bool pause)
        {
            if (!settings || voices == null) return;
            if (creditsCanvas) creditsCanvas.enabled = epilogue && epilogue.State == EpilogueFlow.Phase.Complete && !(gate && gate.ConsoleOpen);
            Paused = pause;
            foreach (var voice in voices) voice.SetPaused(pause);
            if (pause || AudioListener.pause) return;
            Mood next = ResolveMood();
            if (next != CurrentMood)
            {
                activeVoice = 1 - activeVoice;
                voices[activeVoice].Play(Cue(next)); CurrentMood = next;
            }
            float fade = Mathf.Max(0, seconds) / Mathf.Max(0.1f, settings.crossfadeSeconds);
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i].Gain = Mathf.MoveTowards(voices[i].Gain, i == activeVoice && next != Mood.Silence ? 1 : 0, fade);
                if (i != activeVoice && voices[i].Gain <= 0) voices[i].Stop();
            }
            float duck = gate && gate.DeliveryActive ? settings.deliveryGain
                : dialogue && (dialogue.IsActive || dialogue.IsClosing) ? settings.dialogueGain : 1;
            DuckGain = Mathf.MoveTowards(DuckGain, duck, Mathf.Max(0, seconds) / 0.65f);
            foreach (var voice in voices) voice.SetVolume((muted ? 0 : settings.volume) * DuckGain);
            if (epilogue && (epilogue.State == EpilogueFlow.Phase.FinalFade || epilogue.State == EpilogueFlow.Phase.Complete))
                voices[activeVoice].FinishAtBoundary();
        }

        void OnApplicationPause(bool pause) => appPaused = pause;
        void OnDisable()
        {
            if (voices != null) foreach (var voice in voices) voice.Stop();
            if (creditsCanvas) creditsCanvas.enabled = false;
            CurrentMood = Mood.Silence;
        }

        void BuildCredits()
        {
            var root = new GameObject("Music credits", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false); root.layer = 5;
            creditsCanvas = root.GetComponent<Canvas>(); creditsCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            creditsCanvas.sortingOrder = 10001; creditsCanvas.enabled = false;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var label = new GameObject("Attribution", typeof(RectTransform), typeof(Text)); label.layer = 5;
            var rect = label.GetComponent<RectTransform>(); rect.SetParent(root.transform, false);
            rect.anchorMin = new Vector2(.05f, .02f); rect.anchorMax = new Vector2(.95f, .19f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = label.GetComponent<Text>(); text.font = font; text.fontSize = 18; text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(.7f, .65f, .56f); text.raycastTarget = false; text.supportRichText = false;
            text.text = settings.credits;
        }
    }
}
