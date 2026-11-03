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
                L(clerk, "威尼斯与法国代表见到您的核验报告后，当场要求暂停粮站表决。卡拉法已被责令留候彻查。"),
                L(clerk, "印坊换版的内情已经传开。米兰方面拒绝公开对质，好在换版原件仍封存在咱们行会手里。"),
                L(clerk, "米兰人松了口，承认马泰奥大师目前就在圣马丁修道院北门。"),
                L(luca, "他们放弃转移师父了？"),
                L(clerk, "是的。另外还有几份晨会的处理回执，我这就递进来。"));
            if (!flow.transferredReport) flow.transferredReport = E("act6_transfer_report", "暂停转押与原件交接", clerk,
                L(clerk, "卢卡，我回来了。"),
                L(clerk, "威尼斯与法国代表见到您的核验报告后，当场要求暂停粮站表决。卡拉法已被责令留候彻查。"),
                L(clerk, "印坊换版的内情已经传开，那份换版原件也已按您的申请转交出去了。"),
                L(clerk, "这里有几份晨会的处理回执，我给您递进来。"));
            if (!flow.endorsedReport) flow.endorsedReport = E("act6_endorsed_report", "六点的转押登记", clerk,
                L(clerk, "卢卡，米兰人已经把马泰奥大师转移走了。"),
                L(luca, "怎么会？你们没拦住吗？"),
                L(clerk, "您在核验表上替军令的来源签了担保。今早的晨会上，米兰方面占尽了上风。"),
                L(luca, "他们把师父押去哪里了？"),
                L(clerk, "还不清楚，行会正在设法打听。"),
                L(clerk, "这是晨会的处理回执，您过目吧。"));
            if (!flow.reunionArrival) flow.reunionArrival = E("act6_reunion_arrival", "六点半的接回凭据", clerk,
                L(clerk, "卢卡，三方见证人已经一同将马泰奥·里奇大师接回来了。"),
                L(luca, "师父就在门外？"),
                L(clerk, "正是，我这就请他过来。"));
            if (!flow.openEnding) flow.openEnding = E("act6_open", "开门", matteo,
                L(matteo, "卢卡。"),
                L(luca, "师父，我把您的自述呈交上去了。"),
                L(matteo, "做得对。"),
                L(luca, "接下来的审查，他们只怕还会向您追问许多细节。"),
                L(matteo, "我明白。谢谢你，卢卡。"),
                L(matteo, "往后的路还长着呢……"));
            if (!flow.missingEnding) flow.missingEnding = E("act6_missing", "缺页", matteo,
                L(matteo, "卢卡，我附在封套里的那封自述，你交上去了吗？"),
                L(luca, "我最后把它烧了。"),
                L(matteo, "为何要烧？"),
                L(luca, "那张纸一旦公开，您就彻底脱不开身了，行会日后也难免受牵连。"),
                L(matteo, "……"),
                L(matteo, "罢了。缺失的那一页，日后我会亲自向审查会说明白。"),
                L(clerk, "交接手续都办妥了。卢卡，我这就为您启封开门。"));
            if (!flow.transferredEnding) flow.transferredEnding = E("act6_transferred", "真相易手", clerk,
                L(clerk, "{去向后果}"),
                L(luca, "那师父接下来会怎样？"),
                L(clerk, "马泰奥大师仍须留候审查。只是如今原件易手，日后若要核对底稿，都得看持有方的意思了。"),
                L(clerk, "行会会妥善留存这些交接回执。一旦有新的消息，我会再来通知您。"));
            if (!flow.endorsedEnding) flow.endorsedEnding = E("act6_endorsed", "背书", clerk,
                L(clerk, "{去向后果}"),
                L(luca, "我不该替米兰人作保的……这一签，反倒让我们彻底落入被动了。"),
                L(luca, "当初师父迫于胁迫签下名字，酿成大祸；到头来，我竟也在纸上犯下了同样的错。"),
                L(clerk, "行会还会继续追查令师的下落。若有消息，我会再来告知。"));

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
