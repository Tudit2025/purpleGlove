using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PurpleGlove.Editor
{
    public static partial class DeskSceneBuilder
    {
        [MenuItem("Purple Glove/Acts/Install Plaintext Archives")]
        public static void InstallPlaintextArchives()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            LoadSavedSceneAssets(); BuildPlaintextArchives(Camera.main);
            var slip = Camera.main.GetComponent<CipherWorkbench>().plaintextInk.transform.parent;
            var surface = slip.Find("Cipher_PlaintextSlip");
            if (surface) UnityEngine.Object.DestroyImmediate(surface.gameObject);
            BuildPlaintextWritingSurface(slip);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("PLAINTEXT_ARCHIVES_INSTALLED");
        }

        static void BuildPlaintextArchives(Camera camera)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Install plaintext records outside Play Mode.");
            var work = camera.GetComponent<CipherWorkbench>();
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            var dialogue = camera.GetComponent<DialogueController>();
            if (!work || !archive || !dialogue) throw new InvalidOperationException("Install workbench, archive and dialogue first.");
            var controller = camera.GetComponent<PlaintextArchiveController>() ?? camera.gameObject.AddComponent<PlaintextArchiveController>();
            controller.workbench = work; controller.archive = archive; controller.dialogue = dialogue;
            string path = DeskDialogueBuilder.DataRoot + "/Events/plaintext_complete.asset";
            var line = AssetDatabase.LoadAssetAtPath<DialogueEvent>(path);
            if (!line)
            {
                line = ScriptableObject.CreateInstance<DialogueEvent>();
                line.id = "plaintext_complete"; line.title = "卢卡 · 明文完成"; line.arrivalDelay = 0.1f;
                line.lines = new[] { new DialogueEvent.Line { speaker = dialogue.speakers.Single(s => s.id == "luca"), text = "译出来了。得把这份明文妥善留档。" } };
                AssetDatabase.CreateAsset(line, path);
            }
            controller.completionLine = line;
            // No fifth-act answer: its final replies are not another cipher solution.
            string[][] records =
            {
                new[] { "1", "caesar", "KEEP THE ORIGINAL", "保留原件。" },
                new[] { "2", "book", "REQUEST CASE FIFTY SEVEN", "申请调阅第五十七号封档。" },
                new[] { "2", "pigpen", "LEFT GLOVE", "左手手套。" },
                new[] { "3", "vigenere", "FRANCE WILL CROSS ON JUNE EIGHTEEN", "法国将于六月十八日越境。" },
                new[] { "4", "affine", "NORTH GATE SAINT MARTIN", "圣马丁，北门。" },
                new[] { "4", "rail", "I SIGNED THE FALSE ORDER TO FREE BARTOLOS FAMILY. I CAME FOR THE PROOF AND WAS HELD. DO NOT ERASE MY NAME.",
                    "我为救巴尔托洛一家脱身，在假命令上\n签了名。后为取回证据而来，反遭扣押。\n不要抹去我的名字。" }
            };
            var old = archive.transform.Find("Solved plaintext records");
            if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var group = new GameObject("Solved plaintext records").transform; group.SetParent(archive.transform, false);
            var mat = GameObject.Find("WritingMat").transform;
            actDocumentFont = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/SourceHanSerifCN-Regular.otf");
            controller.entries = records.Select(record =>
            {
                int act = int.Parse(record[0]);
                var paper = Place("Paper_Blank", group, Vector3.zero);
                paper.name = "Act" + act + "_Plaintext_" + record[1];
                float x = act == 2 || act == 4 ? (record[1] == "book" || record[1] == "affine" ? -0.15f : 0.15f) : 0;
                paper.transform.position = mat.TransformPoint(V(x, 0.014f, -0.477f));
                paper.transform.rotation = mat.rotation; paper.transform.localScale = Vector3.one * 0.33f;
                var ink = FirstActInk(paper.transform, "Preserved plaintext", PlaintextArchiveController.WrapAnswer(record[2]), documentFont,
                    -0.275f, 0.083f, 0.55f, 0.105f);
                ink.characterSize = Mathf.Min(ink.characterSize, 0.0045f);
                var translation = FirstActInk(paper.transform, "Translation", record[3], HandwrittenChinese, -0.275f, -0.048f, 0.55f, 0.092f);
                translation.characterSize = Mathf.Min(translation.characterSize, 0.0024f);
                ink.text = ""; // Never reveal the solution before the player finishes it.
                var item = paper.GetComponent<SceneObject>();
                item.screenFill = 0.82f; item.minimumViewDistance = 0.18f; item.inspectionFill = new Color(0.85f, 0.78f, 0.64f);
                paper.SetActive(false);
                return new PlaintextArchiveController.Entry { act = act, id = record[1], answer = record[2], document = item, plaintextInk = ink };
            }).ToArray();
            work.plaintextInk.characterSize = 0.0021f; work.plaintextInk.lineSpacing = 1.05f;
            EditorUtility.SetDirty(work.plaintextInk); EditorUtility.SetDirty(controller);
            DeskFirstActFlowBuilder.InstallInScene(camera);
            ConfigureThirdActDocumentReferences(archive);
            ConfigureFourthActDocumentReferences(archive);
            ConfigureOperationalDocuments(archive);
            if (camera.GetComponent<SecondActFlow>()) ConfigureSecondAct(camera);
        }
    }
}
