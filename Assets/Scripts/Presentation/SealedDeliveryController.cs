using UnityEngine;
using UnityEngine.Events;

namespace PurpleGlove
{
    /// <summary>A modal handoff: turn the packet inside, collect it, then reveal the act on the desk.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SceneInputGate), typeof(DeskScenePreview), typeof(SceneObjectInteractor))]
    public sealed class SealedDeliveryController : MonoBehaviour
    {
        public enum Phase { Idle, TurningOutside, TurningInside, AwaitingPickup, FlyingToDesk, Landing }
        public DoorMechanism transferDrum;
        public SceneObject packet;
        public GameObject ordinaryPacketVisual, firstActPacketsVisual;
        public Transform packetHome, deskLanding;
        public ActArchiveController archive;
        [Min(0.1f)] public float flightSeconds = 1.35f;
        [Min(0.05f)] public float landingSeconds = 0.3f;
        public AudioClip pickupSound, landingSound;
        public UnityEvent<int> deliveryCompleted = new UnityEvent<int>();
        public UnityEvent<int> deliveryCancelled = new UnityEvent<int>();
        public Phase State { get; private set; }
        public int ActiveAct { get; private set; }
        public bool IsActive => State != Phase.Idle;

        SceneInputGate gate;
        DeskScenePreview view;
        SceneObjectInteractor interactor;
        AudioSource sound;
        Collider packetCollider;
        bool previousDrumStop;
        bool supplement;
        float elapsed;
        Vector3 flightStart, packetScale;
        Quaternion flightRotation;

        void Awake()
        {
            gate = GetComponent<SceneInputGate>(); view = GetComponent<DeskScenePreview>();
            interactor = GetComponent<SceneObjectInteractor>();
            sound = gameObject.AddComponent<AudioSource>();
            sound.playOnAwake = false; sound.spatialBlend = 0; sound.volume = 0.5f;
            if (packet)
            {
                packetCollider = packet.GetComponent<Collider>();
                packetScale = packet.transform.localScale;
                HidePacket();
            }
        }

        public bool CanDeliver(int act) => CanDeliver(act, false);
        bool CanDeliver(int act, bool additional) => isActiveAndEnabled && !IsActive && archive && archive.isActiveAndEnabled
            && act >= 1 && act <= archive.acts.Length && (additional ? archive.HasPendingSupplement(act) : !archive.IsAvailable(act))
            && gate && !gate.StoryActive && !gate.DialogueActive && !gate.DeliveryActive && view && view.isActiveAndEnabled && !view.DialogueLocked
            && interactor && interactor.isActiveAndEnabled
            && transferDrum && transferDrum.isActiveAndEnabled && transferDrum.pivot && transferDrum.cargoSocket
            && transferDrum.mechanism == DoorMechanism.Mechanism.TransferDrum && !transferDrum.IsMoving && !transferDrum.Cargo
            && packet && packetCollider && packetHome && deskLanding;

        // Console uses the bool result; Inspector story events can use DeliverAct directly.
        public void DeliverAct(int act) => BeginDelivery(act);
        public bool BeginDelivery(int act) => BeginDelivery(act, false);
        public bool BeginSupplement(int act) => BeginDelivery(act, true);
        bool BeginDelivery(int act, bool additional)
        {
            if (!CanDeliver(act, additional)) return false;
            supplement = additional;
            ActiveAct = act; previousDrumStop = transferDrum.AtSecondStop;
            if (ordinaryPacketVisual) ordinaryPacketVisual.SetActive(act != 1 || !firstActPacketsVisual);
            if (firstActPacketsVisual) firstActPacketsVisual.SetActive(act == 1);
            interactor.CancelInteraction();
            gate.SetDelivery(true); view.BeginDeliveryView();
            State = Phase.TurningOutside;
            if (previousDrumStop) transferDrum.MoveTo(false);
            else LoadPacket();
            return true;
        }

        void LoadPacket()
        {
            packet.gameObject.SetActive(true); packet.enabled = true;
            if (!transferDrum.TryLoadCargo(packet.transform)) { Stop(); return; }
            // Tip the wax seal toward the room after the drum has made its half turn.
            packet.transform.localPosition = new Vector3(0, 0.038f, 0);
            packet.transform.localRotation = Quaternion.Euler(28, 0, 0);
            SetPacketLayer(transferDrum.gameObject.layer);
            State = Phase.TurningInside;
            if (!transferDrum.MoveTo(true)) Stop();
        }

