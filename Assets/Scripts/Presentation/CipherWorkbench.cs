using System;
using System.Text;
using UnityEngine;

namespace PurpleGlove
{
    /// <summary>The physical desk owns one active tool and retains each tool's working notes.</summary>
    [DefaultExecutionOrder(-200), DisallowMultipleComponent]
    public sealed partial class CipherWorkbench : MonoBehaviour
    {
        public enum Tool { None = -1, Caesar, Pigpen, SaintCyr, Affine, RailFence }
        public static readonly string[] ToolNames = { "凯撒密码转盘", "猪圈密码表", "维吉尼亚密码 · 圣西尔滑尺", "仿射密码 · 26 × 2 填字表", "栅栏密码纸条" };
        public Font uiFont;
        public SceneObject toolbox;
        public Transform lid, mat, caesarRotor, saintCyrSlide;
        public GameObject[] tools = new GameObject[5];
        public GameObject[] storedTools = new GameObject[5];
        public TextMesh[] affineLetters = new TextMesh[52];
        public TextMesh[] railLetters = new TextMesh[82];
        public TextMesh plaintextInk, keywordInk;
        public Transform letterTray, writingQuill;
        public CipherLetterTile[] letterSamples = new CipherLetterTile[26];
        public AudioClip takeSound, returnSound, paperSound;
        public float slidePitch = 0.0093f;
        public Tool ActiveTool { get; private set; } = Tool.None;
        public bool ToolboxOpen { get; private set; }
        public int CaesarShift { get; private set; }
        public int SlideShift { get; private set; }
        public int KeywordIndex { get; private set; }
        public string Keyword { get; private set; } = "";
        public string Plaintext { get; private set; } = "";
        public string[] Affine { get; } = new string[52];
        public string[] Rails { get; } = new string[82];

        DeskScenePreview view;
        SceneInputGate gate;
        SceneObjectInteractor interactor;
        DeskViewHud viewHud;
        Camera sceneCamera;
        AudioSource sound;
        int editingCell = -1;
        bool writing, typing, pendingFocus, editingKeyword;
        Vector3 quillPosition;
        Quaternion quillRotation;
        Collider quillCollider;
        bool quillColliderEnabled;
        IMECompositionMode previousImeMode;
        public bool IsManipulating => dragTarget || movingTool >= 0;
        bool InWorkspace => view && view.CurrentView == DeskScenePreview.View.DecodeFocus;
        bool Interrupted => (gate && (gate.DialogueActive || gate.ConsoleOpen || gate.DeliveryActive)) || (view && view.DialogueLocked);

        void Awake()
        {
            view = GetComponent<DeskScenePreview>(); gate = GetComponent<SceneInputGate>();
            interactor = GetComponent<SceneObjectInteractor>(); sceneCamera = GetComponent<Camera>();
            viewHud = GetComponent<DeskViewHud>();
            sound = gameObject.AddComponent<AudioSource>(); sound.playOnAwake = false; sound.spatialBlend = 0; sound.volume = 0.42f;
            for (int i = 0; i < Affine.Length; i++) Affine[i] = "";
            for (int i = 0; i < Rails.Length; i++) Rails[i] = "";
            if (writingQuill)
            {
                quillPosition = writingQuill.position; quillRotation = writingQuill.rotation;
                quillCollider = writingQuill.GetComponent<Collider>();
                quillColliderEnabled = quillCollider && quillCollider.enabled;
            }
            RememberToolPoses();
            ApplySelection();
        }

        void Update()
        {
            if (Interrupted)
            {
                if (ToolboxOpen) CloseToolbox();
                if (typing || writing || editingCell >= 0) EndWriting();
                pendingFocus = false;
                pendingDesktop = false;
                CancelManipulation();
                return;
            }
            // Respect the same-frame close of another modal (console/dialogue/inspection).
            if (gate && gate.FrameConsumed) return;
            if (Input.GetKeyDown(KeyCode.Escape) && HandleEscape()) return;
            if (!InWorkspace && !ToolboxOpen && !pendingFocus) { if (typing) EndWriting(); if (IsManipulating) CancelManipulation(); }
            if (pendingDesktop && (!gate || !gate.BlocksWorldInput))
            {
                pendingDesktop = false; view.SetView(DeskScenePreview.View.Desktop);
            }
            if (pendingFocus && (!gate || !gate.BlocksWorldInput))
            {
                pendingFocus = !view.SetView(DeskScenePreview.View.DecodeFocus);
            }
            ProcessPhysicalInput();
        }

