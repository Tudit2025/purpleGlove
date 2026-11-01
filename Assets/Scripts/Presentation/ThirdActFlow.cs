using UnityEngine;

namespace PurpleGlove
{
    /// <summary>Signature, Charles's supplemental receipt, archive request, visitors and act-four transition.</summary>
    [DefaultExecutionOrder(-90), DisallowMultipleComponent]
    public sealed class ThirdActFlow : MonoBehaviour
    {
        public enum Phase { WaitingForSignature, CharlesPending, Charles, SupplementPending, Supplement,
            CuePending, Cue, BellReady, RequestPending, Request, SecretaryPending, Secretary,
            CarafaPending, Carafa, ReturnPending, Returning, Summary, TransitionPending, Transition, Complete, Suspended }
        public PlaintextArchiveController records;
        public ActArchiveController archive;
        public ThirdActVerification verification;
        public DialogueController dialogue;
        public SealedDeliveryController delivery;
        public DeskHandbell bell;
        public PrologueSequence story;
        public DialogueEvent charles, cue, request, secretary, carafa, summary;
        [Min(0.1f)] public float returnSeconds = 0.75f;
        public Phase State { get; private set; }
        public bool FilesComplete => archive && archive.IsAvailable(3) && !archive.HasPendingSupplement(3);
        SceneInputGate gate;
        DeskScenePreview view;
        SceneObjectInteractor interactor;
        Phase resumeAt;
        bool ownsDeskView;
        int storedDeskAct;
        float returnElapsed, startFov, endFov;
        Vector3 startPosition, endPosition;
        Quaternion startRotation, endRotation;

