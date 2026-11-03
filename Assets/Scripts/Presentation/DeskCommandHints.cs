using UnityEngine;

namespace PurpleGlove
{
    /// <summary>Read-only next actions derived from the live story; never advances or remembers progress.</summary>
    public sealed class DeskCommandHints
    {
        public readonly struct Hint
        {
            public readonly string task, detail;
            public bool IsVisible => !string.IsNullOrEmpty(task);
            // Hints are authored in Chinese; already-translated text passes through unchanged.
            public Hint(string task, string detail) { this.task = Localization.Text(task); this.detail = Localization.Text(detail); }
        }

        readonly PlaintextArchiveController records;
        readonly ActArchiveController archive;
        readonly SceneInputGate gate;
        readonly SceneObjectInteractor interactor;
        readonly DeskScenePreview view;
        readonly SealedDeliveryController delivery;
        readonly FirstActFlow first;
        readonly SecondActFlow second;
        readonly ThirdActFlow third;
        readonly FourthActFlow fourth;
        readonly FifthActFlow fifth;

        public DeskCommandHints(GameObject host)
        {
            records = host.GetComponent<PlaintextArchiveController>();
            archive = records ? records.archive : null;
            gate = host.GetComponent<SceneInputGate>();
            interactor = host.GetComponent<SceneObjectInteractor>();
            view = host.GetComponent<DeskScenePreview>();
            delivery = host.GetComponent<SealedDeliveryController>();
            first = host.GetComponent<FirstActFlow>();
            second = host.GetComponent<SecondActFlow>();
            third = host.GetComponent<ThirdActFlow>();
            fourth = host.GetComponent<FourthActFlow>();
            fifth = host.GetComponent<FifthActFlow>();
        }

        SceneObject Inspected => interactor ? interactor.InspectedObject : null;

        public Hint Resolve()
        {
            var form = Inspected ? Inspected.GetComponent<DeskOperationalDocument>() : null;
            if (!archive || !archive.isActiveAndEnabled || !records || !gate || !view
                || gate.ConsoleOpen || gate.DialogueActive || gate.StoryActive || gate.PropActionActive
                || (view.StoryLocked && (!form || !form.IsOpen)) || view.DialogueLocked || archive.AllActsCompleted) return default;

            // Delivery owns the camera, but collecting the packet is still a player action.
            if (delivery && delivery.IsActive)
                return delivery.State == SealedDeliveryController.Phase.AwaitingPickup && !view.IsEnteringDoorway
                    ? new Hint("收取传物舱中的封套", "点击封套，移至案头拆阅。") : default;
            if (gate.DeliveryActive) return default;

            int act = archive.CurrentAct;
            switch (act)
            {
                case 1:
                    if (first && first.State == FirstActFlow.Phase.Suspended) return default;
                    if (!archive.IsAvailable(1)) return Wait("候执事送入文书");
                    if (!Saved("caesar")) return Cipher(1, "破译师父封袋里的密文", "依笔记校准转盘，将译文誊于明文纸上。");
                    return Wait("译文已留档，候门外来人");
                case 2:
                    if (!second || second.State == SecondActFlow.Phase.Suspended) return default;
                    if (second.State == SecondActFlow.Phase.BellReady)
                        return Ring("摇铃，请执事调取封档");
                    if (second.State == SecondActFlow.Phase.OriginalBellReady)
                        return Ring("摇铃，请执事调取原始档案");
                    if (!archive.IsAvailable(2)) return Wait("候执事送入文书");
                    if (!Saved("book")) return Cipher(2, "破译印坊数字便条", "对照祷文集查字，在明文纸上记下译文。");
                    if (archive.HasPendingSupplement(2)) return Wait("候执事调来封档");
                    if (!Saved("pigpen")) return Cipher(2, "破译封档内的猪圈密文", "参看猪圈密码表，将碎纸符号还原至明文纸。");
                    return Wait("核对封档中留下的线索");
                case 3:
                    if (!third || third.State == ThirdActFlow.Phase.Suspended) return default;
                    if (!archive.IsAvailable(3)) return Wait("候执事送入文书");
                    if (!Saved("vigenere")) return Cipher(3, "破译师父译录的军情密文", "对照委托与师父笔记，在译码台还原并记下明文。");
                    if (third.verification && third.verification.CanSign)
                        return new Hint("签署明文还原", Inspected == third.verification.GetComponent<SceneObject>()
                            ? "点击核验表上的「明文还原」签名栏。"
                            : Inspected ? "Esc 放回手中文书，再打开文书架上的军情核验表。"
                            : "打开文书架上的军情核验表，点击明文还原签名栏。");
                    return Wait(third.verification && third.verification.Signed ? "核验已签，候门外来人" : "核对已还原的明文");
                case 4:
                    if (fourth && fourth.State == FourthActFlow.Phase.Suspended) return default;
                    if (!archive.IsAvailable(4)) return Wait("候执事送入文书");
                    bool affine = Saved("affine"), rail = Saved("rail");
                    if (!affine && !rail) return Cipher(4, "破译师父的两张密文纸条", "分别还原仿射与栅栏两段密文，将译文记在明文纸上。");
                    if (!affine) return Cipher(4, "破译仿射密码纸条", "另一段译文已留档，继续还原仿射密文并记下明文。");
                    if (!rail) return Cipher(4, "破译栅栏密码纸条", "另一段译文已留档，继续还原栅栏密文并记下明文。");
                    return Wait("两段译文已留档");
                case 5:
                    if (!fifth || fifth.State == FifthActFlow.Phase.Suspended) return default;
                    if (fifth.CanChoose) return FinalPapers();
                    if (fifth.State == FifthActFlow.Phase.HandoffPending || fifth.State == FifthActFlow.Phase.DispatchPending)
                        return Wait("候执事收取文书");
                    return !archive.IsAvailable(5) ? Wait("候执事送入文书") : default;
                default: return default;
            }
        }

