using System.Linq;
using UnityEngine;

namespace PurpleGlove
{
    /// <summary>First-act arrival and earned ending, using the existing dialogue and sealed deliveries.</summary>
    [DefaultExecutionOrder(-100), DisallowMultipleComponent]
    public sealed class FirstActFlow : MonoBehaviour
    {
        public enum Phase { Idle, Delay, VisitPending, Visit, ReturnPending, Returning, Summary,
            TransitionPending, Transition, SecondIntroPending, SecondIntro, DeliveryPending, Delivery, Complete, Suspended,
            OpeningDelay, OpeningVisitPending, OpeningVisit, OpeningDeliveryPending, OpeningDelivery }
        public PlaintextArchiveController records;
        public ActArchiveController archive;
        public DialogueController dialogue;
        public PrologueSequence story;
        public SealedDeliveryController delivery;
        public DialogueEvent firstVisit, deskSummary, secondActIntro;
        public DialogueEvent openingVisit;
        public AudioSource knockAudio;
        [Min(0)] public float openingDelay = 5;
        [Min(0)] public float knockDelay = 5;
        [Min(0.1f)] public float returnSeconds = 0.75f;
        public Phase State { get; private set; }
        public bool IsRunning => State != Phase.Idle && State != Phase.Complete && State != Phase.Suspended;
        public float DelayElapsed { get; private set; }
        public bool KnockPlayed { get; private set; }
        DeskScenePreview view;
        SceneInputGate gate;
        Phase resumeAt;
        bool ownsDeskView, storedForTransition, paused, openingStarted;
        float elapsed, knockRemaining, startFov, endFov;
        Vector3 startPosition, endPosition;
        Quaternion startRotation, endRotation;

        void Awake() { view = GetComponent<DeskScenePreview>(); gate = GetComponent<SceneInputGate>(); }
        void OnEnable()
        {
            if (records) records.plaintextPreserved.AddListener(Preserved);
            if (dialogue)
            {
                dialogue.eventCompleted.AddListener(DialogueFinished);
                dialogue.eventCancelled.AddListener(DialogueCancelled);
            }
            if (story)
            {
                story.actPrepared.AddListener(ActPrepared);
                story.actEntered.AddListener(ActEntered);
                story.sequenceCancelled.AddListener(TransitionCancelled);
            }
            if (delivery)
            {
                delivery.deliveryCompleted.AddListener(Delivered);
                delivery.deliveryCancelled.AddListener(DeliveryCancelled);
            }
            if (records && records.entries.Any(e => e.act == 1 && e.saved)) Preserved(1);
        }

        internal void PrepareSecondActStartForTesting() { State = Phase.SecondIntroPending; }

        void Preserved(int act)
        {
            if (State != Phase.Idle || act != 1 || !archive || archive.CurrentAct != 1) return;
            DelayElapsed = 0; KnockPlayed = false; State = Phase.Delay;
        }
        bool Ready => records && archive && archive.isActiveAndEnabled && dialogue && dialogue.isActiveAndEnabled
            && story && story.isActiveAndEnabled && delivery && delivery.isActiveAndEnabled
            && view && view.isActiveAndEnabled && gate && firstVisit && deskSummary && secondActIntro && knockAudio;
        bool Busy => dialogue.IsActive || story.IsActive || delivery.IsActive || gate.StoryActive || gate.PropActionActive || gate.FrameConsumed;

        void Update()
        {
            if (!IsRunning) return;
            if (!Ready) { Stop(); return; }
            bool pause = gate.ConsoleOpen || !Application.isFocused;
            if (pause)
            {
                if (!paused) knockAudio.Pause(); paused = true; return;
            }
            if (paused) { knockAudio.UnPause(); paused = false; }
            AdvanceTime(Time.unscaledDeltaTime);
        }

