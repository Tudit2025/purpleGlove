using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PurpleGlove.Editor
{
    public static partial class DeskSceneBuilder
    {
        [MenuItem("Purple Glove/Scene/Configure Table Focus View")]
        public static void ConfigureTableFocusView()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            ConfigureDeskViews(Camera.main);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("TABLE_FOCUS_VIEW_SAVED");
        }

        static void ConfigureDeskViews(Camera camera)
        {
            var view = camera ? camera.GetComponent<DeskScenePreview>() : null;
            var mat = GameObject.Find("WritingMat");
            if (!view || !mat) throw new InvalidOperationException("Missing desk camera or writing mat.");
            view.matFocusAnchor = mat.transform;
            view.matFocusOffset = new Vector3(0, 1.45f, -0.45f);
            view.matFocusFieldOfView = 48;
            EditorUtility.SetDirty(view);
        }
    }
}
