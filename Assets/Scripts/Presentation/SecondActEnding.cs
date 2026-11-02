using UnityEngine;

namespace PurpleGlove
{
    public sealed partial class SecondActFlow
    {
        public PrologueSequence story;
        public DialogueEvent deskSummary, thirdActIntro, originalRequest;
        [Min(0.1f)] public float returnSeconds = 0.75f;
        DeskScenePreview view;
        bool ownsDeskView;
        int storedDeskAct;
        float returnElapsed, startFov, endFov;
        Vector3 startPosition, endPosition;
        Quaternion startRotation, endRotation;
        bool EnteringThirdAct => State == Phase.ThirdIntroPending || State == Phase.ThirdIntro
            || State == Phase.ThirdDeliveryPending || State == Phase.ThirdDelivery;

        void SubscribeEnding()
        {
            if (!story) return;
            story.actPrepared.AddListener(EndingActPrepared);
            story.actEntered.AddListener(EndingActEntered);
            story.sequenceCancelled.AddListener(EndingTransitionCancelled);
        }
        void UnsubscribeEnding()
        {
            if (!story) return;
            story.actPrepared.RemoveListener(EndingActPrepared);
            story.actEntered.RemoveListener(EndingActEntered);
            story.sequenceCancelled.RemoveListener(EndingTransitionCancelled);
        }
        void AdvanceEnding(float seconds)
        {
            if (!story || !story.isActiveAndEnabled || !view || !view.isActiveAndEnabled || !deskSummary || !thirdActIntro || !originalRequest) return;
            switch (State)
            {
                case Phase.ReturnPending:
                    if (!Busy && !story.IsActive) BeginEndingReturn();
                    break;
                case Phase.Returning:
                    returnElapsed += Mathf.Max(0, seconds);
                    float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(returnElapsed / Mathf.Max(0.1f, returnSeconds)));
                    view.SetStoryPose(Vector3.Lerp(startPosition, endPosition, t), Quaternion.Slerp(startRotation, endRotation, t), Mathf.Lerp(startFov, endFov, t));
                    if (returnElapsed >= returnSeconds && dialogue.PlayInPlace(deskSummary)) State = Phase.Summary;
                    break;
                case Phase.TransitionPending:
                    if (!Busy && story.PlayActTransition(3)) State = Phase.Transition;
                    break;
                case Phase.OriginalRequestPending:
                    if (!Busy && dialogue.Play(originalRequest)) State = Phase.OriginalRequest;
                    break;
                case Phase.ThirdIntroPending:
                    if (!Busy && dialogue.Play(thirdActIntro)) State = Phase.ThirdIntro;
                    break;
                case Phase.ThirdDeliveryPending:
                    if (archive.IsAvailable(3)) { State = Phase.Complete; break; }
                    if (!Busy && !dialogue.eyeShutter.IsMoving && !dialogue.eyeShutter.AtSecondStop
                        && delivery.BeginDelivery(3)) State = Phase.ThirdDelivery;
                    break;
            }
        }
        void BeginEndingReturn()
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
        void ReleaseEndingView()
        {
            if (!ownsDeskView) return;
            ownsDeskView = false;
            if (view) view.EndStoryView(true);
            if (gate) gate.SetStory(false);
        }
        void EndingDialogueFinished(string id)
        {
            if (State == Phase.Summary && deskSummary && id == deskSummary.id)
            { ReleaseEndingView(); State = Phase.OriginalBellReady; bell.SetRingAllowed(true); }
            else if (State == Phase.OriginalRequest && originalRequest && id == originalRequest.id) State = Phase.TransitionPending;
            else if (State == Phase.ThirdIntro && thirdActIntro && id == thirdActIntro.id) State = Phase.ThirdDeliveryPending;
        }
        bool IsEndingDialogue(string id) => (State == Phase.Summary && deskSummary && id == deskSummary.id)
            || (State == Phase.OriginalRequest && originalRequest && id == originalRequest.id)
            || (State == Phase.ThirdIntro && thirdActIntro && id == thirdActIntro.id);
        void EndingActPrepared(int act)
        {
            if (State != Phase.Transition || act != 3) return;
            storedDeskAct = archive.DeskAct;
            if (!archive.StoreDeskAct()) storedDeskAct = 0;
        }
        void EndingActEntered(int act)
        {
            if (State != Phase.Transition || act != 3) return;
            if (archive.CurrentAct != 2 || !archive.CompleteAct(2)) { Stop(); return; }
            storedDeskAct = 0; State = Phase.ThirdIntroPending;
        }
        void EndingTransitionCancelled() { if (State == Phase.Transition) Stop(); }
        Phase EndingResumePhase(Phase previous) => previous == Phase.Returning || previous == Phase.Summary ? Phase.ReturnPending
            : previous == Phase.OriginalRequest ? Phase.OriginalRequestPending
            : previous == Phase.Transition ? Phase.TransitionPending : previous == Phase.ThirdIntro ? Phase.ThirdIntroPending
            : previous == Phase.ThirdDelivery ? Phase.ThirdDeliveryPending : previous;
        void StopEnding(Phase previous)
        {
            if (dialogue && ((previous == Phase.Summary && dialogue.ActiveEvent == deskSummary)
                || (previous == Phase.OriginalRequest && dialogue.ActiveEvent == originalRequest)
                || (previous == Phase.ThirdIntro && dialogue.ActiveEvent == thirdActIntro))) dialogue.Stop();
            if (previous == Phase.Transition && story && story.IsActive) story.Stop();
            if (previous == Phase.ThirdDelivery && delivery && delivery.ActiveAct == 3) delivery.Stop();
            ReleaseEndingView();
            if (storedDeskAct > 0 && archive && archive.CurrentAct == 2) archive.ShowActOnDesk(storedDeskAct);
            storedDeskAct = 0;
        }
    }
}
