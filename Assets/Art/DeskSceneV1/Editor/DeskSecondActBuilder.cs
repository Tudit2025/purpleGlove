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
            var positions = new[] { V(-0.27f, 0.014f, 0.20f), V(0.17f, 0.014f, 0.10f), V(-0.25f, 0.014f, -0.17f) };
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
                FirstActInk(paper.transform, "Title", "印坊旧便条 · 威尼斯封件二号", HandwrittenChinese, -0.168f, 0.111f, 0.336f, 0.022f);
                FirstActInk(paper.transform, "Coordinates", BookCipherText.Cipher, documentFont, -0.168f, 0.065f, 0.336f, 0.093f);
                FirstActInk(paper.transform, "Rule", "页—行—字母；忽略空格、标点，从一计数。\n联系人：━━━━━━━━", HandwrittenChinese, -0.168f, -0.064f, 0.336f, 0.043f);
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
            FirstActInk(paper, "Title", "第七号封档 · 印务台账摘要", actDocumentFont, 0, 0.200f, 0.35f, 0.025f, true);
            FirstActInk(paper, "Reference", "核验所调阅副本  /  交存人：巴尔托洛", actDocumentFont, -0.184f, 0.155f, 0.368f, 0.016f);
            string[][] rows = {
                new[] { "装订批号", "7—031" }, new[] { "原定项目", "商队通行文件" }, new[] { "后改项目", "军情译录件" },
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
            FirstActInk(paper, "Custody note", "附：裁纸边角一张。原始签收页涉及委托方私账，\n另存本库；调阅须提供对应的正式委托案号。", actDocumentFont, -0.183f, -0.166f, 0.366f, 0.041f);
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
                L(clerk, "威尼斯代表贝阿特丽切，封件二号。里面只有一张来自印坊的旧便条。"),
                L(beatrice, "我查这件事已经六天。明早是委员会给我的最后期限。"),
                L(beatrice, "印刷匠留了一串数字。我没有他用的版本。你桌上的书，也许能解释它。"),
                L(luca, "把便条给我。我会核对版本。"), L(clerk, "合上观察窗后，我把封包从传物舱送进去。"));
            flow.cue = SecondActEvent(dialogue, "act2_ring_hint", "卢卡 · 摇铃调档", null,
                L(luca, "调阅第七号封档……明文留好了。现在该摇铃，请执事把对应的档案调来。"));
            flow.request = SecondActEvent(dialogue, "act2_request", "第二幕 · 门边调档", clerk,
                L(clerk, "卢卡，你摇铃了。需要什么？"), L(luca, "请调取印坊纠纷封档，第七号，供这次核验使用。"),
                L(clerk, "印坊纠纷，第七号，核验用途。我去向保管人调取，请稍候。"));
            flow.request.arrivalSound = null; flow.request.arrivalDelay = 0.15f;
            flow.arrival = SecondActEvent(dialogue, "act2_archive_arrival", "第二幕 · 第七号封档送达", clerk,
                L(clerk, "第七号封档到了。来源是行会纠纷库，交存人巴尔托洛。"),
                L(clerk, "这次送来台账摘要和一张裁纸边角。原始签收页另存，调阅条件写在摘要下方。"),
                L(luca, "请把这两件送进来。"), L(clerk, "已经登记。我关好观察窗，再转入传物舱。"));
            flow.books = new[] { false, true }.Select(flower => BuildPrayerBookTool(work, archive, flower)).ToArray();
            EditorUtility.SetDirty(flow.request); EditorUtility.SetDirty(flow); EditorUtility.SetDirty(records);
            EditorUtility.SetDirty(archive); EditorUtility.SetDirty(dialogue); EditorUtility.SetDirty(flow.bell);
        }

        static DialogueEvent SecondActEvent(DialogueController dialogue, string id, string title, DialogueSpeaker visitor, params DialogueEvent.Line[] lines)
        {
            string path = DeskDialogueBuilder.DataRoot + "/Events/" + id + ".asset";
            var sequence = AssetDatabase.LoadAssetAtPath<DialogueEvent>(path);
            if (!sequence) { sequence = ScriptableObject.CreateInstance<DialogueEvent>(); AssetDatabase.CreateAsset(sequence, path); }
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
                var title = FirstActInk(spread, "Edition " + page, flower ? "祷文集 · 七瓣花新版" : "祷文集 · 普通旧版", PrintedChinese, left, 0.161f, 0.28f, 0.022f);
                title.transform.localPosition += Vector3.up * 0.004f;
                for (int line = 0; line < 4; line++)
                {
                    var text = FirstActInk(spread, "Page " + (12 + page) + " line " + (line + 1), (line + 1) + "  " + lines[page * 4 + line], documentFont,
                        left, 0.10f - line * 0.058f, 0.28f, 0.026f);
                    text.transform.localPosition += Vector3.up * 0.004f;
                }
                var folio = FirstActInk(spread, "Folio " + page, (12 + page) + "   ·   " + (flower ? "五月十九日装订" : "四月二日装订"), PrintedChinese, left, -0.142f, 0.28f, 0.019f);
                folio.transform.localPosition += Vector3.up * 0.004f;
            }
            foreach (var r in spread.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            tool.workbench = work; tool.archive = archive; tool.note = archive.acts[1].documents[0]; tool.sevenPetal = flower;
            tool.spread = spread.gameObject; spread.gameObject.SetActive(false); tool.enabled = false;
            EditorUtility.SetDirty(tool); return tool;
        }
    }
}