        void LateUpdate()
        {
            // A dialogue can start later in Update; release the preview before either object or camera animates.
            if (Interrupted)
            {
                if (ToolboxOpen) CloseToolbox();
                if (typing || writing || editingCell >= 0) EndWriting();
                CancelManipulation();
                return;
            }
            float blend = 1 - Mathf.Exp(-12 * Time.unscaledDeltaTime);
            if (lid) lid.localRotation = Quaternion.Slerp(lid.localRotation, Quaternion.Euler(ToolboxOpen || movingTool >= 0 ? 108 : 0, 0, 0), blend);
            if (caesarRotor) caesarRotor.localRotation = Quaternion.Slerp(caesarRotor.localRotation,
                Quaternion.Euler(0, CaesarShift * 360f / 26, 0), blend);
            if (saintCyrSlide) saintCyrSlide.localPosition = Vector3.Lerp(saintCyrSlide.localPosition,
                new Vector3(-SlideShift * slidePitch, 0, 0), blend);
            AnimateToolTransfer(Time.unscaledDeltaTime);
            UpdateQuill();
        }

        bool pendingDesktop;

        bool HandleEscape()
        {
            bool busy = ToolboxOpen || IsManipulating || typing;
            if (!busy && !InWorkspace) return false;
            if (IsManipulating) CancelManipulation();
            else if (ToolboxOpen) CloseToolbox();
            else if (typing) EndWriting();
            if (gate) gate.ConsumeFrame();
            if (!busy) pendingDesktop = true;
            return true;
        }

        public bool BlocksPointer(Vector2 position) => isActiveAndEnabled && (ToolboxOpen || InWorkspace);

        public void OpenToolbox()
        {
            if (Interrupted || !toolbox || ToolboxOpen || IsManipulating) return;
            EndWriting();
            pendingDesktop = pendingFocus = false;
            if (interactor) interactor.CancelInteraction();
            ToolboxOpen = true;
            var closeHit = lid ? lid.Find("Close physical lid") : null;
            if (closeHit) closeHit.gameObject.SetActive(true);
            toolbox.BeginInspection(sceneCamera);
            toolbox.GetComponent<BoxCollider>().enabled = false;
            if (gate) gate.SetWorkbenchModal(true);
            Play(takeSound);
        }

        public void CloseToolbox()
        {
            bool wasOpen = ToolboxOpen;
            ToolboxOpen = false;
            if (toolbox) toolbox.RestoreImmediately();
            if (toolbox) toolbox.GetComponent<BoxCollider>().enabled = true;
            if (lid) lid.localRotation = Quaternion.identity;
            var closeHit = lid ? lid.Find("Close physical lid") : null;
            if (closeHit) closeHit.gameObject.SetActive(false);
            if (wasOpen && gate) gate.SetWorkbenchModal(false);
        }

        public bool SelectTool(Tool tool)
        {
            int index = (int)tool;
            if (Interrupted || IsManipulating || index < -1 || index >= tools.Length || (index >= 0 && !tools[index])) return false;
            EndWriting();
            CloseToolbox();
            pendingFocus = true;
            requestedTool = tool;
            if (ActiveTool == tool) return true;
            if (ActiveTool != Tool.None) StartToolTransfer((int)ActiveTool, true);
            else if (tool != Tool.None) StartToolTransfer(index, false);
            return true;
        }

        void ApplySelection()
        {
            for (int i = 0; i < tools.Length; i++)
            {
                if (tools[i]) tools[i].SetActive((int)ActiveTool == i);
                if (i < storedTools.Length && storedTools[i]) storedTools[i].SetActive((int)ActiveTool != i);
            }
        }

