using UnityEditor;
using UnityEngine;

namespace PurpleGlove.Editor
{
    public static partial class DeskSceneBuilder
    {
        static void BuildLetterTray(Transform parent, CipherWorkbench work)
        {
            var tray = Child(parent, "LetterTray_QWERTY", V(0.96f, 0.801f, 0));
            work.letterTray = tray;
            work.letterSamples = new CipherLetterTile[26];
            MeshObject("Cipher_LetterTray", tray, m =>
            {
                m.Box(V(0, 0.006f, 0), V(0.66f, 0.012f, 0.254f), M("Wood_DarkWalnut"), 0.004f);
                m.Box(V(0, 0.013f, 0), V(0.632f, 0.003f, 0.224f), M("Leather_Umber"), 0.001f);
                foreach (float z in new[] { -0.12f, 0.12f }) m.Box(V(0, 0.021f, z), V(0.66f, 0.022f, 0.014f), M("Wood_HoneyOak"), 0.003f);
                foreach (float x in new[] { -0.323f, 0.323f }) m.Box(V(x, 0.021f, 0), V(0.014f, 0.022f, 0.232f), M("Wood_HoneyOak"), 0.003f);
                foreach (float z in new[] { -0.033f, 0.032f }) m.Box(V(0, 0.018f, z), V(0.63f, 0.006f, 0.003f), M("Wood_DarkWalnut"), 0.001f);
            });
            CipherText(tray, "Tray inscription", "字 母 盘", V(0, 0.033f, -0.119f), 0.0013f, work.uiFont);
            string[] rows = { "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
            float[] starts = { -0.2655f, -0.25075f, -0.22125f };
            GameObject tileTemplate = null;
            for (int row = 0; row < rows.Length; row++)
            for (int col = 0; col < rows[row].Length; col++)
            {
                char letter = rows[row][col];
                var stack = Child(tray, "Stack_" + letter, V(starts[row] + col * 0.059f, 0.015f, 0.065f - row * 0.065f));
                for (int layer = 0; layer < 3; layer++)
                {
                    var socket = Child(stack, "Home_" + letter + "_" + layer, V(layer == 1 ? 0.0015f : 0, layer * 0.0065f, 0));
                    socket.localRotation = Quaternion.Euler(0, layer == 0 ? -3 : layer == 1 ? 3 : 0, 0);
                    var tile = Child(stack, "Tile_" + letter + "_" + layer, socket.localPosition);
                    tile.localRotation = socket.localRotation;
                    var identity = tile.gameObject.AddComponent<CipherLetterTile>();
                    identity.alphabetIndex = letter - 'A'; identity.homeSocket = socket;
                    identity.animationPivot = Child(tile, "AnimationPivot", Vector3.zero);
                    if (!tileTemplate)
                        tileTemplate = MeshObject("Cipher_LetterTile", identity.animationPivot, m =>
                            m.Box(V(0, 0.003f, 0), V(0.049f, 0.006f, 0.047f), M("Wood_HoneyOak"), 0.002f));
                    else
                    {
                        var mesh = UnityEngine.Object.Instantiate(tileTemplate, identity.animationPivot);
                        mesh.name = "Cipher_LetterTile"; mesh.transform.localPosition = Vector3.zero;
                    }
                    CipherText(identity.animationPivot, "Letter_" + letter, letter.ToString(), V(0, 0.0065f, 0), 0.0030f, documentFont);
                    if (layer == 2)
                    {
                        var hit = tile.gameObject.AddComponent<CipherWorkbenchTarget>(); hit.kind = CipherWorkbenchTarget.Kind.Letter;
                        var collider = tile.GetComponent<BoxCollider>(); collider.center = V(0, 0.003f, 0); collider.size = V(0.049f, 0.007f, 0.047f);
                        work.letterSamples[letter - 'A'] = identity;
                    }
                }
            }
            // Save the full tray and all 26 reusable top samples with their own letter and animation pivot.
            // Standalone sample prefabs deliberately have no external home socket reference.
            for (int i = 0; i < work.letterSamples.Length; i++)
            {
                var sample = UnityEngine.Object.Instantiate(work.letterSamples[i].gameObject);
                sample.name = "LetterTile_" + (char)('A' + i);
                sample.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                sample.GetComponent<CipherLetterTile>().homeSocket = null;
                PrefabUtility.SaveAsPrefabAsset(sample, Root + "/Prefabs/PF_LetterTile_" + (char)('A' + i) + ".prefab");
                UnityEngine.Object.DestroyImmediate(sample);
            }
            PrefabUtility.SaveAsPrefabAsset(tray.gameObject, Root + "/Prefabs/PF_LetterTray_QWERTY.prefab");
            AddCipherAction(tray.gameObject, work, CipherWorkbenchAction.ActionKind.Focus, V(0, 0.009f, 0), V(0.66f, 0.018f, 0.254f));
        }
    }
}
