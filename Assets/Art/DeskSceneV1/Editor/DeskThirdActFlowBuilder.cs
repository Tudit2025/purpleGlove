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
            if (!flow.signatureHint) flow.signatureHint = ThirdActEvent("act3_signature_hint", "第三幕 · 卢卡提示明文签名", null,
                L(luca, "译文既已核对无误，我该先在军情核验表上签字，确认明文还原的结果。"));
            if (!flow.charles) flow.charles = ThirdActEvent("act3_charles", "第三幕 · 夏尔门边交谈", charles,
                L(clerk, "卢卡，法国代表夏尔先生求见。"),
                L(charles, "日安，大师。倘若那份密文声称法国将于六月十八日越境，请您切勿替它的来源作保。"),
                L(charles, "只要您能找出确凿的物证，法方定会全力护住令师的周全。"),
                L(luca, "原稿上不是有您的亲笔签名吗？倘若军令是伪造的，您为何不当众揭穿？"),
                L(charles, "那确是我的笔迹。但我不便向外界透露自己曾私下与米兰人签署过“另一份”文件，因此不能直接出面否认。"),
                L(charles, "方才的承诺，我会立下字据并亲笔署名。今夜所言，还望大师三思。"),
                L(clerk, "补充凭条已登记，并入第17号委托、批号七二六卷宗。观察窗合上后，我从传物舱递进来。"));
            if (!flow.secretary) flow.secretary = ThirdActEvent("act3_secretary", "第三幕 · 书记官要求归还原件", clerk,
                L(clerk, "米兰代表又来了。"),
                L(secretary, "卢卡大师，密文想来已经译出了吧？既然文字并无出入，还请将随附的原件交还敝方。"),
                L(luca, "按规程，原件须留存至来源核验完毕。现在不能交还。"),
                L(secretary, "……"),
                L(secretary, "孔蒂公爵正在等您的结论，还请大师加快进度，莫误了时辰。"));
            if (!flow.carafa) flow.carafa = ThirdActEvent("act3_carafa", "第三幕 · 卡拉法门边交谈", carafa,
                L(clerk, "米兰方面的卡拉法先生到了。"),
                L(carafa, "年轻人，何必把事情闹得这么复杂？不过是印坊里一笔排字换版的旧账，莫要写成外交风波。"));
            if (!flow.summary) flow.summary = ThirdActEvent("act3_summary", "第三幕 · 卢卡幕末独白", null,
                L(luca, "译录与加密确系师父手笔。可难道就没人提醒过他，那份所谓的法文原稿也有蹊跷吗？"),
                L(luca, "还是说我猜错了，印坊换版只是一桩寻常差错？眼下只能等内库的原始档案调来，才能见分晓了。"),
                L(luca, "米兰方面催得这样紧，不知天亮前还会使出什么手段。"));
            dialogue.events = dialogue.events.Where(e => e && e.id != "act3_ring_hint" && e.id != "act3_request").ToArray();
            foreach (var sequence in new[] { flow.signatureHint, flow.charles, flow.secretary, flow.carafa, flow.summary })
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
