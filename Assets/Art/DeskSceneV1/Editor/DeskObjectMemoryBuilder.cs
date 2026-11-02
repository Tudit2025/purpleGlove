using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PurpleGlove.Editor
{
    public static class DeskObjectMemoryBuilder
    {
        [MenuItem("Purple Glove/Dialogue/Install First Interaction Memories")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            InstallInScene(Camera.main);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("OBJECT_MEMORIES_INSTALLED");
        }

        public static void InstallInScene(Camera camera)
        {
            var dialogue = camera ? camera.GetComponent<DialogueController>() : null;
            if (!dialogue) throw new InvalidOperationException("Install dialogue before object memories.");
            var luca = dialogue.speakers.Single(s => s && s.id == "luca");
            Add("Book_Charcoal", "memory_prayer_old", "旧版祷文集",
                "这随处可以买到的《祷文集》是我学习密码时使用的密码本，有不少密码是使用书本的。", dialogue, luca);
            Add("Book_SevenPetal", "memory_prayer_flower", "七瓣花版祷文集",
                "这本七瓣花版《祷文集》是老师定制的版本，在行会内部使用过。", dialogue, luca);
            Add("Notebook_Open", "memory_notebook", "师父的旧笔记",
                "这是老师的笔记本，应该是他的备课笔记。", dialogue, luca);
            Add("PreservationBox", "memory_toolbox", "解密工具箱",
                "我的解密工具盒，对于一些常见的密码，可以使用工具简化破译流程。", dialogue, luca);
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            if (archive && archive.acts[0].documents.Length == 2)
                InstallFirstActMemory(archive.acts[0].documents[1], dialogue);
        }

        public static void InstallFirstActMemory(SceneObject paper, DialogueController dialogue)
        {
            if (!paper || !dialogue) return;
            Add(paper.gameObject, "memory_three_cuts", "师父的三道裁口",
                "师傅的笔迹，侧面有三道很刻意的裁口，这个可能是凯撒密码。",
                dialogue, dialogue.speakers.Single(s => s && s.id == "luca"));
            paper.GetComponent<FirstInteractionMemory>().duringInspection = true;
        }

        static void Add(string objectName, string id, string title, string line, DialogueController dialogue, DialogueSpeaker luca)
        {
            var item = GameObject.Find(objectName);
            if (!item || !item.GetComponent<SceneObject>()) throw new InvalidOperationException("Missing interactive prop: " + objectName);
            Add(item, id, title, line, dialogue, luca);
        }

        static void Add(GameObject item, string id, string title, string line, DialogueController dialogue, DialogueSpeaker luca)
        {
            string path = DeskDialogueBuilder.DataRoot + "/Events/" + id + ".asset";
            var sequence = AssetDatabase.LoadAssetAtPath<DialogueEvent>(path);
            if (!sequence)
            {
                sequence = ScriptableObject.CreateInstance<DialogueEvent>();
                sequence.id = id; sequence.title = "初见回忆 · " + title;
                sequence.context = "首次点击物品时，卢卡回忆学习经历；读完自动继续原有交互。本次游玩仅播放一次。";
                sequence.arrivalDelay = 0.1f;
                sequence.lines = new[] { new DialogueEvent.Line { speaker = luca, text = line } };
                AssetDatabase.CreateAsset(sequence, path);
            }
            var memory = item.GetComponent<FirstInteractionMemory>();
            if (!memory) memory = item.AddComponent<FirstInteractionMemory>();
            memory.dialogue = dialogue;
            if (!memory.memory) memory.memory = sequence;
            EditorUtility.SetDirty(memory);
        }
    }
}
