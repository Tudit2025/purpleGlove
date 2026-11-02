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
                L(clerk, "法国代表夏尔来找你。"),
                L(charles, "你好，大师。如果那个密码说什么法国将要越境，请你不要为它担保。"),
                L(charles, "如果你能提供什么有利的物证给我，我会全力保护你的师傅的。"),
                L(luca, "这上面不是你的签名吗？如果这是伪造的签名，你为什么不直接说？"),
                L(charles, "这确实是我的签名，但是我不能让人知道我确实跟米兰人签了“某个”文件。所以我不能说。"),
                L(charles, "我承诺的事情我会签名写给你，请你好好考虑一下我今天来说的事。"),
                L(clerk, "凭条已登记，附入第十七号委托，批号七二六。合上观察窗后，从传物舱送入。"));
            if (!flow.secretary) flow.secretary = ThirdActEvent("act3_secretary", "第三幕 · 书记官要求归还原件", clerk,
                L(clerk, "米兰代表来找你了。"),
                L(secretary, "卢卡大师，你已经完成破译了吧，既然没什么问题，把原件还给我们吧。"),
                L(luca, "原件我需要留到来源核验环节结束。"),
                L(secretary, "……"),
                L(secretary, "公爵在等着你的核验结果，麻烦你加快进度了。"));
            if (!flow.carafa) flow.carafa = ThirdActEvent("act3_carafa", "第三幕 · 卡拉法门边交谈", carafa,
                L(clerk, "米兰代表卡拉法来了。"),
                L(carafa, "没必要搞的这么复杂，年轻人，别把排字的旧账写成外交事件。"));
            if (!flow.summary) flow.summary = ThirdActEvent("act3_summary", "第三幕 · 卢卡幕末独白", null,
                L(luca, "确实是师傅做的军情译录和加密，没人提醒他这个法文件也不是原件吗？"),
                L(luca, "难道我想的是错的，换档的事情其实没问题？只有档案到了才能确定了。"),
                L(luca, "米兰来的人很急，不知道后面还会做什么。"));
            dialogue.events = dialogue.events.Where(e => e && e.id != "act3_ring_hint" && e.id != "act3_request").ToArray();
            foreach (var sequence in new[] { flow.charles, flow.secretary, flow.carafa, flow.summary })
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
            sequence.context = "第三幕：明文还原签名、夏尔补充凭条、短暂停顿、两次来访、桌前独白，随后进入第四幕。原始档案已在第二幕申请。";
            sequence.closeShutterOnComplete = true;
            sequence.arrivalSound = visitor && id != "act3_request"
                ? AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/PH/Doors/door_knock_pattern_01.wav") : null;
            sequence.arrivalDelay = sequence.arrivalSound ? sequence.arrivalSound.length : 0.1f;
            AssetDatabase.CreateAsset(sequence, path); return sequence;
        }
    }
}
