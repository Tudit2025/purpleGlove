using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PurpleGlove.Editor
{
    public static partial class DeskSceneBuilder
    {
        // Unify on the largest previous cipher size, then enlarge it by twenty percent.
        const float CipherSizeIncrease = 1.2f;
        const float CipherCharacterSize = 0.002201835f * CipherSizeIncrease;
        const float AffineStripWidth = 0.50f;
        const float RailStripWidth = 1.44f;
        const float RailStripHeight = 0.06f;

        static TextMesh CipherDocumentInk(Transform paper, string name, string value,
            float x, float z, float width, bool centered = true, bool singleLine = false)
        {
            var existing = paper.Find(name);
            if (existing) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            if (!singleLine) value = WrapCipher(value, width);
            PaperText(paper, name, value, V(x, 0.010f, z), width, documentFont);
            var text = paper.Find(name).GetComponent<TextMesh>();
            text.characterSize = CipherCharacterSize;
            text.lineSpacing = 1.1f;
            text.anchor = singleLine ? TextAnchor.MiddleCenter : centered ? TextAnchor.UpperCenter : TextAnchor.UpperLeft;
            text.alignment = centered ? TextAlignment.Center : TextAlignment.Left;
            return text;
        }

        static string WrapCipher(string value, float width)
        {
            documentFont.RequestCharactersInTexture(value, 96);
            float Measure(string line) => line.Sum(c => documentFont.GetCharacterInfo(c, out var info, 96)
                ? info.advance * CipherCharacterSize / 10 : 0);
            var lines = new List<string>();
            foreach (string original in value.Split('\n'))
            {
                string remaining = original;
                while (Measure(remaining) > width)
                {
                    int count = 1;
                    while (count < remaining.Length && Measure(remaining.Substring(0, count + 1)) <= width) count++;
                    int space = remaining.LastIndexOf(' ', count - 1, count);
                    if (space > 0) count = space;
                    lines.Add(remaining.Substring(0, count));
                    remaining = remaining.Substring(count).TrimStart();
                }
                lines.Add(remaining);
            }
            return string.Join("\n", lines);
        }

        static void BuildBookCipherInk(Transform paper)
        {
            var coordinates = BookCipherText.Cipher.Split(new[] { '/', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).ToArray();
            var lines = Enumerable.Range(0, (coordinates.Length + 2) / 3)
                .Select(i => string.Join(" / ", coordinates.Skip(i * 3).Take(3)));
            CipherDocumentInk(paper, "Coordinates", string.Join("\n", lines), -0.168f, 0.155f, 0.336f, false);
        }

        static void PositionRailCipherStrip(Transform paper)
        {
            // The long original lies on the bare front edge, clear of the tool and loose-letter area.
            var desk = GameObject.Find("WritingDesk").transform;
            paper.SetPositionAndRotation(desk.TransformPoint(V(0.67f, 0.815f, -0.74f)), desk.rotation);
            var view = Camera.main.GetComponent<DeskScenePreview>();
            view.decodeReferencePaper = paper.GetComponent<BoxCollider>();
            EditorUtility.SetDirty(view);
        }

        static void EnlargePigpenCipher(Transform cipher)
        {
            var center = cipher.GetComponent<MeshFilter>().sharedMesh.bounds.center;
            cipher.localScale = new Vector3(CipherSizeIncrease, 1, CipherSizeIncrease);
            cipher.localPosition = V(-center.x * CipherSizeIncrease, 0, center.z * (1 - CipherSizeIncrease));
        }

        [MenuItem("Purple Glove/Scene/Apply Unified Cipher Typography")]
        public static void ApplyUnifiedCipherTypography()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Update cipher typography outside Play Mode.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            LoadSavedSceneAssets();
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            var first = archive.acts[0].documents[1].transform;
            CipherDocumentInk(first, "Cipher", ActDocumentPages[0][1][1], 0, 0.085f, 0.29f);
            var book = archive.acts[1].documents[0].transform;
            book.GetComponent<BoxCollider>().size = V(0.38f, 0.012f, 0.36f);
            var sheet = book.Find("Act2_BookCipherNote");
            sheet.localScale = V(1, 1, 0.36f / sheet.GetComponent<MeshFilter>().sharedMesh.bounds.size.z);
            BuildBookCipherInk(book);
            var redaction = book.Find("Act2_NoteRedaction");
            if (redaction) redaction.localPosition = V(0, 0, -0.150f - redaction.GetComponent<MeshFilter>().sharedMesh.bounds.center.z);
            var scrap = archive.acts[1].documents[2].transform;
            scrap.GetComponent<BoxCollider>().size = V(0.38f, 0.012f, 0.22f);
            scrap.Find("Act2_TornPigpenScrap").localScale = V(0.38f / 0.34f, 1, 1);
            var pigpen = scrap.Find("Act2_PigpenCipher");
            // Scale the geometry and centre the existing glyph row without changing its symbols.
            EnlargePigpenCipher(pigpen);
            CipherDocumentInk(archive.acts[2].documents[1].transform, "Cipher", ActDocumentPages[2][1][1], 0, 0.020f, 0.36f);
            foreach (int index in new[] { 2, 4 })
            {
                var paper = archive.acts[3].documents[index].transform;
                float width = index == 2 ? AffineStripWidth : RailStripWidth;
                float height = index == 2 ? 0.075f : RailStripHeight;
                paper.GetComponent<BoxCollider>().size = V(width, 0.012f, height);
                var surface = paper.Find("Act4_CipherStrip");
                var bounds = surface.GetComponent<MeshFilter>().sharedMesh.bounds;
                surface.localScale = V(width / bounds.size.x, 1, height / bounds.size.z);
                BuildFourthActCipher(paper, index);
                if (index == 4) PositionRailCipherStrip(paper);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("UNIFIED_CIPHER_TYPOGRAPHY_INSTALLED");
        }
    }
}