        public void FocusWorkspace()
        {
            if (Interrupted) return;
            if (interactor) interactor.ClearHover();
            if (!view.SetView(DeskScenePreview.View.DecodeFocus)) pendingFocus = true;
        }

        public static int Wrap(int value) => (value % 26 + 26) % 26;
        public void SetCaesarShift(int value) => CaesarShift = Wrap(value);
        public void SetSlideShift(int value) => SlideShift = Wrap(value);
        public static string LettersOnly(string value, int limit)
        {
            var result = new StringBuilder();
            foreach (char c in (value ?? "").ToUpperInvariant())
                if (c >= 'A' && c <= 'Z' && result.Length < limit) result.Append(c);
            return result.ToString();
        }
        public void SetKeyword(string value)
        {
            Keyword = LettersOnly(value, 24); KeywordIndex = 0;
            if (Keyword.Length > 0) SetSlideShift(Keyword[0] - 'A');
            RefreshKeyword();
        }
        public void StepKeyword(int direction)
        {
            if (Keyword.Length == 0) return;
            KeywordIndex = (KeywordIndex + direction % Keyword.Length + Keyword.Length) % Keyword.Length;
            SetSlideShift(Keyword[KeywordIndex] - 'A');
            RefreshKeyword();
        }
        void RefreshKeyword()
        {
            if (keywordInk) keywordInk.text = Keyword.Length == 0 ? "________________________" :
                Keyword + "\n" + new string(' ', KeywordIndex) + "^";
        }
        public void SetCell(Tool tool, int index, string value)
        {
            string[] data = tool == Tool.Affine ? Affine : tool == Tool.RailFence ? Rails : null;
            TextMesh[] ink = tool == Tool.Affine ? affineLetters : railLetters;
            if (data == null || index < 0 || index >= data.Length) return;
            data[index] = LettersOnly(value, 1);
            if (index < ink.Length && ink[index]) ink[index].text = data[index];
        }
        public void SetPlaintext(string value)
        {
            Plaintext = (value ?? "").Replace("\r", "");
            if (Plaintext.Length > 600) Plaintext = Plaintext.Substring(0, 600);
            if (!plaintextInk) return;
            // Show the latest six lines directly on the paper; the complete draft remains in Plaintext.
            var visible = new StringBuilder(); int width = 0, line = 0;
            foreach (char c in Plaintext)
            {
                if (c == '\n' || width + (c > 255 ? 2 : 1) > 48)
                {
                    ++line;
                    visible.Append('\n'); width = 0;
                    if (c == '\n') continue;
                }
                visible.Append(c); width += c > 255 ? 2 : 1;
            }
            string[] lines = visible.ToString().Split('\n');
            plaintextInk.text = string.Join("\n", lines, Mathf.Max(0, lines.Length - 6), Mathf.Min(6, lines.Length));
        }
        void EndWriting()
        {
            editingCell = -1; writing = false; editingKeyword = false;
            if (typing)
            {
                if (gate) gate.SetWorkbenchTyping(false);
                Input.imeCompositionMode = previousImeMode;
            }
            typing = false;
            if (quillCollider) quillCollider.enabled = quillColliderEnabled;
            if (writingQuill) writingQuill.SetPositionAndRotation(quillPosition, quillRotation);
        }
        void BeginTyping()
        {
            if (!typing) previousImeMode = Input.imeCompositionMode;
            typing = true; Input.imeCompositionMode = IMECompositionMode.On;
            if (quillCollider) quillCollider.enabled = false;
            if (gate && !gate.WorkbenchTyping) gate.SetWorkbenchTyping(true);
        }
        void Play(AudioClip clip) { if (sound && clip) sound.PlayOneShot(clip); }
        void OnDisable()
        {
            CloseToolbox(); EndWriting(); pendingFocus = pendingDesktop = false;
            CancelManipulation();
        }
        void OnApplicationFocus(bool focused)
        {
            if (!focused) { CancelManipulation(); EndWriting(); CloseToolbox(); }
        }
    }
}
