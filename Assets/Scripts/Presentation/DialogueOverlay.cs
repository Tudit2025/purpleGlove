using UnityEngine;
using UnityEngine.UI;

namespace PurpleGlove
{
    [DisallowMultipleComponent]
    public sealed class DialogueOverlay : MonoBehaviour
    {
        public Font font;
        Canvas canvas;
        Camera sceneCamera, overlayCamera;
        bool sceneIncludedUI;
        RectTransform panel;
        Text body;
        string fullText = string.Empty;
        public RectTransform Panel => panel;
        public Text Body => body;
        public Camera OverlayCamera => overlayCamera;

        void Awake()
        {
            sceneCamera = GetComponent<Camera>();
            // Draw lettering after the room's low-resolution pass so Chinese strokes stay legible.
            var cameraObject = new GameObject("Dialogue text camera");
            cameraObject.transform.SetParent(transform, false);
            overlayCamera = cameraObject.AddComponent<Camera>();
            overlayCamera.clearFlags = CameraClearFlags.Depth;
            overlayCamera.cullingMask = 1 << 5;
            overlayCamera.depth = sceneCamera.depth + 1;
            overlayCamera.nearClipPlane = 0.01f; overlayCamera.farClipPlane = 1;
            overlayCamera.allowHDR = overlayCamera.allowMSAA = false;
            sceneIncludedUI = (sceneCamera.cullingMask & (1 << 5)) != 0;
            sceneCamera.cullingMask &= ~(1 << 5);
            var root = new GameObject("Dialogue overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.layer = 5;
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = overlayCamera; canvas.planeDistance = 0.1f; canvas.sortingOrder = 100;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            panel = Rect("Single spoken sentence", root.transform);
            var background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.025f, 0.021f, 0.018f, 0.87f); background.raycastTarget = false;
            var accent = Rect("Speaker colour", panel);
            Stretch(accent, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(3, 0));
            accent.gameObject.AddComponent<Image>().raycastTarget = false;
            body = Label("Sentence", panel, 31);
            body.supportRichText = true;
            body.resizeTextForBestFit = true; body.resizeTextMinSize = 18; body.resizeTextMaxSize = 31;
            Stretch(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(28, 22), new Vector2(-28, -22));
            Hide();
        }

        public void Show(DialogueSpeaker speaker, string text)
        {
            // Bottom third for Luca, middle third for every other speaker. One panel is reused.
            float bottom = speaker.isProtagonist ? 0.055f : 0.365f;
            panel.anchorMin = new Vector2(0.12f, bottom); panel.anchorMax = new Vector2(0.88f, bottom + 0.22f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            body.color = speaker.textColor;
            panel.GetChild(0).GetComponent<Image>().color = speaker.textColor;
            fullText = text.Replace('<', '＜').Replace('>', '＞');
            canvas.enabled = overlayCamera.enabled = true;
            Reveal(0);
        }

        public void Reveal(int utf16Length)
        {
            int count = Mathf.Clamp(utf16Length, 0, fullText.Length);
            // Invisible suffix keeps the full sentence's line wrapping and font size stable during typing.
            body.text = count == fullText.Length ? fullText
                : fullText.Substring(0, count) + "<color=#00000000>" + fullText.Substring(count) + "</color>";
        }
        public void Hide()
        {
            if (canvas) canvas.enabled = false;
            if (overlayCamera) overlayCamera.enabled = false;
        }
        void LateUpdate() { if (overlayCamera && sceneCamera) overlayCamera.rect = sceneCamera.rect; }
        void OnDisable() => Hide();
        void OnDestroy()
        {
            if (sceneCamera && sceneIncludedUI) sceneCamera.cullingMask |= 1 << 5;
        }

        Text Label(string title, Transform parent, int size)
        {
            var label = Rect(title, parent).gameObject.AddComponent<Text>();
            label.font = font; label.fontSize = size; label.raycastTarget = false;
            label.alignment = TextAnchor.MiddleLeft; label.supportRichText = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }
        static RectTransform Rect(string title, Transform parent)
        {
            var rect = new GameObject(title, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5;
            rect.SetParent(parent, false); return rect;
        }
        static void Stretch(RectTransform rect, Vector2 min, Vector2 max, Vector2 insetMin, Vector2 insetMax)
        { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = insetMin; rect.offsetMax = insetMax; }
    }
}
