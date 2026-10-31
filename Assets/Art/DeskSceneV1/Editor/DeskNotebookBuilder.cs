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
            var font = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/SourceHanSerifCN-Regular.otf");
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
            book.leftNumber = NotebookText(contents, "Left page number", "01", V(-0.294f, 0.077f, -0.185f), font);
            book.rightNumber = NotebookText(contents, "Right page number", "02  >", V(0.229f, 0.077f, -0.185f), font);
            book.leftNumber.characterSize = book.rightNumber.characterSize = 0.0016f;
            book.pigpenChart = BuildNotebookPigpen(contents, font);
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
                left = "马泰奥的旧笔记\n\n五月十二日\n给米兰内廷编了\n新的文书体系。\n收件人会读，不等于\n读到的东西真的来自\n署名者。加密与签名，\n不能混为一谈。\n\n五月十九日\n巴尔托洛送来\n七瓣花版样书。",
                right = "五月十九日 · 续\n第十二页第四行\n重新排过，旧版\n不能再作查字依据。\n\n六月二日\n学生问，受胁迫时\n签下的名字还算不算\n自己的。我没有答好。\n以后该补这一课。\n\n后附：六种密码教案\n点击左右页翻阅" },
            new NotebookBook.Spread {
                left = "一 · 凯撒密码\n\n字母循环平移。\n加密加 k，解密减 k。\n越过 Z 后回到 A。\n\n例：k = 3\nABC  →  DEF\nXYZ  →  ABC\n\n三道裁口，是我\n练习纸的位移三记号。",
                right = "解密与核验\n\nDEF 向前移三位，\n还原为 ABC。\n空格与标点保留。\n\n常见错误：\n把解密方向写反。\n\n这种密码容易被试出。\n读通一段话，不能\n据此确认署名者。" },
            new NotebookBook.Spread {
                left = "二 · 书本密码\n\n先约定书名、版本、\n计数单位。\n\n页—行—词与\n页—行—字母，\n是两种不同规则。\n\n本案用页—行—字母，\n忽略空格与标点，\n从一开始计数。",
                right = "中性练习\n\n假设约定某页某行：\nA SMALL LAMP\n\n计数时写成：\nASMALLLAMP\n第 1 个字母是 A，\n第 3 个字母是 M。\n\n错版也可能查出字母，\n必须对照版次和原行，\n不能靠顺眼与否判断。" },
            new NotebookBook.Spread {
                left = "三 · 猪圈对照\n图形朝向与圆点固定",
                right = "对照 · 续\n\n\n\n\n\n\n\n图形替换字母。\n带点与不带点不同。\n先辨开口，再认圆点；\n不靠模糊墨迹猜字。" },
            new NotebookBook.Spread {
                left = "四 · 维吉尼亚\n\n字母编号 A=0 … Z=25。\n把关键词换成位移量，\n循环重复使用。\n\n加密：明文加关键词，\n结果对 26 取余。\n解密：密文减关键词，\n负数加 26。\n\n例：ABC 用 KEY\n加密，得到 KFA。",
                right = "逐字演算\n\nA=0    K=10\n0+10=10  →  K\n\nB=1    E=4\n1+4=5      →  F\n\nC=2    Y=24\n2+24=26  →  A\n\n短密文不保证能只靠\n频率分析推出关键词。" },
            new NotebookBook.Spread {
                left = "五 · 仿射密码\n\nA=0，B=1 … Z=25\ny = (ax+b) mod 26\n\na 必须与 26 互质，\n才能唯一逆向还原。\n\n私人练习记号：\n齿数为 a，点数为 b。\n\n例：a=5，b=8\nx = 21(y-8) mod 26",
                right = "演算与限制\n\n明文 A 的编号是 0。\n5×0+8 = 8，得到 I。\n\n反算：21×(8-8)=0，\n得到 A。\n\n若计算结果是负数，\n加 26 直到回到范围内。\n\n它仍保留单表替换的\n统计特征，并非绝对安全。" },
            new NotebookBook.Spread {
                left = "六 · 栅栏密码\n\n按约定行数来回填字，\n再逐行读出。\n\n两行例题：\nHELLOWORLD\n\n上行：H L O O L\n下行：E L W R D\n\n按行读出：\nHLOOLELWRD",
                right = "还原排列\n\n先确定每行的字数，\n把密文分回各行，\n再按上下往返顺序读。\n\n两行、十个字母时，\n每行各有五个字母。\n奇数长度时，上行\n比下行多一个。\n\n解密不会创造或删除\n字母，只恢复排列。" }
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
