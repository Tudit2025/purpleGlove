using UnityEngine;

namespace PurpleGlove
{
    /// <summary>Introduce both independently sealed sources after the act-four fade, then receive their four papers.</summary>
    [DefaultExecutionOrder(-80), DisallowMultipleComponent]
    public sealed class FourthActOpening : MonoBehaviour
    {
        public enum Phase { WaitingForAct, IntroPending, Intro, DeliveryPending, Delivery, Complete, Suspended }
        public ActArchiveController archive;
        public PrologueSequence story;
        public DialogueController dialogue;
        public SealedDeliveryController delivery;
        public DialogueEvent introduction;
        public Phase State { get; private set; }
        Phase resumeAt;
        SceneInputGate gate;
        SceneObjectInteractor interactor;
        void Awake() { gate = GetComponent<SceneInputGate>(); interactor = GetComponent<SceneObjectInteractor>(); }
        void OnEnable()
        {
            if (dialogue) { dialogue.eventCompleted.AddListener(Finished); dialogue.eventCancelled.AddListener(Cancelled); }
            if (delivery) { delivery.deliveryCompleted.AddListener(Delivered); delivery.deliveryCancelled.AddListener(DeliveryCancelled); }
        }
        bool Ready => archive && story && dialogue && delivery && introduction && gate;
        bool Busy => gate.BlocksWorldInput || story.IsActive || dialogue.IsActive || delivery.IsActive
            || (interactor && interactor.InspectedObject);
        void Update() { if (Application.isFocused) Advance(); }
        void Advance()
        {
            if (!Ready || gate.ConsoleOpen || archive.CurrentAct != 4) return;
            switch (State)
            {
                case Phase.WaitingForAct:
                    if (archive.IsAvailable(4)) State = Phase.Complete;
                    else if (archive.IsCompleted(3) && story.PresentedAct == 4 && !story.IsActive) State = Phase.IntroPending;
                    break;
                case Phase.IntroPending:
                    if (!Busy && dialogue.Play(introduction)) State = Phase.Intro;
                    break;
                case Phase.DeliveryPending:
                    if (archive.IsAvailable(4)) { State = Phase.Complete; break; }
                    if (!Busy && !dialogue.eyeShutter.IsMoving && !dialogue.eyeShutter.AtSecondStop
                        && delivery.BeginDelivery(4)) State = Phase.Delivery;
                    break;
            }
        }
        void Finished(string id) { if (State == Phase.Intro && id == introduction.id) State = Phase.DeliveryPending; }
        void Cancelled(string id) { if (State == Phase.Intro && id == introduction.id) Stop(); }
        void Delivered(int act) { if (act == 4 && State == Phase.Delivery) State = Phase.Complete; }
        void DeliveryCancelled(int act) { if (act == 4 && State == Phase.Delivery) Stop(); }
        public void Stop()
        {
            if (State == Phase.WaitingForAct || State == Phase.Complete || State == Phase.Suspended) return;
            var previous = State;
            resumeAt = previous == Phase.Intro ? Phase.IntroPending : previous == Phase.Delivery ? Phase.DeliveryPending : previous;
            State = Phase.Suspended;
            if (previous == Phase.Intro && dialogue && dialogue.ActiveEvent == introduction) dialogue.Stop();
            if (previous == Phase.Delivery && delivery && delivery.ActiveAct == 4) delivery.Stop();
        }
        public bool Resume()
        {
            if (!isActiveAndEnabled || !Ready || State != Phase.Suspended || archive.CurrentAct != 4) return false;
            State = resumeAt; return true;
        }
        void OnDisable()
        {
            Stop();
            if (dialogue) { dialogue.eventCompleted.RemoveListener(Finished); dialogue.eventCancelled.RemoveListener(Cancelled); }
            if (delivery) { delivery.deliveryCompleted.RemoveListener(Delivered); delivery.deliveryCancelled.RemoveListener(DeliveryCancelled); }
        }
    }
}
