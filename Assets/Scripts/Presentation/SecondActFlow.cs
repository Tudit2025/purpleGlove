using System.Linq;
using UnityEngine;

namespace PurpleGlove
{
    /// <summary>Earn the archive request by preserving the book cipher, then call the clerk with the bell.</summary>
    [DefaultExecutionOrder(-90), DisallowMultipleComponent]
    public sealed partial class SecondActFlow : MonoBehaviour
    {
        public enum Phase { WaitingForCipher, CuePending, Cue, BellReady, RequestPending, Request,
            RetrievalDelay, ArrivalPending, Arrival, DeliveryPending, Delivery, Complete, Suspended,
            WaitingForPigpen, ReturnPending, Returning, Summary, TransitionPending, Transition,
            ThirdIntroPending, ThirdIntro, ThirdDeliveryPending, ThirdDelivery }
        public PlaintextArchiveController records;
        public ActArchiveController archive;
        public DialogueController dialogue;
        public SealedDeliveryController delivery;
        public DeskHandbell bell;
        public PrayerBookTool[] books;
        public DialogueEvent cue, request, arrival;
        [Min(0)] public float retrievalSeconds = 6;
        public Phase State { get; private set; }
        public float RetrievalElapsed { get; private set; }
        Phase resumeAt;
        SceneInputGate gate;
        void Awake() { gate = GetComponent<SceneInputGate>(); view = GetComponent<DeskScenePreview>(); }
        void OnEnable()
        {
            SubscribeEnding();
            if (records) records.plaintextPreserved.AddListener(Preserved);
            if (bell) bell.rang.AddListener(Rang);
            if (dialogue)
            {
                dialogue.eventCompleted.AddListener(Finished);
                dialogue.eventCancelled.AddListener(Cancelled);
            }
            if (delivery)
            {
                delivery.deliveryCompleted.AddListener(Delivered);
                delivery.deliveryCancelled.AddListener(DeliveryCancelled);
            }
        }
        bool Ready => records && archive && dialogue && delivery && bell && gate && cue && request && arrival;
        bool Busy => gate.BlocksWorldInput || gate.FrameConsumed || dialogue.IsActive || delivery.IsActive || bell.IsBusy;
        internal void PrepareThirdActStartForTesting() { State = Phase.ThirdIntroPending; }

        void Preserved(int act)
        {
            if (act == 2 && State == Phase.WaitingForCipher && archive.CurrentAct == 2
                && records.entries.Any(e => e.act == 2 && e.id == "book" && e.saved)) State = Phase.CuePending;
            if (act == 2 && State == Phase.WaitingForPigpen && archive.CurrentAct == 2
                && !archive.HasPendingSupplement(2)
                && records.entries.Any(e => e.act == 2 && e.id == "pigpen" && e.saved)) State = Phase.ReturnPending;
        }
        void Update()
        {
            if (!Ready) return;
            foreach (var book in books) if (book) book.enabled = archive.DeskAct == 2 && archive.IsAvailable(2);
            if (archive.CurrentAct == 2) bell.SetRingAllowed(State == Phase.BellReady);
            if (Application.isFocused && !gate.ConsoleOpen) AdvanceTime(Time.unscaledDeltaTime);
        }
        void AdvanceTime(float seconds)
        {
            if (!Ready || gate.ConsoleOpen || (archive.CurrentAct != 2 && !(archive.CurrentAct == 3 && EnteringThirdAct))) return;
            switch (State)
            {
                case Phase.WaitingForCipher: Preserved(2); break;
                case Phase.CuePending:
                    if (!Busy && dialogue.PlayInPlace(cue)) State = Phase.Cue;
                    break;
                case Phase.RequestPending:
                    if (!Busy && dialogue.Play(request)) State = Phase.Request;
                    break;
                case Phase.RetrievalDelay:
                    RetrievalElapsed += Mathf.Max(0, seconds);
                    if (RetrievalElapsed >= retrievalSeconds) State = Phase.ArrivalPending;
                    break;
                case Phase.ArrivalPending:
                    if (!Busy && dialogue.Play(arrival)) State = Phase.Arrival;
                    break;
                case Phase.DeliveryPending:
                    if (!archive.HasPendingSupplement(2)) { State = Phase.WaitingForPigpen; break; }
                    if (!Busy && !dialogue.eyeShutter.IsMoving && !dialogue.eyeShutter.AtSecondStop
                        && delivery.BeginSupplement(2)) State = Phase.Delivery;
                    break;
                case Phase.WaitingForPigpen: Preserved(2); break;
                default: AdvanceEnding(seconds); break;
            }
        }
        void Rang()
        {
            if (State != Phase.BellReady) return;
            bell.SetRingAllowed(false); State = Phase.RequestPending;
        }
        void Finished(string id)
        {
            if (State == Phase.Cue && id == cue.id) { State = Phase.BellReady; bell.SetRingAllowed(true); }
            else if (State == Phase.Request && id == request.id) { RetrievalElapsed = 0; State = Phase.RetrievalDelay; }
            else if (State == Phase.Arrival && id == arrival.id) State = Phase.DeliveryPending;
            else EndingDialogueFinished(id);
        }
        void Cancelled(string id)
        {
            if ((State == Phase.Cue && id == cue.id) || (State == Phase.Request && id == request.id)
                || (State == Phase.Arrival && id == arrival.id) || IsEndingDialogue(id)) Stop();
        }
        void Delivered(int act)
        {
            if (act == 2 && State == Phase.Delivery) State = Phase.WaitingForPigpen;
            else if (act == 3 && State == Phase.ThirdDelivery) State = Phase.Complete;
        }
        void DeliveryCancelled(int act)
        { if ((act == 2 && State == Phase.Delivery) || (act == 3 && State == Phase.ThirdDelivery)) Stop(); }
        public void Stop()
        {
            if (State == Phase.WaitingForCipher || State == Phase.Complete || State == Phase.Suspended) return;
            var previous = State;
            resumeAt = previous == Phase.Cue ? Phase.CuePending : previous == Phase.Request ? Phase.RequestPending
                : previous == Phase.Arrival ? Phase.ArrivalPending : previous == Phase.Delivery ? Phase.DeliveryPending : EndingResumePhase(previous);
            State = Phase.Suspended;
            if (bell) { bell.SetRingAllowed(false); bell.Cancel(); }
            if (dialogue && ((previous == Phase.Cue && dialogue.ActiveEvent == cue)
                || (previous == Phase.Request && dialogue.ActiveEvent == request) || (previous == Phase.Arrival && dialogue.ActiveEvent == arrival))) dialogue.Stop();
            if (previous == Phase.Delivery && delivery && delivery.IsActive) delivery.Stop();
            StopEnding(previous);
        }
        public bool Resume()
        {
            if (!isActiveAndEnabled || !Ready || State != Phase.Suspended) return false;
            State = resumeAt; return true;
        }
        void OnDisable()
        {
            Stop();
            UnsubscribeEnding();
            if (records) records.plaintextPreserved.RemoveListener(Preserved);
            if (bell) bell.rang.RemoveListener(Rang);
            if (dialogue) { dialogue.eventCompleted.RemoveListener(Finished); dialogue.eventCancelled.RemoveListener(Cancelled); }
            if (delivery) { delivery.deliveryCompleted.RemoveListener(Delivered); delivery.deliveryCancelled.RemoveListener(DeliveryCancelled); }
            if (books != null) foreach (var book in books) if (book) book.Cancel();
        }
    }
}
