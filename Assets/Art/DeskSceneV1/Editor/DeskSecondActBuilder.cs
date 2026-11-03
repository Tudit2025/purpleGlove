using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace PurpleGlove.Editor
{
    public static partial class DeskSceneBuilder
    {
        [MenuItem("Purple Glove/Acts/Install Second Act Ending")]
        public static void InstallSecondActEnding()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            ConfigureSecondActEnding(Camera.main);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("SECOND_ACT_ENDING_INSTALLED");
        }

        static void ConfigureSecondActEnding(Camera camera)
        {
            var flow = camera.GetComponent<SecondActFlow>();
            if (!flow) throw new InvalidOperationException("Install second-act progression first.");
            var dialogue = camera.GetComponent<DialogueController>();
            var luca = dialogue.speakers.Single(s => s.id == "luca");
            var clerk = dialogue.speakers.Single(s => s.id == "clerk");
            DialogueEvent.Line L(DialogueSpeaker s, string t) => new DialogueEvent.Line { speaker = s, text = t };
            flow.story = camera.GetComponent<PrologueSequence>();
            flow.deskSummary = SecondActEvent(dialogue, "act2_summary", "第二幕 · 卢卡等待原件", null,
                L(luca, "左手手套，巴尔托洛把委托换档的人记下来了。公爵的委托写过，替米兰代领材料的卡拉法他左手戴紫手套。"),
                L(luca, "这个叫卡拉法的绝对和这件事情有关，十有八九他已经在这里待了有一阵子了……"),
                L(luca, "如果能调到这个“原件”，应该能确定这两件事情的联系。"),
                L(luca, "如果这个换档记录指向了请我核验的军令，那米兰人所用的手段就很明确了：造了假的文件，然后让师傅签字作保。"));
            flow.thirdActIntro = SecondActEvent(dialogue, "act3_arrival", "第三幕 · 正式核验件到达", clerk,
                L(clerk, "卢卡，这次核验委托的内容，军情译录件送到了。确实是我们行会印刷的。"),
                L(clerk, "封包里有四个文件：行会承办这次译录时的译录委托，上面有你师傅的亲笔签名；还有他译录的军情密文；一张核验表。"),
                L(clerk, "最后还有带法方签名的法文原文，这张好像也是我们行会印刷的。"),
                L(luca, "好的，请送进来吧。"));
            flow.originalRequest = SecondActEvent(dialogue, "act2_original_request", "第二幕 · 申请原始档案", clerk,
                L(clerk, "卢卡，有什么事吗？"),
                L(luca, "请调第五十七号封档附件对应的原始档案，第十七号委托，批号七二六。"),
                L(clerk, "好的，我让人去调一下这个档案，但是从编号来看这篇应该封在存放原始文献的地方，需要一点时间。"));
            flow.originalRequest.arrivalSound = null; flow.originalRequest.arrivalDelay = 0.1f;
            EditorUtility.SetDirty(flow.originalRequest);
            flow.deskSummary.context = "猪圈明文实际留存后回桌面独白；等待玩家摇铃申请原始档案，执事应允后进入第三幕。";
            flow.thirdActIntro.context = "第三幕转场完成后介绍正式核验件，观察窗完全关闭后开始收件。";
            EditorUtility.SetDirty(flow.deskSummary); EditorUtility.SetDirty(flow.thirdActIntro);
            EditorUtility.SetDirty(flow); EditorUtility.SetDirty(dialogue);
        }

        [MenuItem("Purple Glove/Acts/Update Book Cipher Note")]
        public static void UpdateBookCipherNote()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            LoadSavedSceneAssets();
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            var paper = archive.acts[1].documents[0].transform;
            foreach (string name in new[] { "Title", "Rule", "Act2_NoteRedaction" })
            {
                var old = paper.Find(name);
                if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            AddBookNoteRedaction(paper);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("BOOK_CIPHER_NOTE_UPDATED");
        }

        static void AddBookNoteRedaction(Transform paper)
        {
            MeshObject("Act2_NoteRedaction", paper, m =>
                m.Box(V(-0.088f, 0.010f, -0.073f), V(0.16f, 0.0006f, 0.009f), M("Ink")))
                .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        [MenuItem("Purple Glove/Acts/Install Second Act Progression")]
        public static void InstallSecondActProgression()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            LoadSavedSceneAssets();
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            var layout = archive.transform.Find(ActDocumentGroup);
            foreach (var item in archive.acts[1].documents) if (item) UnityEngine.Object.DestroyImmediate(item.gameObject);
            archive.acts[1].documents = Enumerable.Range(0, 3).Select(i => CreateSecondActDocument(layout, i)).ToArray();
            ArrangeSecondActDocuments(archive);
            ConfigureSecondAct(Camera.main);
            EditorUtility.SetDirty(archive);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("SECOND_ACT_INSTALLED");
        }

        static void ArrangeSecondActDocuments(ActArchiveController archive)
        {
            var mat = GameObject.Find("WritingMat").transform;
            // Stack both cipher slips on the right; keep the ledger in the left column.
            var positions = new[] { V(0.25f, 0.014f, 0.20f), V(-0.235f, 0.014f, 0.10f), V(0.25f, 0.014f, -0.17f) };
            for (int i = 0; i < 3; i++)
            {
                var item = archive.acts[1].documents[i];
                item.transform.SetPositionAndRotation(mat.TransformPoint(positions[i]), mat.rotation);
                item.transform.localScale = Vector3.one; item.gameObject.SetActive(false);
            }
            archive.acts[1].deferredDocuments = archive.acts[1].documents.Skip(1).ToArray();
        }

        static SceneObject CreateSecondActDocument(Transform parent, int index)
        {
            actDocumentFont = RequiredDocumentFont("SourceHanSerifCN-Regular.otf");
            string[] names = { "印坊数字便条", "印务台账摘要", "猪圈密文碎纸" };
            var paper = new GameObject("Act2_" + (index + 1).ToString("00") + "_" + names[index]);
            paper.transform.SetParent(parent, false);
            var item = paper.AddComponent<SceneObject>();
            item.screenFill = 0.82f; item.minimumViewDistance = 0.18f;
            item.inspectionFill = new Color(0.85f, 0.78f, 0.64f);
            var box = item.GetComponent<BoxCollider>(); box.center = V(0, 0.004f, 0);
            box.size = index == 0 ? V(0.38f, 0.012f, 0.27f) : index == 1 ? V(0.42f, 0.012f, 0.53f) : V(0.34f, 0.012f, 0.22f);
            if (index == 0)
            {
                MeshObject("Act2_BookCipherNote", paper.transform, m => m.Sheet(0.38f, 0.27f, M("Paper_AgedCream"), 0.003f));
                FirstActInk(paper.transform, "Coordinates", BookCipherText.Cipher, documentFont, -0.168f, 0.065f, 0.336f, 0.093f);
                AddBookNoteRedaction(paper.transform);
            }
            else if (index == 1) BuildSecondActLedger(paper.transform);
            else BuildSecondActScrap(paper.transform);
            foreach (var renderer in paper.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = ShadowCastingMode.Off;
            paper.SetActive(false); return item;
        }

        static void BuildSecondActLedger(Transform paper)
        {
            // A clean, squared sheet. Rules and cells are actual ink geometry, not whitespace alignment.
            MeshObject("Act2_LedgerCleanSheet", paper, m => m.Box(V(0, 0.003f, 0), V(0.42f, 0.003f, 0.53f), M("Paper_RagIvory")));
            FirstActInk(paper, "Office", "印 务 行 会 · 纠 纷 封 存 库", actDocumentFont, 0, 0.237f, 0.35f, 0.019f, true);
            FirstActInk(paper, "Title", "第五十七号封档 · 印务台账摘要", actDocumentFont, 0, 0.200f, 0.35f, 0.025f, true);
            FirstActInk(paper, "Reference", "核验所调阅副本  /  交存人：巴尔托洛", actDocumentFont, -0.184f, 0.155f, 0.368f, 0.016f);
            string[][] rows = {
                new[] { "装订批号", "726" }, new[] { "原定项目", "商队通行文件" }, new[] { "后改项目", "军情译录件" },
                new[] { "换版记录", "两次换版均由同一名经手人带来" }, new[] { "款项结算", "米兰内廷印务账户" },
                new[] { "原始签收页", "已交行会纠纷库另存" }
            };
            const float top = 0.122f, rowHeight = 0.044f;
            MeshObject("Act2_LedgerTableRules", paper, m =>
            {
                for (int i = 0; i <= rows.Length; i++) m.Box(V(0, 0.009f, top - i * rowHeight), V(0.37f, 0.0006f, 0.0007f), M("Ink"));
                foreach (float x in new[] { -0.185f, -0.066f, 0.185f })
                    m.Box(V(x, 0.009f, top - rows.Length * rowHeight / 2), V(0.0007f, 0.0006f, rows.Length * rowHeight), M("Ink"));
            });
            for (int i = 0; i < rows.Length; i++)
            {
                var field = FirstActInk(paper, "Field " + i, rows[i][0], actDocumentFont, -0.176f, top - i * rowHeight - 0.009f, 0.10f, 0.021f);
                field.characterSize = 0.00145f;
                var value = FirstActInk(paper, "Value " + i, rows[i][1], PrintedChinese, -0.055f, top - i * rowHeight - 0.008f, 0.225f, 0.033f);
                value.characterSize = 0.0016f;
            }
            FirstActInk(paper, "Custody note", "附：裁纸边角一张。原始签收页涉及委托方私账，\n另存本库；对应第17号委托，批号726，可据此调阅。", actDocumentFont, -0.183f, -0.166f, 0.366f, 0.041f);
            FirstActInk(paper, "Footer", "据交存台账摘录  ·  行会登记存照", actDocumentFont, 0, -0.228f, 0.29f, 0.016f, true);
        }

        static void BuildSecondActScrap(Transform paper)
        {
            MeshObject("Act2_TornPigpenScrap", paper, m =>
            {
                var edge = new[] { V(-0.166f, 0.003f, -0.10f), V(-0.17f, 0.003f, 0.05f), V(-0.154f, 0.003f, 0.10f),
                    V(-0.10f, 0.003f, 0.106f), V(-0.078f, 0.003f, 0.092f), V(-0.049f, 0.003f, 0.108f),
                    V(0.02f, 0.003f, 0.10f), V(0.045f, 0.003f, 0.084f), V(0.065f, 0.003f, 0.10f),
                    V(0.16f, 0.003f, 0.094f), V(0.17f, 0.003f, 0.03f), V(0.154f, 0.003f, 0.004f),
                    V(0.17f, 0.003f, -0.016f), V(0.16f, 0.003f, -0.098f) };
                for (int i = 0; i < edge.Length; i++)
                {
                    var a = edge[i]; var b = edge[(i + 1) % edge.Length];
                    m.Triangle(V(0, 0.003f, 0), a, b, M("Parchment_Aged"), Vector3.up);
                    m.Quad(a, b, b - Vector3.up * 0.0015f, a - Vector3.up * 0.0015f, M("PageEdge"), (a + b).normalized);
                }
            });
            FirstActInk(paper, "Custody", "巴尔托洛 · 随台账交存", HandwrittenChinese, -0.145f, 0.074f, 0.29f, 0.020f);
            MeshObject("Act2_PigpenCipher", paper, m =>
            {
                const string letters = "LEFTGLOVE";
                for (int i = 0; i < letters.Length; i++)
                    DrawNotebookGlyph(m, letters[i], V(-0.135f + i * 0.031f + (i >= 4 ? 0.016f : 0), 0.011f, 0.023f), 0.010f);
            });
            FirstActInk(paper, "Handwritten note", "两次都是他亲手收的。\n姓名在原始签收页，\n不在这张摘要上。", HandwrittenChinese, -0.145f, -0.015f, 0.29f, 0.063f);
        }

        static void ConfigureSecondAct(Camera camera)
        {
            var records = camera.GetComponent<PlaintextArchiveController>();
            var archive = records.archive; var dialogue = camera.GetComponent<DialogueController>();
            var work = camera.GetComponent<CipherWorkbench>();
            var flow = camera.GetComponent<SecondActFlow>() ?? camera.gameObject.AddComponent<SecondActFlow>();
            flow.records = records; flow.archive = archive; flow.dialogue = dialogue;
            flow.delivery = camera.GetComponent<SealedDeliveryController>(); flow.bell = UnityEngine.Object.FindFirstObjectByType<DeskHandbell>();
            flow.bell.consumePermissionOnRing = true; flow.bell.ringAllowed = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(flow.bell);
            archive.acts[1].deferredDocuments = archive.acts[1].documents.Skip(1).ToArray();
            records.entries.Single(e => e.id == "book").sourceDocument = archive.acts[1].documents[0];
            records.entries.Single(e => e.id == "pigpen").sourceDocument = archive.acts[1].documents[2];
            var luca = dialogue.speakers.Single(s => s.id == "luca"); var clerk = dialogue.speakers.Single(s => s.id == "clerk");
            var beatrice = dialogue.speakers.Single(s => s.id == "beatrice");
            DialogueEvent.Line L(DialogueSpeaker s, string t) => new DialogueEvent.Line { speaker = s, text = t };
            SecondActEvent(dialogue, "beatrice", "第二幕 · 贝阿特丽切的便条", beatrice,
                L(clerk, "威尼斯代表贝阿特丽切找你，她带了一份文件来。"),
                L(beatrice, "你就是卢卡大师吧？我调查这个事情已经几天了，你的一个行会同事给了我一张字条。"),
                L(beatrice, "他不想当证人牵扯进来，写的密码。你应该能够破解它。"),
                L(beatrice, "我差不多知道里面会写什么，但是我的立场让我不能跟进这个线索了。"),
                L(luca, "那把便条给我吧，我会自己确认的。"),
                L(clerk, "威尼斯代表带来的资料登记好了，我给你送进去。"));
            flow.cue = SecondActEvent(dialogue, "act2_ring_hint", "卢卡 · 摇铃调档", null,
                L(luca, "“五十七号封档”……明文留好了。现在我该摇手铃请执事把对应的档案调来。"));
            flow.request = SecondActEvent(dialogue, "act2_request", "第二幕 · 门边调档", clerk,
                L(clerk, "卢卡，你需要我做什么吗？"),
                L(luca, "请帮我调取行会五十七号封档，这次核验使用。"),
                L(clerk, "五十七号好像是印坊工作流程纠纷有关的存档，我去调取，你稍微等一会儿。"));
            flow.request.arrivalSound = null; flow.request.arrivalDelay = 0.15f;
            flow.arrival = SecondActEvent(dialogue, "act2_archive_arrival", "第二幕 · 第五十七号封档送达", clerk,
                L(clerk, "五十七号封档到了，交存人是你师傅的熟人排字工巴尔托洛。"),
                L(clerk, "内含台账摘要和一张裁纸边角，我给你送进去。"));
            flow.books = new[] { false, true }.Select(flower => BuildPrayerBookTool(work, archive, flower)).ToArray();
            ConfigureSecondActEnding(camera);
            EditorUtility.SetDirty(flow.request); EditorUtility.SetDirty(flow); EditorUtility.SetDirty(records);
            EditorUtility.SetDirty(archive); EditorUtility.SetDirty(dialogue); EditorUtility.SetDirty(flow.bell);
        }

        static DialogueEvent SecondActEvent(DialogueController dialogue, string id, string title, DialogueSpeaker visitor, params DialogueEvent.Line[] lines)
        {
            string path = DeskDialogueBuilder.DataRoot + "/Events/" + id + ".asset";
            var sequence = AssetDatabase.LoadAssetAtPath<DialogueEvent>(path);
            if (sequence)
            {
                if (!dialogue.events.Contains(sequence)) dialogue.events = dialogue.events.Concat(new[] { sequence }).ToArray();
                return sequence;
            }
            sequence = ScriptableObject.CreateInstance<DialogueEvent>(); AssetDatabase.CreateAsset(sequence, path);
            sequence.id = id; sequence.title = title; sequence.initialVisitor = visitor; sequence.lines = lines;
            sequence.closeShutterOnComplete = true; sequence.context = "第二幕分批收件、书本解读与摇铃调档。";
            sequence.arrivalSound = visitor ? AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/PH/Doors/door_knock_pattern_01.wav") : null;
            sequence.arrivalDelay = sequence.arrivalSound ? sequence.arrivalSound.length : 0.1f;
            if (!dialogue.events.Contains(sequence)) dialogue.events = dialogue.events.Concat(new[] { sequence }).ToArray();
            EditorUtility.SetDirty(sequence); return sequence;
        }

        static PrayerBookTool BuildPrayerBookTool(CipherWorkbench work, ActArchiveController archive, bool flower)
        {
            var book = GameObject.Find(flower ? "Book_SevenPetal" : "Book_Charcoal");
            var tool = book.GetComponent<PrayerBookTool>() ?? book.AddComponent<PrayerBookTool>();
            var old = book.transform.Find("Cipher spread 12-13"); if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            tool.closedSurfaces = book.GetComponentsInChildren<Renderer>(true);
            var spread = new GameObject("Cipher spread 12-13").transform; spread.SetParent(book.transform, false);
            MeshObject("Act2_PrayerSpread_" + (flower ? "Flower" : "Ordinary"), spread, m =>
            {
                m.Box(V(0, 0.003f, 0), V(0.69f, 0.006f, 0.40f), M("Leather_Umber"), 0.003f);
                m.Box(V(-0.168f, 0.008f, 0), V(0.325f, 0.007f, 0.382f), M("Paper_AgedCream"), 0.002f);
                m.Box(V(0.168f, 0.008f, 0), V(0.325f, 0.007f, 0.382f), M("Paper_AgedCream"), 0.002f);
            });
            var lines = flower ? BookCipherText.Flower : BookCipherText.Ordinary;
            for (int page = 0; page < 2; page++)
            {
                float left = page == 0 ? -0.31f : 0.027f;
                for (int line = 0; line < 4; line++)
                {
                    var text = FirstActInk(spread, "Page " + (12 + page) + " line " + (line + 1), lines[page * 4 + line], documentFont,
                        left, 0.10f - line * 0.058f, 0.28f, 0.026f);
                    text.transform.localPosition += Vector3.up * 0.004f;
                }
                var folio = FirstActInk(spread, "Folio " + page, (12 + page).ToString(), PrintedChinese, left, -0.142f, 0.28f, 0.019f);
                folio.transform.localPosition += Vector3.up * 0.004f;
            }
            foreach (var r in spread.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            tool.workbench = work; tool.archive = archive; tool.note = archive.acts[1].documents[0]; tool.sevenPetal = flower;
            tool.letterIndexFont = RequiredDocumentFont("Courgette-Regular.ttf");
            tool.spread = spread.gameObject; spread.gameObject.SetActive(false); tool.enabled = false;
            EditorUtility.SetDirty(tool); return tool;
        }
    }
}
