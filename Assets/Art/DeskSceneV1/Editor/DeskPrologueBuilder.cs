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
                    L(luca, "我们在印务行会当差，日常译校书册，也经手往来密信。"),
                    L(luca, "前几日，师父应召去替米兰公爵办事，至今未归，下落不明。"),
                    L(luca, "坊间传言他背叛行会，替公爵伪造了文书。"),
                    L(luca, "如今，行会接下一桩紧急委托，要核验一份牵涉多方的军令。米兰打算凭它接管威尼斯的边境粮站。"),
                    L(luca, "米兰、法国与威尼斯三方联名委托，要求行会核实密文与明文是否相符、来路是否可靠。六月十六日，也就是明早六点，核验结论将作为他们表决的凭据。"),
                    L(luca, "我作为替补的“解密大师”，被临时调来接手这桩差事。"),
                    L(luca, "我必须查清真相，把师父救出来。"),
                    L(clerk, "卢卡大师，核验室已经备妥了。"),
                    L(clerk, "按行会规程，您入内后房门便要贴上封条，期间不得出入，直到明早表决结束方能启封。"),
                    L(luca, "好，我这就进去。") };
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
