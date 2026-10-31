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
                    L(luca, "我叫卢卡，是马泰奥·里奇的学生。"),
                    L(luca, "我跟他学了七年密码，也替行会处理过往来的密信。"),
                    L(luca, "前几天，师父去替米兰公爵办事，没有回来。公爵的人说，他得留下配合核验。连留在哪里，也不肯告诉我。"),
                    L(luca, "街上已经有人说，他替公爵改过军令。"),
                    L(luca, "现在，行会收到一份密文，声称法国即将越境。米兰要凭它接管威尼斯的边境粮站。明早六点，他们就会在会上作决定。"),
                    L(luca, "这份密文用的是师父编的密码。我参与整理过那套表，所以行会让我来还原文字、查清来路。三方都会复核我的结果。"),
                    L(luca, "我想证明那些传言是错的。"),
                    L(luca, "可我还没有见过那份文件，也没听到师父亲口解释。"),
                    L(clerk, "卢卡，房间备好了。"),
                    L(clerk, "进去以后封门，明早六点开封。期间文件走侧口，每次交接都有登记。没查清的，你可以不签。"),
                    L(luca, "让我进去。") };
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
