using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PurpleGlove.Editor
{
    public static partial class DeskSceneBuilder
    {
        const string HandbellName = "Handbell";

        [MenuItem("Purple Glove/Scene/Install Desk Handbell")]
        public static void InstallDeskHandbell()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            LoadSavedSceneAssets(); BuildDeskHandbell(Camera.main);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("DESK_HANDBELL_INSTALLED");
        }

        static void BuildDeskHandbell(Camera camera)
        {
            var dialogue = camera ? camera.GetComponent<DialogueController>() : null;
            var desk = GameObject.Find("02 Desk and writing area");
            if (!dialogue || !desk) throw new InvalidOperationException("Install the desk and dialogue before the handbell.");
            var luca = dialogue.speakers.Single(s => s && s.id == "luca");
            string linePath = DeskDialogueBuilder.DataRoot + "/Events/handbell_not_needed.asset";
            var line = AssetDatabase.LoadAssetAtPath<DialogueEvent>(linePath);
            if (!line)
            {
                line = ScriptableObject.CreateInstance<DialogueEvent>();
                line.id = "handbell_not_needed"; line.title = "手摇铃 · 暂不需要";
                line.context = "拿起手摇铃后原地独白，读完放回；不触发门口来访。";
                line.arrivalDelay = 0;
                line.lines = new[] { new DialogueEvent.Line { speaker = luca, text = "眼下还不必摇铃唤人。" } };
                AssetDatabase.CreateAsset(line, linePath);
            }
            var existing = desk.transform.Find(HandbellName);
            // Reinstall preserves scene placement, permission and authored event bindings.
            var root = new GameObject(HandbellName);
            var grip = new GameObject("GripPivot").transform;
            grip.SetParent(root.transform, false); grip.localPosition = V(0, 0.215f, 0);
            var body = MeshObject("Handbell_BrassShell", grip, m =>
            {
                // Continuous outer wall, rolled mouth and inner wall: an open bell, not a solid cone.
                m.Lathe(Vector3.zero, new[] { P(0.018f, 0.125f), P(0.033f, 0.114f),
                    P(0.043f, 0.092f), P(0.054f, 0.055f), P(0.074f, 0.019f),
                    P(0.083f, 0.010f), P(0.083f, 0.002f), P(0.076f, 0),
                    P(0.071f, 0.009f), P(0.048f, 0.052f), P(0.037f, 0.087f),
                    P(0.028f, 0.104f), P(0.012f, 0.110f) }.Reverse().ToArray(), 12, M("SceneBrass"), false, false);
                m.Disc(V(0, 0.125f, 0), 0.018f, 12, M("SceneBrass"), Vector3.up);
                m.Disc(V(0, 0.110f, 0), 0.012f, 12, M("SceneBrass"), Vector3.down);
                m.Lathe(Vector3.zero, new[] { P(0.021f, 0.12f), P(0.023f, 0.126f),
                    P(0.023f, 0.138f), P(0.017f, 0.142f) }, 10, M("SceneBrass"));
                m.Ring(V(0, 0.031f, 0), 0.066f, 0.0013f, M("SceneBrass"), Vector3.up, 12);
                m.Ring(V(0, 0.093f, 0), 0.043f, 0.001f, M("SceneBrass"), Vector3.up, 12);
            });
            body.transform.localPosition = V(0, -0.215f, 0);
            var handle = MeshObject("Handbell_WalnutHandle", grip, m =>
            {
                m.Lathe(Vector3.zero, new[] { P(0.015f, 0.134f), P(0.020f, 0.147f),
                    P(0.018f, 0.163f), P(0.014f, 0.179f), P(0.014f, 0.220f),
                    P(0.020f, 0.246f), P(0.024f, 0.256f), P(0.024f, 0.269f),
                    P(0.017f, 0.280f), P(0.006f, 0.284f) }, 10, M("Wood_DarkWalnut"));
                m.Lathe(Vector3.zero, new[] { P(0.016f, 0.152f), P(0.019f, 0.155f),
                    P(0.019f, 0.162f), P(0.016f, 0.164f) }, 10, M("SceneBrass"));
                m.Disc(V(0, 0.285f, 0), 0.006f, 8, M("SceneBrass"), Vector3.up);
            });
            handle.transform.localPosition = body.transform.localPosition;
            var clapper = new GameObject("ClapperPivot").transform;
            clapper.SetParent(grip, false); clapper.localPosition = V(0, -0.111f, 0);
            MeshObject("Handbell_Clapper", clapper, m =>
            {
                m.Rod(Vector3.zero, V(0, -0.064f, 0), 0.003f, M("Iron_Blackened"), 6);
                m.Lathe(V(0, -0.082f, 0), new[] { P(0.004f, 0), P(0.013f, 0.006f),
                    P(0.015f, 0.014f), P(0.012f, 0.023f), P(0.004f, 0.028f) }, 8, M("SceneBrass"));
            });
            foreach (var mesh in root.GetComponentsInChildren<MeshFilter>()) ValidateMeshMetric(mesh.sharedMesh, false);
            var hit = root.AddComponent<BoxCollider>(); hit.center = V(0, 0.1425f, 0); hit.size = V(0.166f, 0.285f, 0.166f);
            root.AddComponent<SceneObject>().hoverDistance = 0.022f;
            var sound = root.AddComponent<AudioSource>();
            sound.playOnAwake = false; sound.loop = false; sound.spatialBlend = 0.65f;
            sound.dopplerLevel = 0; sound.minDistance = 0.65f; sound.maxDistance = 6; sound.volume = 0.8f;
            var bell = root.AddComponent<DeskHandbell>();
            bell.gripPivot = grip; bell.clapperPivot = clapper; bell.notNeededLine = line;
            bell.pickupSound = HandbellAudio("Interactions/object_pickup_01.wav");
            bell.putdownSound = HandbellAudio("Interactions/object_setdown_01.wav");
            bell.strikes = Enumerable.Range(1, 3).Select(i => HandbellAudio("Handbell/handbell_strike_0" + i + ".wav")).ToArray();
            Directory.CreateDirectory(Output + "/obj/Textures");
            ExportObj(root, HandbellName);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/PF_Handbell.prefab");
            prefabs[HandbellName] = prefab;
            UnityEngine.Object.DestroyImmediate(root);
            if (existing)
            {
                bell = existing.GetComponent<DeskHandbell>();
                if (!bell) throw new InvalidOperationException("Handbell name is already used by another object.");
            }
            else
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, desk.transform);
                instance.name = HandbellName;
                // Beside the candle, behind the notebook and left of the reference books.
                instance.transform.position = V(-1.08f, 0.801f, 0.65f);
                instance.transform.rotation = Quaternion.identity;
                bell = instance.GetComponent<DeskHandbell>();
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            }
            bell.dialogue = dialogue;
            PrefabUtility.RecordPrefabInstancePropertyModifications(bell);
        }

        static AudioClip HandbellAudio(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/PH/" + path);
            if (!clip) throw new InvalidOperationException("Missing handbell foley: " + path);
            return clip;
        }
    }
}
