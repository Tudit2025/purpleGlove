using UnityEngine;
using UnityEngine.Events;

namespace PurpleGlove
{
    /// <summary>A reusable desk action with a silent refusal and a story-authorized ringing branch.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SceneObject), typeof(AudioSource))]
    public sealed class DeskHandbell : SceneObjectAction
    {
        public enum Phase { Resting, PickingUp, Speaking, Shaking, Settling, PuttingDown }

        [Header("Story")]
        public DialogueController dialogue;
        public DialogueEvent notNeededLine;
        [Tooltip("Optional silent choice before ringing; only the choice ID 'ring' authorizes a strike.")]
        public DialogueEvent ringConfirmation;
        [Tooltip("Set by the story when calling someone is meaningful. Off by default.")]
        public bool ringAllowed;
        [Tooltip("Optionally use up permission at the first audible strike.")]
        public bool consumePermissionOnRing;
        public UnityEvent rang = new UnityEvent();
        public UnityEvent returnedToDesk = new UnityEvent();

        [Header("Rig (grip pivot at the player's wrist)")]
        public Transform gripPivot;
        public Transform clapperPivot;
        [Min(0.1f)] public float pickupSeconds = 0.55f;
        [Min(0.1f)] public float putdownSeconds = 0.6f;
        [Min(0.1f)] public float shakeSeconds = 1.4f;
        [Range(1, 5)] public int shakeCycles = 3;
        [Range(5, 40)] public float shakeAngle = 24;
        [Min(0.1f)] public float settleSeconds = 0.3f;
        public Vector3 heldCameraOffset = new Vector3(0.13f, 0.10f, 0.68f);
        public Vector3 heldCameraEuler = new Vector3(-12, 0, -8);

        [Header("Foley")]
        public AudioClip pickupSound, putdownSound;
        public AudioClip[] strikes = new AudioClip[0];
        [Range(0, 1)] public float foleyVolume = 0.5f;
        [Range(0, 1)] public float ringVolume = 0.72f;

        public Phase State { get; private set; }
        public override bool IsBusy => State != Phase.Resting;
        public int StrikeCount { get; private set; }
        SceneObject item;
        SceneInputGate gate;
        SceneObjectInteractor interactor;
        Camera sceneCamera;
        AudioSource source;
        Vector3 restPosition, restScale, fromPosition, heldPosition;
        Quaternion restRotation, fromRotation, heldRotation, gripRest, clapperRest;
        float elapsed;
        int nextStrike;
        bool hasPose, itemWasEnabled, ownsLine, paused, authorizedShake;
        DialogueEvent ownedLine;
        bool confirmingRing;

        void Awake() => Resolve();
        void Resolve()
        {
            item = GetComponent<SceneObject>(); source = GetComponent<AudioSource>();
            if (!dialogue && Camera.main) dialogue = Camera.main.GetComponent<DialogueController>();
            sceneCamera = dialogue ? dialogue.GetComponent<Camera>() : Camera.main;
            gate = sceneCamera ? sceneCamera.GetComponent<SceneInputGate>() : null;
            interactor = sceneCamera ? sceneCamera.GetComponent<SceneObjectInteractor>() : null;
        }

        public void SetRingAllowed(bool allowed) => ringAllowed = allowed;
        public override void Activate() => TryInteract();

