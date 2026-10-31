using UnityEngine;

namespace PurpleGlove
{
    public sealed partial class CipherWorkbench
    {
        GUIStyle labelStyle, titleStyle, buttonStyle, fieldStyle, emptyStyle;
        bool focusField, clearFocus;
        readonly Color paper = new Color(0.92f, 0.85f, 0.69f);
        static readonly string[] Descriptions = {
            "旋转内盘，沿同一半径读取密文与明文。",
            "按形状、朝向与圆点查找 A–Z。",
            "固定明文字母行；滑动双字母表，对齐当前密钥。",
            "上行填明文字母，下行填写对应密文。",
            "每行 41 格。填完两段后，按列上下交替读出。" };

        void OnGUI()
        {
            if (Interrupted) return;
            if (labelStyle == null) CreateStyles();
            if (clearFocus) { GUI.FocusControl(null); clearFocus = false; }
            GUI.depth = -50;
            var matrix = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
            float width = Screen.width / scale, height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            if (ToolboxOpen) DrawToolbox(width, height);
            else if (InWorkspace)
            {
                bool enabled = GUI.enabled;
                GUI.enabled = enabled && !writing;
                DrawWorkspace(width, height, scale);
                GUI.enabled = enabled;
                if (writing) DrawWriting(width, height);
            }
            GUI.matrix = matrix;
        }

        void DrawToolbox(float width, float height)
        {
            float top = Mathf.Max(40, (height - 540) * 0.5f);
            Panel(new Rect(24, top, 355, 540));
            GUI.Label(new Rect(44, top + 20, 310, 36), "解密工具盒", titleStyle);
            GUI.Label(new Rect(44, top + 62, 310, 26), "选择一件，放到解码桌垫", labelStyle);
            for (int i = 0; i < ToolNames.Length; i++)
            {
                bool selected = ActiveTool == (Tool)i;
                if (GUI.Button(new Rect(44, top + 104 + i * 67, 315, 40),
                    (selected ? "✓  " : "") + ToolNames[i], buttonStyle)) SelectTool((Tool)i);
                GUI.Label(new Rect(48, top + 144 + i * 67, 307, 22),
                    selected ? "已放在桌垫 · 当前设置会保留" : new[] { "双层字母环", "26 个图形与字母", "固定尺身与可滑动尺芯", "两行空白格 · 手动填写", "两行纸条 · 手动排列" }[i], labelStyle);
            }
            if (GUI.Button(new Rect(44, top + 461, 150, 42), "收回当前工具", buttonStyle)) SelectTool(Tool.None);
            if (GUI.Button(new Rect(209, top + 461, 150, 42), "合上 · Esc", buttonStyle)) CloseToolbox();
            GUI.Label(new Rect(width * 0.4f, height - 74, width * 0.55f, 30), "每次只取一件；更换时原工具自动归盒。", labelStyle);
        }

