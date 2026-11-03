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
                secretary.eyes = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Dialogue/Textures/visitor_duke.png");
                secretary.eyeDescription = "公爵书记官；灰眉、深眼窝的兜帽肖像，与行会执事区分。";
                AssetDatabase.CreateAsset(secretary, secretaryPath);
            }
            if (!dialogue.speakers.Contains(secretary)) dialogue.speakers = dialogue.speakers.Concat(new[] { secretary }).ToArray();
            DialogueEvent.Line L(DialogueSpeaker speaker, string text) => new DialogueEvent.Line { speaker = speaker, text = text };
            var opening = Event("act1_arrival", "第一幕 · 执事交付说明", clerk,
                L(clerk, "卢卡，公爵府的核验委托送到了。另外还有马泰奥……马泰奥大师临行前寄存的信件。"),
                L(clerk, "棕色封包里是会长为您备下的卷宗，他老人家特意叮嘱，请您务必过目。"),
                L(clerk, "会长交代过，您若有任何需要，行会定当全力协助。有事唤我，摇响案头的手铃便是。"),
                L(clerk, "两份物件都已登记在册，我这就从底下的传物舱递进来。"));
            opening.closeShutterOnComplete = true;
            opening.context = "doc/ph-revised.md 第一幕开场；入场五秒后敲门，音效结束后交谈，观察窗完全关闭后送件。";
            EditorUtility.SetDirty(opening);
            var introduction = Event("act1_secretary_arrival", "执事介绍米兰代表", clerk,
                L(clerk, "卢卡，米兰方面的代表，孔蒂公爵的书记官到了。"));
            introduction.closeShutterOnComplete = true;
            var visit = Event("act1_secretary", "第一幕 · 首次门边交谈", secretary,
                new DialogueEvent.Line { speaker = secretary, text = "卢卡大师，待核验的公文稍后即到。此事干系重大，还请您务必确保文书内容与来路皆无纰漏。", choices = new[] { new DialogueEvent.Choice { id = "ask_matteo", text = "我师父马泰奥人在哪里？", nextLine = 1 }, new DialogueEvent.Choice { id = "no_guarantee", text = "文书尚未送到，我无法作保。", nextLine = 2 }, new DialogueEvent.Choice { id = "silence", text = "……", nextLine = 3 } } },
                new DialogueEvent.Line { speaker = secretary, text = "马泰奥大师目前正在协助敝方处理机密要务。", endEvent = true },
                new DialogueEvent.Line { speaker = secretary, text = "自然，自然。一切请按行会的规矩办。", endEvent = true },
                new DialogueEvent.Line { speaker = secretary, text = "您没有异议，那就再好不过了。", endEvent = true });
            visit.arrivalSound = null; visit.arrivalDelay = 0.1f; visit.closeShutterOnComplete = true;
            EditorUtility.SetDirty(introduction); EditorUtility.SetDirty(visit);
            var summary = Event("act1_summary", "第一幕 · 卢卡桌前总结", null,
                L(luca, "师父叮嘱我“保留原件”，指的会是那份还没送到的军令吗？"),
                L(luca, "正件未至，米兰的人倒先来催促施压了。看来师父果真落在他们手里。"),
                L(luca, "“七瓣花”指的应当是那册新装订的《祷文集》。既是特制版次，显然是作密码本用的。"));
            foreach (var sequence in new[] { opening, introduction, visit, summary })
                if (!dialogue.events.Contains(sequence)) dialogue.events = dialogue.events.Concat(new[] { sequence }).ToArray();
            var flow = camera.GetComponent<FirstActFlow>();
            if (!flow) flow = camera.gameObject.AddComponent<FirstActFlow>();
            flow.records = records; flow.archive = records.archive; flow.dialogue = dialogue; flow.story = story; flow.delivery = delivery;
            if (!flow.openingVisit) flow.openingVisit = opening;
            if (!flow.firstVisit) flow.firstVisit = visit;
            if (!flow.secretaryIntroduction) flow.secretaryIntroduction = introduction;
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
