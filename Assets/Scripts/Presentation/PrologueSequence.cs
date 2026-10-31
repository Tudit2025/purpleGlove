using UnityEngine;
using UnityEngine.Events;

namespace PurpleGlove
{
    /// <summary>The threshold scene: fixed exterior view, authored dialogue, opening door, chapter card.</summary>
    [DefaultExecutionOrder(-150), DisallowMultipleComponent]
    [RequireComponent(typeof(DialogueController), typeof(ActTransitionOverlay), typeof(DeskScenePreview))]
    public sealed class PrologueSequence : MonoBehaviour
    {
        public enum Phase { Idle, Monologue, OpeningDoor, Transition }
        public DialogueEvent prologue;
        public Transform doorLeaf;
        public Light exteriorLight;
        public Vector3 exteriorCameraOffset = new Vector3(0.462f, 1.50f, 1.92f);
        public Vector3 exteriorLookAt = new Vector3(0.462f, 1.10f, 0);
        [Range(35, 85)] public float exteriorFieldOfView = 66;
        [Min(0.1f)] public float doorOpenSeconds = 1.35f;
        [Range(15, 110)] public float doorOpenAngle = 85;
        public AudioClip doorOpeningSound, doorClosingSound;
        [Tooltip("Prepare act scenery under the opaque curtain. Cancellation must restore any previewed state.")]
        public UnityEvent<int> actPrepared = new UnityEvent<int>();
        public UnityEvent<int> actEntered = new UnityEvent<int>();
        public UnityEvent sequenceCancelled = new UnityEvent();
        public Phase State { get; private set; }
        public bool IsActive => State != Phase.Idle;
        public int PresentedAct { get; private set; }
        DialogueController dialogue;
        ActTransitionOverlay transition;
        DeskScenePreview view;
        SceneInputGate gate;
        SealedDeliveryController delivery;
        AudioSource sound;
        Quaternion originalDoorRotation;
        bool previousShutterStop, captured, enteringFromOutside, paused;
        float elapsed;
        int targetAct;

        void Awake()
        {
            dialogue = GetComponent<DialogueController>(); transition = GetComponent<ActTransitionOverlay>();
            view = GetComponent<DeskScenePreview>(); gate = GetComponent<SceneInputGate>();
            delivery = GetComponent<SealedDeliveryController>();
            sound = gameObject.AddComponent<AudioSource>(); sound.playOnAwake = false;
            sound.spatialBlend = 0; sound.volume = 0.55f;
        }
        void OnEnable()
        {
            dialogue.eventCompleted.AddListener(DialogueFinished);
            dialogue.eventCancelled.AddListener(DialogueCancelled);
        }

        public bool CanStart => isActiveAndEnabled && !IsActive && dialogue && dialogue.isActiveAndEnabled && !dialogue.IsActive
            && transition && transition.isActiveAndEnabled && !transition.IsActive && view && view.isActiveAndEnabled
            && !view.DialogueLocked && !view.DeliveryLocked && !view.StoryLocked && gate && !gate.StoryActive
            && (!delivery || !delivery.IsActive) && doorLeaf
            && (!dialogue.eyeShutter || !dialogue.eyeShutter.IsMoving)
            && (!delivery || !delivery.transferDrum || !delivery.transferDrum.IsMoving);

        bool Capture()
        {
            if (!CanStart || !view.BeginStoryView()) return false;
            originalDoorRotation = doorLeaf.localRotation;
            previousShutterStop = dialogue.eyeShutter && dialogue.eyeShutter.AtSecondStop;
            captured = true; paused = false; gate.SetStory(true);
            return true;
        }

        public bool PlayPrologue()
        {
            if (!prologue || !prologue.TryValidate(out _) || !Capture()) return false;
            enteringFromOutside = true; targetAct = 1;
            if (exteriorLight) exteriorLight.enabled = true;
            if (dialogue.eyeShutter) dialogue.eyeShutter.SetPoseImmediately(false);
            Vector3 position = doorLeaf.TransformPoint(exteriorCameraOffset);
            Vector3 lookAt = doorLeaf.TransformPoint(exteriorLookAt);
            view.SetStoryPose(position, Quaternion.LookRotation(lookAt - position, Vector3.up), exteriorFieldOfView);
            State = Phase.Monologue;
            if (dialogue.PlayInPlace(prologue)) return true;
            Stop(); return false;
        }
        public void BeginPrologue() => PlayPrologue();

