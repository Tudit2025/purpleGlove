using System.Linq;
using UnityEngine;

namespace PurpleGlove
{
    /// <summary>Both preserved solutions, Luca's reflection, then the transition straight into act five.</summary>
    [DefaultExecutionOrder(-80), DisallowMultipleComponent]
    public sealed class FourthActFlow : MonoBehaviour
    {
        public enum Phase { Waiting, ReturnPending, Returning, Summary, TransitionPending, Transition, Complete, Suspended }
        public PlaintextArchiveController records;
        public ActArchiveController archive;
        public DialogueController dialogue;
        public PrologueSequence story;
        public DeskOperationalDocument confession;
        public DialogueEvent summary;
        public float returnSeconds = 0.75f;
        public Phase State { get; private set; }
        public bool SolutionsReady => records && records.entries.Any(e => e.act == 4 && e.id == "affine" && e.saved)
            && records.entries.Any(e => e.act == 4 && e.id == "rail" && e.saved);
        SceneInputGate gate;
        DeskScenePreview view;
        SceneObjectInteractor interactor;
        Phase resumeAt;
        int storedDeskAct;
        bool ownsView;
        float elapsed, fromFov, toFov;
        Vector3 fromPosition, toPosition;
        Quaternion fromRotation, toRotation;

        void Awake()
        { gate = GetComponent<SceneInputGate>(); view = GetComponent<DeskScenePreview>(); interactor = GetComponent<SceneObjectInteractor>(); }
        void OnEnable()
        {
            if (dialogue) { dialogue.eventCompleted.AddListener(Finished); dialogue.eventCancelled.AddListener(Cancelled); }
            if (story)
            {
                story.actPrepared.AddListener(Prepared); story.actEntered.AddListener(Entered);
                story.sequenceCancelled.AddListener(TransitionCancelled);
            }
        }
        bool Ready => archive && records && dialogue && story && summary && confession && gate && view;
        bool Busy => gate.BlocksWorldInput || dialogue.IsActive || story.IsActive || (interactor && interactor.InspectedObject);
        void Update() { if (Application.isFocused) AdvanceTime(Time.unscaledDeltaTime); }
        void AdvanceTime(float seconds)
        {
            if (!Ready || gate.ConsoleOpen || archive.CurrentAct != 4) return;
            switch (State)
            {
                case Phase.Waiting:
                    if (archive.IsAvailable(4) && SolutionsReady) State = Phase.ReturnPending;
                    break;
                case Phase.ReturnPending:
                    if (!Busy && view.BeginStoryView())
                    {
                        ownsView = true; gate.SetStory(true);
                        var camera = view.sceneCamera;
                        fromPosition = camera.transform.position; fromRotation = camera.transform.rotation; fromFov = camera.fieldOfView;
                        view.ShowStoryDesktop();
                        toPosition = camera.transform.position; toRotation = camera.transform.rotation; toFov = camera.fieldOfView;
                        view.SetStoryPose(fromPosition, fromRotation, fromFov); elapsed = 0; State = Phase.Returning;
                    }
                    break;
                case Phase.Returning:
                    elapsed += Mathf.Max(0, seconds);
                    float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / Mathf.Max(0.1f, returnSeconds)));
                    view.SetStoryPose(Vector3.Lerp(fromPosition, toPosition, t), Quaternion.Slerp(fromRotation, toRotation, t), Mathf.Lerp(fromFov, toFov, t));
                    if (elapsed >= returnSeconds && dialogue.PlayInPlace(summary)) State = Phase.Summary;
                    break;
                case Phase.TransitionPending:
                    if (!Busy && story.PlayActTransition(5)) State = Phase.Transition;
                    break;
            }
        }
        void Finished(string id)
        {
            if (State == Phase.Summary && summary && id == summary.id) { ReleaseView(); State = Phase.TransitionPending; }
        }
        void Cancelled(string id)
        {
            if (State == Phase.Summary && id == summary.id) Stop();
        }
        void Prepared(int act)
        {
            if (State != Phase.Transition || act != 5) return;
            storedDeskAct = archive.DeskAct;
            if (!archive.StoreDeskAct()) storedDeskAct = 0;
        }
        void Entered(int act)
        {
            if (State != Phase.Transition || act != 5) return;
            if (!SolutionsReady || !archive.CompleteAct(4)) { Stop(); return; }
            storedDeskAct = 0; State = Phase.Complete;
        }
        void TransitionCancelled() { if (State == Phase.Transition) Stop(); }
        void ReleaseView()
        {
            if (!ownsView) return;
            ownsView = false; if (view) view.EndStoryView(true); if (gate) gate.SetStory(false);
        }
        public void Stop()
        {
            if (State == Phase.Waiting || State == Phase.Complete || State == Phase.Suspended) return;
            var previous = State;
            resumeAt = previous == Phase.Returning || previous == Phase.Summary ? Phase.ReturnPending
                : previous == Phase.Transition ? Phase.TransitionPending : previous;
            State = Phase.Suspended;
            if (dialogue && previous == Phase.Summary && dialogue.ActiveEvent == summary) dialogue.Stop();
            if (previous == Phase.Transition && story.IsActive) story.Stop();
            ReleaseView();
            if (storedDeskAct > 0 && archive.CurrentAct == 4) archive.ShowActOnDesk(storedDeskAct);
            storedDeskAct = 0;
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
            if (story)
            {
                story.actPrepared.RemoveListener(Prepared); story.actEntered.RemoveListener(Entered);
                story.sequenceCancelled.RemoveListener(TransitionCancelled);
            }
        }
    }
}
