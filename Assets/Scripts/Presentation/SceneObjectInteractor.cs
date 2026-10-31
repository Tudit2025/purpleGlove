using UnityEngine;
using UnityEngine.EventSystems;

namespace PurpleGlove
{
    /// <summary>Owns pointer picking and the single active inspection for a scene camera.</summary>
    [DefaultExecutionOrder(-100), DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    [AddComponentMenu("Purple Glove/Scene Object Interactor")]
    public sealed class SceneObjectInteractor : MonoBehaviour
    {
        [Min(0.1f)] public float interactionDistance = 10f;
        public LayerMask interactionLayers = Physics.DefaultRaycastLayers;

        public SceneObject InspectedObject => inspected;
        public bool BlocksSceneInput => isActiveAndEnabled && inspected;

        Camera sceneCamera;
        SceneObject hovered, inspected;
        bool waitForPointerExit;
        SceneObject dismissed;
        GUIStyle closeStyle;
        const int CloseTextureSize = 24;
        Texture2D closeNormal, closeHovered, closePressed;

        void Awake()
        {
            sceneCamera = GetComponent<Camera>();
        }

        void Update()
        {
            bool canPoint = sceneCamera && sceneCamera.isActiveAndEnabled && Application.isFocused
                && Input.mousePresent && Cursor.lockState != CursorLockMode.Locked
                && sceneCamera.pixelRect.Contains(Input.mousePosition)
                && (!EventSystem.current || !EventSystem.current.IsPointerOverGameObject());
            ProcessInput(Input.mousePosition, canPoint, Input.GetMouseButtonDown(0), Input.GetKeyDown(KeyCode.Escape));
        }

        void ProcessInput(Vector2 position, bool canPoint, bool clicked, bool close)
        {
            if (inspected && (!inspected.isActiveAndEnabled || inspected.State == SceneObject.ObjectState.Resting))
                inspected = null;

            if (inspected)
            {
                if (close) CloseInspection();
                return;
            }
            ProcessPointer(position, canPoint, clicked);
        }

        void ProcessPointer(Vector2 position, bool canPoint, bool clicked)
        {
            SceneObject candidate = null;
            // Props animate in LateUpdate; pick their current pose even between physics steps.
            if (canPoint) Physics.SyncTransforms();
            if (canPoint && Physics.Raycast(sceneCamera.ScreenPointToRay(position), out RaycastHit hit,
                interactionDistance, interactionLayers, QueryTriggerInteraction.Ignore))
            {
                candidate = hit.collider.GetComponentInParent<SceneObject>();
                if (candidate && !candidate.isActiveAndEnabled) candidate = null;
            }

            // Esc must actually put the prop away even if the pointer is still over its home position.
            if (waitForPointerExit)
            {
                if (candidate != dismissed || clicked) { waitForPointerExit = false; dismissed = null; }
                else candidate = null;
            }
            SetHovered(candidate);
            if (clicked && hovered) Inspect(hovered);
        }

        void SetHovered(SceneObject next)
        {
            if (hovered == next) return;
            if (hovered) hovered.SetHovered(false, sceneCamera);
            hovered = next;
            if (hovered) hovered.SetHovered(true, sceneCamera);
        }

        public void Inspect(SceneObject item)
        {
            if (!isActiveAndEnabled || !sceneCamera || !item || !item.isActiveAndEnabled || inspected) return;
            SetHovered(null);
            var action = item.GetComponent<SceneObjectAction>();
            if (action && action.isActiveAndEnabled)
            {
                action.Activate();
                return;
            }
            inspected = item;
            item.BeginInspection(sceneCamera);
        }

        public void CloseInspection()
        {
            if (!inspected || inspected.State != SceneObject.ObjectState.Inspecting) return;
            dismissed = inspected;
            waitForPointerExit = true;
            inspected.ReturnToRest();
        }

        public void ClearHover() => SetHovered(null);

        public void CancelInteraction()
        {
            if (hovered) hovered.RestoreImmediately();
            if (inspected) inspected.RestoreImmediately();
            hovered = inspected = dismissed = null;
            waitForPointerExit = false;
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused) SetHovered(null);
        }

        void OnDisable()
        {
            CancelInteraction();
        }

        void OnGUI()
        {
            if (!inspected || inspected.State != SceneObject.ObjectState.Inspecting || !sceneCamera) return;
            if (closeStyle == null) CreateCloseStyle();
            Rect viewport = sceneCamera.pixelRect;
            // Whole texels at whole screen pixels: keep the frame, cut corners and X equally crisp.
            int scale = Mathf.Max(2, Mathf.RoundToInt(viewport.height / 540f));
            float size = CloseTextureSize * scale;
            var rect = new Rect(Mathf.Floor(viewport.xMax - size - 10 * scale),
                Mathf.Floor(Screen.height - viewport.yMax + 10 * scale), size, size);
            if (GUI.Button(rect, new GUIContent(string.Empty, "Esc"), closeStyle)) CloseInspection();
        }

        void CreateCloseStyle()
        {
            closeNormal = CreateCloseTexture(new Color(0.72f, 0.63f, 0.45f));
            closeHovered = CreateCloseTexture(new Color(0.93f, 0.83f, 0.60f));
            closePressed = CreateCloseTexture(new Color(0.48f, 0.39f, 0.26f));
            closeStyle = new GUIStyle();
            closeStyle.normal.background = closeStyle.onNormal.background = closeNormal;
            closeStyle.hover.background = closeStyle.onHover.background = closeHovered;
            closeStyle.active.background = closeStyle.onActive.background = closePressed;
            closeStyle.focused.background = closeStyle.onFocused.background = closeHovered;
        }

        static Texture2D CreateCloseTexture(Color fill)
        {
            var texture = new Texture2D(CloseTextureSize, CloseTextureSize, TextureFormat.RGBA32, false)
            {
                name = "Pixel Close Button (Runtime)",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[CloseTextureSize * CloseTextureSize];
            for (int y = 0; y < CloseTextureSize; y++)
            {
                for (int x = 0; x < CloseTextureSize; x++)
                {
                    if (!InsideCloseOutline(x, y)) continue;
                    bool border = !InsideCloseOutline(x - 2, y - 2) || !InsideCloseOutline(x + 2, y - 2)
                        || !InsideCloseOutline(x - 2, y + 2) || !InsideCloseOutline(x + 2, y + 2);
                    bool cross = x >= 7 && x <= 16 && y >= 7 && y <= 16
                        && (Mathf.Abs(x - y) <= 1 || Mathf.Abs(x + y - 23) <= 1);
                    pixels[y * CloseTextureSize + x] = border || cross ? Color.black : fill;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static bool InsideCloseOutline(int x, int y)
        {
            return x >= 0 && y >= 0 && x < CloseTextureSize && y < CloseTextureSize
                && Mathf.Min(x, CloseTextureSize - 1 - x) + Mathf.Min(y, CloseTextureSize - 1 - y) >= 3;
        }

        void OnDestroy()
        {
            if (closeNormal) Destroy(closeNormal);
            if (closeHovered) Destroy(closeHovered);
            if (closePressed) Destroy(closePressed);
        }
    }
}
