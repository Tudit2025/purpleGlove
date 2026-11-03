using UnityEngine;

namespace PurpleGlove
{
    public sealed partial class DeskViewHud
    {
        [Header("案头待办")]
        public bool showCommands = true;
        DeskCommandHints commands;
        GUIStyle commandHeading, commandTask, commandDetail;

        void DrawCommands()
        {
            if (!showCommands || commands == null) return;
            var hint = commands.Resolve();
            if (!hint.IsVisible) return;
            Rect viewport = Viewport(out _);
            float scale = Mathf.Clamp(viewport.height / 1080f, 0.7f, 2f);
            float margin = 22 * scale, padding = 18 * scale;
            float width = Mathf.Min(344 * scale, viewport.width - margin * 2);
            if (width <= padding * 2) return;
            if (commandHeading == null)
            {
                commandHeading = CommandStyle(new Color(0.70f, 0.61f, 0.43f));
                commandTask = CommandStyle(new Color(0.94f, 0.86f, 0.69f));
                commandDetail = CommandStyle(new Color(0.76f, 0.71f, 0.61f));
            }
            commandHeading.font = commandTask.font = commandDetail.font = labelFont;
            commandHeading.fontSize = Mathf.RoundToInt(14 * scale);
            commandTask.fontSize = Mathf.RoundToInt(20 * scale);
            commandDetail.fontSize = Mathf.RoundToInt(16 * scale);
            float inner = width - padding * 2;
            float headingHeight = commandHeading.CalcHeight(new GUIContent("案头待办"), inner);
            float taskHeight = commandTask.CalcHeight(new GUIContent(hint.task), inner);
            float detailHeight = commandDetail.CalcHeight(new GUIContent(hint.detail), inner);
            var panel = new Rect(viewport.xMax - margin - width, viewport.yMin + margin, width,
                padding * 2 + headingHeight + taskHeight + detailHeight + 16 * scale);
            // Labels only: no hit regions or input consumption over the physical desk.
            var previousColor = GUI.color;
            GUI.color = new Color(0.045f, 0.036f, 0.025f, 0.88f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = new Color(0.57f, 0.44f, 0.26f, 0.8f);
            GUI.DrawTexture(new Rect(panel.x, panel.y, 2 * scale, panel.height), Texture2D.whiteTexture);
            GUI.color = previousColor;
            float x = panel.x + padding, y = panel.y + padding;
            GUI.Label(new Rect(x, y, inner, headingHeight), "案头待办", commandHeading);
            y += headingHeight + 9 * scale;
            GUI.Label(new Rect(x, y, inner, taskHeight), hint.task, commandTask);
            y += taskHeight + 7 * scale;
            GUI.Label(new Rect(x, y, inner, detailHeight), hint.detail, commandDetail);
        }

        static GUIStyle CommandStyle(Color ink)
        {
            var style = new GUIStyle { wordWrap = true, alignment = TextAnchor.UpperLeft, richText = false };
            style.normal.textColor = ink;
            return style;
        }
    }
}
