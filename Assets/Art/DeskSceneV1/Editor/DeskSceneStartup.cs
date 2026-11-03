using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PurpleGlove.Editor
{
    public sealed class DeskSceneStartup : AssetPostprocessor
    {
        public const string ScenePath = DeskSceneBuilder.Root + "/Scenes/PH_DeskRoom.unity";

        [InitializeOnLoadMethod]
        static void Initialize()
        {
            EditorApplication.delayCall += SetPlayStartScene;
            EditorApplication.delayCall += HidePreviewGizmos;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            EditorApplication.delayCall += SetPlayStartScene;
            if (scene.path == ScenePath || scene.path == MainMenuController.ScenePath) EditorApplication.delayCall += HidePreviewGizmos;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
                EditorApplication.delayCall += HidePreviewGizmos;
        }

        [MenuItem("Purple Glove/Scene/Hide Preview Gizmos")]
        public static void HidePreviewGizmos()
        {
            var path = SceneManager.GetActiveScene().path;
            if (path != ScenePath && path != MainMenuController.ScenePath) return;

            // Hide editor overlays without changing any lights, audio sources or scene objects.
            foreach (var view in Resources.FindObjectsOfTypeAll<SceneView>())
            {
                view.drawGizmos = false;
                view.Repaint();
            }

            // Unity 2022 exposes the Game view toggle only through its internal editor API.
            var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            var drawGizmos = gameViewType?.GetProperty("drawGizmos", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (drawGizmos == null) return;
            foreach (EditorWindow view in Resources.FindObjectsOfTypeAll(gameViewType))
            {
                drawGizmos.SetValue(view, false);
                view.Repaint();
            }
        }

        // Asset references are ready here, including fresh editor/batch launches before their first update.
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom, bool didDomainReload)
        {
            if (didDomainReload || imported.Contains(ScenePath) || imported.Contains(MainMenuController.ScenePath)) SetPlayStartScene();
        }

        static void SetPlayStartScene()
        {
            // Opening the gameplay scene explicitly remains a direct debugging entry.
            bool debugDesk = SceneManager.GetActiveScene().path == ScenePath;
            var menu = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuController.ScenePath);
            var scene = !debugDesk && menu ? menu : AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (scene) EditorSceneManager.playModeStartScene = scene;
        }

        [MenuItem("Purple Glove/Scene/Configure Startup Scenes")]
        public static void Configure()
        {
            var paths = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuController.ScenePath)
                ? new[] { MainMenuController.ScenePath, ScenePath } : new[] { ScenePath };
            var remaining = EditorBuildSettings.scenes.Where(s => !paths.Contains(s.path));
            EditorBuildSettings.scenes = paths.Select(path => new EditorBuildSettingsScene(path, true)).Concat(remaining).ToArray();
            SetPlayStartScene();
        }
    }
}
