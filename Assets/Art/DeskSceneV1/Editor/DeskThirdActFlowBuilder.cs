using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PurpleGlove.Editor
{
    public static partial class DeskSceneBuilder
    {
        static void ConfigureThirdAct(Camera camera)
        {
            var records = camera.GetComponent<PlaintextArchiveController>();
            if (!records || !records.archive || records.archive.acts[2].documents.Length < 4) return;
            var dialogue = camera.GetComponent<DialogueController>();
            var flow = camera.GetComponent<ThirdActFlow>() ?? camera.gameObject.AddComponent<ThirdActFlow>();
            flow.records = records; flow.archive = records.archive; flow.dialogue = dialogue;
            flow.delivery = camera.GetComponent<SealedDeliveryController>(); flow.story = camera.GetComponent<PrologueSequence>();
            flow.bell = UnityEngine.Object.FindFirstObjectByType<DeskHandbell>();
            flow.verification = records.archive.acts[2].documents[2].GetComponent<ThirdActVerification>();
            flow.verification.flow = flow;
            flow.bell.consumePermissionOnRing = true; flow.bell.ringAllowed = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(flow.bell);
            var luca = dialogue.speakers.Single(s => s.id == "luca");
            var clerk = dialogue.speakers.Single(s => s.id == "clerk");
            var charles = dialogue.speakers.Single(s => s.id == "charles");
            var secretary = dialogue.speakers.Single(s => s.id == "ducal_secretary");
            var carafa = dialogue.speakers.Single(s => s.id == "carafa");
            DialogueEvent.Line L(DialogueSpeaker s, string t) => new DialogueEvent.Line { speaker = s, text = t };
            if (!flow.charles) flow.charles = ThirdActEvent("act3_charles", "第三幕 · 夏尔门边交谈", charles,
                L(clerk, "法国代表夏尔已登记，获准就这条越境命令的来源答复。"),
                L(charles, "那张纸上若写着法国将在黎明越境，请先不要替它背书。"),
                L(luca, "我签的是明文还原，来源还没有确认。你否认它，是因为它是假的，还是因为它提前到了我手里？"),
                L(charles, "这个问题值得问。我能给你一份具名否认，并接受与你的原件当面对照。至于你凭什么相信它，该由你找。"),
                L(luca, "那你为什么不立即公开？"),
                L(charles, "因为我与公爵谈过商路。如果没有具体文件，我公开否认一项没人具名承认的军令，只会替它传遍全城。"),
                L(charles, "我把能承担的事写在补充凭条上，具名交给你。正式反证仍须依据有来源记录的具体文件。"),
                L(clerk, "夏尔的补充凭条已登记，附入第十七号委托，批号七—〇三一。合上观察窗后，从传物舱送入。"));
            if (!flow.cue) flow.cue = ThirdActEvent("act3_ring_hint", "第三幕 · 卢卡提示再次调档", null,
                L(luca, "夏尔的补充凭条也收好了。正式案号和批号都有了，现在可以摇铃，请执事调取原始签收页。"));
            if (!flow.request) flow.request = ThirdActEvent("act3_request", "第三幕 · 第二次门边调档", clerk,
                L(clerk, "卢卡，你摇铃了。需要调取哪份原件？"),
                L(luca, "请按检索单调取第七号封档的原始签收页。对应米兰内廷第十七号委托，装订批号七—〇三一。"),
                L(clerk, "案号与批号都和第七号封档的交存记录相符。原始签收页须两位保管见证人共同启封，黎明前送入。你会拿到带出库记录的原件。"),
                L(luca, "请照这个手续办理。"));
            if (!flow.secretary) flow.secretary = ThirdActEvent("act3_secretary", "第三幕 · 书记官要求归还原件", secretary,
                L(secretary, "明文既然完整，原来的工作纸就交回来吧。只留一份清洁的译文。"),
                L(luca, "原件须保留到来源核验结束。明文还原的签名，不是来源确认。"),
                L(secretary, "异议我会记下。公爵仍在等你的核验结果。"));
            if (!flow.carafa) flow.carafa = ThirdActEvent("act3_carafa", "第三幕 · 卡拉法门边交谈", carafa,
                L(clerk, "卡拉法已在前厅登记，等候代领原件。"),
                L(carafa, "印坊天天改版。年轻人，别把排字的旧账写成外交事件。"),
                L(luca, "是不是排字的旧账，要等原始记录送到再核对。现在这些纸还不能交走。"));
            if (!flow.summary) flow.summary = ThirdActEvent("act3_summary", "第三幕 · 卢卡幕末独白", null,
                L(luca, "师父设计过这套密码。"), L(luca, "师父签过这张封面。"),
                L(luca, "这两件事是真的。法国会不会越境，还没有因此变真。"));
            foreach (var sequence in new[] { flow.charles, flow.cue, flow.request, flow.secretary, flow.carafa, flow.summary })
                if (!dialogue.events.Contains(sequence)) dialogue.events = dialogue.events.Concat(new[] { sequence }).ToArray();
            EditorUtility.SetDirty(flow); EditorUtility.SetDirty(flow.verification);
            EditorUtility.SetDirty(flow.bell); EditorUtility.SetDirty(dialogue);
        }

        static DialogueEvent ThirdActEvent(string id, string title, DialogueSpeaker visitor, params DialogueEvent.Line[] lines)
        {
            string path = DeskDialogueBuilder.DataRoot + "/Events/" + id + ".asset";
            var sequence = AssetDatabase.LoadAssetAtPath<DialogueEvent>(path);
            if (sequence) return sequence;
            sequence = ScriptableObject.CreateInstance<DialogueEvent>();
            sequence.id = id; sequence.title = title; sequence.initialVisitor = visitor; sequence.lines = lines;
            sequence.context = "doc/ph-revised.md 第三幕：明文还原签名、夏尔补充凭条、摇铃调档、两次来访、桌前独白，随后进入第四幕。";
            sequence.closeShutterOnComplete = true;
            sequence.arrivalSound = visitor && id != "act3_request"
                ? AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/PH/Doors/door_knock_pattern_01.wav") : null;
            sequence.arrivalDelay = sequence.arrivalSound ? sequence.arrivalSound.length : 0.1f;
            AssetDatabase.CreateAsset(sequence, path); return sequence;
        }
    }
}
