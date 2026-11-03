using UnityEngine;

namespace PurpleGlove
{
    public sealed partial class CipherWorkbench
    {
        public TextMesh affineAInk, affineBInk;
        public string AffineA { get; private set; } = "";
        public string AffineB { get; private set; } = "";
        public bool AffineParametersValid { get; private set; }
        int editingAffineParameter = -1;
        bool replaceAffineInput;

        static bool IsAffineNumber(string value)
        {
            if (value == null || value.Length > 2) return false;
            foreach (char c in value) if (c < '0' || c > '9') return false;
            return true;
        }

        public void SetAffineParameter(int index, string value)
        {
            if (index < 0 || index > 1 || !IsAffineNumber(value)) return;
            if (index == 0) AffineA = value; else AffineB = value;
            decodedStrokes.Clear();
            RefreshAffineTable();
        }

        void RefreshAffineTable()
        {
            bool hasA = int.TryParse(AffineA, out int a), hasB = int.TryParse(AffineB, out int b);
            AffineParametersValid = hasA && hasB && a > 0 && a < 26 && a % 2 != 0 && a != 13 && b >= 0 && b < 26;
            for (int i = 0; i < 26; i++)
            {
                Affine[i] = Alphabet[i].ToString();
                Affine[i + 26] = AffineParametersValid ? Alphabet[Wrap(a * i + b)].ToString() : "";
            }
            for (int i = 0; i < Affine.Length && i < affineLetters.Length; i++)
                if (affineLetters[i]) affineLetters[i].text = Affine[i];
            RefreshAffineParameterInk();
            RefreshKeyboardMapping();
        }

        void RefreshAffineParameterInk()
        {
            if (affineAInk) affineAInk.text = "A = " + (AffineA.Length == 0 ? "?" : AffineA) + (editingAffineParameter == 0 ? "_" : "");
            if (affineBInk) affineBInk.text = "B = " + (AffineB.Length == 0 ? "?" : AffineB) + (editingAffineParameter == 1 ? "_" : "");
        }

        string AffineStatus
        {
            get
            {
                if (AffineA.Length == 0 && AffineB.Length == 0) return "仿射 · 点击 A/B 填参数；未设置时可直接键入明文";
                if (int.TryParse(AffineA, out int a) && (a == 0 || a > 25 || a % 2 == 0 || a == 13))
                    return "A 须在 1–25 内且与 26 互质";
                if (int.TryParse(AffineB, out int b) && b > 25) return "B 须在 0–25 内";
                if (AffineA.Length == 0 || AffineB.Length == 0) return "填完 A、B 后自动生成对照表";
                return "仿射 · 键入密文 → 明文";
            }
        }

        void BeginAffineInput(int index)
        {
            if (index < 0 || index > 1) return;
            EndWriting();
            editingAffineParameter = index; replaceAffineInput = true;
            BeginTyping(); Input.imeCompositionMode = IMECompositionMode.Off;
            RefreshAffineParameterInk(); RefreshKeyboardStatus();
        }

        void HandleAffineInput(Event e)
        {
            if (e.keyCode == KeyCode.Tab)
            {
                BeginAffineInput(1 - editingAffineParameter);
            }
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                if (editingAffineParameter == 0) BeginAffineInput(1);
                else EndWriting();
            }
            else
            {
                string value = editingAffineParameter == 0 ? AffineA : AffineB;
                bool modifier = e.control || e.command;
                if (modifier && e.keyCode == KeyCode.A) replaceAffineInput = true;
                else if (modifier && e.keyCode == KeyCode.V)
                {
                    string pasted = GUIUtility.systemCopyBuffer.Trim();
                    string next = replaceAffineInput ? pasted : value + pasted;
                    if (IsAffineNumber(next)) { SetAffineParameter(editingAffineParameter, next); replaceAffineInput = false; }
                }
                else if (e.keyCode == KeyCode.Delete || (e.keyCode == KeyCode.Backspace && replaceAffineInput))
                { SetAffineParameter(editingAffineParameter, ""); replaceAffineInput = false; }
                else if (e.keyCode == KeyCode.Backspace)
                { if (value.Length > 0) SetAffineParameter(editingAffineParameter, value.Substring(0, value.Length - 1)); }
                else if (!modifier && !e.alt && e.character >= '0' && e.character <= '9')
                {
                    string next = (replaceAffineInput ? "" : value) + e.character;
                    if (IsAffineNumber(next)) { SetAffineParameter(editingAffineParameter, next); replaceAffineInput = false; }
                }
            }
            e.Use();
        }
    }
}
