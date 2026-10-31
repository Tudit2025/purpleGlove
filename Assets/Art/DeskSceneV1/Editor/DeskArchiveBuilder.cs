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
        const string ArchiveGroup = "07 Five act archive";
        static readonly string[] ActNames = { "第一幕", "第二幕", "第三幕", "第四幕", "第五幕" };

        [MenuItem("Purple Glove/Scene/Add Five Act Archive")]
        public static void AddArchiveToSavedScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            LoadSavedSceneAssets();
            CreateArchiveAssets();
            BuildArchive(Camera.main);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("FIVE_ACT_ARCHIVE_SAVED");
        }

        static void LoadSavedSceneAssets()
        {
            materials.Clear(); prefabs.Clear();
            foreach (string path in Directory.GetFiles(Root + "/Materials", "M_Desk_*.mat"))
                materials[Path.GetFileNameWithoutExtension(path).Substring(7)] = AssetDatabase.LoadAssetAtPath<Material>(path.Replace('\\', '/'));
            documentFont = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/EBGaramond-Medium.otf");
            notesFont = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/Courgette-Regular.ttf");
            foreach (string path in Directory.GetFiles(Root + "/Prefabs", "PF_*.prefab"))
                prefabs[Path.GetFileNameWithoutExtension(path).Substring(3)] = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
        }

        static void CreateArchiveAssets()
        {
            Asset("ActBookshelf", go => MeshObject("Archive_FiveShelfFrame", go.transform, m =>
            {
                foreach (float x in new[] { -0.43f, 0.43f })
                    m.Box(V(x, 1.15f, 0), V(0.065f, 2.24f, 0.38f), M("Wood_DarkWalnut"), 0.007f);
                m.Box(V(0, 1.15f, 0.175f), V(0.83f, 2.20f, 0.03f), M("WalnutDark"), 0.004f);
                // Six boards enclose exactly five compartments, ordered I -> V from top to bottom.
                for (int i = 0; i <= 5; i++)
                {
                    float y = 0.16f + i * 0.4f;
                    m.Box(V(0, y, 0), V(0.88f, 0.04f, 0.38f), M("Wood_HoneyOak"), 0.004f);
                }
                m.Box(V(0, 0.07f, 0), V(0.95f, 0.09f, 0.44f), M("WalnutDark"), 0.006f);
                m.Box(V(0, 2.23f, 0), V(0.95f, 0.09f, 0.42f), M("Wood_DarkWalnut"), 0.006f);
            }), false);

            Asset("ActDossier", go => MeshObject("Archive_DossierMesh", go.transform, m =>
            {
                m.Box(V(0, 0.133f, 0), V(0.52f, 0.26f, 0.20f), M("Leather_Umber"), 0.004f);
                m.Box(V(0, 0.268f, 0), V(0.50f, 0.008f, 0.18f), M("PageEdge"), 0.002f);
                for (int i = 0; i < 5; i++)
                    m.Box(V(0, 0.274f + i * 0.001f, -0.06f + i * 0.027f), V(0.49f, 0.001f, 0.001f), M("Paper_RagIvory"), 0.0001f);
                foreach (float x in new[] { -0.19f, 0.19f })
                    m.Box(V(x, 0.135f, -0.104f), V(0.014f, 0.265f, 0.009f), M("Stitch"), 0.001f);
                m.Box(V(0, 0.05f, -0.105f), V(0.1f, 0.019f, 0.012f), M("SceneBrass"), 0.002f);
            }));
        }

        static void BuildArchive(Camera camera)
        {
            var existing = GameObject.Find(ArchiveGroup);
            if (existing)
            {
                RefreshArchiveAppearance(existing.transform);
                if (!existing.transform.Find("Act documents - authored desk layout"))
                    BuildActDocuments(existing.GetComponent<ActArchiveController>());
                return;
            }
            if (!camera) throw new InvalidOperationException("Missing desk camera.");
            var font = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/PHArchiveUI.ttf");
            if (!font) throw new InvalidOperationException("Missing archive UI font.");
            var group = Group(ArchiveGroup);
            var shelf = Place("ActBookshelf", group, V(-1.57f, 0, -1.37f), -90);
            var controller = group.gameObject.AddComponent<ActArchiveController>();
            controller.view = camera.GetComponent<DeskScenePreview>();
            controller.interactor = camera.GetComponent<SceneObjectInteractor>();
            controller.view.bookshelfEuler = Quaternion.LookRotation(V(-1.5f, 1.24f, -1.37f) - camera.transform.position).eulerAngles;
            controller.view.bookshelfFieldOfView = 84;
            camera.name = "Main Camera - WASD views";
            var hud = camera.GetComponent<DeskViewHud>() ?? camera.gameObject.AddComponent<DeskViewHud>();
            hud.labelFont = font;
            controller.acts = new ActArchiveController.ActFiles[5];

            for (int i = 0; i < 5; i++)
            {
                float board = 0.16f + (4 - i) * 0.4f;
                var anchor = new GameObject("Act " + (i + 1) + " storage").transform;
                anchor.SetParent(shelf.transform, false); anchor.localPosition = V(0, board + 0.023f, -0.035f);
                var bundle = Place("ActDossier", anchor, Vector3.zero).AddComponent<ActArchiveBundle>();
                bundle.name = "Act " + (i + 1) + " dossier";
                bundle.archive = controller; bundle.act = i + 1;
                controller.acts[i] = new ActArchiveController.ActFiles
                {
                    title = ActNames[i], storageAnchor = anchor, bundle = bundle
                };
                bundle.gameObject.SetActive(true);
            }
            BuildActDocuments(controller);
            RefreshArchiveAppearance(group);
        }

        static void RefreshArchiveAppearance(Transform group)
        {
            var shelf = group.Find("ActBookshelf");
            if (!shelf) throw new InvalidOperationException("Missing archive bookshelf.");
            // Remove only the generated shelf captions; document text belongs to the archived evidence.
            foreach (var text in shelf.GetComponentsInChildren<TextMesh>(true))
                if (text.name == "Act label" || text.name.StartsWith("Tier ")) UnityEngine.Object.DestroyImmediate(text.gameObject);
            foreach (var act in group.GetComponent<ActArchiveController>().acts) act.statusLabel = null;

            // Bring the whole fixture in front of the side post so its light can reach the shelf openings.
            var bracket = shelf.Find("Archive_CandleBracket");
            if (bracket) UnityEngine.Object.DestroyImmediate(bracket.gameObject);
            MeshObject("Archive_CandleBracket", shelf, m =>
            {
                m.Box(V(0.467f, 1.22f, -0.035f), V(0.012f, 0.20f, 0.10f), M("Iron_Blackened"), 0.003f);
                m.Rod(V(0.474f, 1.16f, -0.035f), V(0.64f, 1.29f, -0.30f), 0.012f, M("Iron_Blackened"), 6);
                m.Lathe(V(0.64f, 1.29f, -0.30f), new[] { P(0.055f, 0), P(0.075f, 0.008f), P(0.075f, 0.015f) }, 10, M("SceneBrass"));
            }, true);
            var candle = shelf.Find("Archive side candle");
            if (!candle)
            {
                candle = Place("Candleholder_Lit", shelf, Vector3.zero).transform;
                candle.name = "Archive side candle";
            }
            candle.localPosition = V(0.64f, 1.305f, -0.30f);
            candle.localScale = Vector3.one * 0.55f;
            ConfigureSceneCandle(candle.GetComponentInChildren<Light>());

            // Furniture shares the desk's layer and scene lighting; no archive-only light remains.
            var fillTransform = group.Find("Archive reading light");
            if (fillTransform) UnityEngine.Object.DestroyImmediate(fillTransform.gameObject);
            foreach (var child in shelf.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 0;
        }
    }
}
