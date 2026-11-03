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
                L(luca, "“左手手套”……巴尔托洛暗中记下了要求换版之人的特征。公爵的委托书上提过，替米兰内廷代领材料的卡拉法，左手常戴一只紫手套。"),
                L(luca, "这个卡拉法必定与此案脱不了干系。看情形，他在行会里暗中活动已有段时日了……"),
                L(luca, "只要能调出封档里提到的“原始签收页”，就能查明这两件事的关联。"),
                L(luca, "倘若那份换版记录当真指向今夜待核的军令，米兰人的伎俩便昭然若揭了：先偷梁换柱伪造公文，再胁迫师父签字作保。"));
            flow.thirdActIntro = SecondActEvent(dialogue, "act3_arrival", "第三幕 · 正式核验件到达", clerk,
                L(clerk, "卢卡，今夜委托核验的正件——军情译录件送到了。确系我们行会印制。"),
                L(clerk, "封包内共有四份文书：行会承接译录时的原始委托书，上面有令师的亲笔签名；他译出的军情密文；还有一份军情核验表。"),
                L(clerk, "最后是一份带有法方署名的法文原稿，瞧纸张和印迹，也是行会印坊出来的。"),
                L(luca, "知道了，请送进来吧。"));
            flow.originalRequest = SecondActEvent(dialogue, "act2_original_request", "第二幕 · 申请原始档案", clerk,
                L(clerk, "卢卡，还有什么需要？"),
                L(luca, "请帮我调取第五十七号封档提及的原始档案——对应第17号委托，批号七二六。"),
                L(clerk, "好，我派人去库房找。不过看这编号，原件应当封存在内库，调取手续繁琐，还请耐心等上一会儿。"));
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
                new[] { "换版记录", "两次换版均由同一经手人送交" }, new[] { "款项结算", "米兰内廷印务账户" },
                new[] { "原始签收页", "已移交行会纠纷库另存" }
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
            FirstActInk(paper, "Custody note", "附：裁纸边角一张。原始签收页事涉委托方私账，\n另存本库；对应第17号委托，批号726，可凭此调阅。", actDocumentFont, -0.183f, -0.166f, 0.366f, 0.041f);
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
            FirstActInk(paper, "Handwritten note", "两次皆由他亲手接领。\n其姓名载于原始签收页，\n未录于此份摘要。", HandwrittenChinese, -0.145f, -0.015f, 0.29f, 0.063f);
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
                L(clerk, "卢卡，威尼斯代表贝阿特丽切女士求见，随身带来了一份文件。"),
                L(beatrice, "你就是卢卡大师吧？这桩案子我已暗中查访数日，贵行会的一位同僚私下塞给我一张字条。"),
                L(beatrice, "他怕惹祸上身，不肯出面作证，便用了密码。想来你应该能解开。"),
                L(beatrice, "字条上的内容我大致猜得到，但碍于威尼斯的立场，这条线我不便再查下去了。"),
                L(luca, "请把便条交给我吧，我会亲自核实。"),
                L(clerk, "威尼斯代表交来的字条已登记入册，我这就递进来。"));
            flow.cue = SecondActEvent(dialogue, "act2_ring_hint", "卢卡 · 摇铃调档", null,
                L(luca, "“第五十七号封档”……译文已经记下。该摇铃请执事把这份档案调来了。"));
            flow.request = SecondActEvent(dialogue, "act2_request", "第二幕 · 门边调档", clerk,
                L(clerk, "卢卡，有什么吩咐？"),
                L(luca, "请帮我调阅行会的第五十七号封档，本次核验需要用到。"),
                L(clerk, "第五十七号封档……印象里似乎是一桩印坊工序纠纷的旧档。我这就去办，请稍候。"));
            flow.request.arrivalSound = null; flow.request.arrivalDelay = 0.15f;
            flow.arrival = SecondActEvent(dialogue, "act2_archive_arrival", "第二幕 · 第五十七号封档送达", clerk,
                L(clerk, "第五十七号封档调来了。交存人是排字工巴尔托洛，与令师素有交情。"),
                L(clerk, "里头有一份台账摘要和一张裁纸边角，这就给您送进来。"));
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
