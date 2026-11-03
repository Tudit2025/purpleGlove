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
        [MenuItem("Purple Glove/Acts/Install Fourth and Fifth Act Progression")]
        public static void InstallFinalActs()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Install final acts outside Play Mode.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath); LoadSavedSceneAssets();
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            if (!archive || !Camera.main.GetComponent<PlaintextArchiveController>()) throw new InvalidOperationException("Install the previous acts and plaintext records first.");
            var layout = archive.transform.Find(ActDocumentGroup);
            foreach (var old in archive.acts[4].documents) if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            archive.acts[4].documents = Enumerable.Range(0, 3).Select(i => CreateFifthActDocument(layout, i)).ToArray();
            archive.acts[4].deferredDocuments = Array.Empty<SceneObject>();
            archive.acts[4].title = "第五幕 · 落笔与交卷";
            ArrangeFifthActDocuments(archive); ConfigureOperationalDocuments(archive);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene); Debug.Log("FINAL_ACTS_INSTALLED");
        }

        static void ConfigureFinalActs(Camera camera)
        {
            var records = camera.GetComponent<PlaintextArchiveController>();
            var archive = records ? records.archive : null;
            var dialogue = camera.GetComponent<DialogueController>();
            var rail = records ? records.entries.FirstOrDefault(e => e.id == "rail") : null;
            if (!archive || !dialogue || rail == null || !rail.document || !rail.document.GetComponent<DeskOperationalDocument>()) return;
            var verification = archive.acts[2].documents.Select(p => p.GetComponent<ThirdActVerification>()).FirstOrDefault(v => v);
            var transfer = archive.acts[0].documents.FirstOrDefault(p => p.name.EndsWith("行会档案转交申请表"));
            if (!verification || !transfer) return;
            var fourth = camera.GetComponent<FourthActFlow>() ?? camera.gameObject.AddComponent<FourthActFlow>();
            var fifth = camera.GetComponent<FifthActFlow>() ?? camera.gameObject.AddComponent<FifthActFlow>();
            fourth.records = records; fourth.archive = archive; fourth.dialogue = dialogue;
            fourth.story = camera.GetComponent<PrologueSequence>(); fourth.confession = rail.document.GetComponent<DeskOperationalDocument>();
            fifth.records = records; fifth.archive = archive; fifth.dialogue = dialogue; fifth.verification = verification;
            fifth.delivery = camera.GetComponent<SealedDeliveryController>(); fifth.curtain = camera.GetComponent<ActTransitionOverlay>();
            fifth.bell = UnityEngine.Object.FindFirstObjectByType<DeskHandbell>(); fifth.transferApplication = transfer; fifth.confession = rail.document;
            fifth.originals = archive.acts[3].documents.Take(2).ToArray();
            var luca = dialogue.speakers.Single(s => s.id == "luca"); var clerk = dialogue.speakers.Single(s => s.id == "clerk");
            DialogueEvent.Line L(DialogueSpeaker speaker, string text) => new DialogueEvent.Line { speaker = speaker, text = text };
            if (!fourth.summary) fourth.summary = FinalActEvent("act4_summary", "第四幕 · 两份明文后的独白", null,
                L(luca, "圣马丁，北门。这个应该就是他们关押他的地方。"),
                L(luca, "他承认自己知道文件是假的，还是签了名字。"),
                L(luca, "伪造的军令用了法国人在商路协议上的签名，这应该就是法国人不想让人看见的东西。"),
                L(luca, "老师的自白和换档的原件是重要证据，看来也到了我做选择的时候了。"));
            // Retire the removed review events, including their entries in the test console.
            dialogue.events = dialogue.events.Where(e => e && !e.id.StartsWith("act4_review_", StringComparison.Ordinal)).ToArray();
            const string oldSummaryEnding = "先把手里能证实的事核清，再决定交出去什么，以及用我的名字担保什么。";
            if (fourth.summary.lines.LastOrDefault()?.text == oldSummaryEnding)
            {
                fourth.summary.lines[fourth.summary.lines.Length - 1].text = "字句已经还原，记录也对上了。接下来，要由我决定怎样签、怎样交。";
                EditorUtility.SetDirty(fourth.summary);
            }
            if (!fifth.introduction) fifth.introduction = FinalActEvent("act5_arrival", "第五幕 · 三位代表的书信", clerk,
                L(clerk, "卢卡，米兰、法国、威尼斯三位代表给你送了信件来。"),
                L(clerk, "时间差不多了，马上需要收你的核验报告送到会议那边去。"),
                L(clerk, "会长说你可能会有移存一些文件的需要，你填写移存申请表就可以了。"),
                L(clerk, "如果你处理完了，就摇手铃，我会把文件送出去。"));
            if (!fifth.handoff) fifth.handoff = FinalActEvent("act5_handoff", "第五幕 · 摇铃交卷", clerk,
                L(clerk, "卢卡，我来拿文件了。"),
                L(clerk, "但是你还不能离开，我白天再来找你了。"));
            if (!fifth.guaranteeWarning) fifth.guaranteeWarning = FinalActEvent("act5_source_warning", "卢卡 · 来源担保", null,
                L(luca, "显然这个军令是伪造的，加密是障眼法，我应该为米兰人担保吗？"));
            foreach (var sequence in new[] { fourth.summary, fifth.introduction, fifth.handoff, fifth.guaranteeWarning })
            {
                sequence.context = "doc/ph-revised.md 第四幕独白后直接转入第五幕，三份主动文件、摇铃交卷。";
                EditorUtility.SetDirty(sequence);
                if (!dialogue.events.Contains(sequence)) dialogue.events = dialogue.events.Concat(new[] { sequence }).ToArray();
            }
            BuildFinalDocumentChoices(fifth);
            var candle = GameObject.Find("Candleholder_Lit").transform;
            var target = candle.Find("Confession burn point");
            if (!target) { target = new GameObject("Confession burn point").transform; target.SetParent(candle, false); }
            target.localPosition = V(0.025f, 0.385f, 0); target.localRotation = Quaternion.Euler(0, 0, 28); fifth.burnTarget = target;
            if (!fifth.ashes)
            {
                var ash = MeshObject("Act5_ConfessionAshes", candle, m =>
                {
                    for (int i = 0; i < 9; i++) m.Box(V(0.11f + i % 3 * 0.012f, 0.006f, -0.04f + i / 3 * 0.011f), V(0.01f, 0.001f, 0.008f), M("Ink"));
                });
                fifth.ashes = ash; ash.SetActive(false);
            }
            if (!fifth.dispatchRoot)
            {
                fifth.dispatchRoot = new GameObject("Final outgoing documents").transform;
                fifth.dispatchRoot.SetParent(camera.transform, false); fifth.dispatchRoot.gameObject.SetActive(false);
            }
            var cargoLight = fifth.dispatchRoot.Find("Outgoing document bounce light");
            if (!cargoLight)
            {
                var light = new GameObject("Outgoing document bounce light").AddComponent<Light>();
                light.transform.SetParent(fifth.dispatchRoot, false); light.transform.localPosition = V(0, 0.18f, -0.12f);
                light.type = LightType.Point; light.range = 0.65f; light.intensity = 1.35f; light.color = new Color(1, 0.87f, 0.69f);
                light.renderMode = LightRenderMode.ForcePixel; light.shadows = LightShadows.None; light.cullingMask = (1 << 8) | 1;
            }
            if (fifth.destroyedTab) UnityEngine.Object.DestroyImmediate(fifth.destroyedTab.gameObject);
            fifth.destroyedTab = FirstActInk(fourth.confession.storageAnchor, "Destroyed plaintext marker", "自述明文已毁", PrintedChinese,
                0, 0.16f, 0.16f, 0.024f, true); fifth.destroyedTab.gameObject.SetActive(false);
            EditorUtility.SetDirty(fourth); EditorUtility.SetDirty(fifth); EditorUtility.SetDirty(dialogue);
            ConfigureEpilogue(camera);
        }

        static DialogueEvent FinalActEvent(string id, string title, DialogueSpeaker visitor, params DialogueEvent.Line[] lines)
        {
            var sequence = ThirdActEvent(id, title, visitor, lines);
            sequence.context = "doc/ph-revised.md 第四幕独白后直接转入第五幕，三份主动文件、摇铃交卷。";
            if (id == "act5_handoff") { sequence.arrivalSound = null; sequence.arrivalDelay = 0.1f; }
            EditorUtility.SetDirty(sequence); return sequence;
        }

        static void BuildFinalDocumentChoices(FifthActFlow flow)
        {
            FifthActDocument Form(SceneObject paper, FifthActDocument.Kind kind)
            {
                var form = paper.GetComponent<FifthActDocument>() ?? paper.gameObject.AddComponent<FifthActDocument>();
                form.kind = kind; form.flow = flow;
                if (form.controls) UnityEngine.Object.DestroyImmediate(form.controls);
                form.controls = new GameObject("Final disposition fields"); form.controls.transform.SetParent(paper.transform, false);
                return form;
            }
            TextMesh Ink(FifthActDocument form, string name, string text, float x, float z, float width, float height, Font font = null)
                => FirstActInk(form.controls.transform, name, text, font ? font : PrintedChinese, x, z, width, height);
            var verification = Form(flow.verification.GetComponent<SceneObject>(), FifthActDocument.Kind.Verification);
            var sourceOptions = verification.transform.Find("Source options"); if (sourceOptions) sourceOptions.gameObject.SetActive(false);
            verification.choices = new[] {
                Ink(verification, "Refuse guarantee", "● 不作来源担保", -0.166f, -0.100f, 0.157f, 0.018f),
                Ink(verification, "Guarantee source", "○ 担保来源可靠", 0.012f, -0.100f, 0.157f, 0.018f) };
            verification.areas = new[] { new Rect(-0.17f, -0.126f, 0.17f, 0.037f), new Rect(0.008f, -0.126f, 0.17f, 0.037f) };
            verification.statusInk = Ink(verification, "Source evidence", "换版记录：法使尚未核签。\n经更正，以当前选择为准。", -0.164f, -0.157f, 0.326f, 0.035f);
            verification.signatureInk = Ink(verification, "Source guarantee signature", "Luca", -0.025f, -0.202f, 0.16f, 0.025f, notesFont);
            verification.signatureInk.text = "";

            var transfer = Form(flow.transferApplication, FifthActDocument.Kind.Transfer);
            var oldChoices = transfer.transform.Find("Recipient choices"); if (oldChoices) oldChoices.gameObject.SetActive(false);
            Ink(transfer, "Archive number", "第五十七号封档 / 17号 / 726", -0.065f, 0.102f, 0.235f, 0.016f);
            Ink(transfer, "Archive title", "商业签收页与换版原件", -0.065f, 0.057f, 0.235f, 0.016f);
            Ink(transfer, "Enclosures", "两页，含旧试印及出库登记", -0.010f, 0.012f, 0.18f, 0.016f);
            transfer.choices = new TextMesh[4]; transfer.areas = new Rect[4];
            for (int i = 0; i < 4; i++)
            {
                float x = -0.045f + i % 2 * 0.115f, z = -0.049f - i / 2 * 0.026f;
                transfer.choices[i] = Ink(transfer, "Custody " + i, "○ " + FifthActFlow.CustodyLabel((FifthActFlow.Custody)i), x, z, 0.108f, 0.016f);
                transfer.areas[i] = new Rect(x - 0.003f, z - 0.021f, 0.112f, 0.025f);
            }
            transfer.statusInk = Ink(transfer, "Disposition", "当前安排：继续封存", -0.07f, -0.104f, 0.24f, 0.018f);
            transfer.signatureInk = Ink(transfer, "Application signature", "Luca", -0.03f, -0.159f, 0.16f, 0.024f, notesFont);
            transfer.signatureInk.text = "";
            var confession = Form(flow.confession, FifthActDocument.Kind.Confession);
            // Reserve a separate lower margin for the disposition and burn control.
            var plaintext = flow.confession.transform.Find("Preserved plaintext");
            var translation = flow.confession.transform.Find("Translation");
            if (plaintext) plaintext.localPosition = V(plaintext.localPosition.x, plaintext.localPosition.y, 0.125f);
            if (translation) translation.localPosition = V(translation.localPosition.x, translation.localPosition.y, -0.006f);
            confession.statusInk = Ink(confession, "Default disposition", "留存则随卷呈交；焚毁后不可复原。", -0.275f, -0.132f, 0.55f, 0.016f);
            confession.choices = new[] { Ink(confession, "Burn plaintext", "将此页自述明文移近烛火焚毁", -0.275f, -0.156f, 0.55f, 0.018f) };
            confession.areas = new[] { new Rect(-0.28f, -0.18f, 0.56f, 0.030f) };
            foreach (var form in new[] { verification, transfer, confession })
            {
                foreach (var renderer in form.controls.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = ShadowCastingMode.Off;
                form.controls.SetActive(false); EditorUtility.SetDirty(form);
            }
        }

        static SceneObject CreateFifthActDocument(Transform parent, int index)
        {
            var page = new GameObject("Act5_" + (index + 1).ToString("00") + "_" + ActDocumentPages[4][index][0]); page.transform.SetParent(parent, false);
            var item = page.AddComponent<SceneObject>(); item.screenFill = 0.82f; item.minimumViewDistance = 0.18f;
            item.inspectionFill = new Color(0.85f, 0.78f, 0.64f);
            var box = item.GetComponent<BoxCollider>(); box.center = V(0, 0.004f, 0); box.size = V(0.42f, 0.012f, 0.56f);
            MeshObject("Act5_RepresentativeLetter", page.transform, m => m.Sheet(0.42f, 0.56f, M("Paper_RagIvory"), 0.003f));
            FirstActInk(page.transform, "Title", ActDocumentPages[4][index][0], PrintedChinese, 0, 0.245f, 0.35f, 0.026f, true);
            FirstActInk(page.transform, "Body", ActDocumentPages[4][index][1], PrintedChinese, -0.177f, 0.182f, 0.354f, 0.36f);
            FirstActInk(page.transform, "Folio", "行会登记来件 / V · 0" + (index + 1), PrintedChinese, 0, -0.24f, 0.34f, 0.015f, true);
            foreach (var r in page.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            page.SetActive(false); return item;
        }
        static void ArrangeFifthActDocuments(ActArchiveController archive)
        {
            var mat = GameObject.Find("WritingMat").transform;
            for (int i = 0; i < archive.acts[4].documents.Length; i++)
            {
                var paper = archive.acts[4].documents[i].transform;
                paper.SetPositionAndRotation(mat.TransformPoint(V((i - 1) * 0.325f, 0.014f, 0.10f)), mat.rotation);
                paper.localScale = Vector3.one * 0.72f;
            }
        }
    }
}
