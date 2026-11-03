using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PurpleGlove.Editor
{
    public static class DeskPrologueBuilder
    {
        [MenuItem("Purple Glove/Dialogue/Install Prologue and Act Transitions")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            InstallInScene(Camera.main);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("PROLOGUE_INSTALLED");
        }

        public static void InstallInScene(Camera camera)
        {
            var dialogue = camera ? camera.GetComponent<DialogueController>() : null;
            if (!dialogue) throw new InvalidOperationException("Install the dialogue system before the prologue.");
            var luca = dialogue.speakers.Single(s => s.id == "luca");
            var clerk = dialogue.speakers.Single(s => s.id == "clerk");
            const string path = DeskDialogueBuilder.DataRoot + "/Events/prologue.asset";
            var sequence = AssetDatabase.LoadAssetAtPath<DialogueEvent>(path);
            if (!sequence)
            {
                sequence = ScriptableObject.CreateInstance<DialogueEvent>();
                sequence.id = "prologue"; sequence.title = "第零幕 · 门槛";
                sequence.context = "doc/ph-revised.md：第零幕。门外固定机位；主角独白与画外执事；最后一句推进后进入工作间。";
                sequence.arrivalDelay = 0.65f;
                DialogueEvent.Line L(DialogueSpeaker speaker, string text) => new DialogueEvent.Line { speaker = speaker, text = text };
                sequence.lines = new[] {
                    L(luca, "我是解密大师马泰奥·里奇的学生。"),
                    L(luca, "我们为印刷行会工作，翻译书本，处理来往密信。"),
                    L(luca, "前几天，师父去替米兰公爵办事，结果一直没有回来，下落不明。"),
                    L(luca, "街上有人说他背叛了行会，为公爵伪造书信。"),
                    L(luca, "现在，行会收到一个紧急核验委托，调查一份许多势力牵扯的军令。米兰要凭它接管威尼斯的边境粮站。"),
                    L(luca, "米兰、法兰西、威尼斯共同委托，要求行会确认密文明文匹配，流程来路可靠。六月十六日，也就是明早六点，核验结果将作为他们表决的凭据。"),
                    L(luca, "我作为替补“解密大师”，被调来处理这个委托。"),
                    L(luca, "我一定要弄清楚事情的真相，把师傅救出来。"),
                    L(clerk, "卢卡大师，房间准备好了。"),
                    L(clerk, "根据流程，你进去之后门会贴封条，届时你不能自由出入了。到明天表决结束才会开门。"),
                    L(luca, "好的，我要进去了。") };
                AssetDatabase.CreateAsset(sequence, path);
            }
            var curtain = camera.GetComponent<ActTransitionOverlay>();
            if (!curtain) curtain = camera.gameObject.AddComponent<ActTransitionOverlay>();
            curtain.font = camera.GetComponent<DialogueOverlay>().font;
            var story = camera.GetComponent<PrologueSequence>();
            if (!story) story = camera.gameObject.AddComponent<PrologueSequence>();
            story.prologue = sequence;
            story.doorLeaf = GameObject.Find("DoorLeaf")?.transform;
            if (!story.doorLeaf) throw new InvalidOperationException("Missing doorway for the prologue.");
            var lightRoot = story.doorLeaf.parent.Find("Prologue corridor bounce");
            if (!lightRoot)
            {
                lightRoot = new GameObject("Prologue corridor bounce").transform;
                lightRoot.SetParent(story.doorLeaf.parent, false);
                lightRoot.position = story.doorLeaf.TransformPoint(new Vector3(-0.12f, 1.62f, 0.95f));
                var light = lightRoot.gameObject.AddComponent<Light>();
                light.type = LightType.Point; light.color = new Color(1f, 0.76f, 0.49f);
                light.intensity = 1.2f; light.range = 2.6f; light.cullingMask = 1 << 8;
                light.shadows = LightShadows.Soft; light.renderMode = LightRenderMode.ForcePixel; light.enabled = false;
            }
            story.exteriorLight = lightRoot.GetComponent<Light>();
            story.doorOpeningSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/PH/Doors/wood_door_open_01.wav");
            story.doorClosingSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/PH/Doors/wood_door_close_01.wav");
            EditorUtility.SetDirty(story); EditorUtility.SetDirty(curtain);
        }
    }
}
