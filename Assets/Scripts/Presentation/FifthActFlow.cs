using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace PurpleGlove
{
    /// <summary>Three working papers, one bell, a physical outgoing handoff and the epilogue transition.</summary>
    [DefaultExecutionOrder(-70), DisallowMultipleComponent]
    public sealed class FifthActFlow : MonoBehaviour
    {
        public enum Phase { Waiting, IntroPending, Intro, DeliveryPending, Delivery, Choosing, Burning,
            HandoffPending, Handoff, DispatchPending, TurningInside, Loading, TurningOutside, FadePending, Fading, Complete, Suspended,
            BurnChoice, BurnPending }
        public enum Custody { Guild, Milan, Venice, France }
        public ActArchiveController archive;
        public PlaintextArchiveController records;
        public ThirdActVerification verification;
        public DialogueController dialogue;
        public SealedDeliveryController delivery;
        public DeskHandbell bell;
        public ActTransitionOverlay curtain;
        public EpilogueFlow epilogue;
        public DialogueEvent introduction, handoff, guaranteeWarning;
        public SceneObject transferApplication, confession;
        public SceneObject[] originals = new SceneObject[0];
        public Transform burnTarget, dispatchRoot;
        public GameObject ashes;
        public TextMesh destroyedTab;
        public float flightSeconds = 1.3f, burnSeconds = 2.4f;
        public UnityEvent submitted = new UnityEvent();
        public Phase State { get; private set; }
        public bool GuaranteesSource { get; private set; }
        public bool SourceRevised { get; private set; }
        public Custody OriginalCustody { get; private set; }
        public bool ConfessionBurned { get; private set; }
        public bool Dispatched { get; private set; }
        public bool CanChoose => isActiveAndEnabled && State == Phase.Choosing && archive && archive.CurrentAct == 5
            && archive.IsAvailable(5) && verification && verification.Signed;
        public string Ending => GuaranteesSource ? "背书"
            : OriginalCustody != Custody.Guild ? "真相易手" : ConfessionBurned ? "缺页" : "开门";
        SceneInputGate gate;
        SceneObjectInteractor interactor;
        DeskScenePreview view;
        Phase resumeAt;
        DialogueEvent handoffInstance, ringChoice, burnChoice;
        readonly List<PaperPose> moving = new List<PaperPose>();
        PaperPose burning;
        float elapsed;
        bool originalDrumStop, sourceChosen;
        int storedDeskAct;
        bool transitionView;
        DoorMechanism Drum => delivery ? delivery.transferDrum : null;
        [Serializable] sealed class PaperPose
        {
            public SceneObject paper;
            public Transform parent;
            public Vector3 localPosition, localScale, worldPosition;
            public Quaternion localRotation, worldRotation;
            public bool enabled, active;
            public static PaperPose Capture(SceneObject paper)
            {
                paper.RestoreImmediately(); var t = paper.transform;
                return new PaperPose { paper = paper, parent = t.parent, localPosition = t.localPosition, localRotation = t.localRotation,
                    localScale = t.localScale, worldPosition = t.position, worldRotation = t.rotation, enabled = paper.enabled, active = paper.gameObject.activeSelf };
            }
            public void Restore()
            {
                if (!paper) return;
                paper.Relocate(parent, localPosition, localRotation, localScale); paper.enabled = enabled; paper.gameObject.SetActive(active);
                var form = paper.GetComponent<DeskOperationalDocument>();
                if (form && form.rackTab) form.rackTab.SetActive(true);
            }
        }
        void Awake()
        { gate = GetComponent<SceneInputGate>(); interactor = GetComponent<SceneObjectInteractor>(); view = GetComponent<DeskScenePreview>(); }
        void OnEnable()
        {
            if (bell) bell.rang.AddListener(Rang);
            if (dialogue) { dialogue.eventCompleted.AddListener(Finished); dialogue.eventCancelled.AddListener(Cancelled); }
            if (delivery) { delivery.deliveryCompleted.AddListener(Delivered); delivery.deliveryCancelled.AddListener(DeliveryCancelled); }
        }
        bool Ready => archive && records && verification && dialogue && delivery && bell && curtain && introduction && handoff
            && gate && view && interactor && transferApplication && confession && burnTarget && dispatchRoot && Drum;
        bool Busy => gate.BlocksWorldInput || dialogue.IsActive || delivery.IsActive || bell.IsBusy || view.StoryLocked || interactor.InspectedObject;
        void Update()
        {
            if (!Ready) return;
            if (State == Phase.Fading) curtain.SetConsoleVisible(gate.ConsoleOpen);
            if (Application.isFocused) AdvanceTime(Time.unscaledDeltaTime);
        }
        void AdvanceTime(float seconds)
        {
            if (!Ready || gate.ConsoleOpen) return;
            EnsureChoices();
            if (archive.CurrentAct >= 5)
            {
                bell.ringConfirmation = ringChoice;
                bell.SetRingAllowed(CanChoose);
            }
            switch (State)
            {
                case Phase.Waiting:
                    if (archive.CurrentAct == 5 && archive.IsCompleted(4)) State = Phase.IntroPending;
                    break;
                case Phase.IntroPending:
                    if (!Busy && dialogue.Play(introduction)) State = Phase.Intro;
                    break;
                case Phase.DeliveryPending:
                    if (archive.IsAvailable(5)) { State = Phase.Choosing; break; }
                    if (!Busy && !dialogue.eyeShutter.IsMoving && !dialogue.eyeShutter.AtSecondStop && delivery.BeginDelivery(5)) State = Phase.Delivery;
                    break;
                case Phase.Burning: AdvanceBurn(seconds); break;
                case Phase.BurnPending:
                    if (!gate.BlocksWorldInput && !dialogue.IsActive) StartBurn();
                    break;
                case Phase.HandoffPending:
                    if (!Busy)
                    {
                        PrepareHandoff();
                        if (dialogue.Play(handoffInstance)) State = Phase.Handoff;
                    }
                    break;
                case Phase.DispatchPending:
                    if (!Busy && !dialogue.eyeShutter.IsMoving && !dialogue.eyeShutter.AtSecondStop) BeginDispatch();
                    break;
                case Phase.TurningInside:
                    if (!Drum.IsMoving && Drum.CargoIsInside && !view.IsEnteringDoorway) LoadPapers();
                    break;
                case Phase.Loading: AdvanceLoading(seconds); break;
                case Phase.TurningOutside:
                    if (!Drum.IsMoving && !Drum.AtSecondStop) CommitDispatch();
                    break;
                case Phase.FadePending:
                    if (epilogue && epilogue.isActiveAndEnabled && !curtain.IsActive
                        && curtain.Begin(6, PrepareEpilogue, Complete)) State = Phase.Fading;
                    break;
                case Phase.Fading: curtain.Advance(seconds); break;
            }
        }
        public static string CustodyLabel(Custody custody) => custody == Custody.Milan ? "交米兰" : custody == Custody.Venice ? "交威尼斯"
            : custody == Custody.France ? "交法国" : "继续封存";
        public bool SetSourceGuarantee(bool guarantee)
        {
            if (!CanChoose || gate.BlocksWorldInput || dialogue.IsActive) return false;
            bool changed = GuaranteesSource != guarantee;
            if (sourceChosen && changed) SourceRevised = true;
            GuaranteesSource = guarantee; sourceChosen = true;
            if (changed && guarantee && guaranteeWarning) dialogue.PlayInPlace(guaranteeWarning, true);
            return true;
        }
        public bool SetCustody(Custody custody)
        {
            if (!CanChoose || gate.BlocksWorldInput || !Enum.IsDefined(typeof(Custody), custody)) return false;
            OriginalCustody = custody; return true;
        }
        public bool BeginBurn()
        {
            if (!CanChoose || ConfessionBurned || gate.BlocksWorldInput || interactor.InspectedObject != confession) return false;
            EnsureChoices();
            if (!dialogue.PlayInPlace(burnChoice, true)) return false;
            State = Phase.BurnChoice;
            return true;
        }
        void EnsureChoices()
        {
            if (ringChoice && burnChoice) return;
            var speaker = dialogue.speakers.FirstOrDefault(s => s && s.isProtagonist);
            DialogueEvent Choice(string id, string prompt, string yesId, string yes, string noId, string no)
            {
                var sequence = ScriptableObject.CreateInstance<DialogueEvent>();
                sequence.name = sequence.id = id; sequence.arrivalDelay = 0;
                sequence.lines = new[] { new DialogueEvent.Line { speaker = speaker, text = prompt, choices = new[] {
                    new DialogueEvent.Choice { id = yesId, text = yes }, new DialogueEvent.Choice { id = noId, text = no } } } };
                return sequence;
            }
            if (!ringChoice) ringChoice = Choice("act5_ring_choice", "现在摇铃，请执事按当前安排接走文件吗？",
                "ring", "摇铃，按当前安排交卷。", "wait", "暂不摇铃，我还要再想一想。");
            if (!burnChoice) burnChoice = Choice("act5_burn_choice", "要烧毁师父的这张自述明文吗？烧毁后，本次交卷将不再附上它。",
                "burn", "烧毁这张自述明文。", "keep", "保留，随核验文件一起递交。");
        }
        void StartBurn()
        {
            if (ConfessionBurned || interactor.InspectedObject != confession) { State = Phase.Choosing; return; }
            interactor.CancelInteraction();
            burning = PaperPose.Capture(confession);
            if (!view.BeginStoryView()) { burning = null; State = Phase.Choosing; return; }
            view.ShowStoryDesktop(); gate.SetStory(true); bell.SetRingAllowed(false);
            confession.enabled = false;
            confession.transform.SetParent(null, true);
            elapsed = 0; State = Phase.Burning;
        }
        void AdvanceBurn(float seconds)
        {
            elapsed += Mathf.Max(0, seconds);
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, burnSeconds));
            float approach = Mathf.SmoothStep(0, 1, Mathf.Min(1, t / 0.5f));
            confession.transform.SetPositionAndRotation(Vector3.Lerp(burning.worldPosition, burnTarget.position, approach),
                Quaternion.Slerp(burning.worldRotation, burnTarget.rotation, approach));
            float remaining = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - 0.5f) * 2));
            confession.transform.localScale = Vector3.Scale(burning.localScale, new Vector3(remaining, 1, remaining));
            if (t < 1) return;
            ConfessionBurned = true; archive.WithdrawDocument(confession); burning = null;
            if (ashes) ashes.SetActive(true);
            if (destroyedTab) { destroyedTab.text = "自述明文已毁"; destroyedTab.gameObject.SetActive(true); }
            view.EndStoryView(true); gate.SetStory(false); State = Phase.Choosing;
        }
        void Rang()
        {
            if (!CanChoose) return;
            bell.SetRingAllowed(false); State = Phase.HandoffPending;
        }
        void PrepareHandoff()
        {
            if (handoffInstance) return;
            handoffInstance = Instantiate(handoff); handoffInstance.name = handoff.name + " (交卷记录)";
            handoffInstance.lines = handoff.lines.Select(l => new DialogueEvent.Line { speaker = l.speaker, text = l.text
                .Replace("{来源}", GuaranteesSource ? "你已签名担保军令来源可靠。" : "明文一致签名保留，来源不作担保。")
                .Replace("{原件}", CustodyLabel(OriginalCustody))
                .Replace("{自述}", ConfessionBurned ? "自述明文已焚毁，本次不附入卷宗。" : "师父自述保留，随卷递交。")
                .Replace("{手续}", GuaranteesSource ? "我会转递你的核验结论，并按申请办理原件交接。"
                    : "我会附上证据副本，先请威尼斯与法国具名参与，再经米兰渠道办理暂停转押。") }).ToArray();
        }
        void Finished(string id)
        {
            if (State == Phase.Intro && id == introduction.id) State = Phase.DeliveryPending;
            else if (State == Phase.Handoff && handoffInstance && id == handoffInstance.id) State = Phase.DispatchPending;
            else if (State == Phase.BurnChoice && burnChoice && id == burnChoice.id)
                State = dialogue.SelectedChoiceId == "burn" ? Phase.BurnPending : Phase.Choosing;
        }
        void Cancelled(string id)
        {
            if (State == Phase.BurnChoice && burnChoice && id == burnChoice.id) State = Phase.Choosing;
            else if ((State == Phase.Intro && id == introduction.id) || (State == Phase.Handoff && handoffInstance && id == handoffInstance.id)) Stop();
        }
        void Delivered(int act) { if (act == 5 && State == Phase.Delivery) State = Phase.Choosing; }
        void DeliveryCancelled(int act) { if (act == 5 && State == Phase.Delivery) Stop(); }
        void BeginDispatch()
        {
            if (Drum.IsMoving || Drum.Cargo || Dispatched) return;
            var papers = new List<SceneObject> { verification.GetComponent<SceneObject>(), transferApplication };
            if (!ConfessionBurned) papers.Add(confession);
            papers.AddRange(originals);
            if (papers.Any(p => !p)) return;
            interactor.CancelInteraction(); moving.Clear();
            foreach (var paper in papers.Distinct()) moving.Add(PaperPose.Capture(paper));
            originalDrumStop = Drum.AtSecondStop;
            view.BeginDeliveryView(); gate.SetDelivery(true); State = Phase.TurningInside;
            if (!Drum.AtSecondStop) Drum.MoveTo(true);
        }
        void LoadPapers()
        {
            dispatchRoot.SetPositionAndRotation(Drum.cargoSocket.position + Drum.cargoSocket.up * 0.03f,
                Drum.cargoSocket.rotation * Quaternion.Euler(28, 0, 0));
            dispatchRoot.gameObject.SetActive(true);
            foreach (var pose in moving)
            {
                pose.paper.enabled = false;
                pose.paper.Relocate(dispatchRoot, Vector3.zero, Quaternion.identity, pose.localScale);
                pose.paper.transform.SetPositionAndRotation(pose.worldPosition, pose.worldRotation);
                pose.paper.gameObject.SetActive(true);
                var rack = pose.paper.GetComponent<DeskOperationalDocument>(); if (rack && rack.rackTab) rack.rackTab.SetActive(false);
            }
            elapsed = 0; State = Phase.Loading;
        }
        void AdvanceLoading(float seconds)
        {
            elapsed += Mathf.Max(0, seconds);
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / Mathf.Max(0.1f, flightSeconds)));
            for (int i = 0; i < moving.Count; i++)
            {
                var pose = moving[i]; var paper = pose.paper;
                var target = dispatchRoot.TransformPoint(new Vector3(0, 0.02f + i * 0.004f, 0));
                paper.transform.SetPositionAndRotation(Vector3.Lerp(pose.worldPosition, target, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.18f),
                    Quaternion.Slerp(pose.worldRotation, dispatchRoot.rotation, t));
                float scale = 0.19f / paper.GetComponent<BoxCollider>().size.x;
                paper.transform.localScale = Vector3.Lerp(pose.localScale, Vector3.one * scale, t);
            }
            if (t < 1) return;
            if (!Drum.TryLoadCargo(dispatchRoot)) { Stop(); return; }
            dispatchRoot.localPosition = new Vector3(0, 0.03f, 0);
            dispatchRoot.localRotation = Quaternion.Euler(28, 0, 0);
            if (!Drum.MoveTo(false)) { Stop(); return; }
            State = Phase.TurningOutside;
        }
        void CommitDispatch()
        {
            if (Dispatched) return;
            Dispatched = true;
            if (Drum.Cargo == dispatchRoot) Drum.ReleaseCargo(transform);
            foreach (var pose in moving) archive.WithdrawDocument(pose.paper);
            moving.Clear(); dispatchRoot.gameObject.SetActive(false);
            view.EndDeliveryView(false); gate.SetDelivery(false); gate.SetStory(true);
            State = Phase.FadePending; submitted.Invoke();
        }
        void PrepareEpilogue()
        {
            storedDeskAct = archive.DeskAct;
            archive.StoreDeskAct();
            transitionView = view.BeginStoryView();
            if (transitionView) view.ShowStoryDesktop();
        }
        void Complete()
        {
            if (!Dispatched || State != Phase.Fading) return;
            archive.CompleteAct(5); State = Phase.Complete;
            storedDeskAct = 0;
            if (transitionView) view.EndStoryView(true);
            transitionView = false;
            epilogue.Begin();
        }
        // Seed the already submitted fifth act for a fresh-scene epilogue test.
        internal void PrepareEpilogueForTesting(bool guarantee, Custody custody, bool burned)
        {
            GuaranteesSource = guarantee; sourceChosen = true; OriginalCustody = custody;
            ConfessionBurned = burned; Dispatched = true; State = Phase.Complete;
            var papers = new List<SceneObject> { verification.GetComponent<SceneObject>(), transferApplication, confession };
            papers.AddRange(originals);
            foreach (var paper in papers.Distinct())
            {
                archive.WithdrawDocument(paper);
                var rack = paper.GetComponent<DeskOperationalDocument>();
                if (rack && rack.rackTab) rack.rackTab.SetActive(false);
            }
            if (ashes) ashes.SetActive(burned);
            if (destroyedTab) { destroyedTab.text = burned ? "自述明文已毁" : ""; destroyedTab.gameObject.SetActive(burned); }
        }

        public void Stop()
        {
            if (State == Phase.Waiting || State == Phase.Complete || State == Phase.Suspended) return;
            if (State == Phase.TurningOutside && !Drum.IsMoving && !Drum.AtSecondStop) CommitDispatch();
            var previous = State;
            resumeAt = Dispatched ? Phase.FadePending : previous == Phase.Intro ? Phase.IntroPending
                : previous == Phase.Delivery ? Phase.DeliveryPending : previous == Phase.Handoff ? Phase.HandoffPending
                : previous == Phase.TurningInside || previous == Phase.Loading || previous == Phase.TurningOutside ? Phase.DispatchPending
                : previous == Phase.Burning || previous == Phase.BurnChoice || previous == Phase.BurnPending ? Phase.Choosing : previous;
            State = Phase.Suspended;
            if (bell) { bell.SetRingAllowed(false); bell.Cancel(); }
            if (dialogue && ((previous == Phase.Intro && dialogue.ActiveEvent == introduction)
                || (previous == Phase.Handoff && dialogue.ActiveEvent == handoffInstance)
                || (previous == Phase.BurnChoice && dialogue.ActiveEvent == burnChoice))) dialogue.Stop();
            if (previous == Phase.Delivery && delivery.IsActive) delivery.Stop();
            if (burning != null) { burning.Restore(); burning = null; view.EndStoryView(true); }
            if (moving.Count > 0)
            {
                Drum.SetPoseImmediately(originalDrumStop);
                if (Drum.Cargo == dispatchRoot) Drum.ReleaseCargo(transform);
                foreach (var pose in moving) pose.Restore();
                moving.Clear(); dispatchRoot.gameObject.SetActive(false);
            }
            if (previous == Phase.Fading || previous == Phase.FadePending) curtain.Cancel();
            if (transitionView) view.EndStoryView(false);
            transitionView = false;
            view.EndDeliveryView(false); gate.SetDelivery(false); gate.SetStory(false);
            if (storedDeskAct > 0 && archive.CurrentAct == 5) archive.ShowActOnDesk(storedDeskAct);
            storedDeskAct = 0;
        }
        public bool Resume()
        {
            if (!isActiveAndEnabled || !Ready || State != Phase.Suspended || archive.CurrentAct != 5) return false;
            State = resumeAt;
            if (Dispatched) gate.SetStory(true);
            return true;
        }
        void OnDisable()
        {
            Stop();
            if (bell)
            {
                bell.rang.RemoveListener(Rang);
                if (bell.ringConfirmation == ringChoice) bell.ringConfirmation = null;
            }
            if (dialogue) { dialogue.eventCompleted.RemoveListener(Finished); dialogue.eventCancelled.RemoveListener(Cancelled); }
            if (delivery) { delivery.deliveryCompleted.RemoveListener(Delivered); delivery.deliveryCancelled.RemoveListener(DeliveryCancelled); }
        }
        void OnDestroy()
        {
            if (handoffInstance) Destroy(handoffInstance);
            if (ringChoice) Destroy(ringChoice);
            if (burnChoice) Destroy(burnChoice);
        }
    }
}
