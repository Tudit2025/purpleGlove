using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PurpleGlove.Editor
{
    public static partial class DeskSceneBuilder
    {
        const float DoorCenter = 0.462f;

        [MenuItem("Purple Glove/Scene/Add Visitation Door and Corridor")]
        public static void AddVisitationDoorToSavedScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            LoadSavedSceneAssets();
            CreateVisitationDoor();
            var architecture = GameObject.Find("01 Room - plaster, timber, stone");
            if (!architecture) throw new InvalidOperationException("Missing room architecture.");
            var oldDoor = architecture.transform.Find("DoorLeaf");
            Vector3 position = oldDoor ? oldDoor.localPosition : V(-0.017f, 0.04f, 3.32f);
            Quaternion rotation = oldDoor ? oldDoor.localRotation : Quaternion.identity;
            Vector3 scale = oldDoor ? oldDoor.localScale : Vector3.one;
            if (oldDoor) UnityEngine.Object.DestroyImmediate(oldDoor.gameObject);
            var door = Place("DoorLeaf", architecture.transform, position);
            door.transform.localRotation = rotation;
            door.transform.localScale = scale;
            BuildExteriorCorridor(architecture.transform);
            foreach (var t in door.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 8;
            if (Camera.main && Camera.main.GetComponent<SealedDeliveryController>())
                DeskDeliveryBuilder.InstallInScene(Camera.main);
            CaptureRoomReflections(Camera.main);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("VISITATION_DOOR_SAVED");
        }

        static void CreateVisitationDoor()
        {
            Asset("DoorLeaf", go =>
            {
                // Separate solid segments leave real through-openings in both the mesh and collision.
                MeshObject("Door_PlanksAndIron", go.transform, m =>
                {
                    // A thin backing closes the decorative plank seams without sealing either opening.
                    foreach (var band in new[] { new Vector2(0, 0.80f), new Vector2(1.24f, 1.54f), new Vector2(1.70f, 2.08f) })
                        m.Box(V(DoorCenter, (band.x + band.y) / 2, 0.029f), V(0.924f, band.y - band.x, 0.018f), M("WalnutDark"), 0.001f);
                    foreach (float x in new[] { 0.071f, 0.853f }) m.Box(V(x, 1.02f, 0.029f), V(0.142f, 0.44f, 0.018f), M("WalnutDark"), 0.001f);
                    foreach (float x in new[] { 0.096f, 0.828f }) m.Box(V(x, 1.62f, 0.029f), V(0.192f, 0.16f, 0.018f), M("WalnutDark"), 0.001f);
                    for (int i = 0; i < 6; i++)
                    {
                        float left = i * 0.154f + 0.0015f, right = (i + 1) * 0.154f - 0.0015f;
                        DoorPlank(m, left, right, 0, 0.80f);
                        DoorPlankOutsideOpening(m, left, right, 0.80f, 1.24f, 0.142f, 0.782f);
                        DoorPlank(m, left, right, 1.24f, 1.54f);
                        DoorPlankOutsideOpening(m, left, right, 1.54f, 1.70f, 0.192f, 0.732f);
                        DoorPlank(m, left, right, 1.70f, 2.08f);
                    }
                    foreach (float y in new[] { 0.30f, 1.91f })
                    {
                        m.Box(V(DoorCenter, y, -0.055f), V(0.915f, 0.095f, 0.045f), M("WalnutDark"), 0.005f);
                        m.Box(V(0.14f, y, -0.084f), V(0.27f, 0.043f, 0.012f), M("Iron_Blackened"), 0.003f);
                        foreach (float x in new[] { 0.035f, 0.23f }) m.Disc(V(x, y, -0.092f), 0.006f, 6, M("Steel"), Vector3.back);
                    }
                    m.Box(V(0.852f, 1.32f, -0.043f), V(0.055f, 0.14f, 0.016f), M("Iron_Blackened"), 0.004f);
                    m.Ring(V(0.852f, 1.34f, -0.071f), 0.028f, 0.004f, M("Steel"), Vector3.back, 10);
                }, true);
                BuildEyeShutter(go.transform);
                BuildTransferDrum(go.transform);
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 8;
            }, false);
        }

        static void DoorPlank(LowPolyMesh mesh, float left, float right, float bottom, float top)
        {
            if (right - left <= 0.001f) return;
            mesh.Box(V((left + right) / 2, (bottom + top) / 2, 0), V(right - left, top - bottom, 0.065f), M("Wood_Weathered"), 0.003f);
        }

        static void DoorPlankOutsideOpening(LowPolyMesh mesh, float left, float right, float bottom, float top, float openingLeft, float openingRight)
        {
            DoorPlank(mesh, left, Mathf.Min(right, openingLeft), bottom, top);
            DoorPlank(mesh, Mathf.Max(left, openingRight), right, bottom, top);
        }

        static DoorMechanism Fitting(Transform parent, string name, Vector3 position, Vector3 hitCenter, Vector3 hitSize)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            var box = root.AddComponent<BoxCollider>(); box.center = hitCenter; box.size = hitSize;
            var item = root.AddComponent<SceneObject>(); item.hoverDistance = 0;
            var action = root.AddComponent<DoorMechanism>();
            var source = root.GetComponent<AudioSource>();
            source.playOnAwake = false; source.loop = false; source.spatialBlend = 1;
            source.volume = 0.45f; source.minDistance = 1.5f; source.maxDistance = 9; source.dopplerLevel = 0;
            action.openingSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/PH/Trapdoor/trapdoor_open_01.wav");
            action.closingSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/PH/Trapdoor/trapdoor_close_01.wav");
            action.stopSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/PH/Trapdoor/trapdoor_latch_01.wav");
            return action;
        }

        static void BuildEyeShutter(Transform door)
        {
            var action = Fitting(door, "Eye shutter - click to open or close", V(DoorCenter, 1.62f, 0), V(0, -0.104f, -0.073f), V(0.63f, 0.048f, 0.12f));
            MeshObject("Door_EyeWindowFrame", action.transform, m =>
            {
                foreach (float x in new[] { -0.292f, 0.292f }) m.Box(V(x, 0, 0), V(0.044f, 0.244f, 0.14f), M("Iron_Blackened"), 0.003f);
                foreach (float y in new[] { -0.102f, 0.102f }) m.Box(V(0, y, 0), V(0.628f, 0.044f, 0.14f), M("Wood_DarkWalnut"), 0.004f);
                foreach (float x in new[] { -0.292f, 0.292f })
                    foreach (float y in new[] { -0.102f, 0.102f }) m.Disc(V(x, y, -0.074f), 0.009f, 6, M("Steel"), Vector3.back);
                // Handle on the fixed lower rail remains clickable with the shutter raised.
                m.Box(V(0, -0.111f, -0.091f), V(0.095f, 0.023f, 0.035f), M("SceneBrass"), 0.004f);
            }, true);
            var pivot = new GameObject("EyeShutterPivot").transform;
            pivot.SetParent(action.transform, false); pivot.localPosition = V(0, 0.105f, -0.083f);
            action.pivot = pivot;
            MeshObject("Door_EyeShutterPanel", pivot, m =>
            {
                m.Box(V(0, -0.096f, 0), V(0.564f, 0.192f, 0.028f), M("Wood_Weathered"), 0.003f);
                foreach (float x in new[] { -0.19f, 0.19f })
                {
                    m.Box(V(x, -0.096f, -0.018f), V(0.025f, 0.18f, 0.009f), M("Iron_Blackened"), 0.002f);
                    m.Rod(V(x - 0.034f, 0, 0), V(x + 0.034f, 0, 0), 0.013f, M("Steel"), 8);
                }
                m.Ring(V(0, -0.15f, -0.031f), 0.021f, 0.003f, M("SceneBrass"), Vector3.back, 8);
            }, true);
        }

        static void BuildTransferDrum(Transform door)
        {
            var action = Fitting(door, "Transfer hatch - click to turn", V(DoorCenter, 1.02f, 0), V(0, -0.226f, -0.13f), V(0.75f, 0.065f, 0.30f));
            action.mechanism = DoorMechanism.Mechanism.TransferDrum;
            action.rotationAxis = Vector3.up; action.travelAngle = 180; action.travelSeconds = 1.2f;
            MeshObject("Door_TransferHousing", action.transform, m =>
            {
                foreach (float x in new[] { -0.345f, 0.345f }) m.Box(V(x, 0, 0), V(0.05f, 0.50f, 0.38f), M("Wood_DarkWalnut"), 0.004f);
                foreach (float y in new[] { -0.235f, 0.235f }) m.Box(V(0, y, 0), V(0.74f, 0.045f, 0.62f), M("Wood_Weathered"), 0.004f);
                foreach (float x in new[] { -0.345f, 0.345f })
                    foreach (float y in new[] { -0.20f, 0.20f }) m.Disc(V(x, y, -0.196f), 0.01f, 6, M("Steel"), Vector3.back);
                m.Box(V(0, -0.241f, -0.30f), V(0.21f, 0.025f, 0.024f), M("SceneBrass"), 0.003f);
                m.Rod(V(0, -0.225f, 0), V(0, 0.225f, 0), 0.013f, M("Iron_Blackened"), 8);
            }, true);
            var pivot = new GameObject("TransferDrumPivot").transform;
            pivot.SetParent(action.transform, false); action.pivot = pivot;
            MeshObject("Door_TransferRotor", pivot, m =>
            {
                // Round tray clears the jamb at every angle; the full-height partition blocks the other side.
                m.Lathe(V(0, -0.201f, 0), new[] { P(0.291f, 0), P(0.299f, 0.008f), P(0.299f, 0.027f), P(0.291f, 0.035f) }, 24, M("Wood_Weathered"));
                m.Box(V(0, 0.014f, 0), V(0.588f, 0.39f, 0.027f), M("WalnutDark"), 0.003f);
                foreach (float z in new[] { -0.021f, 0.021f })
                {
                    m.Box(V(0, 0.015f, z), V(0.58f, 0.032f, 0.012f), M("Iron_Blackened"), 0.002f);
                    m.Ring(V(0, 0.032f, z * 2.5f), 0.037f, 0.006f, M("SceneBrass"), Vector3.forward, 10);
                }
                // Shallow rim keeps the load inside the circular swept volume.
                m.Lathe(V(0, -0.166f, 0), new[] { P(0.299f, 0), P(0.299f, 0.034f), P(0.282f, 0.034f), P(0.282f, 0) }, 24, M("Iron_Blackened"), false, false);
            }, true);
            var socket = new GameObject("CargoSocket - outside at rest").transform;
            socket.SetParent(pivot, false); socket.localPosition = V(0, -0.163f, 0.145f);
            action.cargoSocket = socket;
            var sample = new GameObject("Demonstration sealed packet").transform;
            MeshObject("Door_TransferPacket", sample, m =>
            {
                m.Box(V(0, 0.012f, 0), V(0.25f, 0.024f, 0.13f), M("Paper_AgedCream"), 0.003f);
                m.Box(V(0, 0.025f, 0), V(0.017f, 0.003f, 0.13f), M("Stitch"), 0.001f);
                m.Lathe(V(0, 0.027f, -0.012f), new[] { P(0.014f, 0), P(0.017f, 0.002f), P(0.014f, 0.005f) }, 8, M("Wax_Oxblood"));
            });
            action.TryLoadCargo(sample);
        }

        static void BuildExteriorCorridor(Transform architecture)
        {
            var old = architecture.Find("Door exterior - dim corridor");
            if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var corridor = new GameObject("Door exterior - dim corridor").transform;
            corridor.SetParent(architecture, false);
            MeshObject("Door_ExteriorWalls", corridor, m =>
            {
                m.Box(V(0.445f, 1.3f, 5.48f), V(3.04f, 2.6f, 0.14f), M("Wall_LimePlaster"), 0.004f);
                foreach (float x in new[] { -1.075f, 1.965f }) m.Box(V(x, 1.3f, 4.445f), V(0.14f, 2.6f, 2.21f), M("Wall_LimePlaster"), 0.004f);
                // Exterior face of the doorway wall, also open around the existing door frame.
                m.Box(V(-0.59f, 1.3f, 3.43f), V(0.95f, 2.6f, 0.14f), M("Wall_LimePlaster"), 0.004f);
                m.Box(V(1.48f, 1.3f, 3.43f), V(0.95f, 2.6f, 0.14f), M("Wall_LimePlaster"), 0.004f);
                m.Box(V(0.445f, 2.65f, 3.56f), V(3.04f, 0.10f, 3.98f), M("WalnutDark"), 0.004f);
                for (int x = 0; x < 6; x++)
                    for (int z = 0; z < 4; z++) m.Box(V(-0.805f + x * 0.5f, -0.028f, 3.70f + z * 0.5f), V(0.492f, 0.055f, 0.492f), M("Wall_GreyStone"), 0.004f);
            }, true);
            MeshObject("Door_ExteriorTimber", corridor, m =>
            {
                foreach (float x in new[] { -0.94f, -0.18f, 0.58f, 1.34f, 1.83f }) m.Box(V(x, 1.3f, 5.36f), V(0.10f, 2.6f, 0.12f), M("WalnutDark"), 0.005f);
                foreach (float y in new[] { 0.19f, 0.88f, 2.31f }) m.Box(V(0.445f, y, 5.36f), V(2.88f, 0.085f, 0.12f), M("Wood_Weathered"), 0.004f);
                foreach (float z in new[] { 3.8f, 4.65f, 5.32f }) m.Box(V(0.445f, 2.52f, z), V(2.88f, 0.16f, 0.15f), M("WalnutDark"), 0.005f);
            }, true);
            var candle = Place("Candleholder_Lit", corridor, V(-0.37f, 1.30f, 5.08f));
            candle.name = "Corridor wall candle";
            MeshObject("Door_ExteriorSconce", corridor, m =>
            {
                m.Box(V(-0.37f, 1.276f, 5.12f), V(0.31f, 0.045f, 0.43f), M("Wood_Weathered"), 0.004f);
                m.Rod(V(-0.37f, 1.0f, 5.35f), V(-0.37f, 1.25f, 4.98f), 0.025f, M("Iron_Blackened"), 4);
            }, true);
            var light = candle.GetComponentInChildren<Light>();
            ConfigureSceneCandle(light); light.intensity = 0.48f; light.range = 3.0f; light.cullingMask = 1 << 8;
            // A weak reflected pool in the narrow doorway keeps its fittings legible from the seated view.
            var spill = AddLight(corridor, "Dim reflected light on door fittings", LightType.Point, V(0.10f, 1.48f, 2.50f), new Color(1, 0.72f, 0.43f), 0.86f, 2.2f);
            spill.cullingMask = 1 << 8; spill.shadows = LightShadows.Soft;
            foreach (var t in corridor.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 8;
            var anchor = new GameObject("Visitor eye line - 1.66m").transform;
            anchor.SetParent(corridor, false); anchor.localPosition = V(0.445f, 1.66f, 3.64f);
            anchor.localRotation = Quaternion.Euler(0, 180, 0);
        }
    }
}
