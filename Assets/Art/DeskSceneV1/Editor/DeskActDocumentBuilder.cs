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
                new[] { "公爵的核验委托", "致行会核验人卢卡：\n军情正文及附件将于今夜稍晚送达。\n务于六月十六日六点前核准文义与来源。\n\n若法方越境军情属实，晨会将表决\n由米兰驻军接管边境粮站。明文还原\n与来源确认须分栏签署，不得混同。\n\n卡拉法可代本公爵领受印务材料，\n无权替法方核准军令。彼左手常戴\n紫手套，领件时仍须核验登记与签样。" },
                new[] { "师父封袋里的纸", "NHHS WKH RULJLQDO\nVHYHQ SHWDOV QHZ HGLWLRQ" },
                new[] { "行会会长的信", "卢卡：\n\n"
                    + "米兰欲凭这份越境军情接管边境粮站；威尼斯不肯将粮道与防务拱手让人，却也不敢轻忽战报；法国则绝不容旁人替自己宣告开战。三方都在等今夜的核验结果，也都盼着结果能替自己张目。\n\n"
                    + "行会替各方印制文书、保管契据，如今连核验的名义也成了他们博弈的筹码。迟迟不签，必被指责贻误军机；顺着签了，日后追究起来，又要归咎于行会失察。我们仰赖三方的生意糊口，谁也得罪不起，却断不能让手下工匠替他们背负罪责。\n\n"
                    + "正因三方彼此猜忌，才肯将此事交由共同登记的核验人裁夺。就这份文书而言，你是今夜唯一获三方公认、兼具调查之权与正式发言权的人。你可调阅旧档、质询各方，并将核验结论载入晨会卷宗；纵是我也不能代你落笔。这权力虽不足以平息争端，却能令未明之疑留在纸面，不致被几句催促抹去。\n\n"
                    + "为防今夜生变，我已授权你在必要时决定，将本案存档文件交予米兰、威尼斯或法国中的一方。随信附上档案转交申请表，填明档案编号、转交清单及受领方，签名后交执事办理；若无稳妥人选，仍可继续封存。此为应急之法，务必留意各方索取原件的动向，慎择所托，交接须留凭据。\n\n"
                    + "我深知你记挂马泰奥。但愿你今夜查实的证据，能为他挣得亲口陈情的机会。凡未查清之事，切莫替任何人签字作保。" },
                new[] { "行会档案转交申请表", "申请将行会封存之档案移交指定受领方。\n办理时须核验档案编号、清单及具名签收。" }
            },
            new[]
            {
                new[] { "印坊数字便条", BookCipherText.Cipher },
                new[] { "印务台账摘要", "第五十七号封档 · 印务台账摘要" },
                new[] { "猪圈密文碎纸", "两次都是他亲手收的。姓名在原始签收页，不在这张摘要上。" }
            },
            new[]
            {
                new[] { "米兰与行会的译录委托", "委托方：米兰内廷\n登记承办：印务行会\n案号：第17号 / 装订批号：726\n\n兹委托马泰奥·里奇依附稿译录，\n以既定密码封存，送交行会核验。\n来稿标称法方军情，签发授权待考。\n此页即原委托书，随正文送核。" },
                new[] { "师父译录的军情密文", "XFIRUSEMDZKVGGASFXCRWSQKZHMIF" },
                new[] { "军情核验表", "明文还原与来源确认分别填写、分别签名。" },
                new[] { "夏尔的补充凭条", "本人可代表法方否认此项越境军令。\n若核验所出示具来源记录之争议文书，\n法方将具函正式反证，并派见证人\n会同各方保护相关证人。" },
                new[] { "争议军情的法文原文", "Les troupes françaises franchiront\nla frontière le 18 juin." },
                new[] { "师父的工作纸", "SOIE" }
            },
            new[]
            {
                new[] { "商业文件委托签收页", "委托方：米兰内廷\n原定项目：商队通行文件（商业文件）\n装订批号：726\n付款账户：米兰内廷第17号委托账户\n\n法方签收对象：商队通行文件原稿\n经手领取人：卡拉法" },
                new[] { "换版记录", "第一次换版：商队通行文件改排军情译录件。\n旧试印存根：法方尚未授权跨境行动。\n\n第二次换版：替换为另附之军情正文；\n沿用原封面法使栏位及夏尔签名。\n备注：法使尚未核签，毋须另送法方；\n旧试印悉数缴回。\n\n两次指令均由卡拉法送交并签收。\n军情译录承办：马泰奥·里奇。" },
                new[] { "师父的仿射密码纸条", "VAPZR  MIZC  UIWVZ  QIPZWV" },
                new[] { "师父的明文书信", "卢卡：\n\n是我高估了自己，\n反倒将棘手的后果推给了你。\n\n明早六点我将被转押。转押公文走的是\n米兰内廷渠道。须将‘争议证人留候共同\n核验’之文书送入该渠道，并由威尼斯与\n法国具名联保，监押的书记官才有借口\n暂缓执行。\n\n我所言之事，你也一并查验。若确有其事，\n必能找到独立于我的旁证记录。\n\nA = 5    B = 8" },
                new[] { "师父的栅栏密码纸条", "IINDHFLERETFEBROOFMLIAEOTEROADAHLDNTRSMNMSGETEASODROREATLSAIYCMFRHPOFNWSEDOOEAEYAE" }
            },
            new[]
            {
                new[] { "米兰代表的书信", "公爵授权的书记官\n\n请于核验表上明示是否愿为军令来源作保，\n并将换版原件交还米兰。\n里奇之签名确系真迹，日后追责亦难辞其咎。\n\n若行会立案启动共同争议审查，且有威尼斯、\n法国两方具名联署，监押书记官便会暂缓\n六点之转押，留里奇于圣马丁北门候审。" },
                new[] { "威尼斯代表的书信", "贝阿特丽切\n\n我方希望受领换版原件，并申请保护里奇。\n若核验结论不替军令来源作保，且附有可查验\n之换版记录、案号及原件去向，我将具名提请\n暂缓粮站表决，参与共同审查与证人保护。\n\n原件留存行会封存亦可，唯须保留我方查验权。\n保护证人周全，并非免除其经手之责。" },
                new[] { "法国代表的书信", "夏尔\n\n我方希望受领涉及法方署名之换版原件。\n若核验结论不替军令来源作保，且附有具体军情、\n换版记录及来源登记，我将正式否认该项军令，\n参与共同审查，并遣见证人赶赴圣马丁北门。\n\n原件无论移交何方，皆须登记去向。\n若仍由行会封存，请保留法方查验之权。" }
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
