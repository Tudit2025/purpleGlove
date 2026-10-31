using UnityEngine;
using UnityEngine.Events;

namespace PurpleGlove
{
    /// <summary>A fixed door fitting: only its hinge/rotor moves, never the interaction frame.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
    public sealed class DoorMechanism : SceneObjectAction
    {
        public enum Mechanism { EyeShutter, TransferDrum }
        public Mechanism mechanism;
        public Transform pivot;
        public Vector3 rotationAxis = Vector3.right;
        public float travelAngle = 108;
        [Min(0.05f)] public float travelSeconds = 0.55f;
        public bool startsAtSecondStop;
        public AudioClip openingSound, closingSound, stopSound;
        public UnityEvent reachedFirstStop = new UnityEvent();
        public UnityEvent reachedSecondStop = new UnityEvent();

        [Header("Transfer drum")]
        public Transform cargoSocket;
        [SerializeField] Transform cargo;
        public Transform Cargo => cargo;
        public bool IsMoving { get; private set; }
        public bool AtSecondStop => !IsMoving && secondStop;
        public bool CargoIsInside => mechanism == Mechanism.TransferDrum && AtSecondStop;

        bool secondStop, targetStop;
        float elapsed, fromAngle, currentAngle, moveDuration;
        AudioSource source;
        SceneObject cargoInteraction;
        bool cargoInteractionWasEnabled;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            SetPoseImmediately(startsAtSecondStop);
            if (cargo) SuspendCargoInteraction();
        }

        public override void Activate() => MoveTo(!secondStop);

        // First stop = shutter closed / cargo outside; second = shutter open / cargo inside.
        public bool MoveTo(bool toSecondStop, bool allowReverse = false)
        {
            if (!isActiveAndEnabled || !pivot) return false;
            // Ordinary clicks still ignore an in-flight action; cancellation may return from its current pose.
            if (IsMoving ? !allowReverse || targetStop == toSecondStop : secondStop == toSecondStop) return false;
            targetStop = toSecondStop;
            fromAngle = currentAngle;
            float distance = Mathf.Abs((targetStop ? travelAngle : 0) - fromAngle);
            moveDuration = Mathf.Max(0.05f, travelSeconds * distance / Mathf.Max(0.001f, Mathf.Abs(travelAngle)));
            elapsed = 0;
            IsMoving = true;
            Play(toSecondStop ? openingSound : closingSound);
            return true;
        }

        void Update() => Advance(Time.unscaledDeltaTime);

        void Advance(float deltaTime)
        {
            if (!IsMoving) return;
            elapsed += Mathf.Max(0, deltaTime);
            float t = Mathf.Clamp01(elapsed / moveDuration);
            currentAngle = Mathf.Lerp(fromAngle, targetStop ? travelAngle : 0, t * t * (3 - 2 * t));
            ApplyPose();
            if (t < 1) return;
            secondStop = targetStop;
            IsMoving = false;
            Play(stopSound);
            if (secondStop) reachedSecondStop.Invoke(); else reachedFirstStop.Invoke();
        }

        // Also used by editor previews; does not fire gameplay events or play audio.
        public void SetPoseImmediately(bool toSecondStop)
        {
            secondStop = targetStop = toSecondStop;
            IsMoving = false;
            currentAngle = toSecondStop ? travelAngle : 0;
            ApplyPose();
        }

        void ApplyPose()
        {
            if (pivot) pivot.localRotation = Quaternion.AngleAxis(currentAngle, rotationAxis.normalized);
        }

        public bool TryLoadCargo(Transform item)
        {
            if (mechanism != Mechanism.TransferDrum || !cargoSocket || !item || cargo || IsMoving
                || item == transform || transform.IsChildOf(item) || item.IsChildOf(transform)) return false;
            cargo = item;
            SuspendCargoInteraction();
            cargo.SetParent(cargoSocket, false);
            cargo.localPosition = Vector3.zero;
            cargo.localRotation = Quaternion.identity;
            return true;
        }

        public Transform ReleaseCargo(Transform destination = null)
        {
            if (!cargo || IsMoving || mechanism != Mechanism.TransferDrum
                || (destination && destination.IsChildOf(cargo))) return null;
            var released = cargo;
            released.SetParent(destination, true);
            if (cargoInteraction) cargoInteraction.enabled = cargoInteractionWasEnabled;
            cargo = null;
            cargoInteraction = null;
            return released;
        }

        void SuspendCargoInteraction()
        {
            cargoInteraction = cargo.GetComponent<SceneObject>();
            if (!cargoInteraction) return;
            cargoInteractionWasEnabled = cargoInteraction.enabled;
            cargoInteraction.enabled = false;
        }

        void Play(AudioClip clip)
        {
            if (!source) source = GetComponent<AudioSource>();
            if (Application.isPlaying && source && clip) source.PlayOneShot(clip);
        }
    }
}
