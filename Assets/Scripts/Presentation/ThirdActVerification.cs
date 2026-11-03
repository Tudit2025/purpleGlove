using System.Linq;
using UnityEngine;

namespace PurpleGlove
{
    /// <summary>The transcript signature confirms only the preserved plaintext, never its source.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SceneObject))]
    public sealed class ThirdActVerification : MonoBehaviour
    {
        public ThirdActFlow flow;
        public TextMesh transcriptInk, signatureInk, signPrompt;
        public Rect signatureArea = new Rect(-0.03f, -0.03f, 0.193f, 0.045f);
        public bool Signed { get; private set; }
        public bool CanSign => flow && flow.isActiveAndEnabled && flow.archive
            && flow.archive.CurrentAct == 3
            && flow.archive.IsDocumentAvailable(3, GetComponent<SceneObject>())
            && flow.State == ThirdActFlow.Phase.WaitingForSignature && !Signed
            && flow.records && flow.records.entries.Any(e => e.act == 3 && e.id == "vigenere" && e.saved);

        void Update()
        {
            RefreshInk();
            if (!CanSign || !Application.isFocused || !Camera.main) return;
            var camera = Camera.main;
            var gate = camera.GetComponent<SceneInputGate>();
            var interactor = camera.GetComponent<SceneObjectInteractor>();
            if (!gate || gate.BlocksWorldInput || !interactor || interactor.InspectedObject != GetComponent<SceneObject>()) return;
            var workingForm = GetComponent<DeskOperationalDocument>();
            if (workingForm && !workingForm.IsReady) return;
            bool hovering = IsSignatureAtPointer(camera, Input.mousePosition);
            if (signPrompt) signPrompt.color = hovering ? new Color(0.45f, 0.22f, 0.07f) : new Color(0.18f, 0.13f, 0.08f);
            if (hovering && Input.GetMouseButtonDown(0)) TrySign();
        }

        public bool IsSignatureAtPointer(Camera camera, Vector2 pointer)
        {
            if (!camera || !camera.pixelRect.Contains(pointer)) return false;
            var ray = camera.ScreenPointToRay(pointer);
            var plane = new Plane(transform.up, transform.TransformPoint(new Vector3(0, 0.01f, 0)));
            if (!plane.Raycast(ray, out float distance)) return false;
            var local = transform.InverseTransformPoint(ray.GetPoint(distance));
            return signatureArea.Contains(new Vector2(local.x, local.z));
        }

        public bool TrySign()
        {
            if (!CanSign || !Camera.main) return false;
            var gate = Camera.main.GetComponent<SceneInputGate>();
            var interactor = Camera.main.GetComponent<SceneObjectInteractor>();
            if (!gate || gate.BlocksWorldInput || !interactor || interactor.InspectedObject != GetComponent<SceneObject>()) return false;
            var workingForm = GetComponent<DeskOperationalDocument>();
            if (workingForm && !workingForm.IsReady) return false;
            Signed = true;
            RefreshInk();
            interactor.CloseInspection();
            gate.ConsumeFrame();
            flow.TranscriptSigned();
            return true;
        }

        internal void PrepareCompletedForTesting() { Signed = true; RefreshInk(); }

        public void RefreshInk()
        {
            bool restored = Signed || (flow && flow.records && flow.records.entries.Any(e => e.act == 3 && e.id == "vigenere" && e.saved));
            if (transcriptInk) Localization.SetText(transcriptInk, restored ? "法国将于六月十八日越境。" : "");
            if (signatureInk) signatureInk.text = Signed ? "Luca" : "";
            if (signPrompt) Localization.SetText(signPrompt, CanSign ? "点击签署明文还原" : "");
        }
    }
}
