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
        public UnityEvent<string, string> choiceSelected = new UnityEvent<string, string>();
        public DialogueEvent ActiveEvent { get; private set; }
        public bool IsActive => ActiveEvent;
        public bool IsClosing { get; private set; }
        public bool IsChoosing => choicesVisible && CurrentLine != null && !IsClosing && !IsChangingVisitor;
        public bool IsChangingVisitor => visitorChange != VisitorChange.None;
        public string SelectedChoiceId { get; private set; }
        public bool IsTyping => IsActive && lineIndex >= 0 && visibleElements < textElements.Length;
        public int LineIndex => lineIndex;
        public string VisibleText => lineIndex < 0 ? string.Empty : lineText.Substring(0, VisibleLength);
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
        enum VisitorChange { None, Closing, Opening }
        VisitorChange visitorChange;
        int pendingLine = -1;
        DialogueSpeaker pendingVisitor;
        bool choicesVisible;
        // The current line as shown: authored Chinese or its English translation.
        string lineText = string.Empty;
        // English carries two to three times as many letters per line as Chinese.
        const float EnglishTypingRate = 2f;
        int VisibleLength => visibleElements >= textElements.Length ? lineText.Length : textElements[visibleElements];

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
            ActiveEvent = sequence; lineIndex = -1; SelectedChoiceId = null;
            choicesVisible = false; visitorChange = VisitorChange.None; pendingLine = -1; pendingVisitor = null;
            if (!keepInspection) interactor.CancelInteraction();
            gate.SetDialogue(true);
            if (!inPlace)
            {
                view.SetDialogueLock(true);
                previousShutterStop = eyeShutter.AtSecondStop; shutterCaptured = true;
                shutterOpeningStarted = false;
                // The first actual visitor may be the clerk introducing the named guest.
                var firstVisitor = Array.Find(sequence.lines, line => !line.speaker.isProtagonist)?.speaker
                    ?? sequence.initialVisitor;
                if (eyeShutter.AtSecondStop && visitor.CurrentVisitor != firstVisitor)
                    ChangeVisitor(0, firstVisitor);
                else visitor.Show(firstVisitor);
            }
            if (playArrivalSound && arrivalAudio && sequence.arrivalSound) arrivalAudio.PlayOneShot(sequence.arrivalSound);
            arrivalRemaining = inPlace ? sequence.arrivalDelay : Mathf.Max(sequence.arrivalDelay, eyeShutter.travelSeconds);
            paused = false;
            if (inPlace && arrivalRemaining <= 0) ShowLine(0);
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
            if (IsChoosing)
            {
                bool canPoint = !gate.BlocksDialogueInput && Input.mousePresent && Cursor.lockState != CursorLockMode.Locked;
                ProcessChoicePointer(Input.mousePosition, canPoint, Input.GetMouseButtonDown(0));
                return;
            }
            if (!gate.BlocksDialogueInput && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetMouseButtonDown(0))) Advance();
        }

        // Deterministic clock entry point also permits frame-rate-independent verification.
        void AdvanceTime(float seconds)
        {
            if (!IsActive || gate.ConsoleOpen) return;
            if (IsClosing)
            {
                if (eyeShutter && !eyeShutter.isActiveAndEnabled)
                    eyeShutter.SetPoseImmediately(ActiveEvent.closeShutterOnComplete ? false : previousShutterStop);
                if (!eyeShutter || !eyeShutter.IsMoving) Complete(true);
                return;
            }
            if (IsChangingVisitor)
            {
                arrivalRemaining -= Mathf.Max(0, seconds);
                AdvanceVisitorChange();
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
                char character = lineText[textElements[visibleElements++]];
                Voice.Tick(character);
                float rate = speedOverride > 0 ? speedOverride : CurrentLine.speaker.charactersPerSecond;
                if (Localization.IsEnglish) rate *= EnglishTypingRate;
                float punctuation = "，、；：,;:".IndexOf(character) >= 0 ? 2.8f
                    : "。！？.!?…".IndexOf(character) >= 0 ? 5 : 1;
                nextCharacter += punctuation / Mathf.Clamp(rate, 8, 60);
            }
            Overlay.Reveal(VisibleLength);
            if (!IsTyping) Voice.Stop();
        }

        public void Advance()
        {
            if (!IsActive || lineIndex < 0 || IsChoosing || IsClosing || IsChangingVisitor) return;
            if (IsTyping)
            {
                visibleElements = textElements.Length;
                Voice.Stop(); Overlay.Reveal(VisibleLength);
            }
            else if (CurrentLine.HasChoices) ShowCurrentChoices();
            else if (CurrentLine.endEvent) Finish(true);
            else if (lineIndex + 1 < ActiveEvent.lines.Length) ShowLine(lineIndex + 1);
            else Finish(true);
        }

        void ProcessChoicePointer(Vector2 pointer, bool canPoint, bool clicked)
        {
            int index = canPoint ? Overlay.ChoiceAtPointer(pointer) : -1;
            Overlay.HighlightChoice(index);
            if (clicked && index >= 0) SelectChoice(index);
        }

        public bool SelectChoice(int index)
        {
            if (!isActiveAndEnabled || !IsChoosing || gate.BlocksDialogueInput
                || index < 0 || index >= CurrentLine.choices.Length) return false;
            var choice = CurrentLine.choices[index];
            string id = ActiveEvent.id;
            SelectedChoiceId = choice.id;
            Voice.Stop(); gate.ConsumeFrame();
            if (choice.nextLine < 0) Finish(true); else ShowLine(choice.nextLine);
            choiceSelected.Invoke(id, choice.id);
            return true;
        }

        void ShowLine(int index)
        {
            var next = ActiveEvent.lines[index];
            if (!inPlace && !next.speaker.isProtagonist && visitor.CurrentVisitor != next.speaker)
            {
                arrivalRemaining = 0;
                ChangeVisitor(index, next.speaker);
                return;
            }
            PresentLine(index);
        }

        void ChangeVisitor(int index, DialogueSpeaker next)
        {
            Voice.Stop(); Overlay.Hide(); choicesVisible = false;
            lineIndex = -1; pendingLine = index; pendingVisitor = next;
            visitorChange = VisitorChange.Closing;
            eyeShutter.MoveTo(false, true);
            if (!eyeShutter.isActiveAndEnabled) eyeShutter.SetPoseImmediately(false);
            gate.ConsumeFrame();
        }

        void AdvanceVisitorChange()
        {
            if (!eyeShutter.isActiveAndEnabled)
                eyeShutter.SetPoseImmediately(visitorChange == VisitorChange.Opening);
            if (visitorChange == VisitorChange.Closing)
            {
                if (eyeShutter.IsMoving || view.IsEnteringDoorway) return;
                // Change the portrait only behind a fully closed panel.
                if (eyeShutter.AtSecondStop) { eyeShutter.MoveTo(false, true); return; }
                visitor.Show(pendingVisitor);
                visitorChange = VisitorChange.Opening;
                eyeShutter.MoveTo(true);
                if (!eyeShutter.isActiveAndEnabled) eyeShutter.SetPoseImmediately(true);
                return;
            }
            if (eyeShutter.IsMoving || !eyeShutter.AtSecondStop || arrivalRemaining > 0) return;
            int index = pendingLine;
            pendingLine = -1; pendingVisitor = null; visitorChange = VisitorChange.None;
            PresentLine(index);
        }

        void PresentLine(int index)
        {
            lineIndex = index;
            var line = CurrentLine;
            lineText = Localization.Text(line.text);
            textElements = StringInfo.ParseCombiningCharacters(lineText);
            visibleElements = 0; nextCharacter = 0;
            choicesVisible = false;
            // Luca's confirmation prompts stay instant; a visitor must speak normally first.
            if (line.HasChoices && line.speaker.isProtagonist) ShowCurrentChoices();
            else { Voice.BeginLine(line.speaker); Overlay.Show(line.speaker, lineText); }
            gate.ConsumeFrame();
        }

        // A language switch from the console shows the current sentence complete in the new language.
        void Relocalize()
        {
            if (lineIndex < 0 || CurrentLine == null || IsChangingVisitor) return;
            lineText = Localization.Text(CurrentLine.text);
            textElements = StringInfo.ParseCombiningCharacters(lineText);
            if (choicesVisible) { ShowCurrentChoices(); return; }
            visibleElements = textElements.Length;
            Voice.Stop(); Overlay.Show(CurrentLine.speaker, lineText); Overlay.Reveal(VisibleLength);
        }

        void ShowCurrentChoices()
        {
            var line = CurrentLine;
            choicesVisible = true; visibleElements = textElements.Length;
            Voice.Stop();
            if (arrivalAudio) arrivalAudio.Stop();
            var responder = line.speaker.isProtagonist ? line.speaker
                : Array.Find(speakers, speaker => speaker && speaker.isProtagonist) ?? line.speaker;
            Overlay.ShowChoices(responder, line.speaker.isProtagonist ? lineText : string.Empty, line.choices);
            gate.ConsumeFrame();
        }

        public void Stop() => Finish(false);
        void Finish(bool completed)
        {
            if (!IsActive) return;
            choicesVisible = false; visitorChange = VisitorChange.None; pendingLine = -1; pendingVisitor = null;
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
            if (!inPlace && visitor && (!completed || !eyeShutter || !eyeShutter.AtSecondStop)) visitor.Show(null);
            shutterCaptured = false;
            gate.SetDialogue(false);
            if (!inPlace && view) view.SetDialogueLock(false);
            inPlace = false;
            if (completed) eventCompleted.Invoke(id); else eventCancelled.Invoke(id);
        }
        void OnEnable() => Localization.Changed += Relocalize;
        void OnDisable() { Localization.Changed -= Relocalize; Finish(false); }
    }
}
