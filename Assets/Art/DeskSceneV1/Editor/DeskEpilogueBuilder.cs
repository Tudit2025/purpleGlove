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
        [MenuItem("Purple Glove/Acts/Install Sixth Act Epilogue")]
        public static void InstallEpilogue()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Install the epilogue outside Play Mode.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath); LoadSavedSceneAssets();
            if (!Camera.main || !Camera.main.GetComponent<FifthActFlow>()) throw new InvalidOperationException("Install act five first.");
            ConfigureEpilogue(Camera.main);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene); Debug.Log("EPILOGUE_INSTALLED");
        }

        static void ConfigureEpilogue(Camera camera)
        {
            var fifth = camera.GetComponent<FifthActFlow>();
            if (!fifth) return;
            var flow = camera.GetComponent<EpilogueFlow>() ?? camera.gameObject.AddComponent<EpilogueFlow>();
            var dialogue = camera.GetComponent<DialogueController>();
            flow.fifth = fifth; fifth.epilogue = flow; flow.dialogue = dialogue; flow.curtain = camera.GetComponent<ActTransitionOverlay>();
            flow.drum = camera.GetComponent<SealedDeliveryController>().transferDrum;
            flow.doorLeaf = camera.GetComponent<PrologueSequence>().doorLeaf;
            flow.doorOpeningSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/PH/Doors/wood_door_open_01.wav");
            var clerk = dialogue.speakers.Single(s => s.id == "clerk");
            var luca = dialogue.speakers.Single(s => s.id == "luca");
            var matteo = dialogue.speakers.Single(s => s.id == "matteo");
            var beatrice = dialogue.speakers.Single(s => s.id == "beatrice");
            DialogueEvent.Line L(DialogueSpeaker speaker, string text) => new DialogueEvent.Line { speaker = speaker, text = text };
            DialogueEvent E(string id, string title, DialogueSpeaker visitor, params DialogueEvent.Line[] lines)
            {
                var sequence = ThirdActEvent(id, "第六幕 · " + title, visitor, lines);
                sequence.context = "doc/ph-revised.md 第六幕：所有台词通过探视口，在主门开启前完成。";
                sequence.closeShutterOnComplete = true;
                EditorUtility.SetDirty(sequence);
                if (!dialogue.events.Contains(sequence)) dialogue.events = dialogue.events.Concat(new[] { sequence }).ToArray();
                return sequence;
            }
            if (!flow.savedReport) flow.savedReport = E("act6_saved_report", "六点的处理回执", clerk,
                L(clerk, "卢卡，我回来了。"),
                L(clerk, "威尼斯人和法国人看到你的核验报告要求暂停表决，卡拉法已被要求留候调查。"),
                L(clerk, "所有人都知道了原件换版的事情，米兰拒绝公示，现在原件还在我们这里。"),
                L(clerk, "米兰人说马泰奥在圣马丁北门。"),
                L(luca, "米兰人不打算把他带走了？"),
                L(clerk, "我还带了一些回执文件来，我给你送进去。"));
            if (!flow.transferredReport) flow.transferredReport = E("act6_transfer_report", "暂停转押与原件交接", clerk,
                L(clerk, "卢卡，我回来了。"),
                L(clerk, "威尼斯人和法国人看到你的核验报告要求暂停表决，卡拉法已被要求留候调查。"),
                L(clerk, "所有人都知道了原件换版的事情，现在原件按你的申请已经送走了。"),
                L(clerk, "我还带了一些回执文件来，我给你送进去。"));
            if (!flow.endorsedReport) flow.endorsedReport = E("act6_endorsed_report", "六点的转押登记", clerk,
                L(clerk, "卢卡，米兰人把马泰奥接走了。"),
                L(luca, "你们没拦住他们吗？"),
                L(clerk, "你为军令来源作了担保。米兰人在今早的会议上很占优势。"),
                L(luca, "他被送到哪里？"),
                L(clerk, "不知道，我们还在查。"),
                L(clerk, "这些回执文件你看一下。"));
            if (!flow.reunionArrival) flow.reunionArrival = E("act6_reunion_arrival", "六点半的接回凭据", clerk,
                L(clerk, "马泰奥·里奇，三方见证人员一起把他接回来了。"),
                L(luca, "他在外面？"),
                L(clerk, "对，我把他叫过来。"));
            if (!flow.openEnding) flow.openEnding = E("act6_open", "开门", matteo,
                L(matteo, "卢卡。"),
                L(luca, "我把你的自述交上去了，老师。"),
                L(matteo, "应该的。"),
                L(luca, "他们应该还会问你很多事。"),
                L(matteo, "我知道。谢谢你。"),
                L(matteo, "以后的日子还长呢……"));
            if (!flow.missingEnding) flow.missingEnding = E("act6_missing", "缺页", matteo,
                L(matteo, "卢卡，你把我给你的信交上去了吗？"),
                L(luca, "我最后烧掉了。"),
                L(matteo, "为什么？"),
                L(luca, "看到那些东西，你绝对走不了了，行会以后的事情也不好处理。"),
                L(matteo, "……"),
                L(matteo, "我知道了，剩下的事我会自己说的。"),
                L(clerk, "手续已经办完了，卢卡，我来给你开门。"));
            if (!flow.transferredEnding) flow.transferredEnding = E("act6_transferred", "真相易手", clerk,
                L(clerk, "{去向后果}"),
                L(luca, "之后会怎么样呢？"),
                L(clerk, "马泰奥还要留候核验。以后如果要对照原件，都要向持有者提出申请。"),
                L(clerk, "行会会留好这些回执。还有最新的消息的话，我们会再找你的。"));
            if (!flow.endorsedEnding) flow.endorsedEnding = E("act6_endorsed", "背书", clerk,
                L(clerk, "{去向后果}"),
                L(luca, "我不应该为米兰人担保的，这下我们完全陷入被动了。"),
                L(luca, "当时老师签了一个后患无穷的名字，没想到我也犯了这样的错误。"),
                L(clerk, "行会会继续追查他的去向。有后续的话我会再通知你的。"));

            var mat = GameObject.Find("WritingMat").transform;
            Transform Anchor(string name, Vector3 position)
            {
                var anchor = mat.Find(name);
                if (!anchor) { anchor = new GameObject(name).transform; anchor.SetParent(mat, false); }
                anchor.localPosition = position; anchor.localRotation = Quaternion.identity; return anchor;
            }
            flow.reportAnchor = Anchor("Epilogue result position", V(-0.19f, 0.019f, 0.05f));
            flow.reunionAnchor = Anchor("Epilogue reunion position", V(0.19f, 0.021f, 0.05f));
            if (!flow.reportReceipt) flow.reportReceipt = EpilogueReceipt(flow.reportAnchor, "Epilogue result receipt", "行会 · 处理回执",
                "第17号委托 · 回执六之一\n清晨六点 / 行会登记送达\n\n威尼斯、法国已具名联署。\n米兰监押书记官确认：\n六点转押暂缓，证人留候。\n粮站表决暂停，转入共同审查。\n\n换版原件：继续封存。\n交接手续办结，副本留档。", out flow.reportInk);
            if (!flow.reunionReceipt) flow.reunionReceipt = EpilogueReceipt(flow.reunionAnchor, "Epilogue reunion receipt", "证人接回 · 交接凭据",
                "第17号委托 · 回执六之二\n清晨六点半 / 新收件\n\n马泰奥·里奇\n已由三方见证人会同接回。\n\n米兰、威尼斯、法国见证登记\n行会执事核验并送达。\n\n证人接回不免除后续陈情之责。", out _);
            EditorUtility.SetDirty(flow); EditorUtility.SetDirty(fifth); EditorUtility.SetDirty(dialogue);
        }
        static Transform EpilogueReceipt(Transform anchor, string name, string title, string body, out TextMesh ink)
        {
            var paper = new GameObject(name).transform; paper.SetParent(anchor, false);
            MeshObject("Act6_ReceiptPaper", paper, m => m.Sheet(0.34f, 0.46f, M("Paper_RagIvory"), 0.003f));
            FirstActInk(paper, "Title", title, PrintedChinese, 0, 0.20f, 0.29f, 0.025f, true);
            ink = FirstActInk(paper, "Body", body, PrintedChinese, -0.145f, 0.145f, 0.29f, 0.31f);
            FirstActInk(paper, "Folio", "印务行会 · 具名登记", PrintedChinese, 0, -0.19f, 0.25f, 0.015f, true);
            foreach (var renderer in paper.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = ShadowCastingMode.Off;
            paper.gameObject.SetActive(false); return paper;
        }
    }
}
