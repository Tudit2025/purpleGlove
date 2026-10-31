using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Events;

namespace PurpleGlove
{
    [DefaultExecutionOrder(-200), DisallowMultipleComponent]
    [RequireComponent(typeof(SceneInputGate), typeof(DialogueVoice), typeof(DialogueOverlay))]
    public sealed class DialogueController : MonoBehaviour
    {
        public DialogueSpeaker[] speakers = Array.Empty<DialogueSpeaker>();
        public DialogueEvent[] events = Array.Empty<DialogueEvent>();
        public VisitorEyes visitor;
        public DoorMechanism eyeShutter;
        public AudioSource arrivalAudio;
        [Tooltip("Zero uses each speaker's own rate; the console can override it for testing.")]
        [Range(0, 60)] public float speedOverride;
        public UnityEvent<string> eventCompleted = new UnityEvent<string>();
        public UnityEvent<string> eventCancelled = new UnityEvent<string>();
        public DialogueEvent ActiveEvent { get; private set; }
        public bool IsActive => ActiveEvent;
        public bool IsTyping => IsActive && lineIndex >= 0 && visibleElements < textElements.Length;
        public int LineIndex => lineIndex;
        public string VisibleText => lineIndex < 0 ? string.Empty : CurrentLine.text.Substring(0, VisibleLength);
        public DialogueEvent.Line CurrentLine => IsActive && lineIndex >= 0 ? ActiveEvent.lines[lineIndex] : null;
        public DialogueVoice Voice { get; private set; }
        public DialogueOverlay Overlay { get; private set; }

        SceneInputGate gate;
        SceneObjectInteractor interactor;
        DeskScenePreview view;
        int lineIndex = -1, visibleElements;
        int[] textElements = Array.Empty<int>();
        float arrivalRemaining, nextCharacter;
        bool previousShutterStop, shutterCaptured, paused;
        int VisibleLength => visibleElements >= textElements.Length ? CurrentLine.text.Length : textElements[visibleElements];

        void Awake()
        {
            gate = GetComponent<SceneInputGate>(); interactor = GetComponent<SceneObjectInteractor>();
            view = GetComponent<DeskScenePreview>(); Voice = GetComponent<DialogueVoice>();
            Overlay = GetComponent<DialogueOverlay>();
        }

        public bool Play(string id)
        {
            foreach (var sequence in events) if (sequence && string.Equals(sequence.id, id, StringComparison.OrdinalIgnoreCase))
                return Play(sequence);
            return false;
        }
        public void PlayEvent(string id) => Play(id);

        public bool Play(DialogueEvent sequence)
        {
            if (!isActiveAndEnabled || IsActive || !sequence || !sequence.TryValidate(out _)
                || !view || !interactor || !Overlay || !Voice || !gate || !visitor || !eyeShutter
                || eyeShutter.IsMoving || gate.DeliveryActive || view.DeliveryLocked) return false;
            ActiveEvent = sequence; lineIndex = -1;
            interactor.CancelInteraction();
            gate.SetDialogue(true); view.SetDialogueLock(true);
            previousShutterStop = eyeShutter.AtSecondStop; shutterCaptured = true;
            eyeShutter.MoveTo(true);
            visitor.Show(sequence.initialVisitor);
            if (arrivalAudio && sequence.arrivalSound) arrivalAudio.PlayOneShot(sequence.arrivalSound);
            arrivalRemaining = Mathf.Max(sequence.arrivalDelay, eyeShutter.travelSeconds);
            paused = false;
            return true;
        }

        void Update()
        {
            if (!IsActive) return;
            bool pause = gate.ConsoleOpen || !Application.isFocused;
            if (pause)
            {
                if (!paused) { Voice.Stop(); if (arrivalAudio) arrivalAudio.Pause(); }
                paused = true; return;
            }
            if (paused) { if (arrivalAudio) arrivalAudio.UnPause(); paused = false; }
            AdvanceTime(Time.unscaledDeltaTime);
            if (!gate.BlocksDialogueInput && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetMouseButtonDown(0))) Advance();
        }

        // Deterministic clock entry point also permits frame-rate-independent verification.
        void AdvanceTime(float seconds)
        {
            if (!IsActive) return;
            if (lineIndex < 0)
            {
                arrivalRemaining -= seconds;
                if (arrivalRemaining <= 0 && !eyeShutter.IsMoving) ShowLine(0);
                return;
            }
            if (!IsTyping) return;
            nextCharacter -= seconds;
            while (nextCharacter <= 0 && IsTyping)
            {
                char character = CurrentLine.text[textElements[visibleElements++]];
                Voice.Tick(character);
                float rate = speedOverride > 0 ? speedOverride : CurrentLine.speaker.charactersPerSecond;
                float punctuation = "，、；：,;:".IndexOf(character) >= 0 ? 2.8f
                    : "。！？.!?…".IndexOf(character) >= 0 ? 5 : 1;
                nextCharacter += punctuation / Mathf.Clamp(rate, 8, 60);
            }
            Overlay.Reveal(VisibleLength, !IsTyping);
            if (!IsTyping) Voice.Stop();
        }

        public void Advance()
        {
            if (!IsActive || lineIndex < 0) return;
            if (IsTyping)
            {
                visibleElements = textElements.Length;
                Voice.Stop(); Overlay.Reveal(VisibleLength, true);
            }
            else if (lineIndex + 1 < ActiveEvent.lines.Length) ShowLine(lineIndex + 1);
            else Finish(true);
        }

        void ShowLine(int index)
        {
            lineIndex = index;
            var line = CurrentLine;
            textElements = StringInfo.ParseCombiningCharacters(line.text);
            visibleElements = 0; nextCharacter = 0;
            if (!line.speaker.isProtagonist) visitor.Show(line.speaker);
            Voice.BeginLine(line.speaker); Overlay.Show(line.speaker, line.text);
            gate.ConsumeFrame();
        }

        public void Stop() => Finish(false);
        void Finish(bool completed)
        {
            if (!IsActive) return;
            string id = ActiveEvent.id;
            ActiveEvent = null; lineIndex = -1;
            Voice.Stop(); Overlay.Hide();
            if (arrivalAudio) arrivalAudio.Stop();
            if (visitor) visitor.Show(null);
            // Restore even if cancelled during the opening animation.
            if (eyeShutter && shutterCaptured) eyeShutter.SetPoseImmediately(previousShutterStop);
            shutterCaptured = false;
            gate.SetDialogue(false);
            if (view) view.SetDialogueLock(false);
            if (completed) eventCompleted.Invoke(id); else eventCancelled.Invoke(id);
        }
        void OnDisable() => Finish(false);
    }
}
