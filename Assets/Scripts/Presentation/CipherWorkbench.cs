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
        public static readonly string[] ToolNames = { "凯撒密码转盘", "猪圈密码表", "维吉尼亚密码 · 圣西尔滑尺", "仿射密码 · 参数对照表", "栅栏密码纸条" };
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
        bool typing, pendingFocus, editingKeyword;
        Vector3 quillPosition;
        Quaternion quillRotation;
        Collider quillCollider;
        bool quillColliderEnabled;
        IMECompositionMode previousImeMode;
        public bool IsManipulating => dragTarget || movingTool >= 0;
        bool InWorkspace => view && view.CurrentView == DeskScenePreview.View.DecodeFocus;
        bool Interrupted => (gate && (gate.StoryActive || gate.DialogueActive || gate.ConsoleOpen || gate.DeliveryActive)) || (view && view.DialogueLocked);

        void Awake()
        {
            view = GetComponent<DeskScenePreview>(); gate = GetComponent<SceneInputGate>();
            interactor = GetComponent<SceneObjectInteractor>(); sceneCamera = GetComponent<Camera>();
            viewHud = GetComponent<DeskViewHud>();
            sound = gameObject.AddComponent<AudioSource>(); sound.playOnAwake = false; sound.spatialBlend = 0; sound.volume = 0.42f;
            RefreshAffineTable();
            SetRailCiphertext("");
            if (writingQuill)
            {
                quillPosition = writingQuill.position; quillRotation = writingQuill.rotation;
                quillCollider = writingQuill.GetComponent<Collider>();
                quillColliderEnabled = quillCollider && quillCollider.enabled;
            }
            RememberToolPoses();
            InitializeKeyboardTray();
            ApplySelection();
        }

        void Update()
        {
            if (BookTool && (Interrupted || !InWorkspace || BookTool.archive.DeskAct != 2)) CloseBook();
            if (Interrupted)
            {
                if (ToolboxOpen) CloseToolbox();
                if (typing || editingCell >= 0) EndWriting();
                pendingFocus = false;
                pendingDesktop = false;
                CancelManipulation();
                SuspendDecodeKeyboard();
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
            UpdateDecodeKeyboard();
        }

        void LateUpdate()
        {
            if (BookTool && (Interrupted || !InWorkspace)) CloseBook();
            // A dialogue can start later in Update; release the preview before either object or camera animates.
            if (Interrupted)
            {
                if (ToolboxOpen) CloseToolbox();
                if (typing || editingCell >= 0) EndWriting();
                CancelManipulation();
                SuspendDecodeKeyboard();
                return;
            }
            float blend = 1 - Mathf.Exp(-12 * Time.unscaledDeltaTime);
            if (lid) lid.localRotation = Quaternion.Slerp(lid.localRotation, Quaternion.Euler(ToolboxOpen || movingTool >= 0 ? 108 : 0, 0, 0), blend);
            if (caesarRotor) caesarRotor.localRotation = Quaternion.Slerp(caesarRotor.localRotation,
                Quaternion.Euler(0, CaesarShift * 360f / 26, 0), blend);
            if (saintCyrSlide) saintCyrSlide.localPosition = Vector3.Lerp(saintCyrSlide.localPosition,
                new Vector3(-SlideShift * slidePitch, 0, 0), blend);
            AnimateToolTransfer(Time.unscaledDeltaTime);
            AnimateKeyboardTray(Time.unscaledDeltaTime);
            AnimateLetterStamps(Time.unscaledDeltaTime);
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
            CloseBook();
            var memory = toolbox.GetComponent<FirstInteractionMemory>();
            if (memory && memory.TryBegin(OpenToolbox)) return;
            EndWriting();
            SuspendDecodeKeyboard(); decodedStrokes.Clear();
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
            CloseBook();
            EndWriting();
            decodedStrokes.Clear();
            CloseToolbox();
            pendingFocus = true;
            requestedTool = tool;
            railInputRequested = tool == Tool.RailFence;
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
            RefreshKeyboardMapping();
        }

        public void FocusWorkspace()
        {
            if (Interrupted) return;
            if (interactor) interactor.ClearHover();
            if (!view.SetView(DeskScenePreview.View.DecodeFocus)) pendingFocus = true;
        }

        public static int Wrap(int value) => (value % 26 + 26) % 26;
        public void SetCaesarShift(int value)
        {
            int next = Wrap(value);
            if (next != CaesarShift) caesarConfigured = true;
            CaesarShift = next; RefreshKeyboardMapping();
        }
        public void SetSlideShift(int value) { SlideShift = Wrap(value); RefreshKeyboardMapping(); }
        public static string LettersOnly(string value, int limit)
        {
            var result = new StringBuilder();
            foreach (char c in (value ?? "").ToUpperInvariant())
                if (c >= 'A' && c <= 'Z' && result.Length < limit) result.Append(c);
            return result.ToString();
        }
        public void SetKeyword(string value)
        {
            decodedStrokes.Clear();
            Keyword = LettersOnly(value, 24); KeywordIndex = 0;
            if (Keyword.Length > 0) SetSlideShift(Keyword[0] - 'A');
            else RefreshKeyboardMapping();
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
            // Affine cells are derived from A/B and never accept manual edits or letter drops.
            if (tool != Tool.RailFence || index < 0 || index >= Rails.Length) return;
            Rails[index] = LettersOnly(value, 1);
            if (index < railLetters.Length && railLetters[index]) railLetters[index].text = Rails[index];
        }
        public void SetPlaintext(string value)
        {
            if (!applyingDecodedText) { decodedStrokes.Clear(); ClearLetterStamps(); }
            Plaintext = (value ?? "").Replace("\r", "");
            if (Plaintext.Length > 600) Plaintext = Plaintext.Substring(0, 600);
            RefreshPlaintextInk();
        }
        string FormatPlaintext(string text)
        {
            // Use actual glyph advances: larger lettering must still fit the physical writing slip.
            var visible = new StringBuilder(); float width = 0;
            var font = plaintextInk ? plaintextInk.font : null;
            int size = plaintextInk ? plaintextInk.fontSize : 96;
            float unit = plaintextInk ? plaintextInk.characterSize / 10 : 0.00021f;
            var style = plaintextInk ? plaintextInk.fontStyle : FontStyle.Normal;
            if (font) font.RequestCharactersInTexture(text, size, style);
            foreach (char c in text)
            {
                float advance = font && font.GetCharacterInfo(c, out var glyph, size, style) ? glyph.advance * unit : size * unit;
                if (c == '\n' || (width > 0 && width + advance > 0.55f))
                {
                    visible.Append('\n'); width = 0;
                    if (c == '\n') continue;
                }
                visible.Append(c); width += advance;
            }
            string[] lines = visible.ToString().Split('\n');
            return string.Join("\n", lines, Mathf.Max(0, lines.Length - 3), Mathf.Min(3, lines.Length));
        }
        void EndWriting()
        {
            editingCell = -1; editingKeyword = false;
            editingAffineParameter = -1; replaceAffineInput = false;
            RefreshAffineParameterInk();
            editingRailCipher = railInputRequested = replaceRailInput = false;
            RefreshRailInputInk();
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
            ReleaseDecoderIme(); ClearLetterStamps();
            if (!typing) previousImeMode = Input.imeCompositionMode;
            typing = true; Input.imeCompositionMode = IMECompositionMode.On;
            if (quillCollider) quillCollider.enabled = false;
            if (gate && !gate.WorkbenchTyping) gate.SetWorkbenchTyping(true);
        }
        void Play(AudioClip clip) { if (sound && clip) sound.PlayOneShot(clip); }
        void OnDisable()
        {
            CloseBook();
            CloseToolbox(); EndWriting(); pendingFocus = pendingDesktop = false;
            CancelManipulation();
            SuspendDecodeKeyboard();
        }
        void OnApplicationFocus(bool focused)
        {
            if (!focused) { CancelManipulation(); EndWriting(); CloseToolbox(); SuspendDecodeKeyboard(); }
        }
    }
}
