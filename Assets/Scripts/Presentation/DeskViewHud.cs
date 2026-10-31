using UnityEngine;
using UnityEngine.EventSystems;

namespace PurpleGlove
{
    /// <summary>View/book hints and edge navigation, processed before physical pointer input.</summary>
    [DefaultExecutionOrder(-300), DisallowMultipleComponent, RequireComponent(typeof(DeskScenePreview))]
    public sealed class DeskViewHud : MonoBehaviour
    {
        public Font labelFont;
        [Range(0, 1)] public float arrowOpacity = 0.24f;
        [Range(0, 1)] public float arrowHoverOpacity = 0.95f;
        DeskScenePreview view;
        SceneInputGate inputGate;
        SceneObjectInteractor interactor;
        GUIStyle labelStyle;
        static readonly Color Ink = Color.black;
        static readonly Color Paper = new Color(0.72f, 0.63f, 0.45f);
        static readonly Color Selected = new Color(0.93f, 0.83f, 0.60f);
        static readonly string[] Glyphs = { "10001100011010110101101011010101010", "01110100011000111111100011000110001",
            "01111100001000001110000010000111110", "11110100011000110001100011000111110" };

        void Awake()
        {
            view = GetComponent<DeskScenePreview>();
            inputGate = GetComponent<SceneInputGate>();
            interactor = GetComponent<SceneObjectInteractor>();
            var workbench = GetComponent<CipherWorkbench>();
            // The original hint font is a subset; use the bundled full font for the new return text.
            if (workbench && workbench.uiFont) labelFont = workbench.uiFont;
        }

        Rect Viewport(out int scale)
        {
            var camera = view ? view.sceneCamera : GetComponent<Camera>();
            Rect viewport = camera ? camera.pixelRect : new Rect(0, 0, Screen.width, Screen.height);
            scale = Mathf.Max(1, Mathf.RoundToInt(viewport.height / 1080f));
            return new Rect(viewport.x, Screen.height - viewport.yMax, viewport.width, viewport.height);
        }

        Rect ArrowBounds(DeskScenePreview.Direction direction)
        {
            Rect viewport = Viewport(out int scale);
            float size = 56 * scale, inset = 10 * scale;
            Vector2 center = viewport.center;
            switch (direction)
            {
                case DeskScenePreview.Direction.Up: center.y = viewport.yMin + inset + size / 2; break;
                case DeskScenePreview.Direction.Left: center.x = viewport.xMin + inset + size / 2; break;
                case DeskScenePreview.Direction.Right: center.x = viewport.xMax - inset - size / 2; break;
                case DeskScenePreview.Direction.Down: center.y = viewport.yMax - inset - size / 2; break;
            }
            return new Rect(Mathf.Floor(center.x - size / 2), Mathf.Floor(center.y - size / 2), size, size);
        }

        bool TryPickArrow(Vector2 screenPosition, out DeskScenePreview.View target)
        {
            target = default;
            if (!isActiveAndEnabled || !view || !view.CanNavigate) return false;
            Vector2 pointer = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            for (int i = 0; i < 4; i++)
            {
                var direction = (DeskScenePreview.Direction)i;
                if (view.TryGetNavigationTarget(direction, out target) && ArrowBounds(direction).Contains(pointer)) return true;
            }
            return false;
        }

        NotebookBook InspectedBook
        {
            get
            {
                if (!isActiveAndEnabled || !interactor || !interactor.isActiveAndEnabled || !interactor.InspectedObject) return null;
                var book = interactor.InspectedObject.GetComponent<NotebookBook>();
                return book && book.CanTurnPages ? book : null;
            }
        }

        bool TryPickBookArrow(Vector2 screenPosition, out NotebookBook book, out int step)
        {
            book = InspectedBook; step = 0;
            if (!book) return false;
            Vector2 pointer = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            if (book.CurrentSpread > 0 && ArrowBounds(DeskScenePreview.Direction.Left).Contains(pointer)) step = -1;
            else if (book.CurrentSpread + 1 < book.SpreadCount && ArrowBounds(DeskScenePreview.Direction.Right).Contains(pointer)) step = 1;
            return step != 0;
        }

        public bool BlocksPointer(Vector2 screenPosition) => TryPickBookArrow(screenPosition, out _, out _) || TryPickArrow(screenPosition, out _);

        bool ProcessPointer(Vector2 screenPosition, bool clicked)
        {
            if (TryPickBookArrow(screenPosition, out var book, out int step))
            {
                if (clicked)
                {
                    book.TurnPage(step);
                    if (inputGate) inputGate.ConsumeFrame();
                }
                return true;
            }
            if (!TryPickArrow(screenPosition, out var target)) return false;
            if (clicked && view.SetView(target) && inputGate) inputGate.ConsumeFrame();
            return true;
        }

        void Update()
        {
            if (!Application.isFocused || !Input.mousePresent || Cursor.lockState == CursorLockMode.Locked
                || (EventSystem.current && EventSystem.current.IsPointerOverGameObject())) return;
            ProcessPointer(Input.mousePosition, Input.GetMouseButtonDown(0));
        }