        void DrawWorkspace(float width, float height, float scale)
        {
            float left = (width - 940) * 0.5f;
            Panel(new Rect(left, 18, 940, 62));
            GUI.Label(new Rect(left + 18, 27, 530, 36), ActiveTool == Tool.None ? "解码桌垫" : ToolNames[(int)ActiveTool], titleStyle);
            if (GUI.Button(new Rect(left + 553, 29, 115, 38), "工具盒", buttonStyle)) OpenToolbox();
            if (GUI.Button(new Rect(left + 678, 29, 115, 38), "放回工具", buttonStyle)) SelectTool(Tool.None);
            if (GUI.Button(new Rect(left + 803, 29, 119, 38), "返回桌面", buttonStyle))
            {
                EndWriting(); pendingDesktop = true;
            }

            if (ActiveTool == Tool.Affine || ActiveTool == Tool.RailFence) DrawCells(scale);
            Rect slip = WorldRect(mat, new Vector3(0, 0.014f, -0.222f), 0.57f, 0.15f, scale);
            if (!writing && GUI.Button(slip, GUIContent.none, emptyStyle))
            {
                EndWriting(); writing = true; focusField = true; BeginTyping(); Play(paperSound);
            }
            if (ActiveTool == Tool.None)
                GUI.Label(new Rect(left + 80, height * 0.40f, 800, 45), "打开工具盒，选择一件解密工具。", titleStyle);
            Panel(new Rect(left, height - 125, 940, 83));
            float y = height - 116;
            if (ActiveTool != Tool.None) GUI.Label(new Rect(left + 18, y, 910, 26), Descriptions[(int)ActiveTool], labelStyle);
            if (ActiveTool == Tool.Caesar)
            {
                if (GUI.Button(new Rect(left + 18, y + 31, 70, 32), "−", buttonStyle)) SetCaesarShift(CaesarShift - 1);
                GUI.Label(new Rect(left + 100, y + 31, 300, 32), "位移 " + CaesarShift + "     明文 A ↔ 密文 " + (char)('A' + CaesarShift), labelStyle);
                if (GUI.Button(new Rect(left + 405, y + 31, 70, 32), "+", buttonStyle)) SetCaesarShift(CaesarShift + 1);
                int shift = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(left + 510, y + 42, 400, 20), CaesarShift, 0, 25));
                if (shift != CaesarShift) SetCaesarShift(shift);
            }
            else if (ActiveTool == Tool.SaintCyr)
            {
                if (GUI.Button(new Rect(left + 18, y + 31, 45, 32), "−", buttonStyle)) SetSlideShift(SlideShift - 1);
                GUI.Label(new Rect(left + 72, y + 31, 210, 32), "密钥 " + (char)('A' + SlideShift) + " 对齐 A", labelStyle);
                if (GUI.Button(new Rect(left + 237, y + 31, 45, 32), "+", buttonStyle)) SetSlideShift(SlideShift + 1);
                GUI.Label(new Rect(left + 298, y + 31, 70, 32), "关键词", labelStyle);
                GUI.SetNextControlName("CipherKeyword");
                string key = GUI.TextField(new Rect(left + 370, y + 31, 200, 32), Keyword, 24, fieldStyle);
                if (key != Keyword) SetKeyword(key);
                if (GUI.GetNameOfFocusedControl() == "CipherKeyword") BeginTyping();
                if (GUI.Button(new Rect(left + 580, y + 31, 70, 32), "完成", buttonStyle)) EndWriting();
                if (GUI.Button(new Rect(left + 660, y + 31, 55, 32), "←", buttonStyle)) StepKeyword(-1);
                GUI.Label(new Rect(left + 725, y + 31, 130, 32), Keyword.Length == 0 ? "未填写" : (KeywordIndex + 1) + " / " + Keyword.Length, labelStyle);
                if (GUI.Button(new Rect(left + 855, y + 31, 55, 32), "→", buttonStyle)) StepKeyword(1);
            }
            else
            {
                GUI.Label(new Rect(left + 18, y + 31, 770, 32), editingCell >= 0
                    ? "输入 A–Z 自动前进 · Tab / 方向键换格 · 可粘贴 · Backspace 擦除 · Esc 完成"
                    : "点击格子填写；点击下方「明文书写处」记录结果。", labelStyle);
                if (editingCell >= 0 && GUI.Button(new Rect(left + 825, y + 31, 85, 32), "完成", buttonStyle)) EndWriting();
            }
        }
        bool pendingDesktop;

        void DrawCells(float scale)
        {
            TextMesh[] labels = ActiveTool == Tool.Affine ? affineLetters : railLetters;
            float pitch = ActiveTool == Tool.Affine ? 0.020f : 0.013f;
            for (int i = 0; i < labels.Length; i++)
            {
                if (!labels[i]) continue;
                Rect r = WorldRect(labels[i].transform, Vector3.zero, pitch * 0.95f, 0.035f, scale);
                if (editingCell == i)
                {
                    var old = GUI.color; GUI.color = new Color(0.65f, 0.40f, 0.12f, 0.3f);
                    GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = old;
                }
                if (GUI.Button(r, GUIContent.none, emptyStyle))
                {
                    editingCell = i; BeginTyping(); GUI.FocusControl(null);
                }
            }
            HandleGridInput(Event.current, labels.Length);
        }

        void HandleGridInput(Event e, int length)
        {
            if (editingCell < 0 || e.type != EventType.KeyDown) return;
            int columns = length / 2;
            if ((e.control || e.command) && e.keyCode == KeyCode.V)
            {
                foreach (char c in LettersOnly(GUIUtility.systemCopyBuffer, length - editingCell))
                {
                    SetCell(ActiveTool, editingCell, c.ToString());
                    if (editingCell < length - 1) editingCell++;
                }
            }
            else if (e.keyCode == KeyCode.Backspace) { SetCell(ActiveTool, editingCell, ""); editingCell = Mathf.Max(0, editingCell - 1); }
            else if (e.keyCode == KeyCode.Delete) SetCell(ActiveTool, editingCell, "");
            else if (e.keyCode == KeyCode.LeftArrow) editingCell = Mathf.Max(0, editingCell - 1);
            else if (e.keyCode == KeyCode.RightArrow || e.keyCode == KeyCode.Tab) editingCell = (editingCell + (e.shift ? length - 1 : 1)) % length;
            else if (e.keyCode == KeyCode.UpArrow) editingCell = Mathf.Max(0, editingCell - columns);
            else if (e.keyCode == KeyCode.DownArrow) editingCell = Mathf.Min(length - 1, editingCell + columns);
            else if (!e.control && !e.command && char.IsLetter(e.character) && e.character <= 127)
            {
                SetCell(ActiveTool, editingCell, e.character.ToString()); editingCell = Mathf.Min(length - 1, editingCell + 1);
            }
            else return;
            e.Use();
        }

        void DrawWriting(float width, float height)
        {
            float x = (width - 820) * 0.5f, y = (height - 390) * 0.5f;
            Panel(new Rect(x, y, 820, 390));
            GUI.Label(new Rect(x + 24, y + 18, 600, 35), "明文书写处", titleStyle);
            GUI.SetNextControlName("CipherPlaintext");
            string value = GUI.TextArea(new Rect(x + 24, y + 67, 772, 245), Plaintext, 600, fieldStyle);
            if (value != Plaintext) SetPlaintext(value);
            if (focusField && !clearFocus) { GUI.FocusControl("CipherPlaintext"); focusField = false; }
            GUI.Label(new Rect(x + 24, y + 330, 520, 36), "草稿随工具切换保留 · " + Plaintext.Length + " / 600", labelStyle);
            if (GUI.Button(new Rect(x + 626, y + 330, 170, 40), "落笔 · Esc", buttonStyle)) EndWriting();
        }

        Rect WorldRect(Transform reference, Vector3 localCenter, float w, float h, float scale)
        {
            // Text is rotated onto the tabletop; use mat axes instead of the glyph transform's axes.
            Vector3 center = reference.TransformPoint(localCenter);
            Vector3 a = sceneCamera.WorldToScreenPoint(center - mat.right * w * 0.5f + mat.forward * h * 0.5f);
            Vector3 b = sceneCamera.WorldToScreenPoint(center + mat.right * w * 0.5f - mat.forward * h * 0.5f);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x) / scale, (Screen.height - Mathf.Max(a.y, b.y)) / scale,
                Mathf.Max(a.x, b.x) / scale, (Screen.height - Mathf.Min(a.y, b.y)) / scale);
        }
        void Panel(Rect rect)
        {
            var old = GUI.color; GUI.color = new Color(0.10f, 0.075f, 0.055f, 0.97f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
        }
        void CreateStyles()
        {
            labelStyle = new GUIStyle(GUI.skin.label) { font = uiFont, fontSize = 17, alignment = TextAnchor.MiddleLeft };
            labelStyle.normal.textColor = paper;
            titleStyle = new GUIStyle(labelStyle) { fontSize = 23 };
            buttonStyle = new GUIStyle(GUI.skin.button) { font = uiFont, fontSize = 17 };
            fieldStyle = new GUIStyle(GUI.skin.textArea) { font = uiFont, fontSize = 20, wordWrap = true, padding = new RectOffset(8, 8, 5, 5) };
            emptyStyle = new GUIStyle();
        }
    }
}