        bool Saved(string id)
        {
            foreach (var entry in records.entries)
                if (entry.act == archive.CurrentAct && entry.id == id && entry.saved) return true;
            return false;
        }

        Hint Cipher(int act, string task, string detail)
        {
            if (archive.DeskAct != act)
                return new Hint("取回当前案卷", Inspected ? "Esc 放回手中文书，再到左侧书架取回当前案卷。"
                    : Localization.Format("到左侧书架点击「{0}」，将文书放回案头。", Localization.Text(archive.acts[act - 1].title)));
            if (gate.WorkbenchTyping) detail = "记下完整译文后，按 Esc 放下羽笔，留存译文。";
            else if (Inspected) detail = Localization.Text(detail) + Localization.Text(" 阅毕按 Esc 放回。");
            return new Hint(task, detail);
        }

        Hint Ring(string task) => new Hint(task, Inspected ? "Esc 放回手中文书，再点击桌上的手铃。"
            : gate.WorkbenchModal || gate.WorkbenchTyping ? "Esc 收起手边工具，再回桌面中央点击手铃。"
            : view.CurrentView != DeskScenePreview.View.Desktop ? "返回桌面中央，点击手铃唤来执事。" : "点击桌上的手铃唤来执事。");

        Hint Wait(string task) => new Hint(task, Inspected ? "Esc 放回手中文书，继续办理。"
            : gate.WorkbenchModal || gate.WorkbenchTyping ? "Esc 收起手边的工具，继续办理。" : "稍候片刻，留意门外动静。");

        Hint FinalPapers()
        {
            var form = Inspected ? Inspected.GetComponent<FifthActDocument>() : null;
            if (form)
            {
                switch (form.kind)
                {
                    case FifthActDocument.Kind.Verification:
                        return new Hint("核定来源担保", "在核验表上择定是否担保来源；Esc 放回文书。");
                    case FifthActDocument.Kind.Transfer:
                        return new Hint("登记原件去向", "在转交申请表上择定受领方；Esc 放回文书。");
                    case FifthActDocument.Kind.Confession:
                        return new Hint("斟酌自述明文的处置", "可留存随卷递交，或点击纸上焚毁项；Esc 放回文书。");
                }
            }
            return new Hint("核定文书，摇铃交卷", Inspected ? "Esc 放回手中文书；定妥后点击手铃，请执事收卷。"
                : fifth.ConfessionBurned ? "自述明文已毁。核对来源担保与原件去向，定妥后摇铃请执事收卷。"
                : "在文书架核对来源担保、原件去向与自述处置；定妥后摇铃请执事收卷。");
        }
    }
}
