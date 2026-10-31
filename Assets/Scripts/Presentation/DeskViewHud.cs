using UnityEngine;

namespace PurpleGlove
{
    /// <summary>A small, non-interactive WASD view-switching hint.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(DeskScenePreview))]
    public sealed class DeskViewHud : MonoBehaviour
    {
        public Font labelFont;
        DeskScenePreview view;
        GUIStyle labelStyle;
        static readonly Color Ink = Color.black;
        static readonly Color Paper = new Color(0.72f, 0.63f, 0.45f);
        static readonly Color Selected = new Color(0.93f, 0.83f, 0.60f);
        static readonly string[] Glyphs = { "10001100011010110101101011010101010", "01110100011000111111100011000110001",
            "01111100001000001110000010000111110", "11110100011000110001100011000111110" };

        void Awake()
        {
            view = GetComponent<DeskScenePreview>();
        }

        Rect Bounds(out int scale)
        {
            var camera = view ? view.sceneCamera : GetComponent<Camera>();
            Rect viewport = camera ? camera.pixelRect : new Rect(0, 0, Screen.width, Screen.height);
            scale = Mathf.Max(1, Mathf.RoundToInt(viewport.height / 1080f));
            return new Rect(Mathf.Floor(viewport.xMin + 12 * scale),
                Mathf.Floor(Screen.height - viewport.yMin - 34 * scale), 134 * scale, 22 * scale);
        }

        void OnGUI()
        {
            if (!view) return;
            Rect panel = Bounds(out int scale);
            if (labelStyle == null) labelStyle = new GUIStyle { alignment = TextAnchor.MiddleLeft, font = labelFont };
            labelStyle.fontSize = 14 * scale;
            labelStyle.normal.textColor = Paper;
            Key(panel, scale, 0, 0, KeyCode.W);
            Key(panel, scale, 16, 1, KeyCode.A);
            Key(panel, scale, 32, 2, KeyCode.S);
            Key(panel, scale, 48, 3, KeyCode.D);
            GUI.Label(new Rect(panel.x + 70 * scale, panel.y, 64 * scale, panel.height), "切换视角", labelStyle);
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
