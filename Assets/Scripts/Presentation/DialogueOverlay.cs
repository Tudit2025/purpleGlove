using System.Collections.Generic;
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
        readonly List<Image> choiceRows = new List<Image>();
        int choiceCount;
        bool choicePromptVisible;
        Color choiceColor;
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
            body = Label("Sentence", panel, 31);
            body.supportRichText = true;
            body.resizeTextForBestFit = true; body.resizeTextMinSize = 18; body.resizeTextMaxSize = 31;
            Stretch(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(28, 22), new Vector2(-28, -22));
            Hide();
        }

        public void Show(DialogueSpeaker speaker, string text)
        {
            ClearChoices();
            body.enabled = true;
            // Bottom third for Luca, middle third for every other speaker. One panel is reused.
            float bottom = speaker.isProtagonist ? 0.055f : 0.365f;
            panel.anchorMin = new Vector2(0.12f, bottom); panel.anchorMax = new Vector2(0.88f, bottom + 0.22f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            Stretch(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(28, 22), new Vector2(-28, -22));
            body.color = speaker.textColor;
            fullText = text.Replace('<', '＜').Replace('>', '＞');
            canvas.enabled = overlayCamera.enabled = true;
            Reveal(0);
        }

        public void ShowChoices(DialogueSpeaker speaker, string prompt, DialogueEvent.Choice[] choices)
        {
            Show(speaker, prompt);
            choicePromptVisible = !string.IsNullOrEmpty(prompt);
            body.enabled = choicePromptVisible;
            choiceCount = choices.Length; choiceColor = speaker.textColor;
            panel.anchorMin = new Vector2(0.12f, 0.055f);
            panel.anchorMax = new Vector2(0.88f, 0.055f + Mathf.Min(0.88f,
                (choicePromptVisible ? 0.14f : 0.035f) + choices.Length * 0.075f));
            Stretch(body.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(28, -76), new Vector2(-28, -12));
            Reveal(fullText.Length);
            for (int i = 0; i < choices.Length; i++)
            {
                if (i == choiceRows.Count)
                {
                    var row = Rect("Player response " + (i + 1), panel).gameObject.AddComponent<Image>();
                    row.raycastTarget = false;
                    var label = Label("Response", row.transform, 28);
                    label.resizeTextForBestFit = true; label.resizeTextMinSize = 18; label.resizeTextMaxSize = 28;
                    Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(20, 5), new Vector2(-20, -5));
                    choiceRows.Add(row);
                }
                var image = choiceRows[i];
                image.gameObject.SetActive(true);
                var text = image.GetComponentInChildren<Text>();
                text.text = Localization.Text(choices[i].text); text.color = speaker.textColor;
            }
            LayoutChoices(); HighlightChoice(-1);
        }

        void LayoutChoices()
        {
            if (choiceCount == 0) return;
            float header = choicePromptVisible ? 82 : 12;
            float padding = header + 12;
            // Anchor each row proportionally; do not read the previous frame's canvas dimensions.
            for (int i = 0; i < choiceCount; i++)
            {
                float top = (float)i / choiceCount, bottom = (float)(i + 1) / choiceCount;
                Stretch(choiceRows[i].rectTransform, new Vector2(0, 1 - bottom), new Vector2(1, 1 - top),
                    new Vector2(22, -header + bottom * padding + 6), new Vector2(-22, -header + top * padding));
            }
        }

        public int ChoiceAtPointer(Vector2 pointer)
        {
            if (!canvas || !canvas.enabled || !sceneCamera.pixelRect.Contains(pointer)) return -1;
            for (int i = 0; i < choiceCount; i++)
                if (RectTransformUtility.RectangleContainsScreenPoint(choiceRows[i].rectTransform, pointer, overlayCamera)) return i;
            return -1;
        }
        public void HighlightChoice(int index)
        {
            for (int i = 0; i < choiceCount; i++)
                choiceRows[i].color = i == index ? new Color(choiceColor.r, choiceColor.g, choiceColor.b, 0.25f)
                    : new Color(1, 1, 1, 0.065f);
        }
        void ClearChoices()
        {
            choiceCount = 0;
            foreach (var row in choiceRows) row.gameObject.SetActive(false);
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
            ClearChoices();
            if (canvas) canvas.enabled = false;
            if (overlayCamera) overlayCamera.enabled = false;
        }
        void LateUpdate()
        {
            if (overlayCamera && sceneCamera) overlayCamera.rect = sceneCamera.rect;
        }
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
