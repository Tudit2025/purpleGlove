using System.Collections.Generic;
using UnityEngine;

namespace PurpleGlove
{
    public sealed partial class CipherWorkbench
    {
        const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        bool caesarConfigured, applyingDecodedText, decoderOwnsIme;
        IMECompositionMode decoderPreviousIme;
        Transform[] letterStacks;
        Vector3[] keyboardPositions, stackTargets;
        GameObject[] inputKeyLabels;
        TextMesh trayInscription;
        string originalTrayInscription;
        float nextStampSound;
        readonly List<LetterStamp> letterStamps = new List<LetterStamp>();
        readonly List<DecodedStroke> decodedStrokes = new List<DecodedStroke>();

        sealed class LetterStamp
        {
            public GameObject visual;
            public CipherLetterTile source;
            public Vector3 start, destination, scale;
            public Quaternion rotation, paperRotation;
            public float elapsed;
            public int characterIndex;
        }
        struct DecodedStroke
        {
            public int lengthBefore, keywordIndex, slideShift;
            public string keyword;
        }

        public bool KeyboardDecodeReady { get; private set; }
        public bool HasPendingPlaintextStamps => letterStamps.Count > 0;
        // Index is the physical A-Z key; value is the plaintext letter displayed in that slot.
        public string KeyboardMapping { get; private set; } = Alphabet;
        public bool UsesDecodeKeyboard => isActiveAndEnabled && InWorkspace && KeyboardDecodeReady
            && !typing && !ToolboxOpen && !IsManipulating && !Interrupted
            && (!gate || !gate.BlocksWorldInput) && (!interactor || !interactor.InspectedObject);

        void InitializeKeyboardTray()
        {
            letterStacks = new Transform[26]; keyboardPositions = new Vector3[26]; stackTargets = new Vector3[26];
            inputKeyLabels = new GameObject[26];
            if (!letterTray) return;
            trayInscription = letterTray.Find("Tray inscription")?.GetComponent<TextMesh>();
            if (trayInscription) originalTrayInscription = trayInscription.text;
            for (int i = 0; i < 26; i++)
            {
                var tile = i < letterSamples.Length ? letterSamples[i] : null;
                if (!tile || !tile.homeSocket || tile.transform.parent != tile.homeSocket.parent) continue;
                letterStacks[i] = tile.transform.parent;
                keyboardPositions[i] = stackTargets[i] = letterStacks[i].localPosition;
                var letter = tile.GetComponentInChildren<TextMesh>();
                if (!letter) continue;
                // Etched key names stay in QWERTY positions while the plaintext stacks move.
                var label = Instantiate(letter.gameObject, letterTray).GetComponent<TextMesh>();
                label.name = "Input key " + Alphabet[i]; label.text = Alphabet[i].ToString();
                label.transform.localPosition = keyboardPositions[i] + new Vector3(0, 0.004f, 0.029f);
                label.transform.localRotation = Quaternion.Euler(90, 0, 0);
                label.transform.localScale = Vector3.one;
                label.characterSize = 0.0011f;
                label.color = new Color(0.70f, 0.59f, 0.39f);
                inputKeyLabels[i] = label.gameObject; label.gameObject.SetActive(false);
            }
        }

        public static bool TryBuildAffineMapping(string[] cells, out string mapping)
        {
            mapping = Alphabet;
            if (cells == null || cells.Length != 52) return false;
            int plainSeen = 0, cipherSeen = 0;
            var letters = new char[26];
            for (int i = 0; i < 26; i++)
            {
                string plain = cells[i], cipher = cells[26 + i];
                if (string.IsNullOrEmpty(plain) || plain.Length != 1 || plain[0] < 'A' || plain[0] > 'Z'
                    || string.IsNullOrEmpty(cipher) || cipher.Length != 1 || cipher[0] < 'A' || cipher[0] > 'Z') return false;
                int p = plain[0] - 'A', c = cipher[0] - 'A';
                if ((plainSeen & (1 << p)) != 0 || (cipherSeen & (1 << c)) != 0) return false;
                plainSeen |= 1 << p; cipherSeen |= 1 << c;
                letters[c] = plain[0]; // Top row is plaintext; bottom row is ciphertext.
            }
            mapping = new string(letters);
            return true;
        }

