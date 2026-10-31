using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PurpleGlove.Editor
{
    public static partial class DeskSceneBuilder
    {
        static Font HandwrittenChinese => RequiredDocumentFont("LXGWWenKai-Regular.ttf");
        static Font PrintedChinese => RequiredDocumentFont("ZhuqueFangsong-Regular.ttf");

        static Font RequiredDocumentFont(string file)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/" + file);
            if (!font) throw new InvalidOperationException("Missing bundled document font: " + file);
            return font;
        }

        [MenuItem("Purple Glove/Scene/Apply Chinese Document Fonts")]
        public static void ApplyChineseDocumentFonts()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Apply document fonts outside Play Mode.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (string name in new[] { "Book_Charcoal", "Book_SevenPetal" })
            {
                string path = Root + "/Prefabs/PF_" + name + ".prefab";
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    AddPrayerBookTitles(prefab.transform, name == "Book_SevenPetal");
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            LoadSavedSceneAssets();
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            if (!archive) throw new InvalidOperationException("Install the archive first.");
            for (int act = 0; act < archive.acts.Length; act++)
                for (int page = 0; page < archive.acts[act].documents.Length; page++)
                {
                    var paper = archive.acts[act].documents[page].transform;
                    if (act == 0 && page == 1)
                    {
                        Retype(paper.Find("Address").GetComponent<TextMesh>(), HandwrittenChinese, 0.043f, 0.018f);
                        Retype(paper.Find("Seven petal note").GetComponent<TextMesh>(), HandwrittenChinese, 0.238f, 0.045f);
                    }
                    else if (act > 1) ApplyActDocumentFonts(paper, ActDocumentPages[act][page][0]);
                }
            var notebook = UnityEngine.Object.FindFirstObjectByType<NotebookBook>();
            foreach (var text in new[] { notebook.leftPage, notebook.rightPage, notebook.leafFront, notebook.leafBack })
                Retype(text, HandwrittenChinese, 0.262f, 0.348f);
            notebook.RefreshPages();
            EditorUtility.SetDirty(notebook);
            var records = Camera.main.GetComponent<PlaintextArchiveController>();
            if (records)
                foreach (var entry in records.entries) ApplyPlaintextRecordFonts(entry.document.transform);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("CHINESE_DOCUMENT_FONTS_APPLIED");
        }

        static Font ChineseFontForDocument(string title)
        {
            switch (title)
            {
                case "裁纸边角":
                case "旧编码工作单":
                case "师父双栏密信":
                case "师父明文附记 · 上":
                case "师父明文附记 · 下": return HandwrittenChinese;
                case "印坊数字便条":
                case "印务台账摘要":
                case "第七号封档目录":
                case "原始签收页":
                case "换版指令与旧试印": return PrintedChinese;
                default: return RequiredDocumentFont("SourceHanSerifCN-Regular.otf");
            }
        }

        static void ApplyActDocumentFonts(Transform paper, string title)
        {
            var font = ChineseFontForDocument(title);
            Retype(paper.Find("Title").GetComponent<TextMesh>(), font, 0.552f, 0.043f);
            var body = paper.Find("Body");
            // Keep the coordinate sheet and the two cipher columns in their clear original type.
            if (body && title != "印坊数字便条")
                Retype(body.GetComponent<TextMesh>(), font, 0.552f, 0.205f);
        }

        static void ApplyPlaintextRecordFonts(Transform paper)
        {
            Retype(paper.Find("Record title").GetComponent<TextMesh>(), HandwrittenChinese, 0.55f, 0.035f);
            Retype(paper.Find("Translation").GetComponent<TextMesh>(), HandwrittenChinese, 0.55f, 0.092f);
            Retype(paper.Find("Record act").GetComponent<TextMesh>(), HandwrittenChinese, 0.17f, 0.015f);
        }

        static void Retype(TextMesh text, Font font, float width, float height)
        {
            if (!text) return;
            text.font = font;
            font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
            var renderer = text.GetComponent<Renderer>();
            renderer.sharedMaterial = font.material;
            var size = renderer.localBounds.size;
            text.characterSize *= Mathf.Min(1, width / Mathf.Max(0.001f, size.x), height / Mathf.Max(0.001f, size.y));
            EditorUtility.SetDirty(text); EditorUtility.SetDirty(renderer);
            PrefabUtility.RecordPrefabInstancePropertyModifications(text);
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }

        static void AddPrayerBookTitles(Transform book, bool flower)
        {
            void Label(string name, string value, float z, float width)
            {
                var existing = book.Find(name);
                if (!existing) PaperText(book, name, value, V(0, 0.095f, z), width, PrintedChinese);
                var text = book.Find(name).GetComponent<TextMesh>();
                Retype(text, PrintedChinese, width, 0.04f);
                text.color = new Color(0.68f, 0.48f, 0.24f);
            }
            Label("Prayer title", "祷 文 集", 0.13f, 0.20f);
            Label("Prayer edition", flower ? "七瓣花版" : "旧版", -0.137f, flower ? 0.115f : 0.058f);
        }
    }
}