        void OnGUI()
        {
            var book = InspectedBook;
            if (!book && (!view || !view.CanNavigate)) return;
            Rect viewport = Viewport(out int scale);
            var panel = new Rect(Mathf.Floor(viewport.xMin + 12 * scale),
                Mathf.Floor(viewport.yMax - 34 * scale), 220 * scale, 22 * scale);
            if (labelStyle == null) labelStyle = new GUIStyle { alignment = TextAnchor.MiddleLeft, font = labelFont };
            labelStyle.fontSize = 14 * scale;
            labelStyle.normal.textColor = Paper;
            if (book)
            {
                DrawBookNavigation(book, viewport, panel, scale);
                return;
            }
            for (int i = 0; i < 4; i++)
            {
                var direction = (DeskScenePreview.Direction)i;
                if (view.TryGetNavigationTarget(direction, out _)) DrawArrow(direction, scale);
            }
            if (view.CurrentView == DeskScenePreview.View.MatFocus || view.CurrentView == DeskScenePreview.View.DecodeFocus)
                GUI.Label(new Rect(panel.x, panel.y - 24 * scale, panel.width, panel.height), "滚轮 缩放视角", labelStyle);
            if (view.CurrentView == DeskScenePreview.View.DecodeFocus)
            {
                GUI.Label(panel, "Esc 返回中桌面视角", labelStyle);
                return;
            }
            Key(panel, scale, 0, 0, KeyCode.W);
            Key(panel, scale, 16, 1, KeyCode.A);
            Key(panel, scale, 32, 2, KeyCode.S);
            Key(panel, scale, 48, 3, KeyCode.D);
            GUI.Label(new Rect(panel.x + 70 * scale, panel.y, 64 * scale, panel.height), "切换视角", labelStyle);
        }

        void DrawBookNavigation(NotebookBook book, Rect viewport, Rect panel, int scale)
        {
            if (book.CurrentSpread > 0) DrawArrow(DeskScenePreview.Direction.Left, scale, book.CanGoBack);
            if (book.CurrentSpread + 1 < book.SpreadCount) DrawArrow(DeskScenePreview.Direction.Right, scale, book.CanGoForward);
            Key(panel, scale, 0, 1, KeyCode.A);
            Key(panel, scale, 16, 3, KeyCode.D);
            GUI.Label(new Rect(panel.x + 38 * scale, panel.y, 310 * scale, panel.height),
                "翻页 · ← / → · Esc 放回书本", labelStyle);
            string pages = book.IsTurning ? "翻页中…" : $"第 {book.CurrentSpread * 2 + 1}–{book.CurrentSpread * 2 + 2} 页 / 共 {book.SpreadCount * 2} 页";
            float width = labelStyle.CalcSize(new GUIContent(pages)).x;
            GUI.Label(new Rect(viewport.xMax - width - 12 * scale, panel.y, width, panel.height), pages, labelStyle);
        }

        void DrawArrow(DeskScenePreview.Direction direction, int scale, bool enabled = true)
        {
            Rect rect = ArrowBounds(direction);
            bool hovered = enabled && Application.isFocused && Input.mousePresent && rect.Contains(Event.current.mousePosition);
            Color color = hovered ? Selected : Paper;
            color.a = enabled ? (hovered ? arrowHoverOpacity : arrowOpacity) : arrowOpacity * 0.5f;
            var previous = GUI.color;
            // A pixel chevron with a dark outline remains readable against both paper and wood.
            for (int pass = 0; pass < 2; pass++)
            {
                GUI.color = pass == 0 ? new Color(0, 0, 0, color.a) : color;
                int pixel = 2 * scale;
                int border = pass == 0 ? scale : 0;
                for (int row = -6; row <= 6; row++)
                {
                    Vector2 point = new Vector2(Mathf.Abs(row) - 3, row);
                    if (direction == DeskScenePreview.Direction.Right) point.x = -point.x;
                    else if (direction == DeskScenePreview.Direction.Up) point = new Vector2(point.y, point.x);
                    else if (direction == DeskScenePreview.Direction.Down) point = new Vector2(point.y, -point.x);
                    GUI.DrawTexture(new Rect(rect.center.x + point.x * pixel - pixel - border,
                        rect.center.y + point.y * pixel - pixel - border, 2 * pixel + 2 * border,
                        2 * pixel + 2 * border), Texture2D.whiteTexture);
                }
            }
            GUI.color = previous;
        }

        void Key(Rect panel, int scale, int x, int glyph, KeyCode key)
        {
            var rect = new Rect(panel.x + x * scale, panel.y + 2 * scale, 14 * scale, 18 * scale);
            DrawPanel(rect, Input.GetKey(key) ? Selected : Paper, scale);
            var previous = GUI.color;
            GUI.color = Ink;
            for (int i = 0; i < Glyphs[glyph].Length; i++)
                if (Glyphs[glyph][i] == '1') GUI.DrawTexture(new Rect(rect.x + (4 + i % 5) * scale,
                    rect.y + (5 + i / 5) * scale, scale, scale), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        static void DrawPanel(Rect rect, Color fill, int pixel)
        {
            var previous = GUI.color;
            GUI.color = Ink;
            GUI.DrawTexture(new Rect(rect.x + pixel, rect.y, rect.width - 2 * pixel, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y + pixel, rect.width, rect.height - 2 * pixel), Texture2D.whiteTexture);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(rect.x + 2 * pixel, rect.y + 2 * pixel, rect.width - 4 * pixel, rect.height - 4 * pixel), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