        void RefreshKeyboardMapping()
        {
            // Pigpen is read from the reference card; the keyboard records the player's plaintext as-is.
            bool ready = BookTool || ActiveTool == Tool.Pigpen;
            string mapping = Alphabet;
            if ((ActiveTool == Tool.Caesar && caesarConfigured) || (ActiveTool == Tool.SaintCyr && Keyword.Length > 0))
            {
                int shift = ActiveTool == Tool.Caesar ? CaesarShift : SlideShift;
                var letters = new char[26];
                for (int i = 0; i < 26; i++) letters[i] = Alphabet[Wrap(i - shift)];
                mapping = new string(letters); ready = true;
            }
            else if (ActiveTool == Tool.Affine) ready = TryBuildAffineMapping(Affine, out mapping);
            KeyboardDecodeReady = ready; KeyboardMapping = mapping;
            if (letterStacks != null)
                for (int key = 0; key < 26; key++)
                {
                    stackTargets[mapping[key] - 'A'] = keyboardPositions[key];
                    if (inputKeyLabels[key]) inputKeyLabels[key].SetActive(ready);
                }
            RefreshKeyboardStatus();
        }

        public bool TryDecodeLetter(char cipher, out char plaintext)
        {
            plaintext = default;
            cipher = char.ToUpperInvariant(cipher);
            if (!KeyboardDecodeReady || cipher < 'A' || cipher > 'Z') return false;
            plaintext = KeyboardMapping[cipher - 'A'];
            return true;
        }

        void RefreshKeyboardStatus()
        {
            if (!trayInscription) return;
            string text = originalTrayInscription;
            if (BookTool) text = "书本 · 页—行—字母，直接键入明文 · Esc 放回";
            else if (editingKeyword || editingCell >= 0) text = "Enter 完成设置 · 再键入密文";
            else if (ActiveTool == Tool.Caesar) text = caesarConfigured ? "凯撒 · 键入密文 → 明文" : "转动密码盘后可键入密文";
            else if (ActiveTool == Tool.Pigpen) text = "猪圈 · 对照图形，直接键入明文";
            else if (ActiveTool == Tool.SaintCyr) text = Keyword.Length > 0
                ? "密钥 " + (KeywordIndex + 1) + "/" + Keyword.Length + " · 键入密文 → 明文" : "填写密钥后可键入密文";
            else if (ActiveTool == Tool.Affine) text = KeyboardDecodeReady ? "仿射 · 键入密文 → 明文" : "填满两行，每行 A–Z 不重复";
            if (trayInscription.text != text) trayInscription.text = text;
        }

        void UpdateDecodeKeyboard()
        {
            bool active = Application.isFocused && UsesDecodeKeyboard;
            if (active && !decoderOwnsIme)
            {
                decoderPreviousIme = Input.imeCompositionMode;
                Input.imeCompositionMode = IMECompositionMode.Off; decoderOwnsIme = true;
            }
            else if (!active) ReleaseDecoderIme();
            if (!InWorkspace || Interrupted || ToolboxOpen) ClearLetterStamps();
            RefreshKeyboardStatus();
        }

        void ReleaseDecoderIme()
        {
            if (!decoderOwnsIme) return;
            Input.imeCompositionMode = decoderPreviousIme; decoderOwnsIme = false;
        }

