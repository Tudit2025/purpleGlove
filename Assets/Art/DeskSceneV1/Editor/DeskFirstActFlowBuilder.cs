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
                L(clerk, "卢卡，公爵的核验委托送到了，还有马泰奥……马泰奥大师离开前寄存的信件。"),
                L(clerk, "棕色包裹里有会长给你准备的资料，会长让我嘱托你一定要看。"),
                L(clerk, "会长说如果你有什么需求全力帮助你，如果你要找我，摇一下桌子上的手铃就可以了。"),
                L(clerk, "两件都登记好了。我从下面这个活版门转进去给你。"));
            opening.closeShutterOnComplete = true;
            opening.context = "doc/ph-revised.md 第一幕开场；入场五秒后敲门，音效结束后交谈，观察窗完全关闭后送件。";
            EditorUtility.SetDirty(opening);
            var introduction = Event("act1_secretary_arrival", "执事介绍米兰代表", clerk,
                L(clerk, "卢卡，米兰代表，米兰公爵的书记官到了。"));
            introduction.closeShutterOnComplete = true;
            var visit = Event("act1_secretary", "第一幕 · 首次门边交谈", secretary,
                new DialogueEvent.Line { speaker = secretary, text = "卢卡大师，需要核验的文件马上就到了，事关重大，请您务必确保文件内容和来路都没有问题。", choices = new[] { new DialogueEvent.Choice { id = "ask_matteo", text = "解密大师马泰奥现在在哪里？", nextLine = 1 }, new DialogueEvent.Choice { id = "no_guarantee", text = "我现在什么都没看到，不能给你保证。", nextLine = 2 }, new DialogueEvent.Choice { id = "silence", text = "……", nextLine = 3 } } },
                new DialogueEvent.Line { speaker = secretary, text = "马泰奥大师现在在协助我们的保密工作。", endEvent = true },
                new DialogueEvent.Line { speaker = secretary, text = "当然，当然，请您按规矩来。", endEvent = true },
                new DialogueEvent.Line { speaker = secretary, text = "您没有意见自然是很好的。", endEvent = true });
            visit.arrivalSound = null; visit.arrivalDelay = 0.1f; visit.closeShutterOnComplete = true;
            EditorUtility.SetDirty(introduction); EditorUtility.SetDirty(visit);
            var summary = Event("act1_summary", "第一幕 · 卢卡桌前总结", null,
                L(luca, "师傅让我留下原件，是指还没送来的军令吗？"),
                L(luca, "文件还没到米兰就派人来催了。师傅在他们手里。"),
                L(luca, "七瓣花应该是指新装订的祷言集，特别装订版，显然是当作密码本用的。"));
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
