using UnityEngine;

namespace PurpleGlove
{
    [DisallowMultipleComponent]
    public sealed class DeskScenePreview : MonoBehaviour
    {
        public enum View { Desktop, Doorway, Bookshelf }
        public Camera sceneCamera;
        public Vector3 desktopEuler = new Vector3(34, 0, 0);
        public Vector3 doorwayEuler = new Vector3(5, 5.5f, 0);
        public Vector3 bookshelfEuler = new Vector3(9, -90, 0);
        [Range(30, 100)] public float bookshelfFieldOfView = 84;
        [SerializeField] View currentView;
        public Light candleLight;
        public Transform flame;
        public View CurrentView => currentView;
        public bool lookingAtDoor { get => currentView == View.Doorway; set => SetView(value ? View.Doorway : View.Desktop); }

        [Header("Mouse Look")]
        public bool followMouse = true;
        [Tooltip("Maximum left/right offset from the active view, in degrees.")]
        [Min(0)] public float horizontalMouseAngle = 6f;
        [Tooltip("Maximum up/down offset from the active view, in degrees.")]
        [Min(0)] public float verticalMouseAngle = 4f;
        [Tooltip("Fraction of each screen half that keeps the view centered.")]
        [Range(0, 0.5f)] public float mouseDeadZone = 0.05f;
        [Tooltip("X: distance from the center after the dead zone (0-1). Y: fraction of the maximum angle (0-1).")]
        public AnimationCurve mouseResponseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Tooltip("Rotation smoothing for mouse look and view switching. Higher is faster; zero snaps immediately.")]
        [Min(0)] public float cameraFollowSpeed = 5f;

        float candleIntensity;
        Vector3 flameScale;
        SceneObjectInteractor objectInteractor;

        void Awake()
        {
            if (!sceneCamera) sceneCamera = GetComponent<Camera>();
            if (sceneCamera) objectInteractor = sceneCamera.GetComponent<SceneObjectInteractor>();
            if (candleLight) candleIntensity = candleLight.intensity;
            if (flame) flameScale = flame.localScale;
        }

        void Update()
        {
            if (!objectInteractor || !objectInteractor.BlocksSceneInput)
            {
                if (Input.GetKeyDown(KeyCode.A)) SetView(View.Bookshelf);
                else if (Input.GetKeyDown(KeyCode.W)) SetView(View.Doorway);
                else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.Escape))
                    SetView(View.Desktop);
            }
            if (candleLight)
            {
                float pulse = Mathf.Sin(Time.time * 5.3f) * 0.018f + Mathf.Sin(Time.time * 8.9f + 1.7f) * 0.012f;
                candleLight.intensity = candleIntensity * (1 + pulse);
                if (flame) flame.localScale = Vector3.Scale(flameScale, new Vector3(1 + pulse * 2, 1 - pulse * 3, 1 + pulse));
            }
        }

        void LateUpdate()
        {
            if (!sceneCamera) return;
            if (objectInteractor && objectInteractor.BlocksSceneInput) return;

            Vector2 mouseAngles = GetMouseAngles();
            if (currentView == View.Bookshelf) mouseAngles *= 0.5f;
            Vector3 viewEuler = currentView == View.Bookshelf ? bookshelfEuler : lookingAtDoor ? doorwayEuler : desktopEuler;
            // Always offset the authored view, so mouse movement cannot accumulate rotation.
            var target = Quaternion.Euler(viewEuler + new Vector3(-mouseAngles.y, mouseAngles.x, 0));
            float blend = cameraFollowSpeed <= 0 ? 1 : 1 - Mathf.Exp(-Time.unscaledDeltaTime * cameraFollowSpeed);
            sceneCamera.transform.rotation = Quaternion.Slerp(sceneCamera.transform.rotation, target, blend);
            float fieldOfView = currentView == View.Bookshelf ? bookshelfFieldOfView : lookingAtDoor ? 38 : 60;
            sceneCamera.fieldOfView = Mathf.Lerp(sceneCamera.fieldOfView, fieldOfView, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 5));
        }

        public bool SetView(View view)
        {
            if (objectInteractor && objectInteractor.BlocksSceneInput) return false;
            if (currentView != view && objectInteractor) objectInteractor.ClearHover();
            currentView = view;
            return true;
        }

        Vector2 GetMouseAngles()
        {
            if (!followMouse || !Application.isFocused || !Input.mousePresent || Cursor.lockState == CursorLockMode.Locked)
                return Vector2.zero;

            Rect viewport = sceneCamera.pixelRect;
            Vector2 mouse = Input.mousePosition;
            // Leaving the game view (or losing focus) eases back to the authored view.
            if (viewport.width <= 0 || viewport.height <= 0 || !viewport.Contains(mouse))
                return Vector2.zero;

            float x = (mouse.x - viewport.xMin) / viewport.width * 2 - 1;
            float y = (mouse.y - viewport.yMin) / viewport.height * 2 - 1;
            return new Vector2(EvaluateMouseAxis(x) * Mathf.Max(0, horizontalMouseAngle),
                EvaluateMouseAxis(y) * Mathf.Max(0, verticalMouseAngle));
        }

        float EvaluateMouseAxis(float value)
        {
            float deadZone = Mathf.Clamp(mouseDeadZone, 0, 0.5f);
            float distance = Mathf.Clamp01(Mathf.Abs(value));
            if (distance <= deadZone) return 0;

            float normalized = (distance - deadZone) / (1 - deadZone);
            float response = mouseResponseCurve == null || mouseResponseCurve.length == 0
                ? normalized : mouseResponseCurve.Evaluate(normalized);
            // Custom curve tangents can overshoot; preserve the configured angle limits.
            return Mathf.Sign(value) * Mathf.Clamp01(response);
        }
    }
}
