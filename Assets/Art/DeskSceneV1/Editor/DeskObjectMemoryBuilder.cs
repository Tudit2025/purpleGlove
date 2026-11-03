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
                "这册随处可见的旧版《祷文集》，是我初学密码时常用的参照书。许多书本密码都以它为底本。", dialogue, luca);
            Add("Book_SevenPetal", "memory_prayer_flower", "七瓣花版祷文集",
                "这册七瓣花版《祷文集》是师父特意定制的版次，此前只在行会内部小范围启用过。", dialogue, luca);
            Add("Notebook_Open", "memory_notebook", "师父的旧笔记",
                "这是师父留下的旧笔记，里头记着他当年授课时用的密码教案。", dialogue, luca);
            Add("PreservationBox", "memory_toolbox", "解密工具箱",
                "我的解密工具箱。遇到常见的密码制式，借助里头的器具便能省去不少演算的工夫。", dialogue, luca);
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            if (archive && archive.acts[0].documents.Length == 2)
                InstallFirstActMemory(archive.acts[0].documents[1], dialogue);
        }

        public static void InstallFirstActMemory(SceneObject paper, DialogueController dialogue)
        {
            if (!paper || !dialogue) return;
            Add(paper.gameObject, "memory_three_cuts", "师父的三道裁口",
                "是师父的笔迹。纸页侧边有三道刻意的裁口……应当是凯撒密码。",
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
