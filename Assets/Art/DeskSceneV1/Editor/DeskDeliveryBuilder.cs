using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PurpleGlove.Editor
{
    public static class DeskDeliveryBuilder
    {
        [MenuItem("Purple Glove/Events/Install Sealed Deliveries in Desk Room")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            InstallInScene(Camera.main);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("SEALED_DELIVERY_INSTALLED");
        }

        public static void InstallInScene(Camera camera)
        {
            if (!camera) throw new InvalidOperationException("Delivery requires the desk camera.");
            var drum = UnityEngine.Object.FindObjectsOfType<DoorMechanism>()
                .Single(d => d.mechanism == DoorMechanism.Mechanism.TransferDrum);
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            var mat = GameObject.Find("WritingMat");
            if (!archive || !mat) throw new InvalidOperationException("Install the archive and writing mat before delivery.");
            var delivery = camera.GetComponent<SealedDeliveryController>() ?? camera.gameObject.AddComponent<SealedDeliveryController>();
            var root = GameObject.Find("09 Sealed document delivery") ?? new GameObject("09 Sealed document delivery");
            var packet = delivery.packet ? delivery.packet.transform : null;
            if (!packet)
            {
                if (!drum.Cargo) throw new InvalidOperationException("Missing authored transfer packet.");
                // The demonstration is a prefab child: create a scene-owned packet so it can leave the drum.
                packet = UnityEngine.Object.Instantiate(drum.Cargo.gameObject, root.transform).transform;
            }
            if (drum.Cargo)
            {
                drum.Cargo.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(drum.Cargo.gameObject);
                var serializedDrum = new SerializedObject(drum);
                serializedDrum.FindProperty("cargo").objectReferenceValue = null;
                serializedDrum.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(drum);
            }
            packet.SetParent(root.transform, false);
            packet.name = "Sealed act document packet";
            packet.localPosition = Vector3.zero; packet.localRotation = Quaternion.identity;
            packet.localScale = Vector3.one * 1.15f;
            var item = packet.GetComponent<SceneObject>() ?? packet.gameObject.AddComponent<SceneObject>();
            item.enabled = true; item.hoverDistance = 0.065f; item.hoverSpeed = 14;
            var hitBox = packet.GetComponent<BoxCollider>();
            hitBox.center = new Vector3(0, 0.02f, 0); hitBox.size = new Vector3(0.25f, 0.045f, 0.13f);
            hitBox.enabled = false;
            var action = packet.GetComponent<SealedPacketAction>() ?? packet.gameObject.AddComponent<SealedPacketAction>();
            action.delivery = delivery;
            delivery.packet = item;
            DeskSceneBuilder.BuildFirstActPackets(delivery);
            DeskSceneBuilder.BuildFourthActPackets(delivery);
            var landing = root.transform.Find("Packet landing on writing mat");
            if (!landing)
            {
                landing = new GameObject("Packet landing on writing mat").transform;
                landing.SetParent(root.transform, false);
            }
            landing.position = mat.transform.TransformPoint(new Vector3(0, 0.012f, 0));
            landing.rotation = mat.transform.rotation;
            delivery.packet = item; delivery.packetHome = root.transform; delivery.deskLanding = landing;
            delivery.transferDrum = drum; delivery.archive = archive;
            delivery.pickupSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/PH/PaperBook/paper_pickup_01.wav");
            delivery.landingSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/PH/PaperBook/book_setdown_01.wav");
            archive.requireDelivery = true; archive.previewAllActs = false;
            foreach (var act in archive.acts)
            {
                act.bundle.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(act.bundle.gameObject);
            }
            packet.gameObject.SetActive(false);
            EditorUtility.SetDirty(delivery); EditorUtility.SetDirty(archive);
            DeskSceneBuilder.ConfigureFourthActOpening(camera);
        }
    }
}
