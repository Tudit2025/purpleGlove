using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PurpleGlove
{
    /// <summary>Starts a fresh chapter, with only the preceding chapters completed.</summary>
    [DefaultExecutionOrder(-300), DisallowMultipleComponent]
    [RequireComponent(typeof(SceneInputGate), typeof(DialogueController))]
    public sealed class DialogueTestConsole : MonoBehaviour
    {
        public enum EndingPreset { Open, Missing, Transferred, Endorsed }
        public Font font;
        public bool enableInReleaseBuild;
        public bool IsOpen { get; private set; }
        public bool Available => Application.isEditor || Debug.isDebugBuild || enableInReleaseBuild;
        public bool IsJumping => pendingAct >= 0;
        public int LastStartedAct { get; private set; } = -1;
        public string Status { get; private set; } = "选择一幕，从开场重新开始。";
        static readonly string[] Chapters = { "第零幕 · 门槛", "第一幕 · 留下原件", "第二幕 · 第五十七号封档",
            "第三幕 · 读得通的谎言", "第四幕 · 名字留在纸上", "第五幕 · 落笔与交卷", "第六幕 · 尾声" };
        static readonly string[] Endings = { "开门", "缺页", "真相易手", "背书" };
        static readonly string[] EndingDetails = { "不签保证 · 原件留存 · 保留自述", "不签保证 · 原件留存 · 烧毁自述",
            "不签保证 · 原件交威尼斯 · 保留自述", "签署保证 · 原件留存 · 保留自述" };
        static int pendingAct = -1;
        static string pendingScene;
        static EndingPreset pendingEnding;
        EndingPreset selectedEnding;
        DialogueController dialogue;
        SceneInputGate gate;
        SceneObjectInteractor interactor;
        ActTransitionOverlay curtain;
        bool enteringEpilogue;
        GUIStyle titleStyle, labelStyle, buttonStyle, selectedStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPending() { pendingAct = -1; pendingScene = null; pendingEnding = EndingPreset.Open; }

        void Awake()
        {
            dialogue = GetComponent<DialogueController>(); gate = GetComponent<SceneInputGate>();
            interactor = GetComponent<SceneObjectInteractor>(); curtain = GetComponent<ActTransitionOverlay>();
        }

        void Start()
        {
            // Consume the request after every scene component has run Awake/OnEnable.
            if (pendingAct < 0 || gameObject.scene.path != pendingScene) return;
            int act = pendingAct; selectedEnding = pendingEnding;
            pendingAct = -1; pendingScene = null;
            try
            {
                PreparePreviousActs(act);
                var story = GetComponent<PrologueSequence>();
                bool started = act == 0 ? story.PlayPrologue() : act == 6 ? BeginEpilogue() : story.PlayActTransition(act);
                if (!started) throw new InvalidOperationException("目标幕未能开始，请检查场景的剧情组件。");
                LastStartedAct = act;
                Status = "已从开场进入：" + Chapters[act];
            }
            catch (Exception error)
            {
                Status = "跳转失败：" + error.Message;
                Debug.LogException(error, this); SetOpen(true);
            }
        }

        void Update()
        {
            if (!Available) return;
            if (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.BackQuote)) SetOpen(!IsOpen);
            else if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) SetOpen(false);
            if (enteringEpilogue && !IsOpen && Application.isFocused) curtain.Advance(Time.unscaledDeltaTime);
        }

        public void SetOpen(bool open)
        {
            if (open && !Available) return;
            IsOpen = open; gate.SetConsole(open);
            if (curtain) curtain.SetConsoleVisible(open);
            if (open)
            {
                if (interactor && !dialogue.IsChoosing) interactor.CancelInteraction();
                dialogue.Voice.Stop();
            }
        }

        public bool JumpToAct(int act, EndingPreset ending = EndingPreset.Open)
        {
            if (!Available || !Application.isPlaying || IsJumping || act < 0 || act >= Chapters.Length
                || !Enum.IsDefined(typeof(EndingPreset), ending)) return false;
            string path = gameObject.scene.path;
            if (string.IsNullOrEmpty(path) || !Application.CanStreamedLevelBeLoaded(path))
            { Status = "跳转失败：请将当前场景加入 Build Settings。"; return false; }
            // Reload clears pending dialogue, inspection, burnt papers, moving cargo and final black.
            pendingAct = act; pendingScene = path; pendingEnding = ending;
            SetOpen(true); Status = "正在进入：" + Chapters[act];
            try
            {
                if (SceneManager.LoadSceneAsync(path, LoadSceneMode.Single) == null)
                    throw new InvalidOperationException("场景无法载入。");
                return true;
            }
            catch (Exception error)
            {
                pendingAct = -1; pendingScene = null;
                Status = "跳转失败：" + error.Message; return false;
            }
        }

        void PreparePreviousActs(int target)
        {
            var records = GetComponent<PlaintextArchiveController>();
            var archive = records.archive;
            var fifth = GetComponent<FifthActFlow>();
            for (int act = 1; act < target; act++)
            {
                if (!archive.ReceiveAct(act)) throw new InvalidOperationException("无法准备第 " + act + " 幕卷宗。");
                if (archive.HasPendingSupplement(act)) archive.ReceiveSupplement(act);
                records.PrepareCompletedActForTesting(act);
                if (act == 3) fifth.verification.PrepareCompletedForTesting();
                if (!archive.CompleteAct(act)) throw new InvalidOperationException("无法归档第 " + act + " 幕。");
            }
            if (target == 2) GetComponent<FirstActFlow>().PrepareSecondActStartForTesting();
            if (target == 3) GetComponent<SecondActFlow>().PrepareThirdActStartForTesting();
            if (target == 6) fifth.PrepareEpilogueForTesting(selectedEnding == EndingPreset.Endorsed,
                selectedEnding == EndingPreset.Transferred ? FifthActFlow.Custody.Venice : FifthActFlow.Custody.Guild,
                selectedEnding == EndingPreset.Missing);
        }

        bool BeginEpilogue()
        {
            var view = GetComponent<DeskScenePreview>();
            if (!view.BeginStoryView()) return false;
            view.ShowStoryDesktop(); gate.SetStory(true);
            enteringEpilogue = curtain.Begin(6, null, () =>
            {
                enteringEpilogue = false; view.EndStoryView(true);
                if (!GetComponent<EpilogueFlow>().Begin())
                {
                    gate.SetStory(false); Status = "尾声未能开始，请检查尾声组件。"; SetOpen(true);
                }
            });
            if (!enteringEpilogue) { view.EndStoryView(true); gate.SetStory(false); }
            return enteringEpilogue;
        }

        void OnDisable()
        {
            IsOpen = false;
            if (gate) gate.SetConsole(false);
            if (curtain) curtain.SetConsoleVisible(false);
        }

        void OnGUI()
        {
            if (!Available) return;
            if (labelStyle == null) Styles();
            GUI.depth = -1000;
            if (!IsOpen)
            {
                if (!gate.BlocksWorldInput) GUI.Label(new Rect(16, 12, 320, 28), "F1 / `  跳转到各幕开始", labelStyle);
                return;
            }
            var oldMatrix = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1040f, Screen.height / 750f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 960 * scale) * 0.5f,
                (Screen.height - 670 * scale) * 0.5f, 0), Quaternion.identity, Vector3.one * scale);
            var oldColor = GUI.color;
            GUI.color = new Color(0.035f, 0.031f, 0.026f, 0.98f);
            GUI.DrawTexture(new Rect(0, 0, 960, 670), Texture2D.whiteTexture); GUI.color = oldColor;
            GUILayout.BeginArea(new Rect(28, 22, 904, 626));
            GUILayout.BeginHorizontal();
            GUILayout.Label("紫手套 / 章节测试", titleStyle);
            if (GUILayout.Button("关闭 · F1 / Esc", buttonStyle, GUILayout.Width(190))) SetOpen(false);
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            // Switching keeps the current progress; every dialogue, paper and hint re-reads its text.
            GUILayout.BeginHorizontal();
            GUILayout.Label("语言 / Language", labelStyle, GUILayout.Width(220), GUILayout.Height(48));
            bool english = Localization.IsEnglish;
            if (GUILayout.Button("中文", english ? buttonStyle : selectedStyle, GUILayout.Width(200)))
                Localization.SetLanguage(Localization.Language.Chinese);
            if (GUILayout.Button("English", english ? selectedStyle : buttonStyle, GUILayout.Width(200)))
                Localization.SetLanguage(Localization.Language.English);
            GUILayout.EndHorizontal();
            GUILayout.Space(12);
            GUILayout.Label("跳转会重置本次游玩，并准备前几幕的证据、译文和签名。\n目标幕从开场演出开始；可以重复进入同一幕。", labelStyle);
            GUILayout.Space(18);
            bool oldEnabled = GUI.enabled; GUI.enabled = oldEnabled && !IsJumping;
            for (int act = 0; act < 6; act += 2)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(Chapters[act], buttonStyle, GUILayout.Width(446))) JumpToAct(act);
                if (GUILayout.Button(Chapters[act + 1], buttonStyle, GUILayout.Width(446))) JumpToAct(act + 1);
                GUILayout.EndHorizontal(); GUILayout.Space(8);
            }
            GUILayout.Space(12);
            GUILayout.Label("第六幕 · 尾声 / 选择结局", labelStyle);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Endings.Length; i++)
                if (GUILayout.Button(Endings[i], selectedEnding == (EndingPreset)i ? selectedStyle : buttonStyle))
                    selectedEnding = (EndingPreset)i;
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            GUILayout.Label(EndingDetails[(int)selectedEnding], labelStyle);
            if (GUILayout.Button("进入第六幕 · " + Endings[(int)selectedEnding], buttonStyle)) JumpToAct(6, selectedEnding);
            GUI.enabled = oldEnabled;
            GUILayout.Space(18);
            GUILayout.Label(Status, labelStyle);
            GUILayout.EndArea(); GUI.matrix = oldMatrix;
        }

        void Styles()
        {
            labelStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 19, wordWrap = true, richText = false };
            labelStyle.normal.textColor = new Color(0.82f, 0.78f, 0.67f);
            titleStyle = new GUIStyle(labelStyle) { fontSize = 28 };
            buttonStyle = new GUIStyle(GUI.skin.button) { font = font, fontSize = 21, fixedHeight = 48, richText = false };
            selectedStyle = new GUIStyle(buttonStyle);
            selectedStyle.normal.textColor = new Color(1f, 0.80f, 0.40f);
            selectedStyle.normal.background = GUI.skin.button.active.background;
        }
    }
}
