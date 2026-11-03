using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PurpleGlove.Editor
{
    public static partial class DeskSceneBuilder
    {
        [MenuItem("Purple Glove/Acts/Install First Act Details")]
        public static void InstallFirstActDetails()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            LoadSavedSceneAssets();
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            if (!archive) throw new InvalidOperationException("Install the archive first.");
            var layout = archive.transform.Find(ActDocumentGroup);
            if (!layout) throw new InvalidOperationException("Missing authored document layout.");
            foreach (var item in archive.acts[0].documents)
                if (item) UnityEngine.Object.DestroyImmediate(item.gameObject);
            archive.acts[0].documents = Enumerable.Range(0, ActDocumentPages[0].Length)
                .Select(i => CreateFirstActDocument(layout, i)).ToArray();
            ArrangeFirstActDocuments(archive);
            foreach (var item in archive.acts[0].documents) item.gameObject.SetActive(false);
            DeskDeliveryBuilder.InstallInScene(Camera.main);
            ConfigureOperationalDocuments(archive);
            EditorUtility.SetDirty(archive);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("FIRST_ACT_DETAILS_INSTALLED");
        }

        static void ArrangeFirstActDocuments(ActArchiveController archive)
        {
            var mat = GameObject.Find("WritingMat").transform;
            // Keep the cipher on the right, beside the decoding tools, with room below for its plaintext.
            var positions = new[] { V(-0.235f, 0.014f, 0.20f), V(0.235f, 0.014f, -0.26f), V(0.235f, 0.014f, 0.20f) };
            var angles = new[] { -2f, -3f, 2f };
            for (int i = 0; i < archive.acts[0].documents.Length; i++)
            {
                var paper = archive.acts[0].documents[i].transform;
                if (i == 3) { paper.SetPositionAndRotation(mat.position, mat.rotation); paper.localScale = Vector3.one; continue; }
                paper.position = mat.TransformPoint(positions[i]);
                paper.rotation = mat.rotation * Quaternion.Euler(0, angles[i], 0);
                paper.localScale = Vector3.one;
            }
        }

        static SceneObject CreateFirstActDocument(Transform parent, int index)
        {
            actDocumentFont = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/SourceHanSerifCN-Regular.otf");
            var paper = new GameObject("Act1_" + (index + 1).ToString("00") + "_" + ActDocumentPages[0][index][0]);
            paper.transform.SetParent(parent, false);
            var item = paper.AddComponent<SceneObject>();
            item.screenFill = 0.82f; item.minimumViewDistance = 0.18f;
            item.inspectionFill = new Color(0.85f, 0.78f, 0.64f);
            var box = paper.GetComponent<BoxCollider>();
            box.center = V(0, 0.004f, 0);
            box.size = index == 1 ? V(0.35f, 0.012f, 0.27f) : V(0.42f, 0.012f, 0.60f);
            if (index == 0) BuildDucalLetter(paper.transform);
            else if (index == 1)
            {
                BuildMasterSlip(paper.transform);
                DeskObjectMemoryBuilder.InstallFirstActMemory(item, Camera.main.GetComponent<DialogueController>());
            }
            else if (index == 2) BuildGuildLetter(paper.transform);
            else BuildTransferApplication(paper.transform);
            return item;
        }

        static void BuildGuildLetter(Transform paper)
        {
            MeshObject("Act1_GuildLetterPaper", paper, m => m.Sheet(0.42f, 0.60f, M("Paper_AgedCream"), 0.003f));
            string body = string.Join("\n", ActDocumentPages[0][2][1].Split('\n').Select(WrapGuildParagraph));
            var bodyInk = FirstActInk(paper, "Body", body, HandwrittenChinese, -0.176f, 0.244f, 0.352f, 0.420f);
            var signature = FirstActInk(paper, "Signature", "印务行会会长", HandwrittenChinese,
                0.176f, -0.228f, 0.14f, 0.018f);
            signature.anchor = TextAnchor.UpperRight;
            signature.characterSize = bodyInk.characterSize;
        }

        static string WrapGuildParagraph(string paragraph)
        {
            var lines = new List<string>();
            for (int start = 0; start < paragraph.Length;)
            {
                int count = Math.Min(26, paragraph.Length - start);
                while (count > 1 && start + count < paragraph.Length
                    && "，。；：！？、）”’".IndexOf(paragraph[start + count]) >= 0) count--;
                lines.Add(paragraph.Substring(start, count));
                start += count;
            }
            return string.Join("\n", lines);
        }

        static TextMesh FirstActInk(Transform paper, string name, string value, Font font,
            float x, float z, float width, float height, bool centered = false)
        {
            PaperText(paper, name, value, V(x, 0.010f, z), width, font);
            var text = paper.Find(name).GetComponent<TextMesh>();
            text.anchor = centered ? TextAnchor.UpperCenter : TextAnchor.UpperLeft;
            text.alignment = centered ? TextAlignment.Center : TextAlignment.Left;
            text.lineSpacing = 1.22f;
            float inkHeight = text.GetComponent<Renderer>().localBounds.size.y;
            if (inkHeight > height) text.characterSize *= height / inkHeight;
            return text;
        }

        static void BuildDucalLetter(Transform paper)
        {
            MeshObject("Act1_DucalIvoryPaper", paper, m => m.Sheet(0.42f, 0.60f, M("Paper_RagIvory"), 0.003f));
            FirstActInk(paper, "Ducal letterhead", "CASA CONTI  ·  MILANO", documentFont, 0, 0.224f, 0.27f, 0.018f, true);
            FirstActInk(paper, "Title", "核 验 委 托", actDocumentFont, 0, 0.194f, 0.22f, 0.027f, true);
            FirstActInk(paper, "Body", ActDocumentPages[0][0][1], actDocumentFont, -0.167f, 0.143f, 0.334f, 0.355f);
            FirstActInk(paper, "Signature", "孔蒂公爵 · 米兰内廷", actDocumentFont, 0.157f, -0.228f, 0.19f, 0.017f).anchor = TextAnchor.UpperRight;
            // Printed gilt scrollwork, with a crowned shield at the head and a smaller seal at the foot.
            Variant("FirstActGilt", "SceneBrass", new Color(0.64f, 0.36f, 0.12f));
            M("FirstActGilt").SetFloat("_OutlineWidth", 0);
            MeshObject("Act1_DucalHeadAndFootOrnaments", paper, m =>
            {
                var ink = M("FirstActGilt");
                void Stroke(float x1, float z1, float x2, float z2, float r = 0.00065f) =>
                    m.Rod(V(x1, 0.009f, z1), V(x2, 0.009f, z2), r, ink, 4);
                foreach (float edge in new[] { 0.261f, -0.271f })
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        Stroke(side * 0.037f, edge, side * 0.178f, edge);
                        Stroke(side * 0.045f, edge - 0.004f, side * 0.168f, edge - 0.004f, 0.00035f);
                        for (int curl = 0; curl < 4; curl++)
                        {
                            float cx = side * (0.055f + curl * 0.034f);
                            Vector3 last = V(cx, 0.009f, edge);
                            for (int step = 1; step <= 24; step++)
                            {
                                float a = step / 24f * Mathf.PI * 3;
                                float r = 0.014f * (1 - step / 29f);
                                var next = V(cx + side * Mathf.Sin(a) * r, 0.009f, edge + 0.009f - Mathf.Cos(a) * r);
                                m.Rod(last, next, 0.00055f, ink, 4); last = next;
                            }
                            // Paired pointed leaves beneath each scroll.
                            m.Quad(V(cx, 0.009f, edge), V(cx + side * 0.010f, 0.009f, edge - 0.005f),
                                V(cx + side * 0.016f, 0.009f, edge - 0.017f), V(cx + side * 0.005f, 0.009f, edge - 0.012f), ink, Vector3.up);
                        }
                    }
                    var shield = new[] { V(-0.022f, 0.009f, edge + 0.012f), V(0.022f, 0.009f, edge + 0.012f),
                        V(0.019f, 0.009f, edge - 0.006f), V(0, 0.009f, edge - 0.021f), V(-0.019f, 0.009f, edge - 0.006f) };
                    for (int i = 0; i < shield.Length; i++) m.Rod(shield[i], shield[(i + 1) % shield.Length], 0.001f, ink, 4);
                    Stroke(-0.009f, edge, 0.009f, edge); Stroke(0, edge + 0.008f, 0, edge - 0.010f);
                }
                for (int i = -2; i <= 2; i++)
                {
                    float x = i * 0.008f;
                    Stroke(x, 0.275f, x * 1.17f, 0.285f - Mathf.Abs(i) * 0.0015f);
                    m.Disc(V(x * 1.17f, 0.009f, 0.285f - Mathf.Abs(i) * 0.0015f), 0.0018f, 8, ink, Vector3.up);
                }
                Stroke(-0.020f, 0.274f, 0.020f, 0.274f);
            });
        }

        static void BuildMasterSlip(Transform paper)
        {
            Variant("Paper_FirstActWear", "Parchment_Aged", new Color(0.86f, 0.79f, 0.67f));
            M("Paper_FirstActWear").SetFloat("_OutlineWidth", 0);
            // Horizontal strips leave three real V-shaped cuts at the upper left, including on the reverse.
            MeshObject("Act1_MasterThreeCutPaper", paper, m =>
            {
                float[] z = { 0.135f, 0.122f, 0.112f, 0.102f, 0.094f, 0.084f, 0.074f, 0.066f, 0.056f, 0.046f, 0.02f, -0.03f, -0.08f, -0.135f };
                float Left(int i) => i == 2 || i == 5 || i == 8 ? -0.152f : -0.175f + Mathf.Sin(i * 2.7f) * 0.0015f;
                float Right(int i) => 0.175f + Mathf.Sin(i * 1.8f) * 0.0012f;
                for (int i = 0; i < z.Length - 1; i++)
                {
                    var a = V(Left(i), 0.004f, z[i]); var b = V(Right(i), 0.004f, z[i]);
                    var c = V(Right(i + 1), 0.004f, z[i + 1]); var d = V(Left(i + 1), 0.004f, z[i + 1]);
                    m.Quad(a, b, c, d, M("Parchment_Aged"), Vector3.up);
                    Vector3 down = Vector3.down * 0.0015f;
                    m.Quad(a + down, b + down, c + down, d + down, M("Parchment_Aged"), Vector3.down);
                    m.Quad(a, d, d + down, a + down, M("PageEdge"), Vector3.left);
                    m.Quad(b, c, c + down, b + down, M("PageEdge"), Vector3.right);
                }
            });
            FirstActInk(paper, "Address", "卢卡：", HandwrittenChinese, -0.124f, 0.087f, 0.043f, 0.018f);
            FirstActInk(paper, "Cipher", ActDocumentPages[0][1][1], documentFont, 0, 0.037f, 0.278f, 0.025f, true);
            FirstActInk(paper, "Seven petal note", "七瓣花，新版。\n查账时不要只认签名。", HandwrittenChinese, -0.124f, -0.018f, 0.238f, 0.045f);
            FirstActInk(paper, "Master signature", "Matteo", notesFont, 0.13f, -0.087f, 0.073f, 0.021f).anchor = TextAnchor.UpperRight;
            MeshObject("Act1_MasterPaperWear", paper, m =>
            {
                // Wear stays along the edges and folds, away from cipher letters and the three cuts.
                for (int i = 0; i < 19; i++)
                {
                    float x = -0.157f + i * 0.017f;
                    m.Disc(V(x, 0.0044f, -0.124f + Mathf.Sin(i * 2) * 0.003f), 0.0016f + i % 3 * 0.001f, 5, M("Paper_FirstActWear"), Vector3.up);
                }
                m.Rod(V(-0.163f, 0.0045f, -0.072f), V(0.162f, 0.0045f, -0.069f), 0.00038f, M("Paper_FirstActWear"), 4);
                m.Rod(V(0.146f, 0.0045f, -0.125f), V(0.150f, 0.0045f, 0.124f), 0.0003f, M("Paper_FirstActWear"), 4);
            });
        }

        public static void BuildFirstActPackets(SealedDeliveryController delivery)
        {
            LoadSavedSceneAssets();
            var root = delivery.packet.transform;
            if (!delivery.ordinaryPacketVisual)
            {
                var children = root.Cast<Transform>().ToArray();
                var ordinary = new GameObject("Ordinary sealed packet").transform;
                ordinary.SetParent(root, false);
                foreach (var child in children) child.SetParent(ordinary, false);
                delivery.ordinaryPacketVisual = ordinary.gameObject;
            }
            if (delivery.firstActPacketsVisual) UnityEngine.Object.DestroyImmediate(delivery.firstActPacketsVisual);
            var pair = new GameObject("Act 1 - brown and white packets").transform; pair.SetParent(root, false);
            Variant("Paper_FirstActBrownWrap", "Paper_AgedCream", new Color(0.53f, 0.31f, 0.16f));
            MeshObject("Act1_BrownMasterPacket", pair, m =>
            {
                m.Box(V(-0.013f, 0.010f, 0.004f), V(0.239f, 0.020f, 0.128f), M("Paper_FirstActBrownWrap"), 0.002f);
                m.Box(V(-0.068f, 0.021f, 0.004f), V(0.003f, 0.0015f, 0.129f), M("Stitch"), 0.0002f);
                m.Lathe(V(-0.068f, 0.022f, -0.026f), new[] { P(0.009f, 0), P(0.011f, 0.001f), P(0.009f, 0.003f) }, 9, M("Wax_Oxblood"));
            });
            var white = MeshObject("Act1_WhiteDucalPacket", pair, m =>
            {
                m.Box(V(0.048f, 0.030f, -0.012f), V(0.170f, 0.017f, 0.099f), M("Paper_RagIvory"), 0.002f);
                m.Box(V(0.043f, 0.039f, -0.012f), V(0.014f, 0.002f, 0.10f), M("Leather_Oxblood"), 0.0005f);
                m.Lathe(V(0.043f, 0.040f, -0.015f), new[] { P(0.012f, 0), P(0.014f, 0.001f), P(0.012f, 0.004f) }, 12, M("Wax_Oxblood"));
                m.Ring(V(0.043f, 0.0445f, -0.015f), 0.008f, 0.0006f, M("SceneBrass"), Vector3.up, 12);
            });
            var fill = new GameObject("Packet bounce light").AddComponent<Light>();
            fill.transform.SetParent(white.transform, false); fill.transform.localPosition = V(0, 0.18f, -0.12f);
            fill.type = LightType.Point; fill.range = 0.65f; fill.intensity = 1.35f;
            fill.color = new Color(1, 0.87f, 0.69f); fill.renderMode = LightRenderMode.ForcePixel;
            fill.shadows = LightShadows.None; fill.cullingMask = (1 << 8) | 1;
            delivery.firstActPacketsVisual = pair.gameObject;
            delivery.ordinaryPacketVisual.SetActive(true); pair.gameObject.SetActive(false);
            var box = root.GetComponent<BoxCollider>();
            box.center = V(0, 0.022f, 0); box.size = V(0.269f, 0.047f, 0.139f);
        }
    }
}
