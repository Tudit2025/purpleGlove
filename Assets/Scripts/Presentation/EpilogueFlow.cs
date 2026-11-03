using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace PurpleGlove
{
    /// <summary>Act six: witnessed results, physical receipts and visits through the closed door.</summary>
    [DefaultExecutionOrder(-60), DisallowMultipleComponent]
    public sealed class EpilogueFlow : MonoBehaviour
    {
        public enum Phase { Waiting, ReportPending, Report, ReceiptPending, TurningInside, Flying,
            TurningOutside, ReceiptHold, TimePending, TimeTransition, ArrivalPending, Arrival,
            EndingPending, Ending, FinishPending, FinalFade, Complete, Suspended }
        public FifthActFlow fifth;
        public DialogueController dialogue;
        public ActTransitionOverlay curtain;
        public DoorMechanism drum;
        public Transform doorLeaf, reportReceipt, reunionReceipt, reportAnchor, reunionAnchor;
        public TextMesh reportInk;
        public DialogueEvent savedReport, transferredReport, endorsedReport, reunionArrival;
        public DialogueEvent openEnding, missingEnding, transferredEnding, endorsedEnding;
        public AudioClip doorOpeningSound;
        public float flightSeconds = 1.25f, receiptHoldSeconds = 2.5f, doorOpenSeconds = 1.6f, doorOpenAngle = 85;
        public UnityEvent completed = new UnityEvent();
        public Phase State { get; private set; }
        public string EndingName { get; private set; }
        public bool ReportReceived { get; private set; }
        public bool ReunionReceived { get; private set; }
        public bool IsRunning => State != Phase.Waiting && State != Phase.Complete && State != Phase.Suspended;
        public bool Reunited => EndingName == "开门" || EndingName == "缺页";
        SceneInputGate gate;
        DeskScenePreview view;
        AudioSource sound;
        DialogueEvent report, ending;
        Transform incoming;
        Phase resumeAt;
        Quaternion closedDoor, paperFromRotation;
        Vector3 paperFromPosition, paperFromScale;
        float elapsed, originalFadeSeconds;
        bool receivingReunion, ownsView, paused, fadeDurationChanged;

        void Awake()
        {
            gate = GetComponent<SceneInputGate>(); view = GetComponent<DeskScenePreview>();
            sound = gameObject.AddComponent<AudioSource>(); sound.playOnAwake = false;
            sound.spatialBlend = 0; sound.volume = 0.55f;
        }
        void OnEnable()
        {
            if (dialogue) { dialogue.eventCompleted.AddListener(Finished); dialogue.eventCancelled.AddListener(Cancelled); }
        }
        bool Ready => fifth && dialogue && curtain && drum && doorLeaf && gate && view && reportReceipt
            && reunionReceipt && reportAnchor && reunionAnchor && reportInk && savedReport && transferredReport
            && endorsedReport && reunionArrival && openEnding && missingEnding && transferredEnding && endorsedEnding;

        public bool Begin()
        {
            if (!Ready || !isActiveAndEnabled || State != Phase.Waiting || !fifth.Dispatched
                || fifth.State != FifthActFlow.Phase.Complete || fifth.archive.CurrentAct != 6) return false;
            EndingName = fifth.Ending;
            closedDoor = doorLeaf.localRotation;
            report = Instantiate(fifth.GuaranteesSource ? endorsedReport : Reunited ? savedReport : transferredReport);
            ending = Instantiate(EndingName == "开门" ? openEnding : EndingName == "缺页" ? missingEnding
                : EndingName == "真相易手" ? transferredEnding : endorsedEnding);
            FillCustody(report); FillCustody(ending);
            Localization.SetText(reportInk, ReportText());
            reportReceipt.gameObject.SetActive(false); reunionReceipt.gameObject.SetActive(false);
            gate.SetStory(true); State = Phase.ReportPending;
            return true;
        }
        void FillCustody(DialogueEvent sequence)
        {
            string custody = FifthActFlow.CustodyLabel(fifth.OriginalCustody);
            string consequence;
            switch (fifth.OriginalCustody)
            {
                case FifthActFlow.Custody.Milan:
                    consequence = fifth.GuaranteesSource
                        ? "公爵既收回了换版原件，又握有您的来源担保。依我看，在达成目的之前，他们是绝不会放马泰奥大师回来的。"
                        : "按您的转交申请，换版原件已由米兰代表签收。"; break;
                case FifthActFlow.Custody.Venice:
                    consequence = fifth.GuaranteesSource
                        ? "威尼斯委员会虽拿到了换版原件，但因您签署了来源担保，他们在今早的晨会上依然十分被动。"
                        : "换版原件已移交威尼斯委员会。贝阿特丽切女士带人接走了马泰奥大师，往后要见令师，恐怕得同威尼斯人交涉了。"; break;
                case FifthActFlow.Custody.France:
                    consequence = fifth.GuaranteesSource
                        ? "夏尔先生签收了换版原件，却并未将其公开；他反而质问行会，为何要替一份未经法方核签的军令作保。"
                        : "换版原件已由夏尔先生签收。法方眼下急于追究署名被盗用一事，马泰奥大师只怕要过些时日才能被接回行会。"; break;
                default:
                    consequence = "换版原件虽仍由行会封存，可供三方申请查验，但因那份来源担保，咱们忙活这一整夜，终究是徒劳无功……"; break;
            }
            sequence.lines = sequence.lines.Select(line => new DialogueEvent.Line {
                speaker = line.speaker, text = line.text.Replace("{原件}", custody).Replace("{去向后果}", consequence)
            }).ToArray();
        }
        string ReportText()
        {
            string result = fifth.GuaranteesSource
                ? "六点转押登记：已执行。\n马泰奥已被转移，去向待查。\n本案未启动共同争议审查。\n来源担保签名：Luca。"
                : "威尼斯、法国已具名联署。\n米兰监押书记官确认：\n六点转押暂缓，证人留候。\n粮站表决暂停，转入共同审查。";
            return "第17号委托 · 回执六之一\n清晨六点 / 行会登记送达\n\n" + result
                + "\n\n换版原件：" + FifthActFlow.CustodyLabel(fifth.OriginalCustody) + "。\n交接手续办结，副本留档。";
        }
        void Update()
        {
            if (!Ready || State == Phase.Waiting) return;
            curtain.SetConsoleVisible(gate.ConsoleOpen);
            bool pause = gate.ConsoleOpen || !Application.isFocused;
            if (pause) { if (!paused && sound) sound.Pause(); paused = true; return; }
            if (paused) { if (sound) sound.UnPause(); paused = false; }
            AdvanceTime(Time.unscaledDeltaTime);
        }
        void AdvanceTime(float seconds)
        {
            if (!Ready || gate.ConsoleOpen) return;
            seconds = Mathf.Max(0, seconds);
            switch (State)
            {
                case Phase.ReportPending: PlayVisit(report, Phase.Report); break;
                case Phase.ArrivalPending: PlayVisit(reunionArrival, Phase.Arrival); break;
                case Phase.EndingPending: PlayVisit(ending, Phase.Ending); break;
                case Phase.ReceiptPending: BeginReceipt(); break;
                case Phase.TurningInside:
                    if (!drum.IsMoving && drum.CargoIsInside && !view.IsEnteringDoorway)
                    {
                        drum.ReleaseCargo(transform);
                        paperFromPosition = incoming.position; paperFromRotation = incoming.rotation; paperFromScale = incoming.localScale;
                        view.FollowDeliveryToDesk(); elapsed = 0; State = Phase.Flying;
                    }
                    break;
                case Phase.Flying: FlyReceipt(seconds); break;
                case Phase.TurningOutside:
                    if (!drum.IsMoving && !drum.AtSecondStop)
                    {
                        view.EndDeliveryView(true); gate.SetDelivery(false); HoldView();
                        elapsed = 0; State = Phase.ReceiptHold;
                    }
                    break;
                case Phase.ReceiptHold:
                    elapsed += seconds;
                    if (elapsed >= receiptHoldSeconds)
                        State = Reunited && !ReunionReceived ? Phase.TimePending : Phase.EndingPending;
                    break;
                case Phase.TimePending:
                    HoldView();
                    if (curtain.BeginCard("半小时后\n清晨六点半", null, () => State = Phase.ArrivalPending)) State = Phase.TimeTransition;
                    break;
                case Phase.TimeTransition: curtain.Advance(seconds); break;
                case Phase.FinishPending:
                    elapsed += seconds;
                    if (elapsed >= 0.35f && !dialogue.IsActive && !dialogue.eyeShutter.IsMoving
                        && !dialogue.eyeShutter.AtSecondStop) BeginFinish();
                    break;
                case Phase.FinalFade:
                    elapsed += seconds;
                    if (Reunited) doorLeaf.localRotation = closedDoor * Quaternion.Euler(0,
                        Mathf.SmoothStep(0, doorOpenAngle, Mathf.Clamp01(elapsed / Mathf.Max(0.1f, doorOpenSeconds))), 0);
                    curtain.Advance(seconds);
                    break;
            }
        }
        void PlayVisit(DialogueEvent sequence, Phase playing)
        {
            if (dialogue.IsActive || dialogue.eyeShutter.IsMoving || drum.IsMoving || curtain.IsActive) return;
            ReleaseView(); gate.SetStory(false);
            if (dialogue.Play(sequence)) State = playing;
            else gate.SetStory(true);
        }
        void Finished(string id)
        {
            if (State == Phase.Report && report && id == report.id)
            { gate.SetStory(true); receivingReunion = false; State = Phase.ReceiptPending; }
            else if (State == Phase.Arrival && id == reunionArrival.id)
            { gate.SetStory(true); receivingReunion = true; State = Phase.ReceiptPending; }
            else if (State == Phase.Ending && ending && id == ending.id)
            {
                // Capture the door shot before the free camera can return to the desk.
                HoldView(); elapsed = 0; State = Phase.FinishPending;
            }
        }
        void Cancelled(string id)
        {
            if ((State == Phase.Report && report && id == report.id)
                || (State == Phase.Arrival && id == reunionArrival.id)
                || (State == Phase.Ending && ending && id == ending.id)) Stop();
        }
        void BeginReceipt()
        {
            if (dialogue.IsActive || dialogue.eyeShutter.IsMoving || dialogue.eyeShutter.AtSecondStop
                || drum.IsMoving || drum.Cargo) return;
            ReleaseView(); gate.SetStory(true); view.BeginDeliveryView(); gate.SetDelivery(true);
            incoming = receivingReunion ? reunionReceipt : reportReceipt;
            if (drum.AtSecondStop) drum.SetPoseImmediately(false);
            incoming.gameObject.SetActive(true);
            if (!drum.TryLoadCargo(incoming)) { Stop(); return; }
            incoming.localPosition = new Vector3(0, 0.025f, 0);
            incoming.localRotation = Quaternion.Euler(25, 0, 0); incoming.localScale = Vector3.one * 0.48f;
            drum.MoveTo(true); State = Phase.TurningInside;
        }
        void FlyReceipt(float seconds)
        {
            elapsed += seconds;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / Mathf.Max(0.1f, flightSeconds)));
            var anchor = receivingReunion ? reunionAnchor : reportAnchor;
            incoming.SetPositionAndRotation(Vector3.Lerp(paperFromPosition, anchor.position, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.15f),
                Quaternion.Slerp(paperFromRotation, anchor.rotation, t));
            incoming.localScale = Vector3.Lerp(paperFromScale, Vector3.one, t);
            if (t < 1) return;
            incoming.SetParent(anchor, false); incoming.localPosition = Vector3.zero; incoming.localRotation = Quaternion.identity;
            incoming.localScale = Vector3.one;
            if (receivingReunion) ReunionReceived = true; else ReportReceived = true;
            incoming = null; drum.MoveTo(false); State = Phase.TurningOutside;
        }
        void HoldView()
        { gate.SetStory(true); if (!ownsView) ownsView = view.BeginStoryView(); }
        void ReleaseView()
        { if (ownsView) view.EndStoryView(false); ownsView = false; }
        void BeginFinish()
        {
            HoldView();
            originalFadeSeconds = curtain.fadeOutSeconds; fadeDurationChanged = true;
            curtain.fadeOutSeconds = Mathf.Max(0.1f, doorOpenSeconds);
            if (!curtain.BeginFinalFade("尾声 · " + EndingName + "\n终", Finish)) { RestoreFadeDuration(); return; }
            elapsed = 0; State = Phase.FinalFade;
            if (Reunited && sound && doorOpeningSound) sound.PlayOneShot(doorOpeningSound);
        }
        void Finish()
        {
            RestoreFadeDuration(); State = Phase.Complete;
            gate.SetStory(true); completed.Invoke();
        }
        void RestoreFadeDuration()
        { if (fadeDurationChanged && curtain) curtain.fadeOutSeconds = originalFadeSeconds; fadeDurationChanged = false; }
        public void Stop()
        {
            if (State == Phase.Waiting || State == Phase.Complete || State == Phase.Suspended) return;
            var previous = State;
            resumeAt = previous == Phase.Report ? Phase.ReportPending : previous == Phase.Arrival ? Phase.ArrivalPending
                : previous == Phase.Ending ? Phase.EndingPending : previous == Phase.TimeTransition ? Phase.TimePending
                : previous == Phase.FinalFade ? Phase.FinishPending
                : previous == Phase.TurningInside || previous == Phase.Flying ? Phase.ReceiptPending
                : previous == Phase.TurningOutside ? Phase.ReceiptHold : previous;
            State = Phase.Suspended;
            if (dialogue.IsActive && (dialogue.ActiveEvent == report || dialogue.ActiveEvent == ending || dialogue.ActiveEvent == reunionArrival)) dialogue.Stop();
            if (previous == Phase.TimeTransition || previous == Phase.FinalFade) curtain.Cancel();
            RestoreFadeDuration();
            if (sound) sound.Stop();
            if (incoming)
            {
                drum.SetPoseImmediately(false);
                if (drum.Cargo == incoming) drum.ReleaseCargo(transform);
                incoming.gameObject.SetActive(false); incoming = null;
            }
            else if (previous == Phase.TurningOutside) drum.SetPoseImmediately(false);
            if (previous == Phase.FinalFade) doorLeaf.localRotation = closedDoor;
            view.EndDeliveryView(false); gate.SetDelivery(false); ReleaseView(); gate.SetStory(false); elapsed = 0;
        }
        public bool Resume()
        {
            if (!Ready || !isActiveAndEnabled || State != Phase.Suspended || !fifth.Dispatched || fifth.archive.CurrentAct != 6) return false;
            gate.SetStory(true); State = resumeAt;
            if (State == Phase.FinishPending || State == Phase.ReceiptHold) HoldView();
            return true;
        }
        void OnDisable()
        {
            Stop();
            if (dialogue) { dialogue.eventCompleted.RemoveListener(Finished); dialogue.eventCancelled.RemoveListener(Cancelled); }
        }
        void OnDestroy()
        { if (report) Destroy(report); if (ending) Destroy(ending); }
    }
}