        bool HandleDecodeKey(Event e)
        {
            if (e.type != EventType.KeyDown || e.control || e.command || e.alt || !UsesDecodeKeyboard) return false;
            if (e.keyCode == KeyCode.Backspace)
            {
                if (Plaintext.Length > 0)
                {
                    int last = decodedStrokes.Count - 1;
                    if (last >= 0 && decodedStrokes[last].lengthBefore == Plaintext.Length - 1)
                    {
                        var stroke = decodedStrokes[last]; decodedStrokes.RemoveAt(last);
                        if (stroke.keyword != null && ActiveTool == Tool.SaintCyr && stroke.keyword == Keyword)
                        {
                            KeywordIndex = stroke.keywordIndex; SetSlideShift(stroke.slideShift); RefreshKeyword();
                        }
                    }
                    else decodedStrokes.Clear();
                    ApplyDecodedText(Plaintext.Substring(0, Plaintext.Length - 1));
                    ClearLetterStamps();
                }
                e.Use(); return true;
            }
            char output;
            bool letter = e.keyCode >= KeyCode.A && e.keyCode <= KeyCode.Z;
            if (letter)
            {
                if (!TryDecodeLetter((char)('A' + (int)e.keyCode - (int)KeyCode.A), out output)) return false;
            }
            else if (e.keyCode == KeyCode.Space) output = ' ';
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) output = '\n';
            else if (e.character >= ' ' && e.character <= '~' && !char.IsLetter(e.character)) output = e.character;
            else return false;
            if (Plaintext.Length < 600)
            {
                decodedStrokes.Add(new DecodedStroke { lengthBefore = Plaintext.Length,
                    keyword = letter && ActiveTool == Tool.SaintCyr ? Keyword : null,
                    keywordIndex = KeywordIndex, slideShift = SlideShift });
                ApplyDecodedText(Plaintext + output);
                if (letter)
                {
                    StampLetter(output);
                    if (ActiveTool == Tool.SaintCyr) StepKeyword(1);
                }
            }
            e.Use(); return true;
        }

        void ApplyDecodedText(string value)
        {
            applyingDecodedText = true;
            try { SetPlaintext(value); }
            finally { applyingDecodedText = false; }
        }

        void RefreshPlaintextInk()
        {
            if (!plaintextInk) return;
            // Record keystrokes immediately, but reveal them in order as the tiles touch the paper.
            int visibleLength = Plaintext.Length;
            foreach (var stamp in letterStamps)
                if (stamp.elapsed < 0.15f) visibleLength = Mathf.Min(visibleLength, stamp.characterIndex);
            plaintextInk.text = FormatPlaintext(Plaintext.Substring(0, visibleLength));
        }

        void AnimateKeyboardTray(float delta)
        {
            if (letterStacks == null || IsManipulating) return;
            float blend = 1 - Mathf.Exp(-18 * delta);
            for (int i = 0; i < 26; i++)
            {
                if (!letterStacks[i]) continue;
                var tile = letterSamples[i];
                // A manually composed loose tile stays on the mat when its parent stack moves.
                bool loose = tile && !tile.IsReturning && tile.homeSocket
                    && (tile.transform.position - tile.homeSocket.position).sqrMagnitude > 0.000001f;
                Vector3 loosePosition = tile ? tile.transform.position : Vector3.zero;
                letterStacks[i].localPosition = Vector3.Lerp(letterStacks[i].localPosition, stackTargets[i], blend);
                if (loose) tile.transform.position = loosePosition;
            }
        }

        void StampLetter(char letter)
        {
            int index = letter - 'A';
            if (!plaintextInk || index >= letterSamples.Length || !letterSamples[index]) return;
            // Keep rapid/repeated keystrokes independent of the three physical drag samples.
            if (letterStamps.Count >= 32) { DestroyStampVisual(letterStamps[0].visual); letterStamps.RemoveAt(0); }
            var source = letterSamples[index];
            var visual = Instantiate(source.gameObject);
            visual.name = "Keyboard letter " + letter;
            visual.GetComponent<CipherLetterTile>().enabled = false;
            var target = visual.GetComponent<CipherWorkbenchTarget>(); if (target) target.enabled = false;
            foreach (var collider in visual.GetComponentsInChildren<Collider>()) collider.enabled = false;
            visual.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            visual.transform.localScale = source.transform.lossyScale;
            letterStamps.Add(new LetterStamp { visual = visual, source = source, start = source.transform.position,
                destination = PlaintextStampPosition(), rotation = source.transform.rotation,
                paperRotation = mat ? mat.rotation : source.transform.rotation, scale = source.transform.lossyScale,
                characterIndex = Plaintext.Length - 1 });
            RefreshPlaintextInk();
        }