        public bool TryInteract()
        {
            if (!isActiveAndEnabled || IsBusy) return false;
            Resolve();
            if (!item || !item.isActiveAndEnabled || !gripPivot || !clapperPivot || !sceneCamera
                || !sceneCamera.isActiveAndEnabled || !gate || gate.BlocksWorldInput
                || (interactor && interactor.InspectedObject) || !dialogue || !dialogue.isActiveAndEnabled
                || dialogue.IsActive || !notNeededLine || !notNeededLine.TryValidate(out _)) return false;
            // Hover belongs to SceneObject; finish it before capturing the actual tabletop pose.
            item.RestoreImmediately();
            restPosition = transform.localPosition; restRotation = transform.localRotation; restScale = transform.localScale;
            gripRest = gripPivot.localRotation; clapperRest = clapperPivot.localRotation; hasPose = true;
            itemWasEnabled = item.enabled; item.enabled = false;
            fromPosition = transform.position; fromRotation = transform.rotation;
            heldRotation = sceneCamera.transform.rotation * Quaternion.Euler(heldCameraEuler);
            // Keep the wrist at a stable camera-relative point; the base is below that point.
            heldPosition = sceneCamera.transform.TransformPoint(heldCameraOffset)
                - heldRotation * Vector3.Scale(gripPivot.localPosition, transform.lossyScale);
            elapsed = 0; StrikeCount = nextStrike = 0; authorizedShake = ownsLine = false;
            State = Phase.PickingUp; gate.SetPropAction(true);
            if (interactor) interactor.TrackAction(this);
            Play(pickupSound, foleyVolume);
            return true;
        }

        void Update()
        {
            if (!IsBusy) return;
            if (!sceneCamera || !sceneCamera.isActiveAndEnabled || !gate || !dialogue || !dialogue.isActiveAndEnabled)
            { Cancel(); return; }
            if (gate.StoryActive || gate.DeliveryActive || (dialogue.IsActive && dialogue.ActiveEvent != ownedLine))
            { Cancel(); return; }
            bool pause = !Application.isFocused || gate.ConsoleOpen;
            if (pause) { if (!paused) source.Pause(); paused = true; return; }
            if (paused) { source.UnPause(); paused = false; }
            if (Input.GetKeyDown(KeyCode.Escape) && !gate.FrameConsumed) PutDown();
            AdvanceTime(Time.unscaledDeltaTime);
        }

