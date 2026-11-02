using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PurpleGlove.Editor
{
    public static partial class DeskSceneBuilder
    {
        const string ActDocumentGroup = "Act documents - authored desk layout";
        static Font actDocumentFont;

        [MenuItem("Purple Glove/Scene/Build Act Documents")]
        public static void AddActDocumentsToSavedScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            LoadSavedSceneAssets();
            var existing = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            if (existing) BuildActDocuments(existing);
            else BuildArchive(Camera.main);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("ACT_DOCUMENTS_SAVED");
        }

        // These are inspectable evidence props. Transcripts, judgements and replies remain unfilled.
        // Delivery-enabled scenes keep each set locked until its packet lands on the desk.
        static readonly string[][][] ActDocumentPages =
        {
            new[]
            {
                new[] { "公爵的核验委托", "致行会核验人卢卡：\n军情正文与附件今晚晚些时候送达。\n请于明晨六点前核验文字及其来源。\n\n若法方越境军情属实，晨会将表决\n由米兰军队接管边境粮站。请分别\n填写明文还原与来源确认，不得混签。\n\n卡拉法可代本公爵领取印务材料，\n无权替法方确认军令。他常戴左手\n紫手套，领取时仍须核对登记与签样。" },
                new[] { "师父封袋里的纸", "NHHS WKH RULJLQDO" },
                new[] { "行会会长的信", "卢卡：\n\n"
                    + "米兰要凭这份越境军情接管边境粮站；威尼斯不肯把粮道和驻防交到邻邦手里，却也不敢轻忽战报；法国则不愿由别人替自己宣布开战。三方都在等核验结果，也都盼着结果能替自己说话。\n\n"
                    + "行会替他们印文书、存契据，如今连核验的名义也成了他们争夺的工具。迟迟不签，他们说我们耽误军务；顺着签了，日后追究起来，又会说是行会验过的。我们靠三方的生意养活工匠，谁也得罪不起，却不能让工匠替他们担下所有过错。\n\n"
                    + "正因为他们互不信任，才肯把这件事交给共同登记的核验人。就这份文书而言，你是今夜唯一同时获三方承认，既有调查权、也有正式发言权的人。你可以调阅记录、向各方追问，并把核验意见列入晨会卷宗；连我也不能代你署名。这份权利不能替三方裁定争端，却能让尚未查清的问题留在纸上，不被一句催促抹去。\n\n"
                    + "我已吩咐库房，按你的申请抽调有关档案与旧记录；封存原件仍须备齐调阅手续。需要递信、查问经手人，或有别的难处，便托执事告诉我。行会能调动的人手有限，我会尽力安排。\n\n"
                    + "我知道你挂念马泰奥。愿你今夜查到的东西，能让他有机会亲口说明。没有查清的，不必替任何人签下保证。" },
                new[] { "行会档案转交申请表", "申请将行会保管的档案转交指定受领方。\n办理时须核对档案编号、清单与具名签收。" }
            },
            new[]
            {
                new[] { "印坊数字便条", BookCipherText.Cipher },
                new[] { "印务台账摘要", "第五十七号封档 · 印务台账摘要" },
                new[] { "猪圈密文碎纸", "两次都是他亲手收的。姓名在原始签收页，不在这张摘要上。" }
            },
            new[]
            {
                new[] { "米兰与行会的译录委托", "委托方：米兰内廷\n登记承办：印务行会\n案号：第17号 / 装订批号：726\n\n请马泰奥·里奇据所附来稿译录，\n使用既定密码封存，交行会核验。\n来稿标称法方军情，签发授权待核。\n此页为原委托，随正文送核。" },
                new[] { "师父译录的军情密文", "XFIRUSEMDZKVGGAELRIAF" },
                new[] { "军情核验表", "明文还原与来源确认分别填写、分别签名。" },
                new[] { "夏尔的补充凭条", "本人可以代表法方否认这条具体越境命令。\n若核验所提供具有来源记录的争议文件，\n法方将出具正式反证，并派见证人\n共同保护相关证人。" },
                new[] { "争议军情的法文原文", "Les troupes françaises franchiront\nla frontière à l'aube." }
            },
            new[]
            {
                new[] { "商业文件委托签收页", "委托方：米兰内廷\n原定项目：商队通行文件（商业文件）\n装订批号：726\n付款账户：米兰内廷第17号委托账户\n\n法方签收对象：商队通行文件原稿\n经手领取人：卡拉法" },
                new[] { "换版记录", "第一次换版：商队通行文件改为军情译录件。\n旧试印存根：法方尚未授权跨境行动。\n\n第二次换版：换成另附军情正文；\n保留原封面法使栏位及夏尔签名。\n备注：法使尚未核签，不另送法方；\n旧试印缴回。\n\n两次指令均由卡拉法带来并签收。\n军情译录承办：马泰奥·里奇。" },
                new[] { "师父双栏密信", "VAPZR MIZC\n\nUIWVZ QIPZWV", "IINDHFLERETFEBROOFMLIAEOTEROADAHLDNTRSMNMSGETEASODROREATLSAIYCMFRHPOFNWSEDOOEAEYAE" },
                new[] { "师父的明文书信", "卢卡：\n\n我原以为，先让人出去，再拿回底稿，\n就能把这件事拆开。我把自己估得太高，\n也把后果推给了你。\n\n六点会转押。转押登记走米兰内廷的\n文书渠道。把‘争议证人须留待共同核验’\n的通知送到那条渠道，并让威尼斯和法国\n具名加入保护，监押的书记官才有理由\n暂停执行。\n\n我说的也请你查。能查到的，\n应当有与我无关的记录。" }
            },
            new[]
            {
                new[] { "米兰代表的书信", "公爵授权的书记官\n\n请在核验表上明确是否愿为军令来源作保，\n并将换版原件交回米兰。\n里奇的签名是真迹，追责时他也不能置身事外。\n\n若行会登记共同争议审查，并附威尼斯、\n法国两方具名参与确认，监押书记官将\n暂停六点转押，保留里奇在圣马丁北门候核。" },
                new[] { "威尼斯代表的书信", "贝阿特丽切\n\n我希望取得换版原件，并申请保护里奇。\n若核验结论不担保军令来源，附有可复核的\n换版记录、案号与原件去向，我会具名申请\n暂停粮站表决，参加共同审查与证人保护。\n\n原件继续由行会封存也可以，须保留查验权。\n保护证人不等于免除他的责任。" },
                new[] { "法国代表的书信", "夏尔\n\n我希望取得涉及法方署名的换版原件。\n若核验结论不担保军令来源，附有具体军情、\n换版记录与来源登记，我将正式否认该命令，\n参加共同审查，并派见证人前往圣马丁北门。\n\n原件交给哪一方都应登记去向。\n若仍由行会封存，请保留法方查验权。" }
            }
        };

        static void BuildActDocuments(ActArchiveController controller)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Build act documents outside Play Mode.");
            actDocumentFont = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/SourceHanSerifCN-Regular.otf");
            if (!actDocumentFont) throw new InvalidOperationException("Missing Source Han Serif document font.");
            var references = GameObject.Find("03 Reference books and notebook");
            controller.sharedDocuments = references.GetComponentsInChildren<SceneObject>(true);
            if (controller.sharedDocuments.Length != 3) throw new InvalidOperationException("Expected the notebook and two shared prayer books.");
            // Only replace generated act props, never common references or room furniture.
            foreach (var act in controller.acts)
                foreach (var document in act.documents)
                    if (document && !controller.sharedDocuments.Contains(document)) UnityEngine.Object.DestroyImmediate(document.gameObject);
            foreach (string name in new[] { ActDocumentGroup, "Act documents - desktop poses", "Return current act" })
            {
                var old = controller.transform.Find(name);
                if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            // The full room builder still supplies the original prototype props before building the archive.
            foreach (string name in new[] { "Letter_Cipher", "Paper_Blank", "Envelope_Open", "Envelope_RedSeal", "IncomingEnvelope_Second" })
            {
                var old = GameObject.Find(name);
                if (old && old.GetComponent<SceneObject>()) UnityEngine.Object.DestroyImmediate(old);
            }

            var layout = new GameObject(ActDocumentGroup).transform;
            layout.SetParent(controller.transform, false);
            string[] roman = { "I", "II", "III", "IV", "V" };
            for (int act = 0; act < 5; act++)
            {
                var pages = ActDocumentPages[act];
                controller.acts[act].documents = new SceneObject[pages.Length];
                for (int index = 0; index < pages.Length; index++)
                {
                    if (act == 0)
                    {
                        var firstPaper = CreateFirstActDocument(layout, index);
                        controller.acts[act].documents[index] = firstPaper;
                        firstPaper.gameObject.SetActive(false);
                        continue;
                    }
                    if (act == 1)
                    {
                        controller.acts[act].documents[index] = CreateSecondActDocument(layout, index);
                        continue;
                    }
                    if (act == 2)
                    {
                        controller.acts[act].documents[index] = CreateThirdActDocument(layout, index);
                        continue;
                    }
                    if (act == 3)
                    {
                        controller.acts[act].documents[index] = CreateFourthActDocument(layout, index);
                        continue;
                    }
                    if (act == 4)
                    {
                        controller.acts[act].documents[index] = CreateFifthActDocument(layout, index);
                        continue;
                    }
                    var paper = Place("Paper_Blank", layout, Vector3.zero);
                    paper.name = "Act" + (act + 1) + "_" + (index + 1).ToString("00") + "_" + pages[index][0];
                    paper.transform.localScale = Vector3.one * 0.44f;
                    AddDocumentInk(paper.transform, "Title", pages[index][0], 0.141f, 0.043f);
                    AddDocumentInk(paper.transform, "Body", pages[index][1], 0.077f, 0.205f);
                    AddDocumentInk(paper.transform, "Folio", roman[act] + "  /  " + (index + 1).ToString("00"), -0.151f, 0.017f);
                    ApplyActDocumentFonts(paper.transform, pages[index][0]);
                    var item = paper.GetComponent<SceneObject>();
                    item.screenFill = 0.82f;
                    item.minimumViewDistance = 0.18f;
                    item.inspectionFill = new Color(0.85f, 0.78f, 0.64f);
                    controller.acts[act].documents[index] = item;
                    // Save the intended desk pose but hide all chapter papers in the authored scene.
                    // Awake captures these poses before moving the original instances into the shelf.
                    paper.SetActive(false);
                }
                controller.acts[act].bundle.gameObject.SetActive(!controller.requireDelivery);
            }
            ArrangeDocumentsOnMat(controller);
            ConfigureThirdActDocumentReferences(controller);
            ConfigureFourthActDocumentReferences(controller);
            ConfigureOperationalDocuments(controller);
            BuildMasterNotebook();
            if (Camera.main.GetComponent<SecondActFlow>()) ConfigureSecondAct(Camera.main);
            controller.previewAllActs = !controller.requireDelivery;
            EditorUtility.SetDirty(controller);
        }

        static void ArrangeDocumentsOnMat(ActArchiveController controller)
        {
            var mat = GameObject.Find("WritingMat").transform;
            var oldButton = controller.transform.Find("Return current act");
            if (oldButton) UnityEngine.Object.DestroyImmediate(oldButton.gameObject);
            // The 1.02 x 1.12 m mat is the document area. Keep all four paper corners
            // inside its stitching, including after the mat has been moved or rotated.
            foreach (var act in controller.acts)
            {
                if (act == controller.acts[0]) { ArrangeFirstActDocuments(controller); continue; }
                if (act == controller.acts[1]) { ArrangeSecondActDocuments(controller); continue; }
                if (act == controller.acts[2]) { ArrangeThirdActDocuments(controller); continue; }
                if (act == controller.acts[3]) { ArrangeFourthActDocuments(controller); continue; }
                if (act == controller.acts[4]) { ArrangeFifthActDocuments(controller); continue; }
                for (int i = 0; i < act.documents.Length; i++)
                {
                    var paper = act.documents[i].transform;
                    paper.position = mat.TransformPoint(V((i % 3 - 1) * 0.305f, 0.014f, 0.33f - i / 3 * 0.33f));
                    paper.rotation = mat.rotation;
                    paper.localScale = Vector3.one * 0.44f;
                }
            }
        }

        static string WrapCipher(string value, int width) => string.Join("\n", Enumerable.Range(0, (value.Length + width - 1) / width)
            .Select(i => value.Substring(i * width, Math.Min(width, value.Length - i * width))));

        static void AddDocumentInk(Transform paper, string name, string value, float z, float height, float x = -0.277f, float width = 0.552f)
        {
            PaperText(paper, name, value, V(x, 0.013f, z), width, actDocumentFont);
            var text = paper.Find(name).GetComponent<TextMesh>();
            text.anchor = TextAnchor.UpperLeft;
            text.alignment = TextAlignment.Left;
            text.lineSpacing = 1.12f;
            // Size by both axes; long evidence must never spill past the physical paper.
            var bounds = text.GetComponent<MeshRenderer>().localBounds;
            if (bounds.size.y > height) text.characterSize *= height / bounds.size.y;
            if (name == "Folio") text.color = new Color(0.16f, 0.095f, 0.045f);
        }

        static void AddPigpenCipher(Transform paper)
        {
            // Fixed geometric glyphs from script appendix D, independent of font substitutions.
            MeshObject("Document_Pigpen_LeftGlove", paper, mesh =>
            {
                const string letters = "LEFTGLOVE";
                for (int i = 0; i < letters.Length; i++)
                {
                    float x = -0.224f + i * 0.051f + (i >= 4 ? 0.022f : 0);
                    const float z = 0.012f, s = 0.016f, y = 0.014f;
                    void Line(float ax, float az, float bx, float bz) => mesh.Rod(V(x + ax, y, z + az), V(x + bx, y, z + bz), 0.0011f, M("Ink"), 4);
                    char c = letters[i];
                    if (c == 'T') { Line(-s, s, s, 0); Line(s, 0, -s, -s); }
                    else if (c == 'V') { Line(-s, -s, 0, s); Line(0, s, s, -s); }
                    else
                    {
                        if (c != 'G') Line(-s, -s, -s, s);
                        if (c != 'L') Line(-s, s, s, s);
                        if (c != 'G') Line(-s, -s, s, -s);
                        if (c == 'G' || c == 'E') Line(s, -s, s, s);
                    }
                    if (c == 'L' || c == 'O') mesh.Box(V(x, y, z), V(0.005f, 0.0015f, 0.005f), M("Ink"));
                }
            });
        }
    }
}
