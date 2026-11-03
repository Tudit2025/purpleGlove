using UnityEngine;

namespace PurpleGlove
{
    /// <summary>Clickable writing areas on the three original working documents.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(DeskOperationalDocument))]
    public sealed class FifthActDocument : MonoBehaviour
    {
        public enum Kind { Verification, Transfer, Confession }
        public Kind kind;
        public FifthActFlow flow;
        public TextMesh[] choices = new TextMesh[0];
        public Rect[] areas = new Rect[0];
        public TextMesh statusInk, signatureInk;
        public GameObject controls;
        public bool CanEdit
        {
            get
            {
                if (!flow || !flow.CanChoose || !Camera.main) return false;
                var form = GetComponent<DeskOperationalDocument>();
                var gate = Camera.main.GetComponent<SceneInputGate>();
                var interactor = Camera.main.GetComponent<SceneObjectInteractor>();
                return form.IsReady && form.Available && gate && !gate.BlocksWorldInput
                    && interactor && interactor.InspectedObject == GetComponent<SceneObject>();
            }
        }
        void Update()
        {
            RefreshInk();
            if (!CanEdit || !Application.isFocused) return;
            int hover = ChoiceAtPointer(Camera.main, Input.mousePosition);
            for (int i = 0; i < choices.Length; i++) if (choices[i])
                choices[i].color = i == hover ? new Color(0.49f, 0.24f, 0.07f) : new Color(0.18f, 0.13f, 0.08f);
            if (hover >= 0 && Input.GetMouseButtonDown(0)) TryChoose(hover);
        }
        public int ChoiceAtPointer(Camera camera, Vector2 pointer)
        {
            if (!camera || !camera.pixelRect.Contains(pointer)) return -1;
            var plane = new Plane(transform.up, transform.TransformPoint(new Vector3(0, 0.011f, 0)));
            if (!plane.Raycast(camera.ScreenPointToRay(pointer), out float distance)) return -1;
            var p = transform.InverseTransformPoint(camera.ScreenPointToRay(pointer).GetPoint(distance));
            for (int i = 0; i < areas.Length; i++) if (areas[i].Contains(new Vector2(p.x, p.z))) return i;
            return -1;
        }
        public bool TryChoose(int index)
        {
            if (!CanEdit || index < 0 || index >= choices.Length) return false;
            bool changed = kind == Kind.Verification ? flow.SetSourceGuarantee(index == 1)
                : kind == Kind.Transfer ? flow.SetCustody((FifthActFlow.Custody)index) : flow.BeginBurn();
            if (changed) { RefreshInk(); Camera.main.GetComponent<SceneInputGate>().ConsumeFrame(); }
            return changed;
        }
        public void RefreshInk()
        {
            if (!flow || !flow.archive) return;
            bool fifth = flow.archive.CurrentAct >= 5;
            if (controls) controls.SetActive(fifth);
            if (!fifth) return;
            if (kind == Kind.Verification)
            {
                if (choices.Length >= 2)
                {
                    Localization.SetText(choices[0], (flow.GuaranteesSource ? "○ " : "● ") + "不作来源担保");
                    Localization.SetText(choices[1], (flow.GuaranteesSource ? "● " : "○ ") + "担保来源可靠");
                }
                if (statusInk) Localization.SetText(statusInk, "换版记录：法使尚未核签。" + (flow.SourceRevised ? "\n经更正，以当前选择为准。" : ""));
                if (signatureInk) signatureInk.text = flow.GuaranteesSource ? "Luca" : "";
            }
            else if (kind == Kind.Transfer)
            {
                for (int i = 0; i < choices.Length; i++)
                    Localization.SetText(choices[i], ((int)flow.OriginalCustody == i ? "● " : "○ ") + FifthActFlow.CustodyLabel((FifthActFlow.Custody)i));
                if (statusInk) Localization.SetText(statusInk, "当前安排：" + FifthActFlow.CustodyLabel(flow.OriginalCustody));
                if (signatureInk) signatureInk.text = flow.OriginalCustody == FifthActFlow.Custody.Guild ? "" : "Luca";
            }
            else
            {
                if (choices.Length > 0) Localization.SetText(choices[0], "将此页自述明文移近烛火焚毁");
                if (statusInk) Localization.SetText(statusInk, "留存则随卷呈交；焚毁后不可复原。");
            }
        }
    }
}
