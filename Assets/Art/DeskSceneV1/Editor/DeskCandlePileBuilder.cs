using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PurpleGlove.Editor
{
    public static partial class DeskSceneBuilder
    {
        const string CandlePileName = "CandlePile_Unlit";

        [MenuItem("Purple Glove/Scene/Install Act Candle Pile")]
        public static void InstallActCandlePile()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            LoadSavedSceneAssets();
            BuildActCandlePile(Camera.main);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("ACT_CANDLE_PILE_INSTALLED");
        }

        static void BuildActCandlePile(Camera camera)
        {
            var holder = GameObject.Find("Candleholder_Lit");
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            var story = camera ? camera.GetComponent<PrologueSequence>() : null;
            if (!holder || !archive || !story) throw new InvalidOperationException("Install the desk, archive and act transitions first.");

            var asset = new GameObject(CandlePileName);
            var pile = asset.AddComponent<ActCandlePile>();
            // Three candles rest on the desk; the fourth rests in the front groove and is consumed first.
            var positions = new[] { V(0, 0.034f, -0.072f), V(-0.009f, 0.034f, 0),
                V(0.008f, 0.034f, 0.072f), V(0.004f, 0.092f, -0.036f) };
            var lengths = new[] { 0.252f, 0.263f, 0.245f, 0.258f };
            for (int i = 0; i < 4; i++)
            {
                float length = lengths[i];
                var candle = MeshObject("ReserveCandle_" + (i + 1), asset.transform, mesh =>
                {
                    var origin = V(-length * 0.5f, 0, 0);
                    var rotation = Quaternion.Euler(0, 0, -90);
                    mesh.Lathe(origin, new[] { P(0.029f, 0), P(0.034f, 0.004f),
                        P(0.033f, length * 0.46f), P(0.034f, length - 0.015f),
                        P(0.031f, length - 0.003f), P(0.022f, length) }, 10, M("Wax_Ivory"), true, true, rotation);
                    // Small casting seams and a pale, unused wick distinguish reserves from the burning candle.
                    mesh.Rod(origin + V(0.012f, 0.029f, 0.014f), origin + V(length - 0.019f, 0.028f, 0.014f), 0.0012f, M("Wax_Ivory"), 5);
                    mesh.Rod(V(length * 0.5f - 0.001f, 0, 0), V(length * 0.5f + 0.010f, 0.004f, 0.002f), 0.0017f, M("Stitch"), 5);
                });
                candle.transform.localPosition = positions[i];
                ValidateMeshMetric(candle.GetComponent<MeshFilter>().sharedMesh, false);
                pile.candles[i] = candle;
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(asset, Root + "/Prefabs/PF_" + CandlePileName + ".prefab");
            prefabs[CandlePileName] = prefab;
            Directory.CreateDirectory(Output + "/obj/Textures");
            ExportObj(asset, CandlePileName);
            UnityEngine.Object.DestroyImmediate(asset);

            var old = holder.transform.parent.Find(CandlePileName);
            Vector3 position = old ? old.position : holder.transform.position + V(-0.23f, 0, 0.03f);
            Quaternion rotationOnDesk = old ? old.rotation : Quaternion.Euler(0, 90, 0);
            if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform.parent);
            instance.name = CandlePileName;
            instance.transform.SetPositionAndRotation(position, rotationOnDesk);
            pile = instance.GetComponent<ActCandlePile>();
            pile.archive = archive; pile.story = story;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(pile);
        }
    }
}
