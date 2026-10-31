using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PurpleGlove.Editor
{
    public static class DeskFirstActFlowBuilder
    {
        [MenuItem("Purple Glove/Acts/Install First Act Progression")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            InstallInScene(Camera.main);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("FIRST_ACT_PROGRESSION_INSTALLED");
        }

        public static void InstallInScene(Camera camera)
        {
            var records = camera ? camera.GetComponent<PlaintextArchiveController>() : null;
            var dialogue = camera ? camera.GetComponent<DialogueController>() : null;
            var story = camera ? camera.GetComponent<PrologueSequence>() : null;
            var delivery = camera ? camera.GetComponent<SealedDeliveryController>() : null;
            if (!records || !dialogue || !story || !delivery || !records.archive || !dialogue.arrivalAudio)
                throw new InvalidOperationException("Install plaintext records, dialogue, transitions and delivery first.");
            var luca = dialogue.speakers.Single(s => s.id == "luca");
            var clerk = dialogue.speakers.Single(s => s.id == "clerk");
            const string secretaryPath = DeskDialogueBuilder.DataRoot + "/Speakers/ducal_secretary.asset";
            var secretary = AssetDatabase.LoadAssetAtPath<DialogueSpeaker>(secretaryPath);
            if (!secretary)
            {
                secretary = UnityEngine.Object.Instantiate(clerk);
                secretary.id = "ducal_secretary"; secretary.displayName = "公爵书记官";
                secretary.eyeDescription = "公爵书记官；沿用现有书记员眼部及打字音素材，可独立替换。";
                AssetDatabase.CreateAsset(secretary, secretaryPath);
            }
            if (!dialogue.speakers.Contains(secretary)) dialogue.speakers = dialogue.speakers.Concat(new[] { secretary }).ToArray();
            DialogueEvent.Line L(DialogueSpeaker speaker, string text) => new DialogueEvent.Line { speaker = speaker, text = text };
            var opening = Event("act1_arrival", "第一幕 · 执事交付说明", clerk,
                L(clerk, "卢卡，公爵的核验委托送到了。白色包裹里是正式委托，请分别填写‘明文还原’与‘来源确认’，不得混签。"),
                L(clerk, "棕色包裹是行会交付的文件：马泰奥先生离开前寄存的封袋，登记时间在他被扣押之前。"),
                L(clerk, "两件都登记好了。我合上观察窗后，从侧面的传物舱送进去，请接收。"));
            opening.closeShutterOnComplete = true;
            opening.context = "doc/ph-revised.md 第一幕开场；入场五秒后敲门，音效结束后交谈，观察窗完全关闭后送件。";
            EditorUtility.SetDirty(opening);
            var visit = Event("act1_secretary", "第一幕 · 首次门边交谈", secretary,
                L(secretary, "老师的事，等核验完再谈。请把‘内容读得通’和‘文件没问题’都写清楚。"),
                L(luca, "这是两件事。"),
                L(secretary, "当然。请按规程办。"),
                L(clerk, "已经晚十点了。下一份封件正在登记，请备好新的蜡烛。"));
            var summary = Event("act1_summary", "第一幕 · 卢卡桌前总结", null,
                L(luca, "师父没有说自己无辜。他只让我留下原件。"),
                L(luca, "读得通，只能说明字母排对了，不能证明这份文件是真的。"),
                L(luca, "原件留好。接下来，先查清那本七瓣花的新版。"));
            foreach (var sequence in new[] { opening, visit, summary })
                if (!dialogue.events.Contains(sequence)) dialogue.events = dialogue.events.Concat(new[] { sequence }).ToArray();
            var flow = camera.GetComponent<FirstActFlow>();
            if (!flow) flow = camera.gameObject.AddComponent<FirstActFlow>();
            flow.records = records; flow.archive = records.archive; flow.dialogue = dialogue; flow.story = story; flow.delivery = delivery;
            if (!flow.openingVisit) flow.openingVisit = opening;
            if (!flow.firstVisit) flow.firstVisit = visit;
            if (!flow.deskSummary) flow.deskSummary = summary;
            if (!flow.secondActIntro) flow.secondActIntro = dialogue.events.Single(e => e.id == "beatrice");
            var audioRoot = dialogue.arrivalAudio.transform.Find("First act delayed knock");
            if (!audioRoot)
            {
                audioRoot = new GameObject("First act delayed knock").transform;
                audioRoot.SetParent(dialogue.arrivalAudio.transform, false);
            }
            var audio = audioRoot.GetComponent<AudioSource>();
            if (!audio) audio = audioRoot.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false; audio.spatialBlend = dialogue.arrivalAudio.spatialBlend;
            audio.volume = dialogue.arrivalAudio.volume; audio.minDistance = dialogue.arrivalAudio.minDistance;
            audio.maxDistance = dialogue.arrivalAudio.maxDistance; audio.dopplerLevel = 0;
            flow.knockAudio = audio;
            EditorUtility.SetDirty(flow); EditorUtility.SetDirty(dialogue);
        }

        static DialogueEvent Event(string id, string title, DialogueSpeaker visitor, params DialogueEvent.Line[] lines)
        {
            string path = DeskDialogueBuilder.DataRoot + "/Events/" + id + ".asset";
            var sequence = AssetDatabase.LoadAssetAtPath<DialogueEvent>(path);
            if (sequence) return sequence;
            sequence = ScriptableObject.CreateInstance<DialogueEvent>();
            sequence.id = id; sequence.title = title; sequence.lines = lines; sequence.initialVisitor = visitor;
            sequence.context = "doc/ph-revised.md 第一幕幕末；FirstActFlow 按留存、敲门、交谈、桌前总结的顺序触发。";
            sequence.arrivalSound = visitor ? AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/PH/Doors/door_knock_pattern_01.wav") : null;
            sequence.arrivalDelay = sequence.arrivalSound ? sequence.arrivalSound.length : 0.1f;
            AssetDatabase.CreateAsset(sequence, path); return sequence;
        }
    }
}
