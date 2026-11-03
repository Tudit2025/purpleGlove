using UnityEngine;

namespace PurpleGlove
{
    /// <summary>A persistent working form: stored upright in the desk rack, opened flat on the writing mat.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SceneObject))]
    public sealed class DeskOperationalDocument : MonoBehaviour
    {
        public ActArchiveController archive;
        [Range(1, 5)] public int receivedInAct = 1;
        [Range(1, 5)] public int rackFromAct = 1;
        public bool UsesRack => archive && archive.CurrentAct >= rackFromAct;
        public Transform storageAnchor, desktopAnchor;
        public Vector3 storageScale = Vector3.one * 0.42f;
        public Vector3 desktopScale = Vector3.one;
        public GameObject rackTab;
        [Min(0.1f)] public float focusSeconds = 0.5f;
        public bool IsOpen { get; private set; }
        public bool IsReady => IsOpen && focusElapsed >= focusSeconds;
        public bool Available => UsesRack && archive.IsDocumentAvailable(receivedInAct, GetComponent<SceneObject>());
        DeskScenePreview view;
        SceneInputGate gate;
        float focusElapsed, startFov;
        float inspectionZoom = 1f;
        Vector3 startPosition;
        Quaternion startRotation;

        public void Stow(bool available)
        {
            var item = GetComponent<SceneObject>();
            if (!storageAnchor || !UsesRack) return;
            item.Relocate(storageAnchor, Vector3.zero, Quaternion.identity, storageScale);
            if (rackTab) rackTab.SetActive(true);
            gameObject.SetActive(available);
        }

        public void RefreshRack() => Stow(Available);

        internal bool BeginInteraction(Camera camera)
        {
            if (!isActiveAndEnabled || !Available || !desktopAnchor || !storageAnchor || IsOpen || !camera) return false;
            view = camera.GetComponent<DeskScenePreview>(); gate = camera.GetComponent<SceneInputGate>();
            if (!view || !gate || gate.BlocksWorldInput || !view.BeginStoryView()) return false;
            IsOpen = true; focusElapsed = 0;
            inspectionZoom = 1f;
            startPosition = camera.transform.position; startRotation = camera.transform.rotation; startFov = camera.fieldOfView;
            if (rackTab) rackTab.SetActive(false);
            gate.ConsumeFrame();
            return true;
        }

        void Update()
        {
            if (IsOpen && Application.isFocused && gate && !gate.ConsoleOpen) AdvanceTime(Time.unscaledDeltaTime);
        }
        void AdvanceTime(float seconds)
        {
            if (!IsOpen || !view || !desktopAnchor || !gate || gate.ConsoleOpen) return;
            focusElapsed += Mathf.Max(0, seconds);
            var box = GetComponent<BoxCollider>();
            Vector3 size = Vector3.Scale(box.size, desktopScale);
            var item = GetComponent<SceneObject>();
            inspectionZoom = Mathf.Lerp(inspectionZoom, item.InspectionZoom,
                1 - Mathf.Exp(-Mathf.Max(0.1f, item.inspectionSpeed) * Mathf.Max(0, seconds)));
            const float fov = 48f;
            float distance = Mathf.Max(size.z, size.x / Mathf.Max(0.1f, view.sceneCamera.aspect)) * 0.5f
                / (Mathf.Tan(fov * Mathf.Deg2Rad / 2) * 0.78f) + 0.05f;
            distance = Mathf.Max(distance / inspectionZoom, view.sceneCamera.nearClipPlane + Mathf.Abs(size.y) * 0.5f + 0.05f);
            Vector3 target = desktopAnchor.position + desktopAnchor.up * distance;
            Quaternion rotation = desktopAnchor.rotation * Quaternion.Euler(90, 0, 0);
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(focusElapsed / Mathf.Max(0.1f, focusSeconds)));
            view.SetStoryPose(Vector3.Lerp(startPosition, target, t), Quaternion.Slerp(startRotation, rotation, t), Mathf.Lerp(startFov, fov, t));
        }

        internal void EndInteraction()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (rackTab) rackTab.SetActive(true);
            if (view) view.EndStoryView(false);
        }
        void OnDisable() => EndInteraction();
    }
}