        void AdvanceTime(float seconds)
        {
            if (!IsRunning || !Ready || gate.ConsoleOpen) return;
            seconds = Mathf.Max(0, seconds);
            switch (State)
            {
                case Phase.OpeningDelay:
                    DelayElapsed += seconds;
                    if (DelayElapsed < openingDelay) break;
                    PlayKnock(openingVisit);
                    State = Phase.OpeningVisitPending;
                    break;
                case Phase.OpeningVisitPending:
                    knockRemaining -= seconds;
                    if (knockRemaining <= 0 && !Busy && dialogue.PlayAfterKnock(openingVisit)) State = Phase.OpeningVisit;
                    break;
                case Phase.OpeningDeliveryPending:
                    if (archive.IsAvailable(1)) { State = Phase.Idle; break; }
                    if (!Busy && !dialogue.eyeShutter.IsMoving && !dialogue.eyeShutter.AtSecondStop
                        && delivery.BeginDelivery(1)) State = Phase.OpeningDelivery;
                    break;
                case Phase.Delay:
                    DelayElapsed += seconds;
                    if (DelayElapsed < knockDelay) break;
                    PlayKnock(firstVisit);
                    State = Phase.VisitPending;
                    break;
                case Phase.VisitPending:
                    knockRemaining -= seconds;
                    // Knock on time, but let the player finish the existing completion line before changing dialogue.
                    if (knockRemaining <= 0 && !Busy && dialogue.PlayAfterKnock(firstVisit)) State = Phase.Visit;
                    break;
                case Phase.ReturnPending:
                    if (!Busy) BeginReturn();
                    break;
                case Phase.Returning:
                    elapsed += seconds;
                    float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / Mathf.Max(0.1f, returnSeconds)));
                    view.SetStoryPose(Vector3.Lerp(startPosition, endPosition, t), Quaternion.Slerp(startRotation, endRotation, t), Mathf.Lerp(startFov, endFov, t));
                    if (elapsed >= returnSeconds && dialogue.PlayInPlace(deskSummary)) State = Phase.Summary;
                    break;
                case Phase.TransitionPending:
                    if (!Busy && story.PlayActTransition(2)) State = Phase.Transition;
                    break;
                case Phase.SecondIntroPending:
                    if (!Busy && dialogue.Play(secondActIntro)) State = Phase.SecondIntro;
                    break;
                case Phase.DeliveryPending:
                    if (archive.IsAvailable(2)) { State = Phase.Complete; break; }
                    if (!Busy && delivery.BeginDelivery(2)) State = Phase.Delivery;
                    break;
            }
        }

        void PlayKnock(DialogueEvent sequence)
        {
            knockAudio.clip = sequence.arrivalSound; knockAudio.Play();
            KnockPlayed = true; knockRemaining = sequence.arrivalSound ? sequence.arrivalSound.length : 0;
        }

        void BeginReturn()
        {
            if (!view.BeginStoryView()) return;
            ownsDeskView = true; gate.SetStory(true);
            var camera = view.sceneCamera;
            startPosition = camera.transform.position; startRotation = camera.transform.rotation; startFov = camera.fieldOfView;
            view.ShowStoryDesktop();
            endPosition = camera.transform.position; endRotation = camera.transform.rotation; endFov = camera.fieldOfView;
            view.SetStoryPose(startPosition, startRotation, startFov);
            elapsed = 0; State = Phase.Returning;
        }
        void ReleaseDeskView()
        {
            if (!ownsDeskView) return;
            ownsDeskView = false;
            if (view) view.EndStoryView(true);
            if (gate) gate.SetStory(false);
        }
        void DialogueFinished(string id)
        {
            // Dialogue completion is emitted only after the observation shutter has finished closing.
            if (State == Phase.OpeningVisit && openingVisit && id == openingVisit.id) State = Phase.OpeningDeliveryPending;
            else if (State == Phase.Visit && firstVisit && id == firstVisit.id) State = Phase.ReturnPending;
            else if (State == Phase.Summary && deskSummary && id == deskSummary.id)
            { ReleaseDeskView(); State = Phase.TransitionPending; }
            else if (State == Phase.SecondIntro && secondActIntro && id == secondActIntro.id) State = Phase.DeliveryPending;
        }
        void DialogueCancelled(string id)
        {
            if ((State == Phase.OpeningVisit && openingVisit && id == openingVisit.id)
                || (State == Phase.Visit && firstVisit && id == firstVisit.id)
                || (State == Phase.Summary && deskSummary && id == deskSummary.id)
                || (State == Phase.SecondIntro && secondActIntro && id == secondActIntro.id)) Stop();
        }
        void ActPrepared(int act)
        {
            if (State != Phase.Transition || act != 2) return;
            storedForTransition = archive.StoreDeskAct();
        }
        void ActEntered(int act)
        {
            if (act == 1 && State == Phase.Idle && !openingStarted && archive && archive.CurrentAct == 1
                && !archive.IsAvailable(1) && openingVisit)
            {
                openingStarted = true; DelayElapsed = 0; KnockPlayed = false; State = Phase.OpeningDelay;
                return;
            }
            if (State != Phase.Transition || act != 2) return;
            if (archive.CurrentAct == 1 && !archive.CompleteAct(1)) { Stop(); return; }
            storedForTransition = false; State = Phase.SecondIntroPending;
        }
        void TransitionCancelled() { if (State == Phase.Transition) Stop(); }
        void Delivered(int act)
        {
            if (State == Phase.OpeningDelivery && act == 1) State = Phase.Idle;
            else if (State == Phase.Delivery && act == 2) State = Phase.Complete;
        }
        void DeliveryCancelled(int act)
        { if ((State == Phase.OpeningDelivery && act == 1) || (State == Phase.Delivery && act == 2)) Stop(); }

        public void Stop()
        {
            if (!IsRunning) return;
            var previous = State;
            resumeAt = previous == Phase.OpeningVisit ? Phase.OpeningVisitPending
                : previous == Phase.OpeningDelivery ? Phase.OpeningDeliveryPending
                : previous == Phase.Visit ? Phase.VisitPending
                : previous == Phase.Returning || previous == Phase.Summary ? Phase.ReturnPending
                : previous == Phase.Transition ? Phase.TransitionPending
                : previous == Phase.SecondIntro ? Phase.SecondIntroPending
                : previous == Phase.Delivery ? Phase.DeliveryPending : previous;
            State = Phase.Suspended;
            if (knockAudio) knockAudio.Stop();
            if (dialogue && ((previous == Phase.OpeningVisit && dialogue.ActiveEvent == openingVisit)
                || (previous == Phase.Visit && dialogue.ActiveEvent == firstVisit)
                || (previous == Phase.Summary && dialogue.ActiveEvent == deskSummary)
                || (previous == Phase.SecondIntro && dialogue.ActiveEvent == secondActIntro))) dialogue.Stop();
            if (previous == Phase.Transition && story && story.IsActive) story.Stop();
            if (previous == Phase.Delivery && delivery && delivery.ActiveAct == 2) delivery.Stop();
            if (previous == Phase.OpeningDelivery && delivery && delivery.ActiveAct == 1) delivery.Stop();
            ReleaseDeskView();
            if (storedForTransition && archive && archive.CurrentAct == 1) archive.ShowActOnDesk(1);
            storedForTransition = false;
        }
        public bool Resume()
        {
            if (!isActiveAndEnabled || State != Phase.Suspended || !Ready) return false;
            State = resumeAt; paused = false;
            if (State == Phase.OpeningVisitPending && knockRemaining > 0) PlayKnock(openingVisit);
            return true;
        }
        void OnDisable()
        {
            Stop();
            if (records) records.plaintextPreserved.RemoveListener(Preserved);
            if (dialogue)
            {
                dialogue.eventCompleted.RemoveListener(DialogueFinished);
                dialogue.eventCancelled.RemoveListener(DialogueCancelled);
            }
            if (story)
            {
                story.actPrepared.RemoveListener(ActPrepared);
                story.actEntered.RemoveListener(ActEntered);
                story.sequenceCancelled.RemoveListener(TransitionCancelled);
            }
            if (delivery)
            {
                delivery.deliveryCompleted.RemoveListener(Delivered);
                delivery.deliveryCancelled.RemoveListener(DeliveryCancelled);
            }
        }
    }
}