        // A reusable presentation cue; deliveries and act completion remain owned by their story events.
        public bool PlayActTransition(int act)
        {
            if (act < 1 || act > 5 || !Capture()) return false;
            enteringFromOutside = false; targetAct = act;
            return StartTransition();
        }
        public void ShowAct(int act) => PlayActTransition(act);

        void DialogueFinished(string id)
        {
            if (State != Phase.Monologue || !prologue || id != prologue.id) return;
            State = Phase.OpeningDoor; elapsed = 0;
            if (sound && doorOpeningSound) sound.PlayOneShot(doorOpeningSound);
        }
        void DialogueCancelled(string id)
        { if (State == Phase.Monologue && prologue && id == prologue.id) Stop(); }

        void Update()
        {
            if (!IsActive) return;
            if (!doorLeaf || !transition || !transition.isActiveAndEnabled || !view || !view.isActiveAndEnabled
                || !dialogue || !dialogue.isActiveAndEnabled
                || (State == Phase.Transition && !transition.IsActive)) { Stop(); return; }
            bool pause = gate.ConsoleOpen || !Application.isFocused;
            transition.SetConsoleVisible(gate.ConsoleOpen);
            if (pause)
            {
                if (!paused && sound) sound.Pause();
                paused = true; return;
            }
            if (paused) { if (sound) sound.UnPause(); paused = false; }
            AdvanceTime(Time.unscaledDeltaTime);
        }

        void AdvanceTime(float seconds)
        {
            if (State == Phase.OpeningDoor)
            {
                elapsed += Mathf.Max(0, seconds);
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, doorOpenSeconds));
                doorLeaf.localRotation = originalDoorRotation * Quaternion.Euler(0, Mathf.SmoothStep(0, doorOpenAngle, t), 0);
                if (elapsed >= doorOpenSeconds + 0.3f) StartTransition();
            }
            else if (State == Phase.Transition) transition.Advance(seconds);
        }

        bool StartTransition()
        {
            State = Phase.Transition;
            if (transition.Begin(targetAct, EnterDeskUnderBlack, Complete)) return true;
            Stop(); return false;
        }
        void EnterDeskUnderBlack()
        {
            if (exteriorLight) exteriorLight.enabled = false;
            RestoreDoor(); view.ShowStoryDesktop();
            actPrepared.Invoke(targetAct);
            if (enteringFromOutside && sound && doorClosingSound) sound.PlayOneShot(doorClosingSound);
        }
        void Complete()
        {
            if (exteriorLight) exteriorLight.enabled = false;
            State = Phase.Idle; PresentedAct = targetAct;
            RestoreDoor(); captured = false;
            view.EndStoryView(true); gate.SetStory(false);
            actEntered.Invoke(PresentedAct);
        }
        void RestoreDoor()
        {
            if (!captured) return;
            if (doorLeaf) doorLeaf.localRotation = originalDoorRotation;
            if (dialogue && dialogue.eyeShutter) dialogue.eyeShutter.SetPoseImmediately(previousShutterStop);
        }
        public void Stop()
        {
            if (!IsActive && !captured) return;
            State = Phase.Idle;
            if (dialogue && dialogue.ActiveEvent == prologue) dialogue.Stop();
            if (transition) transition.Cancel();
            if (sound) sound.Stop();
            if (exteriorLight) exteriorLight.enabled = false;
            RestoreDoor(); captured = false;
            if (view) view.EndStoryView(false);
            if (gate) gate.SetStory(false);
            sequenceCancelled.Invoke();
        }
        void OnDisable()
        {
            Stop();
            if (dialogue)
            {
                dialogue.eventCompleted.RemoveListener(DialogueFinished);
                dialogue.eventCancelled.RemoveListener(DialogueCancelled);
            }
        }
    }
}