        void Update()
        {
            if (!IsActive) return;
            if (!transferDrum || !transferDrum.isActiveAndEnabled || !packet
                || (!packet.gameObject.activeInHierarchy && State != Phase.TurningOutside)
                || !deskLanding || !archive || !archive.isActiveAndEnabled
                || !view || !view.isActiveAndEnabled || !interactor || !interactor.isActiveAndEnabled)
            { Stop(); return; }
            if (gate.ConsoleOpen || !Application.isFocused) return;
            AdvanceTime(Time.unscaledDeltaTime);
        }

        void AdvanceTime(float seconds)
        {
            switch (State)
            {
                case Phase.TurningOutside:
                    if (!transferDrum.IsMoving) LoadPacket();
                    break;
                case Phase.TurningInside:
                    if (!transferDrum.CargoIsInside) break;
                    transferDrum.ReleaseCargo(packetHome);
                    // Clear any previous hover origin before allowing the ordinary prop hover animation.
                    packet.Relocate(packetHome, packet.transform.localPosition, packet.transform.localRotation, packetScale);
                    packetCollider.enabled = true;
                    State = Phase.AwaitingPickup;
                    gate.SetDelivery(true, packet);
                    break;
                case Phase.FlyingToDesk:
                    elapsed += Mathf.Max(0, seconds);
                    float t = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, flightSeconds));
                    float eased = t * t * (3 - 2 * t);
                    Vector3 destination = deskLanding.position;
                    // Clear the hatch before descending onto the writing mat.
                    Vector3 control = flightStart - transferDrum.transform.forward * 0.65f + Vector3.up * 0.32f;
                    Vector3 approach = destination + Vector3.up * 0.45f;
                    float u = 1 - eased;
                    packet.transform.SetPositionAndRotation(u * u * u * flightStart + 3 * u * u * eased * control
                        + 3 * u * eased * eased * approach + eased * eased * eased * destination,
                        Quaternion.Slerp(flightRotation, deskLanding.rotation, eased));
                    if (t < 1) break;
                    State = Phase.Landing; elapsed = 0;
                    Play(landingSound);
                    break;
                case Phase.Landing:
                    elapsed += Mathf.Max(0, seconds);
                    if (elapsed >= Mathf.Max(0.05f, landingSeconds)) Finish(true);
                    break;
            }
        }

        public bool CollectPacket()
        {
            if (State != Phase.AwaitingPickup || !gate.CanInteract(packet)) return false;
            interactor.CancelInteraction();
            packet.enabled = false; packetCollider.enabled = false;
            flightStart = packet.transform.position; flightRotation = packet.transform.rotation;
            elapsed = 0; State = Phase.FlyingToDesk;
            gate.SetDelivery(true); view.FollowDeliveryToDesk();
            SetPacketLayer(deskLanding.gameObject.layer);
            Play(pickupSound);
            return true;
        }

        public void Stop() => Finish(false);
        void Finish(bool completed)
        {
            if (!IsActive) return;
            int act = ActiveAct;
            if (interactor) interactor.CancelInteraction();
            if (transferDrum)
            {
                transferDrum.SetPoseImmediately(completed || previousDrumStop);
                if (packet && transferDrum.Cargo == packet.transform) transferDrum.ReleaseCargo(packetHome);
            }
            HidePacket();
            State = Phase.Idle; ActiveAct = 0;
            if (!completed && sound) sound.Stop();
            if (view) view.EndDeliveryView(completed);
            if (gate) gate.SetDelivery(false);
            bool received = completed && archive && (supplement ? archive.ReceiveSupplement(act) : archive.ReceiveAct(act));
            supplement = false;
            if (received) deliveryCompleted.Invoke(act);
            else deliveryCancelled.Invoke(act);
        }

        void HidePacket()
        {
            if (!packet) return;
            packet.enabled = true;
            packet.Relocate(packetHome, Vector3.zero, Quaternion.identity, packetScale);
            if (packetCollider) packetCollider.enabled = false;
            packet.gameObject.SetActive(false);
        }

        void SetPacketLayer(int layer)
        {
            foreach (var child in packet.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
        }
        void Play(AudioClip clip) { if (sound && clip) sound.PlayOneShot(clip); }
        void OnDisable() => Stop();
    }
}