        void Awake()
        {
            gate = GetComponent<SceneInputGate>(); view = GetComponent<DeskScenePreview>();
            interactor = GetComponent<SceneObjectInteractor>();
        }
        void OnEnable()
        {
            if (bell) bell.rang.AddListener(Rang);
            if (dialogue) { dialogue.eventCompleted.AddListener(Finished); dialogue.eventCancelled.AddListener(Cancelled); }
            if (delivery) { delivery.deliveryCompleted.AddListener(Delivered); delivery.deliveryCancelled.AddListener(DeliveryCancelled); }
            if (story)
            {
                story.actPrepared.AddListener(ActPrepared); story.actEntered.AddListener(ActEntered);
                story.sequenceCancelled.AddListener(TransitionCancelled);
            }
        }
        bool Ready => records && archive && verification && dialogue && delivery && bell && story && view && gate
            && charles && cue && request && secretary && carafa && summary;
        bool Busy => gate.BlocksWorldInput || dialogue.IsActive || delivery.IsActive || bell.IsBusy || story.IsActive
            || (interactor && interactor.InspectedObject);
        public void TranscriptSigned()
        {
            if (Ready && archive.CurrentAct == 3 && State == Phase.WaitingForSignature && verification.Signed)
                State = Phase.CharlesPending;
        }
        void Update()
        {
            if (!Ready) return;
            if (archive.CurrentAct == 3) bell.SetRingAllowed(State == Phase.BellReady);
            if (Application.isFocused) AdvanceTime(Time.unscaledDeltaTime);
        }
        void AdvanceTime(float seconds)
        {
            if (!Ready || gate.ConsoleOpen || archive.CurrentAct != 3) return;
            switch (State)
            {
                case Phase.WaitingForSignature: TranscriptSigned(); break;
                case Phase.CharlesPending: BeginDoor(charles, Phase.Charles); break;
                case Phase.SupplementPending:
                    if (FilesComplete) { State = Phase.CuePending; break; }
                    if (!Busy && !dialogue.eyeShutter.IsMoving && !dialogue.eyeShutter.AtSecondStop
                        && delivery.BeginSupplement(3)) State = Phase.Supplement;
                    break;
                case Phase.CuePending:
                    if (!Busy && FilesComplete && dialogue.PlayInPlace(cue)) State = Phase.Cue;
                    break;
                case Phase.RequestPending: BeginDoor(request, Phase.Request); break;
                case Phase.SecretaryPending: BeginDoor(secretary, Phase.Secretary); break;
                case Phase.CarafaPending: BeginDoor(carafa, Phase.Carafa); break;
                case Phase.ReturnPending:
                    if (!Busy) BeginReturn();
                    break;
                case Phase.Returning:
                    returnElapsed += Mathf.Max(0, seconds);
                    float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(returnElapsed / Mathf.Max(0.1f, returnSeconds)));
                    view.SetStoryPose(Vector3.Lerp(startPosition, endPosition, t), Quaternion.Slerp(startRotation, endRotation, t), Mathf.Lerp(startFov, endFov, t));
                    if (returnElapsed >= returnSeconds && dialogue.PlayInPlace(summary)) State = Phase.Summary;
                    break;
                case Phase.TransitionPending:
                    if (!Busy && FilesComplete && story.PlayActTransition(4)) State = Phase.Transition;
                    break;
            }
        }
        void BeginDoor(DialogueEvent sequence, Phase playing)
        {
            if (!Busy && dialogue.Play(sequence)) State = playing;
        }
        void Rang()
        {
            if (archive.CurrentAct != 3 || State != Phase.BellReady || !FilesComplete) return;
            bell.SetRingAllowed(false); State = Phase.RequestPending;
        }
        DialogueEvent ActiveSequence => State == Phase.Charles ? charles : State == Phase.Cue ? cue
            : State == Phase.Request ? request : State == Phase.Secretary ? secretary : State == Phase.Carafa ? carafa
            : State == Phase.Summary ? summary : null;
        void Finished(string id)
        {
            if (!ActiveSequence || id != ActiveSequence.id) return;
            switch (State)
            {
                case Phase.Charles: State = Phase.SupplementPending; break;
                case Phase.Cue: State = Phase.BellReady; bell.SetRingAllowed(true); break;
                case Phase.Request: State = Phase.SecretaryPending; break;
                case Phase.Secretary: State = Phase.CarafaPending; break;
                case Phase.Carafa: State = Phase.ReturnPending; break;
                case Phase.Summary: ReleaseView(); State = Phase.TransitionPending; break;
            }
        }
        void Cancelled(string id) { if (ActiveSequence && id == ActiveSequence.id) Stop(); }
        void Delivered(int act) { if (act == 3 && State == Phase.Supplement && FilesComplete) State = Phase.CuePending; }
        void DeliveryCancelled(int act) { if (act == 3 && State == Phase.Supplement) Stop(); }
        void BeginReturn()
        {
            if (!view.BeginStoryView()) return;
            ownsDeskView = true; gate.SetStory(true);
            var camera = view.sceneCamera;
            startPosition = camera.transform.position; startRotation = camera.transform.rotation; startFov = camera.fieldOfView;
            view.ShowStoryDesktop();
            endPosition = camera.transform.position; endRotation = camera.transform.rotation; endFov = camera.fieldOfView;
            view.SetStoryPose(startPosition, startRotation, startFov);
            returnElapsed = 0; State = Phase.Returning;
        }
        void ReleaseView()
        {
            if (!ownsDeskView) return;
            ownsDeskView = false; view.EndStoryView(true); gate.SetStory(false);
        }
        void ActPrepared(int act)
        {
            if (State != Phase.Transition || act != 4) return;
            storedDeskAct = archive.DeskAct;
            if (!archive.StoreDeskAct()) storedDeskAct = 0;
        }
        void ActEntered(int act)
        {
            if (State != Phase.Transition || act != 4) return;
            if (!FilesComplete || !archive.CompleteAct(3)) { Stop(); return; }
            storedDeskAct = 0; State = Phase.Complete;
        }
        void TransitionCancelled() { if (State == Phase.Transition) Stop(); }
        public void Stop()
        {
            if (State == Phase.WaitingForSignature || State == Phase.Complete || State == Phase.Suspended) return;
            var previous = State; var sequence = ActiveSequence;
            resumeAt = previous == Phase.Charles ? Phase.CharlesPending : previous == Phase.Supplement ? Phase.SupplementPending
                : previous == Phase.Cue ? Phase.CuePending : previous == Phase.Request ? Phase.RequestPending
                : previous == Phase.Secretary ? Phase.SecretaryPending : previous == Phase.Carafa ? Phase.CarafaPending
                : previous == Phase.Returning || previous == Phase.Summary ? Phase.ReturnPending
                : previous == Phase.Transition ? Phase.TransitionPending : previous;
            State = Phase.Suspended;
            if (bell) { bell.SetRingAllowed(false); bell.Cancel(); }
            if (sequence && dialogue && dialogue.ActiveEvent == sequence) dialogue.Stop();
            if (previous == Phase.Supplement && delivery && delivery.ActiveAct == 3) delivery.Stop();
            if (previous == Phase.Transition && story && story.IsActive) story.Stop();
            ReleaseView();
            if (storedDeskAct > 0 && archive && archive.CurrentAct == 3) archive.ShowActOnDesk(storedDeskAct);
            storedDeskAct = 0;
        }
        public bool Resume()
        {
            if (!isActiveAndEnabled || !Ready || State != Phase.Suspended || archive.CurrentAct != 3) return false;
            State = resumeAt;
            bell.SetRingAllowed(State == Phase.BellReady);
            return true;
        }
        void OnDisable()
        {
            Stop();
            if (bell) bell.rang.RemoveListener(Rang);
            if (dialogue) { dialogue.eventCompleted.RemoveListener(Finished); dialogue.eventCancelled.RemoveListener(Cancelled); }
            if (delivery) { delivery.deliveryCompleted.RemoveListener(Delivered); delivery.deliveryCancelled.RemoveListener(DeliveryCancelled); }
            if (story)
            {
                story.actPrepared.RemoveListener(ActPrepared); story.actEntered.RemoveListener(ActEntered);
                story.sequenceCancelled.RemoveListener(TransitionCancelled);
            }
        }
    }
}
