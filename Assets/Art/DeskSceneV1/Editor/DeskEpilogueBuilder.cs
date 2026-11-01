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
                L(clerk, "六点了。卢卡，处理回执已经送到。我先从探视口把结果读给你。"),
                L(clerk, "威尼斯和法国已具名参加共同审查。米兰监押书记官确认：暂停六点转押，马泰奥留在圣马丁北门候核。"),
                L(luca, "转押确实停下来了？"),
                L(clerk, "这是监押书记官签收后的回执。接回仍在办理，我还不能把他已经回来的话写上去。"),
                L(beatrice, "粮站表决也暂停了。卡拉法已被要求留候调查，说明两次换版的签名。公爵的责任还得继续追查。"),
                L(luca, "原件呢？"),
                L(clerk, "已经加上行会的共同见证封条。三方可以申请查验。现在合上探视口，把处理回执从传物舱送进去。"));
            if (!flow.transferredReport) flow.transferredReport = E("act6_transfer_report", "暂停转押与原件交接", clerk,
                L(clerk, "六点了。共同审查的参与确认和暂停转押回执都已送到。马泰奥仍在圣马丁北门候核。"),
                L(luca, "原件交走了，副本也能拦下这次转押？"),
                L(clerk, "证据副本保留了来源争议。威尼斯与法国已具名参加，监押书记官确认暂停；粮站表决也已暂停。"),
                L(clerk, "原件另有具名交接凭据，登记去向是：{原件}。"),
                L(luca, "把这些结果留下。"),
                L(clerk, "我合上探视口，将处理回执和交接登记合页送进去。"));
            if (!flow.endorsedReport) flow.endorsedReport = E("act6_endorsed_report", "六点的转押登记", clerk,
                L(clerk, "六点的转押登记送回来了。卢卡，马泰奥已按原安排被转移。"),
                L(luca, "你们没有把他留下？"),
                L(clerk, "你为军令来源作了担保。本次交卷没有启动共同争议审查，也没有办成暂停转押的手续。"),
                L(luca, "他被送到哪里？"),
                L(clerk, "这份登记只记了转出，后续去向还在追查。换版证据仍在，事情没有到此结束。"),
                L(clerk, "原件交接也已登记：{原件}。我合上探视口，把回执送进去。"));
            if (!flow.reunionArrival) flow.reunionArrival = E("act6_reunion_arrival", "六点半的接回凭据", clerk,
                L(clerk, "六点半了。新的交接凭据刚到：马泰奥·里奇，已由三方见证人员共同接回。"),
                L(luca, "他在外面？"),
                L(clerk, "就在我身后。先把这份具名凭据交给你，我关上探视口，送件后请他过来。"));
            if (!flow.openEnding) flow.openEnding = E("act6_open", "开门", matteo,
                L(matteo, "卢卡。"), L(luca, "你的自述，我交上去了。"),
                L(matteo, "应该的。"), L(luca, "他们还会问你很多事。"),
                L(matteo, "这次我会自己答。"), L(luca, "我留下了原件，也留下了你的名字。"),
                L(matteo, "我知道。谢谢你让我有机会回来，把话说完。"),
                L(clerk, "见证手续办妥，封门的期限也到了。我先合上探视口，再解除门封。"));
            if (!flow.missingEnding) flow.missingEnding = E("act6_missing", "缺页", matteo,
                L(matteo, "卢卡。封套里那段自述，也交上去了吗？"),
                L(luca, "我烧了译出来的那张纸。"), L(matteo, "为什么？"),
                L(luca, "我怕他们拿着你的承认，就不肯让你回来。"),
                L(matteo, "信还在，签过的名字也还在。你不能替我说，事情已经讲清楚了。"),
                L(luca, "我只是想先保住你。"), L(matteo, "我回来了。剩下的事，让我自己回答。"),
                L(clerk, "见证手续办妥，封门的期限也到了。我先合上探视口，再解除门封。"));
            if (!flow.transferredEnding) flow.transferredEnding = E("act6_transferred", "真相易手", clerk,
                L(clerk, "凭据已经送到。{去向后果}"),
                L(luca, "这次转押停了。以后的事呢？"),
                L(clerk, "马泰奥还要留候核验。以后每一次对照原件，都要向持有者提出申请。"),
                L(luca, "我把证据送出去了，也把往后查证的路交到了别人手里。"),
                L(clerk, "行会会留好这些回执。还有新的消息，我仍从这里告诉你。"));
            if (!flow.endorsedEnding) flow.endorsedEnding = E("act6_endorsed", "背书", clerk,
                L(clerk, "转押登记已经送到。{去向后果}"),
                L(luca, "我的来源担保，也在他们手里。"),
                L(clerk, "是。原件上的换版记录没有消失，核验表上的签名也没有。"),
                L(luca, "师父曾经签下一个需要解释的名字。现在，我也一样。"),
                L(clerk, "行会会继续追查他的去向。你今夜作出的核验，也需要留下说明。"));

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
                "第17号委托 · 回执六之一\n清晨六点 / 行会登记送达\n\n威尼斯、法国已具名参与。\n米兰监押书记官确认：\n六点转押暂停，证人留候。\n粮站表决暂停，转共同审查。\n\n换版原件：继续封存。\n交接手续已办结，副本留档。", out flow.reportInk);
            if (!flow.reunionReceipt) flow.reunionReceipt = EpilogueReceipt(flow.reunionAnchor, "Epilogue reunion receipt", "证人接回 · 交接凭据",
                "第17号委托 · 回执六之二\n清晨六点半 / 新收件\n\n马泰奥·里奇\n已由三方见证人员共同接回。\n\n米兰、威尼斯、法国见证登记\n行会执事核对并送达。\n\n接回不免除后续说明责任。", out _);
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
