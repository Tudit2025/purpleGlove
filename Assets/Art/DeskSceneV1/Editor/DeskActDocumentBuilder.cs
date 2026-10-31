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
                new[] { "公爵的核验委托", "行会收件 · 米兰内廷\n\n请于明晨六点前核验待送军情。\n分别填写明文还原与来源确认，\n不得混签。\n\n若法方越境军情属实，晨会将\n表决由米兰军队接管边境粮站。" },
                new[] { "师父封袋里的纸", "马泰奥 · 寄存封袋\n\nNHHS WKH RULJLQDO\n\n纸角：三道旧式裁口\n\n纸背浅色蜡痕的压字：\n七瓣花，新版。\n查账时不要只认签名。" },
                new[] { "代表登记册补页", "行会 · 代表授权范围\n\n卡拉法可以代公爵领取印务材料，\n无权替法方确认军令。\n\n领取人特征：常戴左手紫手套。\n\n印务领取与军令核签，分别登记。" },
                new[] { "封袋寄存登记", "行会 · 寄存件\n\n寄存人：马泰奥·里奇\n交付对象：卢卡\n\n封袋登记在里奇被扣押之前。\n随袋纸张须保留，以供后续查验。" },
                new[] { "入场任务便笺", "卢卡 · 本次文书核验人\n\n任务：核验文字与文件来源。\n期限：明晨六点。\n\n师父已被扣押，去向有待核实。\n文书由侧面投递口登记送入。\n正门封纸由三方共同见证。" },
                new[] { "空白核验表", "明文还原：________________\n\n密码规则：________________\n\n来源确认：________________\n\n依据原件：________________\n\n核验人：________  时间：____" }
            },
            new[]
            {
                new[] { "威尼斯封件二号", "贝阿特丽切 · 附印坊旧便条\n\n我查这件事已经六天。\n明早是委员会给我的最后期限。\n\n印刷匠留了一串数字。\n我没有他用的版本。\n你桌上的书，也许能解释它。" },
                new[] { "印坊数字便条", "12-4-11 / 12-4-7 / 12-4-4 / 27-2-12\n27-2-2 / 27-2-24 / 27-2-29 / 41-3-10\n12-4-25 / 12-4-9 / 27-2-23 / 27-2-24\n27-2-27 / 27-2-26 / 41-3-2 / 12-4-14\n\n页—行—字母，从一开始计数。\n去掉该行空格与标点。\n\n联系人：━━━━━━━━" },
                new[] { "印务台账摘要", "装订批号：7—031\n\n原定：商队通行文件\n后改：军情译录件\n\n两次换版由同一名经手人带来。\n经米兰内廷印务账户结算。\n原始签收页已交行会纠纷库。" },
                new[] { "裁纸边角", "巴尔托洛 · 随台账交存\n\n\n\n\n两次都是他亲手收的。\n姓名在原始签收页，\n不在这张摘要上。" },
                new[] { "第七号封档目录", "行会纠纷库 · 印坊纠纷\n\n内附：印务台账摘要、裁纸边角。\n\n原始签收页另存，\n涉及委托方私账。\n调阅须提供对应的正式委托案号。" },
                new[] { "空白检索单", "检索对象：________________\n\n封档编号：________________\n\n调阅用途：________________\n\n正式案号：________________\n\n申请人：__________________" },
                new[] { "取字工作纸", "使用版本：________________\n\n页 / 行 / 字母：____________\n\n取字记录：________________\n\n明文：____________________\n\n待核事项：________________" }
            },
            new[]
            {
                new[] { "正式核验件封面", "米兰内廷 · 第17号委托\n装订批号：7—031\n\n军情转录委托\n旧委托封面署名：马泰奥·里奇\n\n封面、正文与版次已登记。\n署名真实性与正文来源分别核验。" },
                new[] { "军情密文字条", "第17号委托 · 批号7—031\n\nXFIRUSEMDZKVGGAELRIAF\n\n标称来源：法方\n\n随件附旧编码工作单与货款样单。\n明文还原：________________" },
                new[] { "旧编码工作单", "马泰奥 · 委托用语\n\n四字母材质词，按法语填写。\n随附样单：丝绸货款。\n\nSOIE      丝绸\nLAINE    羊毛\nLIN        亚麻" },
                new[] { "证人安排表", "暂留地点：圣马丁修道院\n转押出口：北门 · 时刻：六点\n\n转为共同争议审查时，\n证人须原地留候共同核验。\n行会编号通知经米兰渠道送达，\n并附威尼斯、法国两方参与确认。" },
                new[] { "法方预备说明", "夏尔 · 具名说明\n\n本人可代表法方否认这条\n具体越境命令。\n\n若提供有来源记录的争议文件，\n法方将出具正式反证，\n并派见证人共同保护相关证人。" },
                new[] { "原件调阅登记", "检索：米兰内廷第17号委托\n批号：7—031\n\n与第七号封档交存记录相符。\n原始签收页须两位保管见证人\n共同启封，黎明前送入。\n原件随附出库记录。" },
                new[] { "核验工作表", "明文还原：________________\n\n来源确认（待填写）：\n已证实 / 待核 / 存在冲突\n\n对应案号与批号：____________\n\n核验依据：________________" },
                new[] { "来源比对工作纸", "封面：____________________\n\n正文：____________________\n\n换版记录：________________\n\n签发授权：________________\n\n尚待取得的原件：____________" }
            },
            new[]
            {
                new[] { "原始签收页", "第17号委托 · 批号7—031\n\n旧试印：法方尚未授权跨境行动。\n二次指令：替换正文，保留法使栏。\n法使尚未核签，不另送法方；\n旧试印缴回。\n两次换版经手人：卡拉法\n军情转录委托署名：马泰奥·里奇\n款项：米兰内廷第17号委托账户" },
                new[] { "换版指令与旧试印", "行会纠纷库 · 交存存根\n\n旧试印：\n法方尚未授权跨境行动。\n\n第二次换版指令：\n换成另附的军情正文，\n保留原封面上的法使栏位。" },
                new[] { "原件出库记录", "行会纠纷库 · 第七号封档\n对应：第17号委托 / 批号7—031\n\n出库：原始签收页、换版指令、\n旧试印存根。\n\n两位保管见证人共同启封。\n本页随原件送入核验所。" },
                new[] { "师父双栏密信", "VAPZRMIZCUIWVZQIPZWV", "IINDHFLERETFEBROOFMLIAEOTEROADAHLDNTRSMNMSGETEASODROREATLSAIYCMFRHPOFNWSEDOOEAEYAE" },
                new[] { "师父明文附记 · 上", "卢卡：\n\n我原以为，先让人出去，\n再拿回底稿，就能把这件事拆开。\n\n我把自己估得太高，\n也把后果推给了你。\n\n六点会转押。" },
                new[] { "师父明文附记 · 下", "转押登记走米兰内廷文书渠道。\n把争议证人留待共同核验的通知\n送到那条渠道，并让威尼斯、法国\n具名加入保护，监押书记官\n才有理由暂停执行。\n\n我说的也请你查。能查到的，\n应当有与我无关的记录。" },
                new[] { "修补封套来路说明", "送件人交执事登记\n\n修道院装订工把信缝进待修封套，\n经清晨送货车带到行会。\n\n装订工只答应送出这一次，\n不能保证继续通信。" },
                new[] { "证据核验工作纸", "军令授权依据：____________\n\n换版经手记录：____________\n\n师父的签名与陈述：________\n\n独立佐证：________________\n\n未证实推断：______________" },
                new[] { "共同核验母稿", "明文与密码规则：__________\n\n事实 / 原件编号 / 页号：\n________________________\n\n未证实推断：______________\n\n拟定保管方式：____________" }
            },
            new[]
            {
                new[] { "米兰正式来件", "公爵授权书记官\n\n请确认明文与委托内容相符，\n并归还工作原件。\n\n里奇在封面上的签名是真迹。\n若核验转为追责，他也无法\n置身事外。请慎重。" },
                new[] { "威尼斯正式来件", "贝阿特丽切\n\n将原始签收页交我，\n我可以申请立即保护里奇。\n若坚持行会共同封存，\n请提供可复核副本与封存编号。\n我会签署共同审查申请，\n但不能保证任何人免于问责。" },
                new[] { "法国正式来件", "夏尔\n\n收到具备来源记录的证据副本后，\n我将具名否认该越境命令，\n参加共同审查，并派见证人\n前往圣马丁北门。\n\n原件请保留，法方须有查验权。" },
                new[] { "给米兰的回件", "收件渠道：米兰内廷\n\n核验结论：________________\n争议审查卷宗号：__________\n威尼斯参与确认：__________\n法国参与确认：____________\n请求与证人安排：__________\n\n核验人：________  待签发" },
                new[] { "给威尼斯的回件", "收件人：贝阿特丽切\n\n核验事实：________________\n对应原件编号：____________\n案号 / 批号：______________\n保管位置 / 封存号：________\n共同审查与保护请求：______\n\n核验人：________  待签发" },
                new[] { "给法国的回件", "收件人：夏尔\n\n具体军情：________________\n换版记录与来源号：________\n核签情况：________________\n具名否认与共同审查请求：__\n证人保护请求：____________\n\n核验人：________  待签发" },
                new[] { "原件保管清单", "原件编号与页数：__________\n\n保管方式（待填写）：\n共同封存 / 交某方 / 暂留 / 销毁\n\n领受方或封存编号：________\n\n见证人：________  时间：____" },
                new[] { "终卷附件清单", "共同卷宗编号：____________\n\n核验母稿：________________\n来源记录副本：____________\n\n师父自述：附入 / 暂不提交\n\n参与确认与回执：__________\n核验人：________  待签发" }
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
                    var paper = Place("Paper_Blank", layout, Vector3.zero);
                    paper.name = "Act" + (act + 1) + "_" + (index + 1).ToString("00") + "_" + pages[index][0];
                    paper.transform.localScale = Vector3.one * 0.44f;
                    AddDocumentInk(paper.transform, "Title", pages[index][0], 0.141f, 0.043f);
                    if (act == 3 && index == 3)
                    {
                        AddDocumentInk(paper.transform, "Left Body", "第一栏\n五个齿、八个点\n\n" + WrapCipher(pages[index][1], 10) + "\n\n两行连续读取", 0.077f, 0.205f, -0.277f, 0.25f);
                        AddDocumentInk(paper.transform, "Right Body", "第二栏 · 两行栅栏\n上下往返，按行读出\n\n" + WrapCipher(pages[index][2], 12), 0.077f, 0.205f, 0.02f, 0.255f);
                    }
                    else AddDocumentInk(paper.transform, "Body", pages[index][1], 0.077f, 0.205f);
                    AddDocumentInk(paper.transform, "Folio", roman[act] + "  /  " + (index + 1).ToString("00"), -0.151f, 0.017f);
                    if (act == 1 && index == 3) AddPigpenCipher(paper.transform);
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
            BuildMasterNotebook();
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
                for (int i = 0; i < act.documents.Length; i++)
                {
                    var paper = act.documents[i].transform;
                    paper.position = mat.TransformPoint(V((i % 3 - 1) * 0.305f, 0.014f, 0.33f - i / 3 * 0.33f));
                    paper.rotation = mat.rotation;
                    paper.localScale = Vector3.one * 0.44f;
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
