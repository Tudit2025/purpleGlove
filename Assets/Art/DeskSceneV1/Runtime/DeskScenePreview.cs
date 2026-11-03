using UnityEngine;

namespace PurpleGlove
{
    [DisallowMultipleComponent]
    public sealed class DeskScenePreview : MonoBehaviour
    {
        // Keep existing serialized values; Doorway is reserved for event-driven visits.
        public enum View { Desktop = 0, Doorway = 1, Bookshelf = 2, MatFocus = 3, DecodeFocus = 4 }
        public enum Direction { Up, Left, Right, Down }
        public const float DoorwayFieldOfView = 28.5f;
        public Camera sceneCamera;
        [Min(0.1f)] public float doorwayTransitionSeconds = 0.85f;
        public Vector3 desktopEuler = new Vector3(34, 0, 0);
        public Vector3 doorwayEuler = new Vector3(5, 5.5f, 0);
        public Vector3 bookshelfEuler = new Vector3(9, -90, 0);
        [Range(30, 100)] public float bookshelfFieldOfView = 84;
        [Header("Table focus (W)")]
        public Transform matFocusAnchor;
        [Tooltip("X/Z select the top-down center; Y is the full-view camera height.")]
        public Vector3 matFocusOffset = new Vector3(0, 1.45f, 0);
        [Range(30, 80)] public float matFocusFieldOfView = 48;
        [Header("Decoding mat (D)")]
        public Transform decodeFocusAnchor;
        public Transform decodeDetailAnchor;
        [Tooltip("Keep a long source strip fully visible while it is on the desk, including at narrow aspect ratios.")]
        public BoxCollider decodeReferencePaper;
        [Tooltip("Horizontal camera offset at every tool zoom level; negative moves left toward the cipher papers.")]
        public float decodeFocusHorizontalOffset = -0.24f;
        public float decodeFocusHeight = 1.06f;
        [Range(30, 80)] public float decodeFocusFieldOfView = 45;
        [SerializeField] View currentView;
        public Light candleLight;
        public Transform flame;
        public View CurrentView => currentView;
        // Compatibility entry for explicit story triggers, never a player view shortcut.
        public bool lookingAtDoor { get => currentView == View.Doorway; set => SetDialogueLock(value); }

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
        SceneInputGate inputGate;
        CipherWorkbench workbench;
        Vector3 seatedPosition;
        float matZoom = 1;
        float decodeZoom = 1;
        public bool CanNavigate => isActiveAndEnabled && !StoryLocked && !DialogueLocked && !DeliveryLocked
            && (!inputGate || !inputGate.BlocksWorldInput)
            && (!objectInteractor || !objectInteractor.BlocksSceneInput)
            && (!workbench || !workbench.IsManipulating);
        public bool DialogueLocked { get; private set; }
        public bool DeliveryLocked { get; private set; }
        public bool StoryLocked { get; private set; }
        public bool IsEnteringDoorway { get; private set; }
        Vector3 doorwayStartPosition;
        Quaternion doorwayStartRotation;
        float doorwayStartFov, doorwayElapsed;
        View viewBeforeStory;
        Vector3 positionBeforeStory;
        Quaternion rotationBeforeStory;
        float fovBeforeStory;
        View viewBeforeDialogue;
        View viewBeforeDelivery;

        void Awake()
        {
            if (!sceneCamera) sceneCamera = GetComponent<Camera>();
            if (sceneCamera) objectInteractor = sceneCamera.GetComponent<SceneObjectInteractor>();
            if (sceneCamera) seatedPosition = sceneCamera.transform.position;
            if (!matFocusAnchor)
            {
                var mat = GameObject.Find("WritingMat");
                if (mat) matFocusAnchor = mat.transform;
            }
            if (currentView == View.Doorway) currentView = View.Desktop;
            inputGate = GetComponent<SceneInputGate>();
            workbench = GetComponent<CipherWorkbench>();
            if (candleLight) candleIntensity = candleLight.intensity;
            if (flame) flameScale = flame.localScale;
        }

