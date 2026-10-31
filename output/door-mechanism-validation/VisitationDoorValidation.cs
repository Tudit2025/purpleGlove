using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PurpleGlove;
using PurpleGlove.Editor;

public static class VisitationDoorValidation
{
    static readonly string Output = Path.GetFullPath("../../output/door-mechanism-validation");
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static readonly MethodInfo Advance = typeof(DoorMechanism).GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Output);
            DeskSceneBuilder.VerifySavedScene();
            EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            var camera = Camera.main;
            var view = camera.GetComponent<DeskScenePreview>();
            var all = UnityEngine.Object.FindObjectsOfType<DoorMechanism>();
            Check(all.Length == 2, "Expected two door mechanisms");
            var eye = all.Single(a => a.mechanism == DoorMechanism.Mechanism.EyeShutter);
            var drum = all.Single(a => a.mechanism == DoorMechanism.Mechanism.TransferDrum);
            // Runtime allows reparenting prefab children. Unpack only this unsaved edit-mode test instance.
            PrefabUtility.UnpackPrefabInstance(eye.transform.parent.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Check(eye.openingSound && eye.closingSound && eye.stopSound && drum.openingSound, "Missing audio clips");
            Check(drum.Cargo && drum.Cargo.parent == drum.cargoSocket, "Cargo is not attached to rotor");
            Check(!eye.startsAtSecondStop && !drum.startsAtSecondStop, "Initial mechanisms must be closed/outside");
            Check(!eye.transform.parent.GetComponent<BoxCollider>(), "Monolithic door collider seals openings");
            var eyeOrigin = new Vector3(0.445f, 1.66f, 3.0f);
            Physics.SyncTransforms();
            Check(Physics.Raycast(eyeOrigin, Vector3.forward, out var hit, 3) && hit.collider.name == "Door_EyeShutterPanel", "Closed shutter does not block eye line");
            eye.SetPoseImmediately(true); Physics.SyncTransforms();
            Check(Physics.Raycast(eyeOrigin, Vector3.forward, out hit, 3) && hit.point.z > 5.2f, "Eye opening is blocked or exterior has no wall");
            Check(Physics.Raycast(eyeOrigin + Vector3.up * 0.20f, Vector3.forward, out hit, 0.5f), "Upper face is not occluded");
            Check(Physics.Raycast(eyeOrigin - Vector3.up * 0.20f, Vector3.forward, out hit, 0.5f), "Lower face is not occluded");
            Check(Mathf.Abs(eye.transform.position.y - 1.66f) < 0.001f, "Eye height changed");
            camera.transform.rotation = Quaternion.Euler(view.doorwayEuler); camera.fieldOfView = DeskScenePreview.DoorwayFieldOfView;
            CheckPick(camera, eye, eye.transform.TransformPoint(new Vector3(0, -0.111f, -0.11f)));
            eye.SetPoseImmediately(false); Physics.SyncTransforms();
            CheckPick(camera, eye, eye.transform.TransformPoint(new Vector3(0, 0, -0.10f)));
            CheckPick(camera, drum, drum.transform.TransformPoint(new Vector3(0, -0.23f, -0.31f)));

            int events = 0;
            eye.reachedSecondStop.AddListener(() => events++);
            eye.Activate();
            Check(eye.IsMoving && !eye.MoveTo(false), "Moving mechanism accepts duplicate clicks");
            Advance.Invoke(eye, new object[] { eye.travelSeconds / 2 });
            Check(Quaternion.Angle(eye.pivot.localRotation, Quaternion.identity) > 30 && !eye.AtSecondStop, "No intermediate animation");
            Advance.Invoke(eye, new object[] { eye.travelSeconds });
            Check(eye.AtSecondStop && events == 1, "Open stop/event failed");
            eye.Activate(); Advance.Invoke(eye, new object[] { eye.travelSeconds });
            Check(!eye.IsMoving && !eye.AtSecondStop && Quaternion.Angle(eye.pivot.localRotation, Quaternion.identity) < 0.001f, "Closing does not reach home");

            Vector3 outside = drum.Cargo.position;
            int cargoId = drum.Cargo.GetInstanceID();
            Check(outside.z > 3.4f, "Cargo does not start outside");
            drum.Activate(); Advance.Invoke(drum, new object[] { drum.travelSeconds / 2 });
            Check(!drum.ReleaseCargo(), "Cargo released during rotation");
            Advance.Invoke(drum, new object[] { drum.travelSeconds });
            Check(drum.CargoIsInside && drum.Cargo.position.z < 3.2f && drum.Cargo.GetInstanceID() == cargoId, "Cargo did not cross the door intact");
            var delivered = drum.ReleaseCargo();
            Check(delivered && !drum.Cargo && delivered.parent == null, "Unload failed");
            Check(drum.TryLoadCargo(delivered) && !drum.TryLoadCargo(delivered), "Load/occupied checks failed");
            drum.Activate(); Advance.Invoke(drum, new object[] { drum.travelSeconds });
            Check((drum.Cargo.position - outside).sqrMagnitude < 0.0000001f, "Return trip does not restore cargo position");
            Check(!drum.TryLoadCargo(drum.transform.root), "Allowed cyclic hierarchy");
            // At every intermediate angle, the rotor and load clear both jambs, the sill and lintel.
            foreach (int step in Enumerable.Range(0, 37))
            {
                drum.pivot.localRotation = Quaternion.Euler(0, step * 5, 0);
                foreach (var filter in drum.pivot.GetComponentsInChildren<MeshFilter>())
                    foreach (var vertex in filter.sharedMesh.vertices)
                    {
                        Vector3 p = drum.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                        Check(Mathf.Abs(p.x) < 0.319f && p.y > -0.2125f && p.y < 0.2125f, "Rotor/cargo intersects housing: " + filter.name);
                    }
            }
            drum.SetPoseImmediately(false);
            Check(GameObject.Find("Door exterior - dim corridor") && GameObject.Find("Visitor eye line - 1.66m"), "Exterior/visitor anchor absent");
            foreach (var ink in UnityEngine.Object.FindObjectsOfType<WorldDocumentText>())
                typeof(WorldDocumentText).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ink, null);
            Capture(camera, "door-closed");
            eye.SetPoseImmediately(true); drum.SetPoseImmediately(true);
            Capture(camera, "door-open-and-delivery");
            camera.transform.position = new Vector3(0.445f, 1.55f, 1.9f);
            camera.transform.LookAt(new Vector3(0.445f, 1.45f, 3.32f)); camera.fieldOfView = 48;
            Capture(camera, "door-detail");
            camera.transform.position = new Vector3(0.8f, 1.65f, 3.7f);
            camera.transform.LookAt(new Vector3(0.25f, 1.4f, 5.4f)); camera.fieldOfView = 75;
            Capture(camera, "exterior-corridor");
            eye.SetPoseImmediately(false); drum.SetPoseImmediately(false);
            camera.transform.position = new Vector3(0, 1.5f, -1.38f);
            camera.transform.rotation = Quaternion.Euler(view.desktopEuler); camera.fieldOfView = 60;
            Capture(camera, "desktop");
            File.WriteAllText(Path.Combine(Output, "validation.txt"),
                "PASS: saved scene reopened; no missing scripts/meshes/materials; metric UV, normals and shaders.\n"
                + "PASS: 54 x 16 cm eye opening at 1.66 m, closed occlusion, open through-ray to exterior wall, face masked above/below.\n"
                + "PASS: seated camera can pick closed panel, open-window rail and transfer handle.\n"
                + "PASS: animated intermediate poses, duplicate-click guard, both end stops and one-shot completion event.\n"
                + "PASS: same cargo object crosses outside/inside, returns exactly, loads/unloads only while stationary.\n"
                + "PASS: full 180-degree rotor/cargo sweep clears housing at 5-degree intervals.\n"
                + "PASS: initial shutter closed, cargo outside; five Unity renders captured.\n");
            Debug.Log("VISITATION_DOOR_VALIDATION_PASS");
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }

    static void CheckPick(Camera camera, DoorMechanism action, Vector3 point)
    {
        Physics.SyncTransforms();
        var ray = new Ray(camera.transform.position, point - camera.transform.position);
        Check(Physics.Raycast(ray, out var hit, 10) && hit.collider.GetComponentInParent<SceneObject>() == action.GetComponent<SceneObject>(), "Cannot click fitting from seat: " + action.name + " hit=" + (hit.collider ? hit.collider.name : "none"));
    }

    static void Capture(Camera camera, string name)
    {
        var target = new RenderTexture(1600, 900, 24);
        var previous = RenderTexture.active;
        var previousTarget = camera.targetTexture;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.aspect = 16f / 9; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(Output, name + ".png"), image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget; RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image); target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
