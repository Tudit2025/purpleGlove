using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PurpleGlove.Editor
{
    public static partial class DeskSceneBuilder
    {
        // Rect X/Y describe the tray's local X/Z plane. Reserve a label below each tool.
        static readonly Rect[] ToolboxPockets = {
            new Rect(-0.216f, -0.024f, 0.140f, 0.142f),
            new Rect(-0.216f, -0.118f, 0.140f, 0.082f),
            new Rect(-0.064f, -0.118f, 0.086f, 0.236f),
            new Rect( 0.032f, -0.118f, 0.086f, 0.236f),
            new Rect( 0.128f, -0.118f, 0.086f, 0.236f)
        };

        [MenuItem("Purple Glove/Scene/Arrange Cipher Toolbox Contents")]
        public static void ArrangeCipherToolboxContents()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            var work = Camera.main ? Camera.main.GetComponent<CipherWorkbench>() : null;
            var tray = work && work.toolbox ? work.toolbox.transform.Find("Cipher tray") : null;
            if (!tray) throw new InvalidOperationException("Missing installed cipher toolbox.");
            LoadSavedSceneAssets();
            LayoutCipherToolboxContents(work, tray);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("CIPHER_TOOLBOX_CONTENTS_SAVED");
        }

        static void LayoutCipherToolboxContents(CipherWorkbench work, Transform tray)
        {
            var oldTray = tray.Find("Cipher_BoxTray");
            if (oldTray) UnityEngine.Object.DestroyImmediate(oldTray.gameObject);
            var oldTitle = tray.Find("Toolbox title");
            if (oldTitle) UnityEngine.Object.DestroyImmediate(oldTitle.gameObject);
            MeshObject("Cipher_BoxTray", tray, m =>
            {
                m.Box(Vector3.zero, V(0.44f, 0.008f, 0.255f), M("Leather_Umber"), 0.002f);
                foreach (float x in new[] { -0.070f, 0.027f, 0.123f })
                    m.Box(V(x, 0.010f, 0), V(0.003f, 0.012f, 0.247f), M("Wood_HoneyOak"), 0.0005f);
                m.Box(V(-0.146f, 0.010f, -0.030f), V(0.148f, 0.012f, 0.003f), M("Wood_HoneyOak"), 0.0005f);
            });
            for (int i = 0; i < ToolboxPockets.Length; i++)
            {
                Rect pocket = ToolboxPockets[i];
                var item = work.storedTools[i].transform;
                item.localPosition = Vector3.zero;
                item.localScale = Vector3.one;
                item.localRotation = Quaternion.Euler(0, i < 2 ? 0 : 90, 0);
                Bounds bounds = ToolboxGeometryBounds(item, tray);
                const float padding = 0.003f, labelHeight = 0.016f;
                float scale = Mathf.Min((pocket.width - 2 * padding) / bounds.size.x,
                    (pocket.height - labelHeight - 2 * padding) / bounds.size.z);
                item.localScale = Vector3.one * scale;
                item.localPosition = V(pocket.center.x - bounds.center.x * scale,
                    0.007f - bounds.min.y * scale, pocket.center.y + labelHeight / 2 - bounds.center.z * scale);
                UpdateStoredToolMeshes(work.tools[i].transform, item, i, scale);

                var pick = tray.Find("Take " + (CipherWorkbench.Tool)i).GetComponent<BoxCollider>();
                pick.center = V(pocket.center.x, 0.024f, pocket.center.y);
                pick.size = V(pocket.width, 0.04f, pocket.height);
                var label = tray.Find("Slot " + i).GetComponent<TextMesh>();
                label.transform.localPosition = V(pocket.center.x, 0.005f, pocket.yMin + labelHeight / 2);
                label.characterSize = 0.0011f;
                EditorUtility.SetDirty(item); EditorUtility.SetDirty(pick);
                EditorUtility.SetDirty(label); EditorUtility.SetDirty(label.transform);
            }
        }

        static Bounds ToolboxGeometryBounds(Transform item, Transform tray)
        {
            bool first = true;
            var bounds = new Bounds();
            foreach (var filter in item.GetComponentsInChildren<MeshFilter>(true))
            {
                Bounds mesh = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 p = mesh.center + Vector3.Scale(mesh.extents, new Vector3(
                        (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    p = tray.InverseTransformPoint(filter.transform.TransformPoint(p));
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                    else bounds.Encapsulate(p);
                }
            }
            if (first) throw new InvalidOperationException("Missing stored tool geometry: " + item.name);
            return bounds;
        }

        static void UpdateStoredToolMeshes(Transform sourceTool, Transform storedTool, int index, float scale)
        {
            foreach (var filter in storedTool.GetComponentsInChildren<MeshFilter>(true))
            {
                string relativePath = AnimationUtility.CalculateTransformPath(filter.transform, storedTool);
                var source = sourceTool.Find(relativePath).GetComponent<MeshFilter>().sharedMesh;
                string name = "Stored_" + index + "_" + source.name;
                string path = Root + "/Meshes/" + name + ".asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (!mesh)
                {
                    mesh = UnityEngine.Object.Instantiate(source); mesh.name = name;
                    AssetDatabase.CreateAsset(mesh, path);
                }
                else
                {
                    // A full toolbox rebuild must also refresh any changed source geometry.
                    EditorUtility.CopySerialized(source, mesh);
                    mesh.name = name;
                }
                // Always start from the full-size source UVs, so repeated layout does not compound scale.
                var uv = source.uv;
                for (int u = 0; u < uv.Length; u++) uv[u] *= scale;
                mesh.uv = uv;
                filter.sharedMesh = mesh;
                EditorUtility.SetDirty(mesh); EditorUtility.SetDirty(filter);
            }
        }
    }
}