        void Update()
        {
            if (CanNavigate)
            {
                if (sceneCamera && sceneCamera.pixelRect.Contains(Input.mousePosition))
                    AdjustFocusZoom(Input.GetAxis("Mouse ScrollWheel"));
                if (Input.GetKeyDown(KeyCode.Escape)) HandleViewKey(KeyCode.Escape);
                else if (Input.GetKeyDown(KeyCode.A)) HandleViewKey(KeyCode.A);
                else if (Input.GetKeyDown(KeyCode.W)) HandleViewKey(KeyCode.W);
                else if (Input.GetKeyDown(KeyCode.D)) HandleViewKey(KeyCode.D);
                else if (Input.GetKeyDown(KeyCode.S)) HandleViewKey(KeyCode.S);
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
            if (StoryLocked) return;
            if (DialogueLocked) { AnimateView(Time.unscaledDeltaTime, Vector2.zero); return; }
            if (DeliveryLocked)
            {
                AnimateView(Time.unscaledDeltaTime, Vector2.zero);
                return;
            }
            if (objectInteractor && objectInteractor.BlocksSceneInput) return;

            AnimateView(Time.unscaledDeltaTime, GetMouseAngles());
        }

        void AnimateView(float deltaTime, Vector2 mouseAngles)
        {
            if (currentView == View.Doorway) { AnimateDoorwayView(deltaTime); return; }
            if (currentView == View.Bookshelf) mouseAngles *= 0.5f;
            if (currentView == View.MatFocus || currentView == View.DecodeFocus) mouseAngles = Vector2.zero;
            Vector3 position = seatedPosition;
            Vector3 viewEuler = currentView == View.Bookshelf ? bookshelfEuler : desktopEuler;
            float fieldOfView = currentView == View.Bookshelf ? bookshelfFieldOfView : 60;
            if (currentView == View.MatFocus && matFocusAnchor)
            {
                Vector3 focus = matFocusAnchor.TransformPoint(new Vector3(matFocusOffset.x, 0, matFocusOffset.z));
                position = focus + Vector3.up * (matFocusOffset.y * matZoom);
                viewEuler = new Vector3(90, matFocusAnchor.eulerAngles.y, 0);
                fieldOfView = matFocusFieldOfView;
            }
            if (currentView == View.DecodeFocus && decodeFocusAnchor)
            {
                Vector3 focus = decodeDetailAnchor ? Vector3.Lerp(decodeDetailAnchor.position, decodeFocusAnchor.position, (decodeZoom - 0.60f) / 0.40f)
                    : decodeFocusAnchor.position;
                focus += decodeFocusAnchor.right * decodeFocusHorizontalOffset;
                if (workbench && workbench.BookTool)
                    focus = workbench.mat.TransformPoint(new Vector3(-0.20f, 0, 0.01f));
                position = focus + Vector3.up * (decodeFocusHeight * decodeZoom);
                viewEuler = new Vector3(90, decodeFocusAnchor.eulerAngles.y, 0);
                fieldOfView = decodeFocusFieldOfView;
                if (decodeReferencePaper && decodeReferencePaper.gameObject.activeInHierarchy)
                {
                    var paper = decodeReferencePaper.GetComponent<SceneObject>();
                    if (!paper || paper.State == SceneObject.ObjectState.Resting || paper.State == SceneObject.ObjectState.Hovered)
                    {
                        // Leave room below the strip for the bottom navigation hint and return arrow.
                        Vector3 offset = decodeFocusAnchor.forward * -0.10f;
                        focus += offset;
                        position += offset;
                        float tangent = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
                        Quaternion frame = Quaternion.Euler(0, decodeFocusAnchor.eulerAngles.y, 0);
                        float height = position.y - focus.y;
                        for (int i = 0; i < 8; i++)
                        {
                            var corner = decodeReferencePaper.center + Vector3.Scale(decodeReferencePaper.size * 0.5f,
                                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                            Vector3 delta = Quaternion.Inverse(frame) * (decodeReferencePaper.transform.TransformPoint(corner) - focus);
                            float fitted = Mathf.Max((Mathf.Abs(delta.x) + 0.025f) / Mathf.Max(0.01f, sceneCamera.aspect),
                                Mathf.Abs(delta.z) + 0.025f) / tangent + delta.y;
                            height = Mathf.Max(height, fitted);
                        }
                        position.y = focus.y + height;
                    }
                }
            }
            // Always offset the authored view, so mouse movement cannot accumulate rotation.
            var target = Quaternion.Euler(viewEuler + new Vector3(-mouseAngles.y, mouseAngles.x, 0));
            float blend = cameraFollowSpeed <= 0 ? 1 : 1 - Mathf.Exp(-deltaTime * cameraFollowSpeed);
            sceneCamera.transform.position = Vector3.Lerp(sceneCamera.transform.position, position, blend);
            sceneCamera.transform.rotation = Quaternion.Slerp(sceneCamera.transform.rotation, target, blend);
            sceneCamera.fieldOfView = Mathf.Lerp(sceneCamera.fieldOfView, fieldOfView, blend);
        }

        bool HandleViewKey(KeyCode key)
        {
            // Letter keys belong to the decoding workspace once entered; Esc still leaves it.
            if (currentView == View.DecodeFocus && key != KeyCode.Escape) return false;
            if (key == KeyCode.Escape) return SetView(View.Desktop);
            Direction direction;
            switch (key)
            {
                case KeyCode.W: direction = Direction.Up; break;
                case KeyCode.A: direction = Direction.Left; break;
                case KeyCode.D: direction = Direction.Right; break;
                case KeyCode.S: direction = Direction.Down; break;
                default: return false;
            }
            return TryGetNavigationTarget(direction, out var target) && SetView(target);
        }

        void AdjustFocusZoom(float scroll)
        {
            if (!CanNavigate) return;
            if (currentView == View.MatFocus)
                matZoom = Mathf.Clamp(matZoom - scroll * 0.65f, 0.60f, 1);
            else if (currentView == View.DecodeFocus)
                decodeZoom = Mathf.Clamp(decodeZoom - scroll * 0.65f, 0.60f, 1);
        }

        public bool TryGetNavigationTarget(Direction direction, out View target)
        {
            target = currentView;
            switch (currentView)
            {
                case View.Desktop:
                    if (direction == Direction.Up) target = View.MatFocus;
                    else if (direction == Direction.Left) target = View.Bookshelf;
                    else if (direction == Direction.Right) target = View.DecodeFocus;
                    break;
                case View.Bookshelf:
                    if (direction == Direction.Right) target = View.Desktop;
                    break;
                case View.DecodeFocus:
                    if (direction == Direction.Left) target = View.MatFocus;
                    break;
                case View.MatFocus:
                    if (direction == Direction.Right) target = View.DecodeFocus;
                    else if (direction == Direction.Down) target = View.Desktop;
                    break;
            }
            return target != currentView && (target != View.MatFocus || matFocusAnchor)
                && (target != View.DecodeFocus || decodeFocusAnchor);
        }

        public bool TryFocusMat(Collider surface)
        {
            if (currentView == View.MatFocus || !matFocusAnchor || !surface
                || !surface.transform.IsChildOf(matFocusAnchor)) return false;
            return SetView(View.MatFocus);
        }

        public bool SetView(View view)
        {
            if (view != View.Desktop && view != View.Bookshelf && view != View.MatFocus && view != View.DecodeFocus) return false;
            if (view == View.MatFocus && !matFocusAnchor) return false;
            if (view == View.DecodeFocus && !decodeFocusAnchor) return false;
            if (!CanNavigate) return false;
            if (currentView != view)
            {
                if (objectInteractor) objectInteractor.ClearHover();
                if (view == View.DecodeFocus) decodeZoom = 1;
                if (view == View.MatFocus) matZoom = 1;
            }
            currentView = view;
            return true;
        }

        public void FocusBookSpread() { if (currentView == View.DecodeFocus) decodeZoom = 0.60f; }

        public void SetDialogueLock(bool locked)
        {
            if (locked && (DeliveryLocked || StoryLocked)) return;
            if (DialogueLocked == locked) return;
            DialogueLocked = locked;
            if (locked)
            {
                if (objectInteractor) objectInteractor.CancelInteraction();
                viewBeforeDialogue = currentView; currentView = View.Doorway;
                BeginDoorwayTransition();
            }
            else { IsEnteringDoorway = false; currentView = viewBeforeDialogue; }
        }

        public void BeginDeliveryView()
        {
            if (DialogueLocked || DeliveryLocked || StoryLocked) return;
            if (objectInteractor) objectInteractor.CancelInteraction();
            viewBeforeDelivery = currentView;
            DeliveryLocked = true; currentView = View.Doorway;
            BeginDoorwayTransition();
        }

        public void FollowDeliveryToDesk()
        {
            if (DeliveryLocked) { IsEnteringDoorway = false; currentView = View.Desktop; }
        }

        public void EndDeliveryView(bool completed)
        {
            if (!DeliveryLocked) return;
            DeliveryLocked = false;
            IsEnteringDoorway = false;
            currentView = completed ? View.Desktop : viewBeforeDelivery;
        }

        void BeginDoorwayTransition()
        {
            if (!sceneCamera) return;
            doorwayStartPosition = sceneCamera.transform.position;
            doorwayStartRotation = sceneCamera.transform.rotation;
            doorwayStartFov = sceneCamera.fieldOfView;
            doorwayElapsed = 0;
            IsEnteringDoorway = Vector3.Distance(doorwayStartPosition, seatedPosition) > 0.001f
                || Quaternion.Angle(doorwayStartRotation, Quaternion.Euler(doorwayEuler)) > 0.05f
                || Mathf.Abs(doorwayStartFov - DoorwayFieldOfView) > 0.01f;
        }

        void AnimateDoorwayView(float deltaTime)
        {
            doorwayElapsed += Mathf.Max(0, deltaTime);
            float t = IsEnteringDoorway ? Mathf.Clamp01(doorwayElapsed / Mathf.Max(0.1f, doorwayTransitionSeconds)) : 1;
            float eased = t * t * (3 - 2 * t);
            sceneCamera.transform.SetPositionAndRotation(Vector3.Lerp(doorwayStartPosition, seatedPosition, eased),
                Quaternion.Slerp(doorwayStartRotation, Quaternion.Euler(doorwayEuler), eased));
            sceneCamera.fieldOfView = Mathf.Lerp(doorwayStartFov, DoorwayFieldOfView, eased);
            if (t >= 1) IsEnteringDoorway = false;
        }

        public bool BeginStoryView()
        {
            if (!isActiveAndEnabled || !sceneCamera || StoryLocked || DialogueLocked || DeliveryLocked) return false;
            if (objectInteractor) objectInteractor.CancelInteraction();
            viewBeforeStory = currentView;
            positionBeforeStory = sceneCamera.transform.position;
            rotationBeforeStory = sceneCamera.transform.rotation;
            fovBeforeStory = sceneCamera.fieldOfView;
            StoryLocked = true;
            return true;
        }

        public void SetStoryPose(Vector3 position, Quaternion rotation, float fieldOfView)
        {
            if (!StoryLocked || !sceneCamera) return;
            sceneCamera.transform.SetPositionAndRotation(position, rotation);
            sceneCamera.fieldOfView = fieldOfView;
        }

        public void ShowStoryDesktop()
        {
            if (!StoryLocked) return;
            currentView = View.Desktop;
            AnimateView(100, Vector2.zero);
        }

        public void EndStoryView(bool completed)
        {
            if (!StoryLocked) return;
            if (completed) ShowStoryDesktop();
            else
            {
                currentView = viewBeforeStory;
                if (sceneCamera)
                {
                    sceneCamera.transform.SetPositionAndRotation(positionBeforeStory, rotationBeforeStory);
                    sceneCamera.fieldOfView = fovBeforeStory;
                }
            }
            StoryLocked = false;
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
