using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PurpleGlove.Editor
{
    public static partial class DeskSceneBuilder
    {
        [MenuItem("Purple Glove/Scene/Arrange Desk and Add Notebook Pages")]
        public static void ArrangeDeskAndNotebook()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            LoadSavedSceneAssets();
            ArrangeDocumentsOnMat(UnityEngine.Object.FindFirstObjectByType<ActArchiveController>());
            BuildMasterNotebook();
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("DESK_NOTEBOOK_SAVED");
        }

        static void BuildMasterNotebook()
        {
            var notebook = GameObject.Find("Notebook_Open");
            if (!notebook) throw new InvalidOperationException("Missing shared notebook.");
            var font = HandwrittenChinese;
            var referenceFont = RequiredDocumentFont("SourceHanSerifCN-Regular.otf");
            foreach (string name in new[] { "Alphabet grid", "Handwritten notes", "Notebook contents" })
            {
                var child = notebook.transform.Find(name);
                if (child) UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            notebook.transform.position = V(-0.94f, 0.801f, -0.25f);
            Vector3 towardCamera = Camera.main.transform.position - notebook.transform.position;
            float yaw = Mathf.Atan2(-towardCamera.x, -towardCamera.z) * Mathf.Rad2Deg;
            notebook.transform.rotation = Quaternion.Euler(0, yaw, 0);
            notebook.transform.localScale = Vector3.one * 0.78f;
            var item = notebook.GetComponent<SceneObject>();
            item.screenFill = 0.82f;
            item.minimumViewDistance = 0.18f;
            item.inspectionFill = new Color(0.85f, 0.78f, 0.64f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            PrefabUtility.RecordPrefabInstancePropertyModifications(notebook.transform);
            var book = notebook.GetComponent<NotebookBook>();
            if (!book) book = notebook.AddComponent<NotebookBook>();
            var contents = new GameObject("Notebook contents").transform;
            contents.SetParent(notebook.transform, false);
            book.spreads = NotebookSpreads();
            book.leftPage = NotebookText(contents, "Left page", "", V(-0.294f, 0.076f, 0.194f), font);
            book.rightPage = NotebookText(contents, "Right page", "", V(0.029f, 0.076f, 0.194f), font);
            book.leftNumber = NotebookText(contents, "Left page number", "01", V(-0.294f, 0.077f, -0.185f), referenceFont);
            book.rightNumber = NotebookText(contents, "Right page number", "02  >", V(0.229f, 0.077f, -0.185f), referenceFont);
            book.leftNumber.characterSize = book.rightNumber.characterSize = 0.0016f;
            book.pigpenChart = BuildNotebookPigpen(contents, referenceFont);
            book.pigpenSpread = 3;
            var pivot = new GameObject("Turning leaf pivot").transform;
            pivot.SetParent(contents, false); pivot.localPosition = V(0, 0.082f, 0);
            MeshObject("Notebook_TurningLeaf", pivot, mesh =>
            {
                mesh.Quad(V(0, 0, -0.222f), V(0.307f, 0, -0.222f), V(0.307f, 0, 0.222f), V(0, 0, 0.222f), M("Paper_AgedCream"), Vector3.up);
                mesh.Quad(V(0, -0.001f, -0.222f), V(0.307f, -0.001f, -0.222f), V(0.307f, -0.001f, 0.222f), V(0, -0.001f, 0.222f), M("Paper_AgedCream"), Vector3.down);
            });
            book.turningLeaf = pivot;
            book.leafFront = NotebookText(pivot, "Leaf front", "", V(0.029f, 0.003f, 0.194f), font);
            book.leafBack = NotebookText(pivot, "Leaf back", "", V(0.294f, -0.004f, 0.194f), font);
            book.leafBack.transform.localRotation = Quaternion.Euler(90, 0, 180);
            book.pageAudio = notebook.GetComponent<AudioSource>();
            if (!book.pageAudio) book.pageAudio = notebook.AddComponent<AudioSource>();
            book.pageAudio.playOnAwake = false; book.pageAudio.loop = false;
            book.pageAudio.volume = 0.35f; book.pageAudio.spatialBlend = 0; book.pageAudio.dopplerLevel = 0;
            book.pageTurnSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/PH/PaperBook/page_turn_01.wav");
            book.RefreshPages();
            EditorUtility.SetDirty(book);
        }

        static TextMesh NotebookText(Transform parent, string name, string value, Vector3 position, Font font)
        {
            PaperText(parent, name, value, position, 0.262f, font);
            var text = parent.Find(name).GetComponent<TextMesh>();
            text.anchor = TextAnchor.UpperLeft; text.alignment = TextAlignment.Left;
            text.lineSpacing = 1.12f;
            return text;
        }

        static NotebookBook.Spread[] NotebookSpreads() => new[]
        {
            new NotebookBook.Spread {
                left = "马泰奥的旧笔记\n\n五月十二日\n为米兰内廷拟定了\n新的文书加密体例。\n收件人能读通密文，\n不代表文书真出自\n署名者之手。加密与\n签名，不可混为一谈。\n\n五月十九日\n巴尔托洛送来\n七瓣花版样书。",
                right = "五月十九日 · 续\n第十二页第四行\n重排过字版，旧版\n已不可作查字依据。\n\n六月二日\n卢卡问我，受胁迫时\n签下的名字，还算不算\n自己的。我未答好。\n日后当补上这一课。\n\n后附：六种密码教案\n点击左右页翻阅" },
            new NotebookBook.Spread {
                left = "一 · 凯撒密码\n\n字母按序循环平移。\n加密加 k，解密减 k。\n越过 Z 后折返至 A。\n\n例：k = 3\nABC  →  DEF\nXYZ  →  ABC\n\n纸侧三道裁口，是我\n习字时的移位三记号。",
                right = "解密与核验\n\nDEF 向前逆移三位，\n即还原为 ABC。\n空格与标点照旧保留。\n\n常见疏漏：\n将解密方向算反。\n\n此法极易被试出。\n能读通文义，绝不可\n据此认定署名真伪。" },
            new NotebookBook.Spread {
                left = "二 · 书本密码\n\n须先约定书名、版次\n及计数规则。\n\n页—行—词与\n页—行—字母，\n系两种不同算法。\n\n本案取页—行—字母，\n略去空格与标点，\n自一始按序计数。",
                right = "中性练习\n\n设约定某页某行为：\nA SMALL LAMP\n\n计数时连写为：\nASMALLLAMP\n第 1 个字母为 A，\n第 3 个字母为 M。\n\n错版亦可能凑出字母，\n务须核对版次与原行，\n切忌仅凭文义通顺断案。" },
            new NotebookBook.Spread {
                left = "三 · 猪圈对照\n图形开口与圆点固定",
                right = "对照 · 续\n\n\n\n\n\n\n\n以几何图形代指字母。\n有点与无点分属两列。\n先辨边框开口，再认圆点；\n切忌凭模糊墨迹臆测。" },
            new NotebookBook.Spread {
                left = "四 · 维吉尼亚\n\n字母编号 A=0 … Z=25。\n将关键词化作位移量，\n首尾循环使用。\n\n加密：明文加关键词，\n所得对 26 取余。\n解密：密文减关键词，\n若为负数则加 26。\n\n例：ABC 以 KEY\n加密，即得 KFA。",
                right = "逐字演算\n\nA=0    K=10\n0+10=10  →  K\n\nB=1    E=4\n1+4=5      →  F\n\nC=2    Y=24\n2+24=26  →  A\n\n篇幅过短之密文，难仅凭\n频率分析反推关键词。" },
            new NotebookBook.Spread {
                left = "五 · 仿射密码\n\nA=0，B=1 … Z=25\ny = (ax+b) mod 26\n\na 须与 26 互质，\n方可唯一逆向还原。\n\n私用习字记号：\n齿数记 a，点数记 b。\n\n例：a=5，b=8\nx = 21(y-8) mod 26",
                right = "演算与限制\n\n明文 A 编号为 0。\n5×0+8 = 8，得密文 I。\n\n反算：21×(8-8)=0，\n还原得 A。\n\n若算得负数，则累加上\n26 直至归入正数范围。\n\n此法仍留有单表替换之\n统计特征，非万全之策。" },
            new NotebookBook.Spread {
                left = "六 · 栅栏密码\n\n依约定行数上下填字，\n继而逐行横向读出。\n\n两行例题：\nHELLOWORLD\n\n上行：H L O O L\n下行：E L W R D\n\n按行读出：\nHLOOLELWRD",
                right = "还原排列\n\n先核定各行应有字数，\n将密文拆回上下各行，\n再依上下往返之序拼读。\n\n两行、十字母之例，\n上下两行各容五字。\n若总字数为奇，则上行\n较下行多出一字。\n\n解密绝不增删字母，\n唯恢复其本有之次序。" }
        };

        static GameObject BuildNotebookPigpen(Transform parent, Font font)
        {
            var chart = new GameObject("Pigpen alphabet");
            chart.transform.SetParent(parent, false);
            MeshObject("Notebook_PigpenAlphabet", chart.transform, mesh =>
            {
                for (int i = 0; i < 26; i++)
                {
                    bool left = i < 18;
                    int index = left ? i : i - 18;
                    int rows = left ? 9 : 4;
                    int column = index / rows, row = index % rows;
                    float x = (left ? -0.255f : 0.064f) + column * 0.136f;
                    float z = 0.104f - row * 0.029f;
                    var label = NotebookText(chart.transform, "Letter " + (char)('A' + i), ((char)('A' + i)).ToString(), V(x - 0.033f, 0.077f, z + 0.009f), font);
                    label.characterSize = 0.0018f;
                    DrawNotebookGlyph(mesh, (char)('A' + i), V(x + 0.023f, 0.078f, z), 0.010f);
                }
            });
            return chart;
        }

        static void DrawNotebookGlyph(LowPolyMesh mesh, char letter, Vector3 origin, float s)
        {
            void Line(float ax, float az, float bx, float bz) => mesh.Rod(origin + V(ax, 0, az), origin + V(bx, 0, bz), 0.00065f, M("Ink"), 4);
            int i = letter - 'A';
            bool dot = (i >= 9 && i < 18) || i >= 22;
            if (i < 18)
            {
                int cell = i % 9;
                // A..I are the nine fixed grid fragments in script appendix D.
                if (cell % 3 != 0) Line(-s, -s, -s, s);
                if (cell % 3 != 2) Line(s, -s, s, s);
                if (cell / 3 != 0) Line(-s, s, s, s);
                if (cell / 3 != 2) Line(-s, -s, s, -s);
            }
            else
            {
                int wedge = (i - 18) % 4;
                if (wedge == 0) { Line(-s, s, 0, -s); Line(0, -s, s, s); }
                if (wedge == 1) { Line(-s, s, s, 0); Line(s, 0, -s, -s); }
                if (wedge == 2) { Line(s, s, -s, 0); Line(-s, 0, s, -s); }
                if (wedge == 3) { Line(-s, -s, 0, s); Line(0, s, s, -s); }
            }
            if (dot) mesh.Box(origin, V(0.0035f, 0.001f, 0.0035f), M("Ink"));
        }
    }
}
