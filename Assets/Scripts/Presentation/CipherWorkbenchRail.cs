using UnityEngine;

namespace PurpleGlove
{
    public sealed partial class CipherWorkbench
    {
        public TextMesh railInputInk;
        public string RailCiphertext { get; private set; } = "";
        bool editingRailCipher, railInputRequested, replaceRailInput;

        public void SetRailCiphertext(string value)
        {
            RailCiphertext = LettersOnly(value, Rails.Length);
            int split = (RailCiphertext.Length + 1) / 2;
            int columns = Rails.Length / 2;
            for (int i = 0; i < Rails.Length; i++)
            {
                int source = i < columns ? i : split + i - columns;
                bool filled = i < columns ? i < split : source < RailCiphertext.Length;
                Rails[i] = filled ? RailCiphertext[source].ToString() : "";
                if (i < railLetters.Length && railLetters[i]) railLetters[i].text = Rails[i];
            }
            RefreshRailInputInk();
        }

        void RefreshRailInputInk()
        {
            if (railInputInk) Localization.SetFormat(railInputInk,
                editingRailCipher ? "密文 {0}/82 · Enter 誊写明文" : "点击输入密文 · {0}/82", RailCiphertext.Length);
        }

        void BeginRailCipherInput()
        {
            EndWriting();
            editingRailCipher = true;
            BeginTyping(); Input.imeCompositionMode = IMECompositionMode.Off;
            RefreshRailInputInk(); RefreshKeyboardStatus();
        }

        void HandleRailCipherInput(Event e)
        {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            { EndWriting(); e.Use(); return; }
            bool modifier = e.control || e.command;
            if (modifier && e.keyCode == KeyCode.A) replaceRailInput = true;
            else if (modifier && e.keyCode == KeyCode.V)
            {
                string letters = LettersOnly(GUIUtility.systemCopyBuffer, Rails.Length);
                if (letters.Length > 0)
                { SetRailCiphertext((replaceRailInput ? "" : RailCiphertext) + letters); replaceRailInput = false; }
            }
            else if (e.keyCode == KeyCode.Delete || (e.keyCode == KeyCode.Backspace && replaceRailInput))
            { SetRailCiphertext(""); replaceRailInput = false; }
            else if (e.keyCode == KeyCode.Backspace)
            {
                if (RailCiphertext.Length > 0) SetRailCiphertext(RailCiphertext.Substring(0, RailCiphertext.Length - 1));
            }
            else if (!modifier && !e.alt)
            {
                string letter = LettersOnly(e.character.ToString(), 1);
                if (letter.Length > 0)
                { SetRailCiphertext((replaceRailInput ? "" : RailCiphertext) + letter); replaceRailInput = false; }
            }
            e.Use();
        }
    }
}