        // Shared clock for the complete action; strikes cross discrete thresholds even at low FPS.
        void AdvanceTime(float seconds)
        {
            if (!IsBusy || paused) return;
            elapsed += Mathf.Max(0, seconds);
            switch (State)
            {
                case Phase.PickingUp:
                    Move(fromPosition, heldPosition, fromRotation, heldRotation, elapsed / Mathf.Max(0.1f, pickupSeconds), 0.075f);
                    if (elapsed >= pickupSeconds)
                    {
                        elapsed = 0;
                        if (ringAllowed && HasStrikes())
                        {
                            if (ringConfirmation) Speak(ringConfirmation, true);
                            else { authorizedShake = true; State = Phase.Shaking; }
                        }
                        else Refuse();
                    }
                    break;
                case Phase.Speaking:
                    if (!ownsLine || dialogue.ActiveEvent != ownedLine) PutDown();
                    break;
                case Phase.Shaking:
                    // Revoking permission before the first strike must never produce a call.
                    if (!ringAllowed && (!consumePermissionOnRing || StrikeCount == 0))
                    { if (StrikeCount == 0) Refuse(); else PutDown(); break; }
                    float t = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, shakeSeconds));
                    float wave = Mathf.Sin(t * Mathf.PI * 2 * Mathf.Max(1, shakeCycles));
                    float envelope = Mathf.SmoothStep(0, 1, Mathf.Min(t * 8, (1 - t) * 8));
                    gripPivot.localRotation = gripRest * Quaternion.Euler(4 * wave * envelope, 0, shakeAngle * wave * envelope);
                    clapperPivot.localRotation = clapperRest * Quaternion.Euler(0, 0, -34 * wave * envelope);
                    int count = Mathf.Max(1, shakeCycles) * 2;
                    while (nextStrike < count && t >= (nextStrike + 0.5f) / count)
                    {
                        nextStrike++;
                        var clip = strikes[StrikeCount % strikes.Length];
                        Play(clip, ringVolume * (nextStrike % 2 == 0 ? 0.83f : 1));
                        StrikeCount++;
                        if (StrikeCount == 1)
                        {
                            if (consumePermissionOnRing) ringAllowed = false;
                            rang.Invoke();
                            if (State != Phase.Shaking) return;
                        }
                    }
                    if (t >= 1) { ResetRig(); elapsed = 0; State = Phase.Settling; }
                    break;
                case Phase.Settling:
                    if (elapsed >= settleSeconds) PutDown();
                    break;
                case Phase.PuttingDown:
                    float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, putdownSeconds));
                    Move(fromPosition, RestWorldPosition(), fromRotation, RestWorldRotation(), progress, 0.055f);
                    gripPivot.localRotation = Quaternion.Slerp(gripPivot.localRotation, gripRest, Mathf.Clamp01(seconds * 18));
                    clapperPivot.localRotation = Quaternion.Slerp(clapperPivot.localRotation, clapperRest, Mathf.Clamp01(seconds * 18));
                    if (progress >= 1)
                    {
                        Restore(); Play(putdownSound, foleyVolume); returnedToDesk.Invoke();
                    }
                    break;
            }
        }

        bool HasStrikes()
        {
            if (strikes == null || strikes.Length == 0) return false;
            foreach (var clip in strikes) if (!clip) return false;
            return true;
        }
        void Refuse()
        {
            ResetRig(); authorizedShake = false;
            Speak(notNeededLine, false);
        }
        void Speak(DialogueEvent line, bool confirmation)
        {
            State = Phase.Speaking; elapsed = 0;
            ownedLine = line; confirmingRing = confirmation;
            if (confirmation) source.Stop();
            ownsLine = dialogue.PlayInPlace(line, true);
            if (ownsLine)
            {
                dialogue.eventCompleted.AddListener(LineFinished);
                dialogue.eventCancelled.AddListener(LineCancelled);
            }
            if (!ownsLine) PutDown();
        }
        void LineFinished(string id)
        {
            if (!ownsLine || !ownedLine || ownedLine.id != id) return;
            bool ring = confirmingRing && dialogue.SelectedChoiceId == "ring" && ringAllowed && HasStrikes();
            StopOwnLine();
            if (ring) { authorizedShake = true; elapsed = 0; State = Phase.Shaking; }
            else PutDown();
        }
        void LineCancelled(string id)
        {
            if (ownsLine && ownedLine && ownedLine.id == id) PutDown();
        }
        public void PutDown()
        {
            if (!IsBusy || State == Phase.PuttingDown) return;
            StopOwnLine();
            if (!authorizedShake || StrikeCount == 0) source.Stop();
            fromPosition = transform.position; fromRotation = transform.rotation;
            elapsed = 0; State = Phase.PuttingDown;
        }
        Vector3 RestWorldPosition() => transform.parent ? transform.parent.TransformPoint(restPosition) : restPosition;
        Quaternion RestWorldRotation() => transform.parent ? transform.parent.rotation * restRotation : restRotation;
        void Move(Vector3 from, Vector3 to, Quaternion rotationFrom, Quaternion rotationTo, float progress, float arc)
        {
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress));
            transform.SetPositionAndRotation(Vector3.Lerp(from, to, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * arc),
                Quaternion.Slerp(rotationFrom, rotationTo, t));
        }
        void ResetRig()
        {
            if (gripPivot) gripPivot.localRotation = gripRest;
            if (clapperPivot) clapperPivot.localRotation = clapperRest;
        }
        void StopOwnLine()
        {
            bool stop = ownsLine && dialogue && dialogue.ActiveEvent == ownedLine;
            ownsLine = confirmingRing = false; ownedLine = null;
            if (dialogue)
            {
                dialogue.eventCompleted.RemoveListener(LineFinished);
                dialogue.eventCancelled.RemoveListener(LineCancelled);
            }
            if (stop) dialogue.Stop();
        }
        void Restore()
        {
            if (hasPose)
            {
                transform.localPosition = restPosition; transform.localRotation = restRotation; transform.localScale = restScale;
                ResetRig();
                if (item) item.enabled = itemWasEnabled;
            }
            hasPose = paused = false; State = Phase.Resting;
            if (gate) gate.SetPropAction(false);
        }
        public override void Cancel()
        {
            if (!IsBusy) return;
            StopOwnLine(); if (source) source.Stop(); Restore();
        }
        void Play(AudioClip clip, float volume) { if (clip && source) source.PlayOneShot(clip, volume); }
        void OnDisable() => Cancel();
    }
}
