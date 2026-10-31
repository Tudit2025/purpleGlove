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
        public bool IsClosing { get; private set; }
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
        bool previousShutterStop, shutterCaptured, shutterOpeningStarted, paused, inPlace;
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

        public bool Play(DialogueEvent sequence) => Begin(sequence, false);
        // A story cue may already have played the knock while waiting for another line to finish.
        public bool PlayAfterKnock(DialogueEvent sequence) => Begin(sequence, false, false, false);

        // Scripted monologues reuse the text/voice pipeline while their caller owns the camera and door.
        public bool PlayInPlace(DialogueEvent sequence, bool keepInspection = false) => Begin(sequence, true, keepInspection);

        bool Begin(DialogueEvent sequence, bool keepCamera, bool keepInspection = false, bool playArrivalSound = true)
        {
            if (!isActiveAndEnabled || IsActive || !sequence || !sequence.TryValidate(out _)
                || !view || !interactor || !Overlay || !Voice || !gate
                || gate.DeliveryActive || view.DeliveryLocked) return false;
            if (!keepCamera && (gate.StoryActive || !visitor || !eyeShutter || eyeShutter.IsMoving)) return false;
            inPlace = keepCamera;
            ActiveEvent = sequence; lineIndex = -1;
            if (!keepInspection) interactor.CancelInteraction();
            gate.SetDialogue(true);
            if (!inPlace)
            {
                view.SetDialogueLock(true);
                previousShutterStop = eyeShutter.AtSecondStop; shutterCaptured = true;
                shutterOpeningStarted = false;
                visitor.Show(sequence.initialVisitor);
            }
            if (playArrivalSound && arrivalAudio && sequence.arrivalSound) arrivalAudio.PlayOneShot(sequence.arrivalSound);
            arrivalRemaining = inPlace ? sequence.arrivalDelay : Mathf.Max(sequence.arrivalDelay, eyeShutter.travelSeconds);
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
            if (IsClosing)
            {
                if (eyeShutter && !eyeShutter.isActiveAndEnabled)
                    eyeShutter.SetPoseImmediately(ActiveEvent.closeShutterOnComplete ? false : previousShutterStop);
                if (!eyeShutter || !eyeShutter.IsMoving) Complete(true);
                return;
            }
            if (lineIndex < 0)
            {
                arrivalRemaining -= seconds;
                if (!inPlace && !shutterOpeningStarted)
                {
                    // Let the player see the entire opening after the camera has settled on the door.
                    if (view.IsEnteringDoorway) return;
                    eyeShutter.MoveTo(true);
                    shutterOpeningStarted = true;
                }
                if (arrivalRemaining <= 0 && (inPlace || !eyeShutter.IsMoving)) ShowLine(0);
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
            Overlay.Reveal(VisibleLength);
            if (!IsTyping) Voice.Stop();
        }

        public void Advance()
        {
            if (!IsActive || lineIndex < 0) return;
            if (IsTyping)
            {
                visibleElements = textElements.Length;
                Voice.Stop(); Overlay.Reveal(VisibleLength);
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
            if (!inPlace && !line.speaker.isProtagonist) visitor.Show(line.speaker);
            Voice.BeginLine(line.speaker); Overlay.Show(line.speaker, line.text);
            gate.ConsumeFrame();
        }

        public void Stop() => Finish(false);
        void Finish(bool completed)
        {
            if (!IsActive) return;
            Voice.Stop(); Overlay.Hide();
            if (arrivalAudio) arrivalAudio.Stop();
            if (eyeShutter && shutterCaptured)
            {
                bool endStop = completed && ActiveEvent.closeShutterOnComplete ? false : previousShutterStop;
                eyeShutter.MoveTo(endStop, true);
                if (!eyeShutter.isActiveAndEnabled) eyeShutter.SetPoseImmediately(endStop);
                if (completed && eyeShutter.IsMoving)
                {
                    // Hold the door shot and visitor until the panel has finished closing.
                    IsClosing = true; lineIndex = -1;
                    return;
                }
            }
            Complete(completed);
        }

        void Complete(bool completed)
        {
            string id = ActiveEvent.id;
            ActiveEvent = null; lineIndex = -1; IsClosing = false;
            if (!inPlace && visitor) visitor.Show(null);
            shutterCaptured = false;
            gate.SetDialogue(false);
            if (!inPlace && view) view.SetDialogueLock(false);
            inPlace = false;
            if (completed) eventCompleted.Invoke(id); else eventCancelled.Invoke(id);
        }
        void OnDisable() => Finish(false);
    }
}
