using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace PurpleGlove.Editor
{
    public static partial class DeskSceneBuilder
    {
        const string NightSkyGroup = "08 Exterior night sky";

        [MenuItem("Purple Glove/Scene/Add Night Sky")]
        public static void AddNightSkyToSavedScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            LoadSavedSceneAssets();
            ConfigureWindowGlass();
            BuildNightSky();
            CaptureRoomReflections(Camera.main);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("NIGHT_SKY_SAVED");
        }

        static void ConfigureWindowGlass()
        {
            var shader = Shader.Find("PurpleGlove/Window Glass");
            if (!shader || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Window glass shader failed to compile.");
            materials["WindowNight"] = SaveMaterial("WindowNight", new Material(shader)
            {
                color = new Color(0.08f, 0.12f, 0.18f, 0.08f)
            });
        }

        static void BuildNightSky()
        {
            var shader = Shader.Find("PurpleGlove/Night Sky");
            if (!shader || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Night sky shader failed to compile.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/M_Desk_NightSky.mat");
            if (!material) material = SaveMaterial("NightSky", new Material(shader));
            materials["NightSky"] = material;

            var existing = GameObject.Find(NightSkyGroup);
            if (existing) UnityEngine.Object.DestroyImmediate(existing);
            var sky = Group(NightSkyGroup);
            sky.position = V(0, 1.5f, 0);
            // A 9 m sphere encloses the room and corridor inside the existing 20 m camera range.
            var sphere = MeshObject("NightSky_Sphere", sky, mesh =>
            {
                const int rings = 24;
                var profile = new Vector2[rings + 1];
                for (int ring = 0; ring <= rings; ring++)
                {
                    float angle = ring * Mathf.PI / rings;
                    profile[ring] = new Vector2(ring == 0 || ring == rings ? 0 : Mathf.Sin(angle) * 9, -Mathf.Cos(angle) * 9);
                }
                mesh.Lathe(Vector3.zero, profile, 48, material, false, false);
            });
            var renderer = sphere.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }
}