        Vector3 PlaintextStampPosition()
        {
            string[] lines = FormatPlaintext(Plaintext).Split('\n');
            string last = lines[lines.Length - 1];
            float unit = plaintextInk.characterSize / 10, advance = 0;
            var font = plaintextInk.font;
            if (font)
            {
                font.RequestCharactersInTexture(last, plaintextInk.fontSize, plaintextInk.fontStyle);
                for (int i = 0; i < last.Length; i++)
                    if (font.GetCharacterInfo(last[i], out var glyph, plaintextInk.fontSize, plaintextInk.fontStyle))
                        advance += glyph.advance * (i == last.Length - 1 ? 0.5f : 1);
            }
            float lineHeight = plaintextInk.fontSize, ascent = lineHeight, glyphCenter = lineHeight * 0.5f;
            if (font)
            {
                float ratio = plaintextInk.fontSize / (float)Mathf.Max(1, font.fontSize);
                lineHeight = font.lineHeight * ratio; ascent = font.ascent * ratio;
                if (last.Length > 0 && font.GetCharacterInfo(last[last.Length - 1], out var glyph, plaintextInk.fontSize, plaintextInk.fontStyle))
                    glyphCenter = (glyph.minY + glyph.maxY) * 0.5f;
            }
            Vector3 local = new Vector3(advance * unit,
                (-(lines.Length - 1) * lineHeight * plaintextInk.lineSpacing - ascent + glyphCenter) * unit, 0);
            return plaintextInk.transform.TransformPoint(local) + (mat ? mat.up : Vector3.up) * 0.008f;
        }

        void AnimateLetterStamps(float delta)
        {
            bool refreshInk = false;
            for (int i = letterStamps.Count - 1; i >= 0; i--)
            {
                var stamp = letterStamps[i];
                bool touchingPaper = stamp.elapsed < 0.15f && stamp.elapsed + delta >= 0.15f;
                stamp.elapsed += delta;
                if (touchingPaper)
                {
                    refreshInk = true;
                    if (sound && paperSound && Time.unscaledTime >= nextStampSound)
                    {
                        sound.PlayOneShot(paperSound, 0.22f); nextStampSound = Time.unscaledTime + 0.05f;
                    }
                }
                if (!stamp.visual || !stamp.source || stamp.elapsed >= 0.40f)
                { DestroyStampVisual(stamp.visual); letterStamps.RemoveAt(i); refreshInk = true; continue; }
                bool outbound = stamp.elapsed < 0.18f;
                float t = Mathf.Clamp01(outbound ? stamp.elapsed / 0.15f : (stamp.elapsed - 0.18f) / 0.22f);
                float eased = Mathf.SmoothStep(0, 1, t);
                Vector3 home = stamp.source.homeSocket ? stamp.source.homeSocket.position : stamp.source.transform.position;
                Quaternion homeRotation = stamp.source.homeSocket ? stamp.source.homeSocket.rotation : stamp.source.transform.rotation;
                stamp.visual.transform.SetPositionAndRotation(
                    Vector3.Lerp(outbound ? stamp.start : stamp.destination, outbound ? stamp.destination : home, eased)
                        + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.07f),
                    Quaternion.Slerp(outbound ? stamp.rotation : stamp.paperRotation, outbound ? stamp.paperRotation : homeRotation, eased));
                stamp.visual.transform.localScale = stamp.scale * Mathf.Lerp(outbound ? 1 : 0.48f, outbound ? 0.48f : 1, eased);
            }
            if (refreshInk) RefreshPlaintextInk();
        }

        static void DestroyStampVisual(GameObject visual)
        {
            if (!visual) return;
            visual.SetActive(false); Destroy(visual);
        }

        void ClearLetterStamps()
        {
            if (letterStamps.Count == 0) return;
            foreach (var stamp in letterStamps) DestroyStampVisual(stamp.visual);
            letterStamps.Clear();
            RefreshPlaintextInk();
        }

        void SuspendDecodeKeyboard()
        {
            ReleaseDecoderIme(); ClearLetterStamps();
        }
    }
}
