using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace PurpleGlove.Editor
{
    // Explicit menu/batch operation; never rebuilds or changes a user's scene on import.
    public static class MaterialLibraryBuilder
    {
        const string Root = "Assets/Art/MaterialLibraryV1";
        const string Output = "output/material-library-v1";
        const int Size = 128;
        const int PaletteSize = 12;

        [Serializable] public class Entry
        {
            public int index;
            public string id, label, category;
            public float metallic, smoothness;
        }
        [Serializable] public class Manifest { public Entry[] assets; }

        [MenuItem("Purple Glove/Materials/Rebuild V1 Library")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var previousScenes = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                BuildAssets();
                if (!Application.isBatchMode && previousScenes.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else if (previousScenes.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
                throw;
            }
        }

        static void BuildAssets()
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Output + "/generation-prompts.json"));
            if (manifest.assets == null || manifest.assets.Length != 22)
                throw new InvalidDataException("Expected all 22 material definitions.");
            foreach (var entry in manifest.assets)
                if (!File.Exists(Output + "/sources/" + entry.id + ".png"))
                    throw new FileNotFoundException("Missing generated source: " + entry.id);

            Directory.CreateDirectory(Root + "/Textures");
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Scenes");
            Directory.CreateDirectory(Root + "/Preview");
            var validation = new List<string> { "Material library V1 validation", "128x128 RGB | 12-color palette maximum | Point | Repeat | no mipmaps | uncompressed | sRGB" };
            foreach (var entry in manifest.assets)
            {
                var source = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (!ImageConversion.LoadImage(source, File.ReadAllBytes(Output + "/sources/" + entry.id + ".png")))
                    throw new InvalidDataException("Invalid PNG: " + entry.id);
                var pixels = Downsample(source);
                UnityEngine.Object.DestroyImmediate(source);
                MatchEdges(pixels);
                pixels = Quantize(pixels);
                ValidatePixels(entry.id, pixels);
                var texture = new Texture2D(Size, Size, TextureFormat.RGB24, false);
                texture.SetPixels32(pixels);
                texture.Apply();
                var texturePath = Root + "/Textures/T_" + entry.id + ".png";
                File.WriteAllBytes(texturePath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.alphaIsTransparency = false;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.anisoLevel = 0;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = Size;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.isReadable = false;
                importer.SaveAndReimport();
                var importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                var materialPath = Root + "/Materials/M_" + entry.id + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(Shader.Find("Standard"));
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                material.mainTexture = importedTexture;
                material.color = Color.white;
                material.SetFloat("_Metallic", entry.metallic);
                material.SetFloat("_Glossiness", entry.smoothness);
                material.SetFloat("_GlossyReflections", 1);
                material.SetFloat("_SpecularHighlights", 1);
                material.SetFloat("_Mode", 0);
                material.SetInt("_SrcBlend", (int)BlendMode.One);
                material.SetInt("_DstBlend", (int)BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = -1;
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                if (importedTexture.width != Size || importedTexture.height != Size || material.mainTexture == null)
                    throw new InvalidDataException("Invalid imported material: " + entry.id);
                validation.Add(entry.id + " | PASS | colors=" + pixels.Select(p => (p.r << 16) | (p.g << 8) | p.b).Distinct().Count() + " | opposite edge mismatch=0");
            }
            AssetDatabase.SaveAssets();
            BuildPreview(manifest.assets);
            AssetDatabase.SaveAssets();
            AssetDatabase.ExportPackage(new[] { Root + "/Textures", Root + "/Materials", Root + "/Scenes", Root + "/Preview", Root + "/README.md" }, Output + "/PurpleGlove_Materials_V1.unitypackage", ExportPackageOptions.Recurse);
            File.WriteAllLines(Output + "/validation.txt", validation);
            Debug.Log("MATERIAL_LIBRARY_V1_PASS: 22 textures, 22 materials, pixel/palette/edge/import/reference checks passed.");
        }

        // Area averaging creates a consistent real 128px source grid regardless of the AI output size.
        static Color32[] Downsample(Texture2D source)
        {
            var input = source.GetPixels32();
            var result = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int x0 = x * source.width / Size, x1 = (x + 1) * source.width / Size;
                int y0 = y * source.height / Size, y1 = (y + 1) * source.height / Size;
                int red = 0, green = 0, blue = 0, count = 0;
                for (int sy = y0; sy < y1; sy++)
                for (int sx = x0; sx < x1; sx++)
                {
                    var pixel = input[sy * source.width + sx];
                    red += pixel.r; green += pixel.g; blue += pixel.b; count++;
                }
                result[y * Size + x] = new Color32((byte)(red / count), (byte)(green / count), (byte)(blue / count), 255);
            }
            return result;
        }

        // Narrow four-pixel border conditioning; the outermost opposite texels match exactly.
        static void MatchEdges(Color32[] pixels)
        {
            const int band = 4;
            for (int y = 0; y < Size; y++)
            for (int distance = 0; distance < band; distance++)
            {
                int a = y * Size + distance, b = y * Size + Size - 1 - distance;
                var average = Color32.Lerp(pixels[a], pixels[b], 0.5f);
                float weight = (band - distance) / (float)band;
                pixels[a] = Color32.Lerp(pixels[a], average, weight);
                pixels[b] = Color32.Lerp(pixels[b], average, weight);
            }
            for (int x = 0; x < Size; x++)
            for (int distance = 0; distance < band; distance++)
            {
                int a = distance * Size + x, b = (Size - 1 - distance) * Size + x;
                var average = Color32.Lerp(pixels[a], pixels[b], 0.5f);
                float weight = (band - distance) / (float)band;
                pixels[a] = Color32.Lerp(pixels[a], average, weight);
                pixels[b] = Color32.Lerp(pixels[b], average, weight);
            }
        }

        // Deterministic k-means limits tonal complexity equally for every surface.
        static Color32[] Quantize(Color32[] pixels)
        {
            var sorted = pixels.OrderBy(p => p.r * 0.2126f + p.g * 0.7152f + p.b * 0.0722f).ToArray();
            var centers = new Vector3[PaletteSize];
            for (int k = 0; k < PaletteSize; k++)
            {
                var p = sorted[(int)((k + 0.5f) / PaletteSize * (sorted.Length - 1))];
                centers[k] = new Vector3(p.r, p.g, p.b);
            }
            var labels = new int[pixels.Length];
            for (int iteration = 0; iteration < 16; iteration++)
            {
                var sums = new Vector3[PaletteSize];
                var counts = new int[PaletteSize];
                for (int i = 0; i < pixels.Length; i++)
                {
                    var p = new Vector3(pixels[i].r, pixels[i].g, pixels[i].b);
                    int best = 0;
                    float bestDistance = float.MaxValue;
                    for (int k = 0; k < PaletteSize; k++)
                    {
                        float distance = (p - centers[k]).sqrMagnitude;
                        if (distance < bestDistance) { best = k; bestDistance = distance; }
                    }
                    labels[i] = best; sums[best] += p; counts[best]++;
                }
                for (int k = 0; k < PaletteSize; k++)
                    if (counts[k] > 0) centers[k] = sums[k] / counts[k];
            }
            return labels.Select(k => new Color32((byte)Mathf.RoundToInt(centers[k].x), (byte)Mathf.RoundToInt(centers[k].y), (byte)Mathf.RoundToInt(centers[k].z), 255)).ToArray();
        }

        static void ValidatePixels(string id, Color32[] pixels)
        {
            for (int i = 0; i < Size; i++)
                if (!pixels[i * Size].Equals(pixels[i * Size + Size - 1]) || !pixels[i].Equals(pixels[(Size - 1) * Size + i]))
                    throw new InvalidDataException("Repeat boundary mismatch: " + id);
            if (pixels.Select(p => (p.r << 16) | (p.g << 8) | p.b).Distinct().Count() > PaletteSize)
                throw new InvalidDataException("Palette overflow: " + id);
        }

        static void BuildPreview(Entry[] entries)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.44f, 0.48f, 0.53f);
            RenderSettings.ambientEquatorColor = new Color(0.30f, 0.30f, 0.29f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.14f, 0.12f);
            RenderSettings.reflectionIntensity = 0.8f;
            var camera = new GameObject("Library Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -16);
            camera.orthographic = true;
            camera.orthographicSize = 5.45f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.075f, 0.082f, 0.085f);
            camera.allowHDR = false;
            camera.tag = "MainCamera";
            var key = new GameObject("Neutral warm key").AddComponent<Light>();
            key.type = LightType.Directional;
            key.transform.rotation = Quaternion.Euler(32, -28, 0);
            key.color = new Color(1f, 0.93f, 0.82f);
            key.intensity = 1.15f;
            var fill = new GameObject("Cool fill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.transform.rotation = Quaternion.Euler(-20, 145, 0);
            fill.color = new Color(0.65f, 0.75f, 1);
            fill.intensity = 0.5f;
            var sphere = CreateFacetedSphere();
            var meshPath = Root + "/Preview/FacetedSample.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing == null) AssetDatabase.CreateAsset(sphere, meshPath);
            else { EditorUtility.CopySerialized(sphere, existing); UnityEngine.Object.DestroyImmediate(sphere); sphere = existing; }
            AddLabel("PURPLE GLOVE / MATERIAL LIBRARY 01", new Vector3(0, 4.85f, 0), 0.10f);
            AddLabel("22 SURFACES   /   128 PX   /   POINT SAMPLING   /   BUILT-IN STANDARD", new Vector3(0, 4.4f, 0), 0.04f);
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                float x = (i % 6 - 2.5f) * 2.7f, y = 3.15f - (i / 6) * 2.35f;
                var go = new GameObject(entry.id);
                go.transform.position = new Vector3(x, y, 0);
                go.transform.rotation = Quaternion.Euler(0, 22, 0);
                go.AddComponent<MeshFilter>().sharedMesh = sphere;
                go.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/M_" + entry.id + ".mat");
                AddLabel(entry.index.ToString("00") + " " + entry.id.Replace('_', ' '), new Vector3(x, y - 1f, 0), 0.035f);
            }
            AddLabel("Neutral-light response samples. Production UV target: 128 texels / metre.", new Vector3(0, -5.1f, 0), 0.035f);
            EditorSceneManager.SaveScene(scene, Root + "/Scenes/MaterialLibraryPreview.unity");
            // Low-resolution render enlarged without filtering, matching the project's pixel presentation.
            var target = RenderTexture.GetTemporary(960, 600, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.aspect = 1.6f;
            camera.Render();
            RenderTexture.active = target;
            var screenshot = new Texture2D(960, 600, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, 960, 600), 0, 0);
            screenshot.Apply();
            File.WriteAllBytes(Output + "/unity-material-preview.png", screenshot.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(screenshot);
            camera.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
        }

        static void AddLabel(string text, Vector3 position, float size)
        {
            var label = new GameObject(text).AddComponent<TextMesh>();
            label.transform.position = position;
            label.text = text;
            label.fontSize = 48;
            label.characterSize = size;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = new Color(0.85f, 0.82f, 0.73f);
        }

        static Mesh CreateFacetedSphere()
        {
            const int segments = 12, rings = 8;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            // Diameter is 1.6m; UV distances approximate metres, so 1 UV unit = 1m.
            for (int row = 0; row < rings; row++)
            for (int column = 0; column < segments; column++)
            {
                var corners = new[] { new Vector2(column / (float)segments, row / (float)rings), new Vector2((column + 1f) / segments, row / (float)rings), new Vector2((column + 1f) / segments, (row + 1f) / rings), new Vector2(column / (float)segments, (row + 1f) / rings) };
                foreach (int corner in new[] { 0, 1, 2, 0, 2, 3 })
                {
                    var uv = corners[corner];
                    float longitude = uv.x * Mathf.PI * 2, latitude = uv.y * Mathf.PI;
                    vertices.Add(new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude)) * 0.8f);
                    uvs.Add(new Vector2(uv.x * 2 * Mathf.PI * 0.8f, uv.y * Mathf.PI * 0.8f));
                    triangles.Add(triangles.Count);
                }
            }
            var mesh = new Mesh { name = "Faceted material sample (12 x 8)" };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
