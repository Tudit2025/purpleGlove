using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace PurpleGlove.Editor
{
    public static partial class DeskSceneBuilder
    {
        static GameObject Place(string name, Transform parent, Vector3 position, float yaw = 0)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[name]);
            instance.name = name;
            instance.transform.SetParent(parent, false); instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            return instance;
        }

        static Transform Group(string name) => new GameObject(name).transform;

        [MenuItem("Purple Glove/Scene/Refresh Room Surfaces and Lighting")]
        public static void RefreshRoomSurfacesAndLighting()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            LoadSavedSceneAssets();
            var architecture = GameObject.Find("01 Room - plaster, timber, stone");
            if (!architecture) throw new InvalidOperationException("Missing room architecture.");
            BuildRoomSurfaces(architecture.transform);
            var camera = Camera.main;
            ConfigureSceneCandle(camera.GetComponent<DeskScenePreview>().candleLight);
            BuildArchive(camera);
            CaptureRoomReflections(camera);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("ROOM_SURFACES_AND_LIGHTING_SAVED");
        }

        static void BuildRoom()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.14f, 0.17f, 0.23f) * LightMultiplier;
            RenderSettings.ambientEquatorColor = new Color(0.055f, 0.060f, 0.075f) * LightMultiplier;
            RenderSettings.ambientGroundColor = new Color(0.028f, 0.025f, 0.025f) * LightMultiplier;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.018f; RenderSettings.fogColor = new Color(0.025f, 0.031f, 0.044f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = null;
            RenderSettings.reflectionIntensity = 0.7f;

            var architecture = Group("01 Room - plaster, timber, stone");
            BuildRoomSurfaces(architecture);
            Place("DoorFrame", architecture, V(0.445f, 0, 3.29f));
            Place("DoorLeaf", architecture, V(-0.017f, 0.040f, 3.32f));
            BuildExteriorCorridor(architecture);
            Place("WindowFrame", architecture, V(-1.85f, 1.62f, 0.55f), -90);
            var hatch = Place("LetterHatch", architecture, V(1.385f, 1.16f, 1.49f));
            hatch.transform.Find("HatchPivot").localRotation = Quaternion.Euler(68, 0, 0);
            MeshObject("Door_CorridorGlow", architecture, m => m.Box(V(0.445f, 0.032f, 3.37f), V(0.91f, 0.014f, 0.008f), M("DoorLight"), 0.001f));

            BuildNightSky();
            BuildRoomContents(scene, architecture);
        }

        static void BuildRoomSurfaces(Transform architecture)
        {
            // Refresh only generated surfaces; preserve doors, windows, props and gameplay state.
            foreach (string name in new[] { "Room_WallsAndFloor", "Room_TimberFraming" })
            {
                var existing = architecture.Find(name);
                if (existing) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }
            MeshObject("Room_WallsAndFloor", architecture, m =>
            {
                // The wall facing the desk leaves actual openings for the door and letter hatch.
                m.Box(V(-0.998f, 1.30f, 1.62f), V(1.79f, 2.6f, 0.14f), M("Wall_LimePlaster"), 0.005f);
                m.Box(V(0.445f, 2.39f, 3.43f), V(1.10f, 0.42f, 0.14f), M("Wall_LimePlaster"), 0.005f);
                foreach (float x in new[] { -0.13f, 1.02f }) m.Box(V(x,1.30f,2.50f),V(0.14f,2.60f,1.85f),M("Wall_LimePlaster"),0.005f);
                m.Box(V(0.445f,-0.025f,2.48f),V(1.12f,0.05f,1.95f),M("Wall_GreyStone"),0.005f);
                m.Box(V(1.45f, 0.505f, 1.62f), V(0.90f, 1.01f, 0.14f), M("Wall_LimePlaster"), 0.005f);
                m.Box(V(1.45f, 1.955f, 1.62f), V(0.90f, 1.29f, 0.14f), M("Wall_LimePlaster"), 0.005f);
                m.Box(V(1.063f, 1.16f, 1.62f), V(0.126f, 0.30f, 0.14f), M("Wall_LimePlaster"), 0.003f);
                m.Box(V(1.773f, 1.16f, 1.62f), V(0.254f, 0.30f, 0.14f), M("Wall_LimePlaster"), 0.003f);
                // Left window opening: z=0.20..0.90, y=1.14..2.10.
                m.Box(V(-1.94f, 0.57f, -0.20f), V(0.14f, 1.14f, 3.64f), M("Wall_LimePlaster"), 0.004f);
                m.Box(V(-1.94f, 2.35f, -0.20f), V(0.14f, 0.50f, 3.64f), M("Wall_LimePlaster"), 0.004f);
                m.Box(V(-1.94f, 1.62f, -0.91f), V(0.14f, 0.96f, 2.22f), M("Wall_LimePlaster"), 0.004f);
                m.Box(V(-1.94f, 1.62f, 1.26f), V(0.14f, 0.96f, 0.72f), M("Wall_LimePlaster"), 0.004f);
                m.Box(V(1.94f, 1.30f, -0.20f), V(0.14f, 2.6f, 3.64f), M("Wall_LimePlaster"), 0.004f);
                // Close the wall behind the seat, leaving clearance for the bookshelf crown and plinth.
                m.Box(V(0, 1.30f, -2.02f), V(3.95f, 2.6f, 0.14f), M("Wall_LimePlaster"), 0.004f);
                m.Box(V(0, 2.65f, -0.195f), V(3.95f, 0.10f, 3.79f), M("WalnutDark"), 0.004f);
                for (int x = 0; x < 8; x++)
                for (int z = 0; z < 8; z++)
                {
                    m.Box(V(-1.75f + x * 0.5f, -0.028f, -1.85f + z * 0.5f), V(0.492f, 0.055f, 0.492f), M("Wall_GreyStone"), 0.006f);
                }
            }, true);
            MeshObject("Room_TimberFraming", architecture, m =>
            {
                foreach (float x in new[] { -1.84f, -1.18f, -0.50f, 1.83f }) m.Box(V(x, 1.3f, 1.51f), V(0.10f, 2.6f, 0.13f), M("WalnutDark"), 0.006f);
                foreach (float y in new[] { 0.19f, 0.88f, 2.31f })
                {
                    m.Box(V(-1.0f, y, 1.51f), V(1.76f, 0.085f, 0.12f), M("Wood_Weathered"), 0.005f);
                    m.Box(V(1.45f, y, 1.51f), V(0.9f, 0.085f, 0.12f), M("Wood_Weathered"), 0.005f);
                    foreach (float x in new[] { -1.83f, 1.83f }) m.Box(V(x, y, -0.205f), V(0.12f, 0.085f, 3.41f), M("Wood_Weathered"), 0.005f);
                    m.Box(V(0, y, -1.91f), V(3.76f, 0.085f, 0.10f), M("Wood_Weathered"), 0.005f);
                }
                foreach (float x in new[] { -1.83f, -0.63f, 0.63f, 1.83f })
                    m.Box(V(x, 1.3f, -1.91f), V(0.10f, 2.6f, 0.10f), M("WalnutDark"), 0.006f);
                foreach (float x in new[] { -1.83f, 1.83f })
                foreach (float z in new[] { -1.55f, -0.63f, 0.12f, 1.03f }) m.Box(V(x, 1.3f, z), V(0.12f, 2.6f, 0.10f), M("WalnutDark"), 0.006f);
                foreach (float z in new[] { -1.2f, 0.0f, 1.2f }) m.Box(V(0, 2.52f, z), V(3.8f, 0.16f, 0.15f), M("WalnutDark"), 0.008f);
            }, true);
            foreach (var child in architecture.GetComponentsInChildren<Transform>()) child.gameObject.layer = 8;
        }

        static void BuildRoomContents(UnityEngine.SceneManagement.Scene scene, Transform architecture)
        {
            var desk = Group("02 Desk and writing area");
            Place("WritingDesk", desk, V(0, 0, -0.125f));
            Place("WritingMat", desk, V(-0.02f, 0.801f, -0.39f), -1);
            Place("Letter_Cipher", desk, V(0.015f, 0.813f, -0.19f), -3);
            Place("Paper_Blank", desk, V(0.02f, 0.813f, -0.67f), 2);
            Place("Envelope_Open", desk, V(0.48f, 0.802f, 0.16f), -16);

            var books = Group("03 Reference books and notebook");
            Place("Book_Charcoal", books, V(-0.73f, 0.801f, 0.46f), -12);
            Place("Book_SevenPetal", books, V(-0.34f, 0.801f, 0.43f), -5);
            Place("Notebook_Open", books, V(-0.76f, 0.801f, -0.60f), 12);

            var tools = Group("04 Candle and writing tools");
            var candle = Place("Candleholder_Lit", tools, V(-1.07f, 0.801f, 0.18f));
            Place("InkBottle", tools, V(0.87f, 0.801f, -0.34f), 8);
            Place("SandShaker", tools, V(1.15f, 0.801f, -0.28f), 10);
            Place("Quill", tools, V(0.74f, 0.802f, -0.63f), -25);
            Place("PaperKnife", tools, V(0.97f, 0.802f, -0.60f), 42);

            var storage = Group("05 Correspondence and original evidence");
            Place("PreservationBox", storage, V(1.08f, 0.801f, 0.10f), -5);
            Place("CorrespondenceRack", storage, V(0.96f, 0.801f, 0.58f), -7);
            Place("ReceivingTray", storage, V(1.32f, 0.965f, 1.17f), 0);
            Place("Envelope_RedSeal", storage, V(1.32f, 0.995f, 1.17f), -10);
            var secondEnvelope = Place("Envelope_RedSeal", storage, V(1.36f, 1.010f, 1.21f), 5);
            // The second instance is a second envelope, not a new unique mesh.
            secondEnvelope.name = "IncomingEnvelope_Second";
            MeshObject("ReceivingShelf_Brackets", storage, m =>
            {
                m.Box(V(1.32f, 0.949f, 1.25f), V(0.63f, 0.032f, 0.51f), M("Wood_Weathered"), 0.004f);
                foreach (float x in new[] { 1.08f, 1.56f }) m.Rod(V(x, 0.72f, 1.49f), V(x, 0.93f, 1.06f), 0.024f, M("Iron_Blackened"), 4);
            });

            var lighting = Group("06 Warm candle and cold window light");
            // Layer 8 is scene-local lighting separation; it needs no project-wide layer-name edit.
            foreach (var child in architecture.GetComponentsInChildren<Transform>()) child.gameObject.layer = 8;
            ConfigureSceneCandle(candle.GetComponentInChildren<Light>());
            var roomWarmth = AddLight(lighting, "Dim candle spill on room", LightType.Point, V(-1.07f, 1.20f, 0.18f), new Color(1,0.69f,0.40f), 0.22f, 3.2f);
            roomWarmth.cullingMask = 1 << 8;
            var roomFill = AddLight(lighting, "Cool room ambient fill", LightType.Directional, V(0,2,0), new Color(0.40f,0.51f,0.72f), 0.16f, 8);
            roomFill.transform.rotation = Quaternion.Euler(55,-25,0); roomFill.cullingMask = 1 << 8;
            var moon = AddLight(lighting, "Window moonlight", LightType.Spot, V(-1.63f, 1.95f, 0.52f), new Color(0.30f, 0.48f, 0.82f), 1.45f, 6);
            moon.transform.LookAt(V(0.35f, 0.65f, -0.15f)); moon.spotAngle = 100; moon.innerSpotAngle = 48; moon.shadows = LightShadows.Soft;
            var bounce = AddLight(lighting, "Candle bounce across papers", LightType.Point, V(-0.15f, 1.6f, -0.70f), new Color(1, 0.80f, 0.57f), 0.72f, 2.7f);
            bounce.shadows = LightShadows.None;
            bounce.cullingMask = ~(1 << 8);
            var corridor = AddLight(lighting, "Door sill spill", LightType.Point, V(0.45f, 0.13f, 3.23f), new Color(1, 0.62f, 0.28f), 0.65f, 1.2f);
            corridor.shadows = LightShadows.None;

            var camera = new GameObject("Main Camera - WASD views").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.transform.position = V(0, 1.50f, -1.38f);
            camera.transform.rotation = Quaternion.Euler(34, 0, 0); camera.fieldOfView = 60;
            camera.nearClipPlane = 0.035f; camera.farClipPlane = 20; camera.aspect = 16f / 9;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.018f, 0.022f, 0.03f);
            camera.allowHDR = false; camera.allowMSAA = false; camera.renderingPath = RenderingPath.Forward;
            camera.gameObject.AddComponent<AudioListener>();
            var pixel = camera.gameObject.AddComponent<PixelSceneView>(); pixel.verticalResolution = 540;
            var preview = camera.gameObject.AddComponent<DeskScenePreview>(); preview.sceneCamera = camera;
            ConfigureDeskViews(camera);
            camera.gameObject.AddComponent<SceneObjectInteractor>();
            preview.candleLight = candle.GetComponentInChildren<Light>(); preview.flame = candle.transform.Find("Candle_Flame");
            BuildArchive(camera);
            var audio = new GameObject("Rain ambience - indoor loop").AddComponent<AudioSource>();
            audio.transform.position = V(-1.7f, 1.4f, 0.55f);
            audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/PH/Ambience/rain_room_muffled_loop_10s.wav");
            // The stereo clip already contains the room's muffling. Keep its level stable in both camera views.
            audio.loop = true; audio.playOnAwake = false; audio.volume = 0; audio.spatialBlend = 0;
            audio.dopplerLevel = 0;
            audio.minDistance = 0.5f; audio.maxDistance = 7;
            if (!audio.clip) throw new InvalidOperationException("Missing existing room-rain audio.");
            var ambience = audio.gameObject.AddComponent<SceneAmbience>();
            ambience.volume = 0.8f; ambience.fadeSeconds = 1.25f;

            DeskDialogueBuilder.InstallInScene(camera);
            DeskDeliveryBuilder.InstallInScene(camera);
            BuildCipherWorkbench(camera);

            CaptureRoomReflections(camera);

            var allRenderers = UnityEngine.Object.FindObjectsOfType<MeshRenderer>();
            if (allRenderers.Any(r => r.sharedMaterials.Any(m => !m))) throw new InvalidOperationException("Scene has missing materials.");
            int sceneTriangles = UnityEngine.Object.FindObjectsOfType<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length / 3);
            report.Add("SCENE | mesh_renderers=" + allRenderers.Length + " | geometry_triangles=" + sceneTriangles + " | missing_materials=0 | cameras=1 | audio=1");
            EditorSceneManager.SaveScene(scene, Root + "/Scenes/PH_DeskRoom.unity");
            Capture(camera, "desktop", 1920, 1080);
            camera.transform.rotation = Quaternion.Euler(preview.doorwayEuler); camera.fieldOfView = DeskScenePreview.DoorwayFieldOfView;
            Capture(camera, "doorway", 1920, 1080);
            camera.transform.position = V(1.60f, 2.30f, -1.50f); camera.transform.LookAt(V(0, 0.72f, 0.1f)); camera.fieldOfView = 76;
            pixel.pixelate = false;
            Capture(camera, "overview", 1920, 1080);
            // Restore the saved gameplay view; capture-only poses never alter the deliverable scene.
            camera.transform.position = V(0, 1.5f, -1.38f); camera.transform.rotation = Quaternion.Euler(preview.desktopEuler); camera.fieldOfView = 60; pixel.pixelate = true;
            EditorSceneManager.SaveScene(scene, Root + "/Scenes/PH_DeskRoom.unity");
        }

        static void CaptureRoomReflections(Camera camera)
        {
            // Capture the completed room so reflective props also see the rear wall.
            var reflectionCamera = new GameObject("Temporary reflection capture").AddComponent<Camera>();
            try
            {
                reflectionCamera.transform.position = V(0.25f, 1.08f, -0.05f);
                reflectionCamera.nearClipPlane = 0.03f; reflectionCamera.farClipPlane = 12;
                reflectionCamera.clearFlags = CameraClearFlags.SolidColor; reflectionCamera.backgroundColor = camera.backgroundColor;
                reflectionCamera.allowHDR = false;
                string cubePath = Root + "/Materials/DeskRoom_Reflections.asset";
                var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(cubePath);
                bool newCube = !cube;
                if (newCube) cube = new Cubemap(128, TextureFormat.RGB24, true) { name = "DeskRoom_Reflections" };
                if (!reflectionCamera.RenderToCubemap(cube)) throw new InvalidOperationException("Reflection capture failed.");
                if (newCube) AssetDatabase.CreateAsset(cube, cubePath);
                EditorUtility.SetDirty(cube);
                RenderSettings.customReflectionTexture = cube;
            }
            finally { UnityEngine.Object.DestroyImmediate(reflectionCamera.gameObject); }
        }

        static void ConfigureSceneCandle(Light light)
        {
            // Shared by the desktop and shelf fixtures, including the room's existing light separation.
            light.type = LightType.Point; light.color = new Color(1, 0.65f, 0.29f);
            light.intensity = 1.9f * LightMultiplier; light.range = 3.4f;
            light.cullingMask = ~(1 << 8); light.renderMode = LightRenderMode.ForcePixel;
            light.shadows = LightShadows.Soft; light.shadowStrength = 0.72f;
            light.shadowBias = 0.015f; light.shadowNormalBias = 0.025f; light.shadowNearPlane = 0.034f;
        }

        static Light AddLight(Transform parent, string name, LightType type, Vector3 position, Color color, float intensity, float range)
        {
            var light = new GameObject(name).AddComponent<Light>(); light.transform.SetParent(parent, false);
            light.transform.localPosition = position; light.type = type; light.color = color; light.intensity = intensity * LightMultiplier; light.range = range;
            light.renderMode = LightRenderMode.ForcePixel; light.shadowBias = 0.02f; light.shadowNormalBias = 0.025f; light.shadowNearPlane = 0.03f;
            return light;
        }

        static void Capture(Camera camera, string name, int width, int height)
        {
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active; var previousTarget = camera.targetTexture;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget; RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}
