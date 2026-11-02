using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace PurpleGlove
{
    /// <summary>The shared reference book opens at the cipher spread and returns to its original desk pose.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SceneObject))]
    public sealed class PrayerBookTool : SceneObjectAction
    {
        public CipherWorkbench workbench;
        public ActArchiveController archive;
        public SceneObject note;
        public GameObject spread;
        public Renderer[] closedSurfaces;
        public bool sevenPetal;
        public Font letterIndexFont;
        public override bool IsBusy => workbench && workbench.BookTool == this;
        SceneObject book;
        Vector3 bookPosition, bookScale, notePosition, noteScale;
        Quaternion bookRotation, noteRotation;
        bool[] surfaceStates;
        bool bookEnabled, noteEnabled, bookColliderEnabled, noteColliderEnabled;

        public override void Activate() => TryOpen();
        public bool TryOpen()
        {
            if (!isActiveAndEnabled || !archive || archive.DeskAct != 2 || !archive.IsDocumentAvailable(2, note)
                || !workbench || !spread || IsBusy) return false;
            return workbench.OpenBook(this);
        }

        internal void PlaceOnWorkbench()
        {
            book = GetComponent<SceneObject>(); book.RestoreImmediately(); note.RestoreImmediately();
            bookPosition = transform.localPosition; bookRotation = transform.localRotation; bookScale = transform.localScale;
            notePosition = note.transform.localPosition; noteRotation = note.transform.localRotation; noteScale = note.transform.localScale;
            bookEnabled = book.enabled; noteEnabled = note.enabled; book.enabled = note.enabled = false;
            bookColliderEnabled = book.GetComponent<Collider>().enabled; noteColliderEnabled = note.GetComponent<Collider>().enabled;
            book.GetComponent<Collider>().enabled = note.GetComponent<Collider>().enabled = false;
            surfaceStates = new bool[closedSurfaces.Length];
            for (int i = 0; i < closedSurfaces.Length; i++)
            { surfaceStates[i] = closedSurfaces[i].enabled; closedSurfaces[i].enabled = false; }
            transform.SetPositionAndRotation(workbench.mat.TransformPoint(new Vector3(0, 0.02f, -0.09f)), workbench.mat.rotation);
            transform.localScale = Vector3.one;
            spread.SetActive(true);
            EnsureLetterIndices();
            note.transform.SetPositionAndRotation(workbench.mat.TransformPoint(new Vector3(-0.57f, 0.03f, -0.04f)), workbench.mat.rotation);
            note.transform.localScale = Vector3.one;
        }

        void EnsureLetterIndices()
        {
            if (!letterIndexFont) return;
            letterIndexFont.RequestCharactersInTexture("0123456789", 96);
            for (int page = 12; page <= 13; page++)
            for (int line = 1; line <= 4; line++)
            {
                var row = spread.transform.Find("Page " + page + " line " + line);
                if (!row || row.Find("Letter indices")) continue;
                var text = row.GetComponent<TextMesh>();
                text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
                var centers = new List<float>();
                float advance = 0, unit = text.characterSize * 0.1f;
                foreach (char character in text.text)
                {
                    if (!text.font.GetCharacterInfo(character, out var glyph, text.fontSize, text.fontStyle)) continue;
                    // Book coordinates count letters only, restarting on every printed line.
                    if (character >= 'A' && character <= 'Z')
                        centers.Add((advance + (glyph.minX + glyph.maxX) * 0.5f) * unit);
                    advance += glyph.advance;
                }
                if (centers.Count == 0) continue;
                var root = new GameObject("Letter indices").transform;
                root.SetParent(row, false);
                // Configure ink while inactive so its depth-tested material is ready on first display.
                root.gameObject.SetActive(false);
                // TextMesh bounds include leading; use visible glyphs to tuck notes just above the ink.
                var generator = new TextGenerator();
                generator.Populate(text.text, new TextGenerationSettings {
                    font = text.font, fontSize = text.fontSize, fontStyle = text.fontStyle, scaleFactor = 1,
                    color = Color.white, textAnchor = text.anchor, lineSpacing = text.lineSpacing,
                    horizontalOverflow = HorizontalWrapMode.Overflow, verticalOverflow = VerticalWrapMode.Overflow
                });
                float top = float.NegativeInfinity, bottom = float.PositiveInfinity;
                var vertices = generator.verts;
                for (int vertex = 0; vertex + 3 < vertices.Count; vertex += 4)
                {
                    float high = vertices[vertex].position.y, low = vertices[vertex + 2].position.y;
                    if (high <= low) continue; // Spaces and the trailing empty quad have no ink.
                    top = Mathf.Max(top, high * unit); bottom = Mathf.Min(bottom, low * unit);
                }
                ((System.IDisposable)generator).Dispose();
                float size = text.characterSize * 0.68f;
                var indices = new List<TextMesh>();
                for (int i = 0; i < centers.Count; i++)
                {
                    var ink = new GameObject("Letter " + (i + 1)).AddComponent<TextMesh>();
                    ink.transform.SetParent(root, false);
                    ink.font = letterIndexFont; ink.fontSize = 96; ink.characterSize = size;
                    ink.anchor = TextAnchor.LowerCenter; ink.alignment = TextAlignment.Center;
                    ink.text = (i + 1).ToString(CultureInfo.InvariantCulture);
                    ink.color = new Color(0.24f, 0.17f, 0.105f);
                    var renderer = ink.GetComponent<Renderer>();
                    renderer.sharedMaterial = letterIndexFont.material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    ink.gameObject.AddComponent<WorldDocumentText>().textShader = Shader.Find("PurpleGlove/World Document Text");
                    indices.Add(ink);
                }
                // Condense width, not height, so two-digit notes remain readable on the longest lines.
                for (int i = 0; i < indices.Count; i++)
                {
                    var ink = indices[i];
                    var labelBounds = ink.GetComponent<Renderer>().localBounds;
                    float leftGap = i > 0 ? centers[i] - centers[i - 1] : float.MaxValue;
                    float rightGap = i + 1 < centers.Count ? centers[i + 1] - centers[i] : float.MaxValue;
                    float fit = Mathf.Min(1, Mathf.Min(leftGap, rightGap) * 0.88f / Mathf.Max(0.0001f, labelBounds.size.x));
                    ink.transform.localScale = new Vector3(fit, 1, 1);
                    ink.transform.localPosition = new Vector3(centers[i] - labelBounds.center.x * fit,
                        top + (top - bottom) * 0.18f - labelBounds.min.y, -0.0001f);
                }
                root.gameObject.SetActive(true);
            }
        }

        internal void RestoreBook()
        {
            if (!book) return;
            spread.SetActive(false);
            for (int i = 0; i < closedSurfaces.Length; i++) if (closedSurfaces[i]) closedSurfaces[i].enabled = surfaceStates[i];
            transform.localPosition = bookPosition; transform.localRotation = bookRotation; transform.localScale = bookScale;
            book.enabled = bookEnabled; book.GetComponent<Collider>().enabled = bookColliderEnabled;
            if (note)
            {
                note.transform.localPosition = notePosition; note.transform.localRotation = noteRotation; note.transform.localScale = noteScale;
                note.enabled = noteEnabled; note.GetComponent<Collider>().enabled = noteColliderEnabled;
            }
            book = null;
        }
        public override void Cancel() { if (IsBusy) workbench.CloseBook(); }
        void OnDisable() => Cancel();
    }
}
