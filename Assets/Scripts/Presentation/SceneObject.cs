using UnityEngine;

namespace PurpleGlove
{
    /// <summary>A scene prop that can be pulled forward and presented to the camera.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    [AddComponentMenu("Purple Glove/Scene Object")]
    public sealed class SceneObject : MonoBehaviour
    {
        public enum ObjectState { Resting, Hovered, Inspecting, Returning }

        [Header("Hover")]
        [Min(0)] public float hoverDistance = 0.035f;
        [Min(0.1f)] public float hoverSpeed = 14f;

        [Header("Inspection")]
        [Tooltip("Rotation relative to the camera. -90 X presents the top of a desk prop upright.")]
        public Vector3 inspectionEuler = new Vector3(-90, 0, 0);
        [Tooltip("Maximum fraction of the viewport occupied by the presented object.")]
        [Range(0.2f, 0.85f)] public float screenFill = 0.68f;
        [Min(0.05f)] public float minimumViewDistance = 0.4f;
        [Min(0.1f)] public float inspectionSpeed = 10f;
        [Min(0.1f)] public float returnSpeed = 12f;
        [Tooltip("Soft fill on the inspected surface, so paper stays readable away from the candle.")]
        [ColorUsage(false)] public Color inspectionFill = new Color(0.3f, 0.26f, 0.2f);

        public ObjectState State { get; private set; }

        Camera viewCamera;
        BoxCollider hitBox;
        Vector3 restPosition, restScale;
        Quaternion restRotation;
        bool hasRestPose;
        static readonly int FillId = Shader.PropertyToID("_InspectionFill");
        Renderer[] surfaces;
        MaterialPropertyBlock[] originalProperties, inspectionProperties;
        float fillAmount;

        void RememberRestPose()
        {
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            restScale = transform.localScale;
            hitBox = GetComponent<BoxCollider>();
            hasRestPose = true;
            surfaces = GetComponentsInChildren<Renderer>(true);
            originalProperties = new MaterialPropertyBlock[surfaces.Length];
            inspectionProperties = new MaterialPropertyBlock[surfaces.Length];
            for (int i = 0; i < surfaces.Length; i++)
            {
                originalProperties[i] = new MaterialPropertyBlock();
                inspectionProperties[i] = new MaterialPropertyBlock();
                surfaces[i].GetPropertyBlock(originalProperties[i]);
                surfaces[i].GetPropertyBlock(inspectionProperties[i]);
            }
        }

        internal void SetHovered(bool hovered, Camera camera)
        {
            if (State == ObjectState.Inspecting) return;
            if (hovered)
            {
                if (State == ObjectState.Resting) RememberRestPose();
                viewCamera = camera;
                State = ObjectState.Hovered;
            }
            else if (State == ObjectState.Hovered) ReturnToRest();
        }

        internal void BeginInspection(Camera camera)
        {
            if (State == ObjectState.Resting) RememberRestPose();
            viewCamera = camera;
            State = ObjectState.Inspecting;
        }

        internal void ReturnToRest()
        {
            if (hasRestPose) State = ObjectState.Returning;
        }

        internal void RestoreImmediately()
        {
            if (hasRestPose)
            {
                transform.localPosition = restPosition;
                transform.localRotation = restRotation;
                transform.localScale = restScale;
            }
            State = ObjectState.Resting;
            viewCamera = null;
            var workingForm = GetComponent<DeskOperationalDocument>();
            if (workingForm) workingForm.EndInteraction();
            fillAmount = 0;
            if (surfaces != null)
                for (int i = 0; i < surfaces.Length; i++)
                    if (surfaces[i]) surfaces[i].SetPropertyBlock(originalProperties[i]);
        }

        void OnDisable() => RestoreImmediately();

        // Archives move the actual prop, preserving its content without retaining an old hover origin.
        public void Relocate(Transform parent, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            RestoreImmediately();
            transform.SetParent(parent, false);
            transform.localPosition = position;
            transform.localRotation = rotation;
            transform.localScale = scale;
            hasRestPose = false;
            surfaces = null;
        }

        void LateUpdate() => Animate(Time.unscaledDeltaTime);

        void Animate(float deltaTime)
        {
            if (State == ObjectState.Resting) return;
            if (!viewCamera) ReturnToRest();

            Vector3 targetPosition = transform.parent ? transform.parent.TransformPoint(restPosition) : restPosition;
            Quaternion targetRotation = transform.parent ? transform.parent.rotation * restRotation : restRotation;
            Vector3 targetScale = restScale;
            float speed = returnSpeed;

            if (State == ObjectState.Hovered)
            {
                targetPosition += (viewCamera.transform.position - targetPosition).normalized * hoverDistance;
                speed = hoverSpeed;
            }
            else if (State == ObjectState.Inspecting)
            {
                GetInspectionPose(out targetPosition, out targetRotation);
                var workingForm = GetComponent<DeskOperationalDocument>();
                if (workingForm && workingForm.IsOpen) targetScale = workingForm.desktopScale;
                speed = inspectionSpeed;
            }

            float blend = 1 - Mathf.Exp(-Mathf.Max(0.1f, speed) * deltaTime);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, targetPosition, blend),
                Quaternion.Slerp(transform.rotation, targetRotation, blend));
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, blend);
            fillAmount = Mathf.Lerp(fillAmount, State == ObjectState.Inspecting ? 1 : 0, blend);
            for (int i = 0; i < surfaces.Length; i++)
            {
                if (!surfaces[i]) continue;
                inspectionProperties[i].SetColor(FillId, inspectionFill * fillAmount);
                surfaces[i].SetPropertyBlock(inspectionProperties[i]);
            }

            if (State == ObjectState.Returning && Vector3.SqrMagnitude(transform.position - targetPosition) < 0.00000001f
                && Quaternion.Angle(transform.rotation, targetRotation) < 0.05f)
                RestoreImmediately();
        }

        void GetInspectionPose(out Vector3 position, out Quaternion rotation)
        {
            var workingForm = GetComponent<DeskOperationalDocument>();
            if (workingForm && workingForm.IsOpen && workingForm.desktopAnchor)
            {
                position = workingForm.desktopAnchor.position;
                rotation = workingForm.desktopAnchor.rotation;
                return;
            }
            Quaternion presentation = Quaternion.Euler(inspectionEuler);
            rotation = viewCamera.transform.rotation * presentation;
            Vector3 scale = transform.lossyScale;
            Vector3 halfSize = Vector3.Scale(hitBox.size * 0.5f, scale);
            Vector3 extents = Vector3.zero;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = presentation * new Vector3((i & 1) == 0 ? -halfSize.x : halfSize.x,
                    (i & 2) == 0 ? -halfSize.y : halfSize.y, (i & 4) == 0 ? -halfSize.z : halfSize.z);
                extents = Vector3.Max(extents, new Vector3(Mathf.Abs(corner.x), Mathf.Abs(corner.y), Mathf.Abs(corner.z)));
            }

            float tangent = Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float distance = Mathf.Max(extents.y, extents.x / Mathf.Max(0.01f, viewCamera.aspect))
                / (tangent * Mathf.Clamp(screenFill, 0.2f, 0.85f)) + extents.z;
            distance = Mathf.Max(distance, minimumViewDistance, viewCamera.nearClipPlane + extents.z + 0.05f);
            // Center the visible bounds, not the modelling pivot at the bottom of the book.
            position = viewCamera.transform.position + viewCamera.transform.forward * distance
                - rotation * Vector3.Scale(hitBox.center, scale);
        }
    }
}
