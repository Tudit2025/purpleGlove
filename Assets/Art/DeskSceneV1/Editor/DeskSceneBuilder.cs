using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace PurpleGlove.Editor
{
    public static partial class DeskSceneBuilder
    {
        public const string Root = "Assets/Art/DeskSceneV1";
        const string Library = "Assets/Art/MaterialLibraryV1";
        const string Output = "output/desk-scene-v1";
        const float LightMultiplier = 0.5f;
        const string SurfaceShader = "PurpleGlove/Pixel Outlined";
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        static readonly List<string> report = new List<string>();
        static Font documentFont, notesFont;
        static Material M(string key) => materials[key];
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Vector2 P(float radius, float height) => new Vector2(radius, height);

        // Separate-process verification reads the saved assets rather than trusting the builder's live objects.
        public static void VerifySavedScene()
        {
            try
            {
                EditorSceneManager.OpenScene(Root + "/Scenes/PH_DeskRoom.unity", OpenSceneMode.Single);
                foreach (var go in UnityEngine.Object.FindObjectsOfType<Transform>())
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go.gameObject) != 0) throw new InvalidDataException("Missing script: " + go.name);
                foreach (var filter in UnityEngine.Object.FindObjectsOfType<MeshFilter>())
                {
                    if (!filter.sharedMesh) throw new InvalidDataException("Missing persisted mesh: " + filter.name);
                    ValidateMeshMetric(filter.sharedMesh, filter.name == "Candle_Halo");
                    if (filter.GetComponent<Renderer>().sharedMaterials.Any(m => !m)) throw new InvalidDataException("Missing persisted material.");
                }
                var camera = Camera.main;
                if (!camera || !camera.GetComponent<DeskScenePreview>() || !camera.GetComponent<PixelSceneView>()) throw new InvalidDataException("Missing camera preview components.");
                var mainCandle = camera.GetComponent<DeskScenePreview>().candleLight.transform.parent;
                var wax = mainCandle.Find("Candle_IvoryWax").GetComponent<Renderer>();
                var flame = camera.GetComponent<DeskScenePreview>().flame.GetComponent<Renderer>();
                if (flame.bounds.max.y < wax.bounds.max.y + 0.05f) throw new InvalidDataException("Flame is hidden in the candle body.");
                var ambience = UnityEngine.Object.FindObjectOfType<SceneAmbience>();
                if (!ambience || !ambience.enabled || ambience.muted || ambience.volume <= 0 || ambience.fadeSeconds <= 0)
                    throw new InvalidDataException("Missing or silent scene ambience controller.");
                var rain = ambience.GetComponent<AudioSource>();
                if (!rain || !rain.clip || Mathf.Abs(rain.clip.length - 10) > 0.1f) throw new InvalidDataException("Rain clip reference is invalid.");
                if (!rain.enabled || !rain.loop || rain.playOnAwake || rain.mute || rain.spatialBlend != 0 || rain.dopplerLevel != 0)
                    throw new InvalidDataException("Rain must use a continuous stereo loop with controller-managed playback.");
                if (UnityEngine.Object.FindObjectsOfType<AudioListener>().Count(listener => listener.isActiveAndEnabled) != 1)
                    throw new InvalidDataException("Expected exactly one active AudioListener.");
                VerifyStyle();
                Capture(camera, "desktop", 1920, 1080);
                camera.transform.rotation = Quaternion.Euler(camera.GetComponent<DeskScenePreview>().doorwayEuler); camera.fieldOfView = 38;
                Capture(camera, "doorway", 1920, 1080);
                camera.transform.position = V(1.60f, 2.30f, -1.50f); camera.transform.LookAt(V(0, 0.72f, 0.1f)); camera.fieldOfView = 76;
                camera.GetComponent<PixelSceneView>().pixelate = false;
                Capture(camera, "overview", 1920, 1080);
                if (ShaderUtil.ShaderHasError(Shader.Find(SurfaceShader))) throw new InvalidDataException("Outline shader rendering errors.");
                File.WriteAllText(Output + "/saved-scene-validation.txt", "PASS: saved scene reopened in a fresh Unity process.\nNo missing scripts, meshes, materials or rain clip.\nTriangle winding, nondegenerate geometry and metric UV verified.\nMaterial pixel size = 4 (32 texels/m); outline shader and UV3 normals verified.\nAll six scene lights and ambient colours = 50% of v1.\nBuild scene 0 and editor Play start scene = PH_DeskRoom.\nFlame protrudes above wax; desktop, doorway and overview rendered from saved assets.\nUnity 2022.3.62f2 / Built-in / Direct3D 11.\n");
                Debug.Log("SAVED_DESK_SCENE_PASS");
            }
            catch (Exception exception) { Debug.LogException(exception); if (Application.isBatchMode) EditorApplication.Exit(1); throw; }
        }

        static void VerifyStyle()
        {
            var shader = Shader.Find(SurfaceShader);
            if (!shader || !shader.isSupported || ShaderUtil.ShaderHasError(shader)) throw new InvalidDataException("Unsupported or broken style shader.");
            foreach (var filter in UnityEngine.Object.FindObjectsOfType<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                var smooth = new List<Vector3>(); mesh.GetUVs(2, smooth);
                if (smooth.Count != mesh.vertexCount || smooth.Any(n => float.IsNaN(n.sqrMagnitude) || Mathf.Abs(n.magnitude - 1) > 0.01f))
                    throw new InvalidDataException("Missing/invalid outline normals: " + filter.name);
                foreach (var material in filter.GetComponent<Renderer>().sharedMaterials)
                {
                    if (material.shader.name == "Standard") throw new InvalidDataException("Unstyled mesh material: " + material.name);
                    if (material.shader == shader && material.GetFloat("_PixelSize") != 4) throw new InvalidDataException("Inconsistent material pixel size.");
                }
            }
            var expected = new Dictionary<string, float>
            {
                { "Dim candle spill on room", 0.11f }, { "Cool room ambient fill", 0.08f },
                { "Window moonlight", 0.725f }, { "Candle bounce across papers", 0.36f }, { "Door sill spill", 0.325f }
            };
            foreach (var entry in expected)
            {
                var light = GameObject.Find(entry.Key).GetComponent<Light>();
                if (Mathf.Abs(light.intensity - entry.Value) > 0.0001f) throw new InvalidDataException("Light not halved: " + entry.Key);
            }
            if (Mathf.Abs(Camera.main.GetComponent<DeskScenePreview>().candleLight.intensity - 0.95f) > 0.0001f)
                throw new InvalidDataException("Candle intensity not halved.");
            var ambient = new[] { RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor };
            var baseline = new[] { new Color(0.14f, 0.17f, 0.23f), new Color(0.055f, 0.060f, 0.075f), new Color(0.028f, 0.025f, 0.025f) };
            for (int i = 0; i < ambient.Length; i++)
            {
                var delta = ambient[i] - baseline[i] * 0.5f;
                if (Mathf.Abs(delta.r) + Mathf.Abs(delta.g) + Mathf.Abs(delta.b) > 0.0001f) throw new InvalidDataException("Ambient intensity not halved.");
            }
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0 || !scenes[0].enabled || scenes[0].path != DeskSceneStartup.ScenePath)
                throw new InvalidDataException("Desk room is not build scene 0.");
            if (AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) != DeskSceneStartup.ScenePath)
                throw new InvalidDataException("Desk room is not the editor Play start scene.");
        }

        [MenuItem("Purple Glove/Scene/Build Desk Room V1")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                materials.Clear(); prefabs.Clear(); report.Clear();
                foreach (var folder in new[] { "Meshes", "Prefabs", "Materials", "Scenes" }) Directory.CreateDirectory(Root + "/" + folder);
                Directory.CreateDirectory(Output + "/obj/Textures");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                LoadMaterials();
                documentFont = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/EBGaramond-Medium.otf");
                notesFont = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/Courgette-Regular.ttf");
                if (!documentFont || !notesFont) throw new InvalidOperationException("The two bundled fonts and their OFL licenses must be present.");
                CreateAssets();
                AssetDatabase.SaveAssets();
                BuildRoom();
                DeskSceneStartup.Configure();
                AssetDatabase.SaveAssets();
                File.WriteAllLines(Output + "/asset-report.txt", report);
                ExportScenePackage();
                Debug.Log("DESK_SCENE_V1_PASS: " + prefabs.Count + " prefabs, meshes/UVs/materials/colliders validated; scene and three camera captures saved.");
                if (!Application.isBatchMode && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                throw;
            }
        }

        public static void ExportScenePackage()
        {
            AssetDatabase.ExportPackage(new[] { Root, "Assets/Scripts/Presentation", Library + "/Materials", Library + "/Textures", "Assets/Audio/PH/Ambience/rain_room_muffled_loop_10s.wav", "Assets/Audio/PH/Trapdoor" }, Output + "/PurpleGlove_DeskScene_V1.unitypackage", ExportPackageOptions.Recurse);
        }

        public static void VerifyAndPackage()
        {
            VerifySavedScene();
            ExportScenePackage();
        }

        static void LoadMaterials()
        {
            var shader = Shader.Find(SurfaceShader);
            if (!shader || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Pixel outline shader failed to compile.");
            foreach (var path in Directory.GetFiles(Library + "/Materials", "*.mat"))
            {
                var source = AssetDatabase.LoadAssetAtPath<Material>(path.Replace('\\', '/'));
                string key = Path.GetFileNameWithoutExtension(path).Substring(2);
                var material = new Material(source) { shader = shader };
                SetSurfaceStyle(material, key);
                materials[key] = SaveMaterial(key, material);
            }
            Variant("WalnutDark", "Wood_DarkWalnut", new Color(0.72f, 0.69f, 0.64f));
            Variant("WalnutWarm", "Wood_DarkWalnut", new Color(0.93f, 0.84f, 0.72f));
            Variant("PageEdge", "Paper_AgedCream", new Color(0.73f, 0.68f, 0.58f));
            Variant("Stitch", "Leather_Umber", new Color(1.7f, 1.5f, 1.2f));
            Variant("Steel", "Iron_Blackened", new Color(1.75f, 1.8f, 1.8f));
            Variant("SceneBrass", "Metal_AgedBrass", Color.white);
            M("SceneBrass").SetFloat("_Metallic", 0.65f);
            M("SceneBrass").SetFloat("_Glossiness", 0.32f);
            EditorUtility.SetDirty(M("SceneBrass"));
            materials["Metal_AgedBrass"] = M("SceneBrass");
            Solid("Ink", new Color(0.012f, 0.016f, 0.017f), 0, 0.74f);
            Solid("Soot", new Color(0.025f, 0.021f, 0.016f), 0, 0.08f);
            Solid("Mark", new Color(0.11f, 0.065f, 0.033f), 0, 0.05f);
            Solid("WindowNight", Color.white, 0, 0.1f, new Color(0.04f, 0.12f, 0.26f));
            Solid("FlameOuter", Color.white, 0, 0, new Color(1, 0.55f, 0.10f));
            Solid("FlameCore", Color.white, 0, 0, new Color(1, 0.93f, 0.58f));
            Solid("DoorLight", Color.white, 0, 0, new Color(0.8f, 0.45f, 0.14f));
            var glow = new Material(Shader.Find("PurpleGlove/Candle Glow"));
            glow.SetColor("_Color", new Color(LightMultiplier, 0.31f * LightMultiplier, 0.055f * LightMultiplier, 0.32f));
            materials["Glow"] = SaveMaterial("Glow", glow);
        }

        static void SetSurfaceStyle(Material material, string key)
        {
            material.SetFloat("_PixelSize", 4);
            material.SetColor("_OutlineColor", new Color(0.025f, 0.017f, 0.028f, 1));
            float width = key.StartsWith("Paper_") || key.StartsWith("Parchment_") ? 0.75f : 1.2f;
            if (key == "Stitch" || key == "Mark" || key == "Soot") width = 0;
            material.SetFloat("_OutlineWidth", width);
        }

        static void Variant(string key, string source, Color tint)
        {
            var material = new Material(M(source)) { color = tint };
            SetSurfaceStyle(material, key);
            materials[key] = SaveMaterial(key, material);
        }

        static void Solid(string key, Color color, float metallic, float smoothness, Color? emission = null)
        {
            var material = new Material(Shader.Find(emission.HasValue ? "Unlit/Color" : SurfaceShader)) { color = emission.HasValue ? emission.Value * LightMultiplier : color };
            if (!emission.HasValue) { material.SetFloat("_Metallic", metallic); material.SetFloat("_Glossiness", smoothness); SetSurfaceStyle(material, key); }
            materials[key] = SaveMaterial(key, material);
        }

        static Material SaveMaterial(string key, Material value)
        {
            value.name = "M_Desk_" + key;
            string path = Root + "/Materials/" + value.name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!existing) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, existing); UnityEngine.Object.DestroyImmediate(value);
            return existing;
        }

        static GameObject MeshObject(string id, Transform parent, Action<LowPolyMesh> build, bool collider = false)
        {
            var data = new LowPolyMesh(); build(data);
            var mesh = data.Create(id);
            string path = Root + "/Meshes/" + id + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!existing) AssetDatabase.CreateAsset(mesh, path);
            else
            {
                // Updating the native mesh through setters invalidates its GPU buffers on repeated builds.
                existing.Clear(); existing.indexFormat = mesh.indexFormat;
                existing.vertices = mesh.vertices; existing.normals = mesh.normals; existing.uv = mesh.uv;
                var outlineNormals = new List<Vector3>(); mesh.GetUVs(2, outlineNormals); existing.SetUVs(2, outlineNormals);
                existing.subMeshCount = mesh.subMeshCount;
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++) existing.SetTriangles(mesh.GetTriangles(submesh), submesh);
                existing.RecalculateBounds(); existing.RecalculateTangents();
                UnityEngine.Object.DestroyImmediate(mesh); mesh = existing;
                EditorUtility.SetDirty(mesh);
            }
            var go = new GameObject(id);
            if (parent) go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterials = data.Materials;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        static void Asset(string name, Action<GameObject> build, bool collider = true)
        {
            var go = new GameObject(name); build(go);
            if (name == "WritingMat" || name == "Letter_Cipher" || name == "Paper_Blank" || name == "Parchment_Sheet")
                foreach (var renderer in go.GetComponentsInChildren<MeshRenderer>()) renderer.shadowCastingMode = ShadowCastingMode.Off;
            var filters = go.GetComponentsInChildren<MeshFilter>();
            if (filters.Length == 0) throw new InvalidDataException("Empty prefab: " + name);
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            int count = 0;
            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh;
                if (!mesh || mesh.vertexCount == 0 || mesh.uv.Length != mesh.vertexCount || mesh.normals.Length != mesh.vertexCount)
                    throw new InvalidDataException("Missing mesh/UV/normals: " + name);
                ValidateMeshMetric(mesh, mesh.name == "Candle_Halo");
                if (filter.GetComponent<Renderer>().sharedMaterials.Any(m => !m || !m.shader || m.shader.name.Contains("Error")))
                    throw new InvalidDataException("Missing material/shader: " + name);
                foreach (var p in mesh.vertices)
                {
                    var point = go.transform.InverseTransformPoint(filter.transform.TransformPoint(p));
                    if (float.IsNaN(point.sqrMagnitude) || float.IsInfinity(point.sqrMagnitude)) throw new InvalidDataException("Invalid vertex: " + name);
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; } else bounds.Encapsulate(point);
                }
                count += mesh.triangles.Length / 3;
            }
            if (collider)
            {
                var box = go.AddComponent<BoxCollider>(); box.center = bounds.center;
                box.size = new Vector3(Mathf.Max(0.006f, bounds.size.x), Mathf.Max(0.006f, bounds.size.y), Mathf.Max(0.006f, bounds.size.z));
            }
            else
            {
                foreach (var filter in filters)
                    if (!filter.GetComponent<MeshCollider>()) filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }
            foreach (var meshCollider in go.GetComponentsInChildren<MeshCollider>())
                if (!meshCollider.sharedMesh || meshCollider.sharedMesh.triangles.Length == 0) throw new InvalidDataException("Invalid mesh collider: " + name);
            if (name.StartsWith("Book_") || name == "Notebook_Open" || name == "Letter_Cipher"
                || name == "Paper_Blank" || name == "Parchment_Sheet" || name.StartsWith("Envelope_") || name == "ActDossier")
                go.AddComponent<SceneObject>();
            prefabs[name] = PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/PF_" + name + ".prefab");
            ExportObj(go, name);
            report.Add(name + " | triangles=" + count + " | dimensions_m=" + bounds.size.ToString("F3") + " | meshes=" + filters.Length + " | colliders=" + go.GetComponentsInChildren<Collider>().Length + " | metric_uv=PASS | PASS");
            UnityEngine.Object.DestroyImmediate(go);
        }

        static void ValidateMeshMetric(Mesh mesh, bool isBillboard)
        {
            var positions = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv; var indices = mesh.triangles;
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                var cross = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
                if (cross.sqrMagnitude < 1e-14f) throw new InvalidDataException("Degenerate triangle: " + mesh.name);
                if (Vector3.Dot(cross / cross.magnitude, normals[a]) < 0.99f) throw new InvalidDataException("Winding/normal mismatch: " + mesh.name);
                if (!isBillboard && cross.magnitude > 0.0000001f)
                {
                    var u = uv[b] - uv[a]; var v = uv[c] - uv[a];
                    float ratio = Mathf.Abs(u.x * v.y - u.y * v.x) / cross.magnitude;
                    if (Mathf.Abs(ratio - 1) > 0.05f) throw new InvalidDataException("UV metre scale mismatch: " + mesh.name + " ratio=" + ratio);
                }
            }
        }

        static void CreateAssets()
        {
            Asset("WritingDesk", go => MeshObject("Desk_Timber", go.transform, m =>
            {
                for (int i = 0; i < 8; i++) m.Box(V(0, 0.755f, -0.9f + (i + 0.5f) * 0.225f), V(2.85f, 0.09f, 0.222f), M(i % 3 == 0 ? "WalnutWarm" : "Wood_DarkWalnut"), 0.007f);
                m.Box(V(0, 0.724f, -0.91f), V(2.89f, 0.075f, 0.045f), M("WalnutDark"), 0.008f);
                m.Box(V(0, 0.646f, -0.80f), V(2.65f, 0.15f, 0.075f), M("WalnutDark"), 0.008f);
                m.Box(V(0, 0.646f, 0.80f), V(2.65f, 0.15f, 0.075f), M("WalnutDark"), 0.008f);
                foreach (float x in new[] { -1.28f, 1.28f })
                {
                    m.Box(V(x, 0.65f, 0), V(0.07f, 0.14f, 1.65f), M("WalnutDark"), 0.006f);
                    foreach (float z in new[] { -0.73f, 0.73f })
                    {
                        m.Box(V(x, 0.35f, z), V(0.105f, 0.70f, 0.105f), M("Wood_DarkWalnut"), 0.012f);
                        m.Box(V(x, 0.10f, z), V(0.125f, 0.13f, 0.125f), M("WalnutDark"), 0.009f);
                    }
                    m.Box(V(x, 0.17f, 0), V(0.065f, 0.065f, 1.55f), M("WalnutDark"), 0.007f);
                }
                m.Box(V(0, 0.18f, 0.48f), V(2.6f, 0.065f, 0.065f), M("WalnutDark"), 0.006f);
                for (int i = 0; i < 2; i++)
                {
                    float x = i == 0 ? -0.60f : 0.60f;
                    m.Box(V(x, 0.64f, -0.85f), V(1.08f, 0.115f, 0.025f), M("Wood_DarkWalnut"), 0.006f);
                    m.Ring(V(x, 0.64f, -0.874f), 0.026f, 0.003f, M("Metal_AgedBrass"), Vector3.back, 10);
                }
            }), false);

            Asset("WritingMat", go => MeshObject("Mat_LeatherAndStitch", go.transform, m =>
            {
                m.Box(V(0, 0.004f, 0), V(1.02f, 0.008f, 1.12f), M("Leather_Charcoal"), 0.003f);
                for (int i = 0; i < 28; i++)
                {
                    float x = -0.472f + i * 0.035f;
                    foreach (float z in new[] { -0.531f, 0.531f }) m.Quad(V(x-0.006f,0.0085f,z-0.001f),V(x+0.006f,0.0085f,z-0.001f),V(x+0.006f,0.0085f,z+0.001f),V(x-0.006f,0.0085f,z+0.001f),M("Stitch"),Vector3.up);
                }
                for (int i = 0; i < 30; i++)
                {
                    float z = -0.507f + i * 0.035f;
                    foreach (float x in new[] { -0.483f, 0.483f }) m.Quad(V(x-0.001f,0.0085f,z-0.006f),V(x+0.001f,0.0085f,z-0.006f),V(x+0.001f,0.0085f,z+0.006f),V(x-0.001f,0.0085f,z+0.006f),M("Stitch"),Vector3.up);
                }
            }));

            ClosedBook("Book_Charcoal", "Leather_Charcoal", false);
            ClosedBook("Book_SevenPetal", "Leather_Oxblood", true);
            ClosedBook("Book_Moss", "Leather_Moss", false);
            Asset("Notebook_Open", go =>
            {
                MeshObject("Notebook_CoverAndEdges", go.transform, m =>
                {
                    m.Box(V(0, 0.009f, 0), V(0.65f, 0.018f, 0.49f), M("Leather_Umber"), 0.004f);
                    foreach (float x in new[] { -0.163f, 0.163f })
                    {
                        m.Box(V(x, 0.031f, 0), V(0.297f, 0.036f, 0.455f), M("Paper_AgedCream"), 0.004f);
                        for (int i = 0; i < 7; i++) m.Box(V(x, 0.016f + i * 0.0046f, -0.229f), V(0.297f, 0.0007f, 0.001f), M("PageEdge"), 0.0001f);
                    }
                });
                MeshObject("Notebook_OpenPages", go.transform, m => m.Sheet(0.625f, 0.457f, M("Paper_AgedCream"), 0.051f, true));
                PaperText(go.transform, "Alphabet grid", "A  B  C  D  E\nF  G  H  I  K\nL  M  N  O  P\nQ  R  S  T  U\nV  W  X  Y  Z", V(-0.16f, 0.075f, 0.015f), 0.245f, documentFont);
                PaperText(go.transform, "Handwritten notes", "Appunti\n\nA > D   B > E\nC > F   D > G\n\n1  2  3  4  5\n5  4  3  2  1", V(0.16f, 0.075f, 0.01f), 0.23f, notesFont);
            });

            Asset("Letter_Cipher", go =>
            {
                MeshObject("Letter_FoldedSurface", go.transform, m => m.Sheet(0.63f, 0.35f, M("Paper_RagIvory"), 0.003f));
                PaperText(go.transform, "Cipher", "NHHS WKH RULJLQDO", V(0, 0.012f, 0), 0.51f, documentFont);
            });
            Asset("Paper_Blank", go => MeshObject("Paper_BlankSurface", go.transform, m => m.Sheet(0.62f, 0.36f, M("Paper_RagIvory"), 0.003f)));
            Asset("Parchment_Sheet", go => MeshObject("Parchment_Surface", go.transform, m => m.Sheet(0.40f, 0.30f, M("Parchment_Aged"), 0.003f)));
            Envelope("Envelope_Open", false);
            Envelope("Envelope_RedSeal", true);
            CreateCandle(); CreateTools(); CreateStorage(); CreateArchitecture();
            CreateArchiveAssets();
        }

        static void ClosedBook(string name, string leather, bool flower)
        {
            Asset(name, go => MeshObject(name + "_Mesh", go.transform, m =>
            {
                m.Box(V(0, 0.042f, 0), V(0.286f, 0.062f, 0.402f), M("Paper_AgedCream"), 0.004f);
                m.Box(V(0, 0.008f, 0), V(0.32f, 0.016f, 0.44f), M(leather), 0.004f);
                m.Box(V(0, 0.084f, 0), V(0.32f, 0.018f, 0.44f), M(leather), 0.005f);
                m.Box(V(-0.153f, 0.047f, 0), V(0.026f, 0.075f, 0.431f), M(leather), 0.008f);
                for (int i = 0; i < 8; i++) m.Box(V(0.003f, 0.019f + i * 0.007f, -0.203f), V(0.274f, 0.001f, 0.0015f), M("PageEdge"), 0.0001f);
                foreach (float z in new[] { -0.15f, -0.08f, 0.08f, 0.15f }) m.Box(V(-0.161f, 0.048f, z), V(0.022f, 0.07f, 0.011f), M(leather), 0.004f);
                foreach (float x in new[] { -0.136f, 0.136f }) m.Box(V(x, 0.0932f, 0), V(0.0018f, 0.0005f, 0.39f), M("Stitch"), 0.0001f);
                foreach (float z in new[] { -0.193f, 0.193f }) m.Box(V(0, 0.0932f, z), V(0.273f, 0.0005f, 0.0018f), M("Stitch"), 0.0001f);
                if (flower)
                {
                    m.Disc(V(0, 0.094f, 0), 0.009f, 7, M("Metal_AgedBrass"), Vector3.up);
                    for (int i = 0; i < 7; i++)
                    {
                        float a = i * Mathf.PI * 2 / 7;
                        var direction = V(Mathf.Cos(a), 0, Mathf.Sin(a)); var side = V(-direction.z, 0, direction.x);
                        m.Polygon(new[] { V(0, 0.094f, 0) + direction * 0.012f, V(0, 0.094f, 0) + direction * 0.034f + side * 0.010f, V(0, 0.094f, 0) + direction * 0.055f, V(0, 0.094f, 0) + direction * 0.034f - side * 0.010f }, M("Metal_AgedBrass"), Vector3.up);
                    }
                }
            }));
        }

        static void Envelope(string name, bool sealedEnvelope)
        {
            Asset(name, go => MeshObject(name + "_Mesh", go.transform, m =>
            {
                m.Box(V(0, 0.004f, 0), V(0.29f, 0.008f, 0.18f), M("Paper_AgedCream"), 0.002f);
                var center = V(0, 0.012f, -0.008f);
                m.Triangle(V(-0.145f, 0.009f, -0.09f), V(-0.145f, 0.009f, 0.09f), center, M("Paper_RagIvory"), Vector3.up);
                m.Triangle(V(0.145f, 0.009f, 0.09f), V(0.145f, 0.009f, -0.09f), center, M("Paper_RagIvory"), Vector3.up);
                m.Triangle(V(-0.145f, 0.011f, -0.09f), V(0.145f, 0.011f, -0.09f), center, M("Paper_RagIvory"), Vector3.up);
                var tip = sealedEnvelope ? V(0, 0.013f, -0.025f) : V(0, 0.08f, 0.17f);
                m.Triangle(V(-0.145f, 0.012f, 0.09f), V(0.145f, 0.012f, 0.09f), tip, M("Paper_AgedCream"), Vector3.up);
                m.Triangle(V(-0.145f, 0.0115f, 0.09f), V(0.145f, 0.0115f, 0.09f), tip - Vector3.up * 0.0005f, M("Paper_AgedCream"), Vector3.down);
                if (sealedEnvelope)
                {
                    m.Lathe(V(0, 0.013f, -0.018f), new[] { P(0.023f, 0), P(0.025f, 0.003f), P(0.021f, 0.006f) }, 12, M("Wax_Oxblood"));
                    m.Ring(V(0, 0.0195f, -0.018f), 0.014f, 0.0011f, M("Wax_Oxblood"), Vector3.up, 12);
                }
            }));
        }

        static void PaperText(Transform parent, string name, string value, Vector3 position, float width, Font font)
        {
            var text = new GameObject(name).AddComponent<TextMesh>();
            text.transform.SetParent(parent, false); text.transform.localPosition = position;
            text.transform.localRotation = Quaternion.Euler(90, 0, 0);
            text.font = font; text.fontSize = 96; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
            // Font glyphs are unlit: use darker ink so they remain dark on the half-lit paper.
            text.text = value; text.color = new Color(0.018f, 0.012f, 0.006f); text.lineSpacing = 1.15f;
            font.RequestCharactersInTexture(value, text.fontSize);
            int longest = 1;
            foreach (var line in value.Split('\n'))
            {
                int advance = 0;
                foreach (char c in line) if (font.GetCharacterInfo(c, out CharacterInfo info, text.fontSize)) advance += info.advance;
                longest = Math.Max(longest, advance);
            }
            text.characterSize = width * 10 / longest;
            text.GetComponent<Renderer>().sharedMaterial = font.material;
            text.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            text.gameObject.AddComponent<WorldDocumentText>().textShader = Shader.Find("PurpleGlove/World Document Text");
        }

        static void ExportObj(GameObject root, string name)
        {
            var obj = new StringBuilder("# Purple Glove; metres; right-handed OBJ; UV 1 unit = 1m\nmtllib " + name + ".mtl\n");
            var mtl = new StringBuilder(); var written = new HashSet<string>(); int offset = 1;
            string F(float f) => f.ToString("0.######", CultureInfo.InvariantCulture);
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh; var renderer = filter.GetComponent<MeshRenderer>();
                obj.AppendLine("o " + filter.name);
                foreach (var v in mesh.vertices)
                {
                    var p = root.transform.InverseTransformPoint(filter.transform.TransformPoint(v));
                    obj.AppendLine("v " + F(-p.x) + " " + F(p.y) + " " + F(p.z));
                }
                foreach (var v in mesh.uv) obj.AppendLine("vt " + F(v.x) + " " + F(v.y));
                foreach (var v in mesh.normals)
                {
                    var p = root.transform.InverseTransformDirection(filter.transform.TransformDirection(v)).normalized;
                    obj.AppendLine("vn " + F(-p.x) + " " + F(p.y) + " " + F(p.z));
                }
                for (int slot = 0; slot < mesh.subMeshCount; slot++)
                {
                    var mat = renderer.sharedMaterials[slot]; obj.AppendLine("usemtl " + mat.name);
                    if (written.Add(mat.name))
                    {
                        Color color = mat.HasProperty("_Color") ? mat.color : Color.white;
                        mtl.AppendLine("newmtl " + mat.name + "\nKd " + F(color.r) + " " + F(color.g) + " " + F(color.b) + "\nd 1\nillum 2");
                        if (mat.HasProperty("_MainTex") && mat.mainTexture)
                        {
                            string path = AssetDatabase.GetAssetPath(mat.mainTexture);
                            if (File.Exists(path)) { File.Copy(path, Output + "/obj/Textures/" + Path.GetFileName(path), true); mtl.AppendLine("map_Kd Textures/" + Path.GetFileName(path)); }
                        }
                        mtl.AppendLine();
                    }
                    int[] indices = mesh.GetTriangles(slot);
                    string Index(int i) { int v = offset + i; return v + "/" + v + "/" + v; }
                    for (int i = 0; i < indices.Length; i += 3) obj.AppendLine("f " + Index(indices[i]) + " " + Index(indices[i + 2]) + " " + Index(indices[i + 1]));
                }
                offset += mesh.vertexCount;
            }
            File.WriteAllText(Output + "/obj/" + name + ".obj", obj.ToString());
            File.WriteAllText(Output + "/obj/" + name + ".mtl", mtl.ToString());
        }
    }
}
