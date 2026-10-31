using UnityEngine;
using UnityEditor;
using System.Linq;

namespace PurpleGlove.Editor
{
    public static partial class DeskSceneBuilder
    {
        static void CreateCandle()
        {
            Asset("Candleholder_Lit", go =>
            {
                MeshObject("Candleholder_Brass", go.transform, m => m.Lathe(Vector3.zero, new[] { P(0.105f, 0), P(0.12f, 0.012f), P(0.116f, 0.028f), P(0.06f, 0.04f), P(0.041f, 0.054f), P(0.022f, 0.073f), P(0.022f, 0.118f), P(0.034f, 0.133f), P(0.075f, 0.143f), P(0.081f, 0.153f), P(0.062f, 0.164f) }.Select(p => new Vector2(p.x * 0.85f, p.y * 0.8f)).ToArray(), 10, M("Metal_AgedBrass")));
                var wax = MeshObject("Candle_IvoryWax", go.transform, m => WaxMesh(m, "Wax_Ivory", 0.1312f));
                // A small stylized approximation of wax transmission; avoid an opaque pillar shadow.
                wax.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var flame = MeshObject("Candle_Flame", go.transform, m =>
                {
                    m.Lathe(Vector3.zero, new[] { P(0.002f, 0), P(0.009f, 0.012f), P(0.010f, 0.026f), P(0.006f, 0.051f), P(0, 0.079f) }, 6, M("FlameOuter"));
                    m.Lathe(V(0, 0, -0.004f), new[] { P(0.001f, 0), P(0.0055f, 0.012f), P(0.004f, 0.026f), P(0, 0.048f) }, 6, M("FlameCore"));
                });
                flame.transform.localPosition = V(0, 0.363f, 0);
                flame.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var glow = MeshObject("Candle_Halo", go.transform, m => m.Quad(V(-0.14f, -0.14f, 0), V(0.14f, -0.14f, 0), V(0.14f, 0.14f, 0), V(-0.14f, 0.14f, 0), M("Glow"), Vector3.back));
                var glowMesh = glow.GetComponent<MeshFilter>().sharedMesh;
                glowMesh.uv = glowMesh.vertices.Select(p => new Vector2(p.x / 0.28f + 0.5f, p.y / 0.28f + 0.5f)).ToArray();
                EditorUtility.SetDirty(glowMesh);
                glow.transform.localPosition = V(0, 0.397f, 0);
                glow.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var light = new GameObject("Candle warm light").AddComponent<Light>();
                light.transform.SetParent(go.transform, false); light.transform.localPosition = V(0, 0.397f, 0);
                light.type = LightType.Point; light.color = new Color(1, 0.65f, 0.29f); light.intensity = 2.8f * LightMultiplier; light.range = 3.4f;
                light.shadows = LightShadows.Soft; light.shadowBias = 0.015f; light.shadowNormalBias = 0.025f; light.shadowNearPlane = 0.02f;
                light.shadowStrength = 0.72f;
                light.renderMode = LightRenderMode.ForcePixel;
            });
            foreach (string color in new[] { "Oxblood", "Ochre", "Indigo" })
                Asset("Candle_" + color, go => MeshObject("Candle_" + color + "_Mesh", go.transform, m => WaxMesh(m, "Wax_" + color, 0)));
        }

        static void WaxMesh(LowPolyMesh m, string material, float bottom)
        {
            m.Lathe(V(0, bottom, 0), new[] { P(0.034f, 0), P(0.037f, 0.006f), P(0.036f, 0.18f), P(0.038f, 0.211f), P(0.033f, 0.222f), P(0.022f, 0.217f), P(0.008f, 0.213f) }, 10, M(material));
            for (int i = 0; i < 7; i++)
            {
                float a = i * 0.89f, length = 0.018f + (i % 3) * 0.014f;
                m.Lathe(V(Mathf.Cos(a) * 0.034f, bottom + 0.219f - length, Mathf.Sin(a) * 0.034f), new[] { P(0.001f, 0), P(0.004f, 0.004f), P(0.005f, length) }, 5, M(material));
            }
            for (int i = 0; i < 12; i++) m.Rod(V(-0.010f, bottom + 0.018f + i * 0.014f, -0.035f), V(0.010f, bottom + 0.018f + i * 0.014f, -0.035f), 0.00065f, M("PageEdge"), 4);
            m.Rod(V(0, bottom + 0.214f, 0), V(0.002f, bottom + 0.234f, 0), 0.0017f, M("Soot"), 5);
        }

        static void CreateTools()
        {
            Asset("InkBottle", go => MeshObject("InkBottle_GlassRimAndInk", go.transform, m =>
            {
                m.Lathe(Vector3.zero, new[] { P(0.047f, 0), P(0.064f, 0.012f), P(0.067f, 0.025f), P(0.064f, 0.108f), P(0.058f, 0.127f), P(0.032f, 0.15f), P(0.032f, 0.168f), P(0.038f, 0.171f), P(0.038f, 0.18f), P(0.027f, 0.18f), P(0.026f, 0.149f) }, 10, M("Glass_InkBlack"), true, false);
                m.Disc(V(0, 0.151f, 0), 0.026f, 10, M("Ink"), Vector3.up);
            }));
            Asset("SandShaker", go => MeshObject("SandShaker_Brass", go.transform, m =>
            {
                m.Lathe(Vector3.zero, new[] { P(0.027f, 0), P(0.034f, 0.007f), P(0.034f, 0.018f), P(0.026f, 0.024f), P(0.025f, 0.097f), P(0.033f, 0.103f), P(0.034f, 0.114f), P(0.03f, 0.121f) }, 10, M("Metal_AgedBrass"));
                for (int i = 0; i < 7; i++)
                {
                    float a = i * Mathf.PI * 2 / 7;
                    m.Disc(V(Mathf.Cos(a) * 0.017f, 0.1215f, Mathf.Sin(a) * 0.017f), 0.002f, 5, M("Soot"), Vector3.up);
                }
                m.Disc(V(0, 0.1215f, 0), 0.002f, 5, M("Soot"), Vector3.up);
            }));
            Asset("Quill", go => MeshObject("Quill_ShaftAndBarbs", go.transform, m =>
            {
                m.Rod(V(-0.21f, 0.004f, 0), V(-0.02f, 0.013f, 0), 0.0016f, M("Paper_RagIvory"));
                m.Rod(V(-0.02f, 0.013f, 0), V(0.22f, 0.025f, 0.016f), 0.002f, M("Wax_Ivory"));
                m.Rod(V(-0.21f, 0.004f, 0), V(-0.185f, 0.005f, 0), 0.0014f, M("Ink"), 4);
                for (int i = 0; i < 12; i++)
                {
                    float t = i / 12f, x = -0.035f + t * 0.255f;
                    float width = Mathf.Sin((t + 0.13f) / 1.13f * Mathf.PI) * 0.049f;
                    var a = V(x, 0.014f + t * 0.010f, t * 0.016f);
                    var b = a + V(0.024f, 0.001f, 0.0015f);
                    foreach (float sign in new[] { -1f, 1f })
                    {
                        var c = a + V(-0.019f, -0.004f, width * sign);
                        var d = a + V(0.009f, -0.003f, (width - 0.004f) * sign);
                        m.Quad(a, b, d, c, M("Paper_RagIvory"), Vector3.up);
                        m.Quad(a - V(0, 0.0007f, 0), b - V(0, 0.0007f, 0), d - V(0, 0.0007f, 0), c - V(0, 0.0007f, 0), M("Paper_AgedCream"), Vector3.down);
                    }
                }
            }));
            Asset("PaperKnife", go => MeshObject("PaperKnife_BladeAndHandle", go.transform, m =>
            {
                m.Box(V(-0.105f, 0.012f, 0), V(0.13f, 0.023f, 0.026f), M("Wood_DarkWalnut"), 0.007f);
                m.Box(V(-0.04f, 0.012f, 0), V(0.017f, 0.024f, 0.032f), M("Metal_AgedBrass"), 0.003f);
                m.Polygon(new[] { V(-0.033f, 0.014f, -0.012f), V(0.13f, 0.014f, -0.01f), V(0.19f, 0.014f, 0), V(0.13f, 0.014f, 0.01f), V(-0.033f, 0.014f, 0.012f) }, M("Steel"), Vector3.up);
                m.Polygon(new[] { V(-0.033f, 0.012f, -0.012f), V(0.13f, 0.012f, -0.01f), V(0.19f, 0.012f, 0), V(0.13f, 0.012f, 0.01f), V(-0.033f, 0.012f, 0.012f) }, M("Steel"), Vector3.down);
                foreach (float x in new[] { -0.14f, -0.08f }) m.Disc(V(x, 0.024f, 0), 0.0026f, 6, M("Metal_AgedBrass"), Vector3.up);
            }));
        }

        static void CreateStorage()
        {
            Asset("ReceivingTray", go => MeshObject("Tray_Boards", go.transform, m =>
            {
                m.Box(V(0, 0.012f, 0), V(0.56f, 0.024f, 0.33f), M("Wood_HoneyOak"), 0.004f);
                foreach (float x in new[] { -0.271f, 0.271f }) m.Box(V(x, 0.056f, 0), V(0.025f, 0.09f, 0.35f), M("Wood_HoneyOak"), 0.004f);
                m.Box(V(0, 0.061f, 0.163f), V(0.55f, 0.10f, 0.024f), M("Wood_HoneyOak"), 0.003f);
                m.Box(V(0, 0.031f, -0.163f), V(0.55f, 0.045f, 0.024f), M("Wood_HoneyOak"), 0.004f);
            }), false); // Keep the tray hollow so its envelopes can be picked.
            Asset("CorrespondenceRack", go => MeshObject("Rack_ThreeCompartments", go.transform, m =>
            {
                m.Box(V(0, 0.013f, 0), V(0.66f, 0.026f, 0.30f), M("Wood_HoneyOak"), 0.004f);
                m.Box(V(0, 0.137f, 0.145f), V(0.66f, 0.25f, 0.025f), M("Wood_HoneyOak"), 0.004f);
                m.Box(V(0, 0.056f, -0.145f), V(0.66f, 0.086f, 0.025f), M("Wood_HoneyOak"), 0.004f);
                foreach (float x in new[] { -0.318f, -0.107f, 0.107f, 0.318f })
                {
                    var a = V(x - 0.009f, 0.025f, -0.14f); var b = V(x - 0.009f, 0.10f, -0.14f); var c = V(x - 0.009f, 0.262f, 0.14f); var d = V(x - 0.009f, 0.025f, 0.14f);
                    var offset = V(0.018f, 0, 0);
                    m.Quad(a, b, c, d, M("Wood_HoneyOak"), Vector3.left);
                    m.Quad(a + offset, b + offset, c + offset, d + offset, M("Wood_HoneyOak"), Vector3.right);
                    m.Quad(b, c, c + offset, b + offset, M("Wood_HoneyOak"), Vector3.up);
                    m.Quad(a, b, b + offset, a + offset, M("Wood_HoneyOak"), Vector3.back);
                }
                m.Ring(V(-0.211f, 0.06f, -0.159f), 0.021f, 0.0014f, M("Mark"), Vector3.back, 12);
                foreach (float sign in new[] { -1f, 1f }) m.Rod(V(-0.017f, 0.06f - 0.017f * sign, -0.159f), V(0.017f, 0.06f + 0.017f * sign, -0.159f), 0.0016f, M("Mark"), 4);
                m.Rod(V(0.191f, 0.06f, -0.159f), V(0.231f, 0.06f, -0.159f), 0.0016f, M("Mark"), 4);
                m.Rod(V(0.211f, 0.04f, -0.159f), V(0.211f, 0.08f, -0.159f), 0.0016f, M("Mark"), 4);
            }));
            Asset("PreservationBox", go =>
            {
                MeshObject("Box_BaseAndHardware", go.transform, m =>
                {
                    m.Box(V(0, 0.013f, 0), V(0.49f, 0.026f, 0.31f), M("Wood_DarkWalnut"), 0.004f);
                    foreach (float x in new[] { -0.234f, 0.234f }) m.Box(V(x, 0.09f, 0), V(0.023f, 0.16f, 0.31f), M("Wood_DarkWalnut"), 0.003f);
                    foreach (float z in new[] { -0.144f, 0.144f }) m.Box(V(0, 0.09f, z), V(0.49f, 0.16f, 0.023f), M("Wood_DarkWalnut"), 0.003f);
                    foreach (float x in new[] { -0.20f, 0.20f })
                    {
                        m.Box(V(x, 0.091f, -0.158f), V(0.023f, 0.164f, 0.005f), M("Iron_Blackened"), 0.001f);
                        foreach (float y in new[] { 0.034f, 0.14f }) m.Disc(V(x, y, -0.161f), 0.003f, 6, M("Metal_AgedBrass"), Vector3.back);
                    }
                    m.Box(V(0, 0.13f, -0.16f), V(0.035f, 0.06f, 0.01f), M("Iron_Blackened"), 0.002f);
                });
                var pivot = new GameObject("LidPivot").transform; pivot.SetParent(go.transform, false); pivot.localPosition = V(0, 0.174f, 0.15f);
                MeshObject("Box_HingedLid", pivot, m =>
                {
                    m.Box(V(0, 0.012f, -0.15f), V(0.51f, 0.024f, 0.325f), M("Wood_DarkWalnut"), 0.006f);
                    foreach (float x in new[] { -0.20f, 0.20f }) m.Box(V(x, 0.026f, -0.15f), V(0.024f, 0.005f, 0.324f), M("Iron_Blackened"), 0.001f);
                    m.Box(V(0.035f, 0.025f, -0.14f), V(0.045f, 0.001f, 0.24f), M("Paper_AgedCream"), 0.0002f);
                });
            });
        }

        static void CreateArchitecture()
        {
            CreateVisitationDoor();
            Asset("DoorFrame", go => MeshObject("DoorFrame_Timber", go.transform, m =>
            {
                foreach (float x in new[] { -0.53f, 0.53f }) m.Box(V(x, 1.08f, 0), V(0.115f, 2.16f, 0.18f), M("WalnutDark"), 0.009f);
                m.Box(V(0, 2.15f, 0), V(1.17f, 0.14f, 0.18f), M("WalnutDark"), 0.009f);
                m.Box(V(0, 0.012f, 0), V(1.08f, 0.024f, 0.20f), M("Wood_Weathered"), 0.005f);
            }), false);
            Asset("LetterHatch", go =>
            {
                MeshObject("Hatch_FrameAndRecess", go.transform, m =>
                {
                    m.Box(V(0, 0, 0.10f), V(0.51f, 0.27f, 0.02f), M("Soot"), 0.001f);
                    foreach (float x in new[] { -0.265f, 0.265f }) m.Box(V(x, 0, 0), V(0.04f, 0.32f, 0.17f), M("Wood_Weathered"), 0.004f);
                    foreach (float y in new[] { -0.145f, 0.145f }) m.Box(V(0, y, 0), V(0.57f, 0.035f, 0.17f), M("Wood_Weathered"), 0.004f);
                });
                var hinge = new GameObject("HatchPivot").transform; hinge.SetParent(go.transform, false); hinge.localPosition = V(0, 0.125f, -0.09f);
                MeshObject("Hatch_HingedPanel", hinge, m =>
                {
                    m.Box(V(0, -0.125f, 0), V(0.50f, 0.25f, 0.026f), M("Wood_Weathered"), 0.004f);
                    m.Box(V(0, -0.22f, -0.019f), V(0.07f, 0.022f, 0.018f), M("Iron_Blackened"), 0.003f);
                });
            });
            Asset("WindowFrame", go => MeshObject("Window_TimberLeadedGlass", go.transform, m =>
            {
                m.Box(V(0, 0, 0.045f), V(0.62f, 0.88f, 0.008f), M("WindowNight"), 0.001f);
                foreach (float x in new[] { -0.34f, 0.34f }) m.Box(V(x, 0, 0), V(0.065f, 1.0f, 0.18f), M("WalnutDark"), 0.005f);
                foreach (float y in new[] { -0.47f, 0.47f }) m.Box(V(0, y, 0), V(0.74f, 0.065f, 0.18f), M("WalnutDark"), 0.005f);
                m.Box(V(0, 0, -0.035f), V(0.028f, 0.92f, 0.035f), M("Iron_Blackened"), 0.003f);
                m.Box(V(0, -0.12f, -0.035f), V(0.65f, 0.026f, 0.035f), M("Iron_Blackened"), 0.003f);
                m.Rod(V(-0.31f, -0.42f, -0.035f), V(0.31f, 0.42f, -0.035f), 0.012f, M("Iron_Blackened"), 4);
                m.Rod(V(-0.31f, 0.42f, -0.035f), V(0.31f, -0.42f, -0.035f), 0.012f, M("Iron_Blackened"), 4);
                m.Box(V(0, -0.50f, -0.07f), V(0.84f, 0.07f, 0.30f), M("Wood_Weathered"), 0.006f);
            }), false);
            Asset("WallPanel_Plaster", go => MeshObject("WallPanel_PlasterMesh", go.transform, m => m.Box(V(0, 1.2f, 0), V(1, 2.4f, 0.12f), M("Wall_LimePlaster"), 0.004f), true), false);
            Asset("TimberBeam", go => MeshObject("TimberBeam_Mesh", go.transform, m => m.Box(V(0, 1.2f, 0), V(0.11f, 2.4f, 0.12f), M("WalnutDark"), 0.008f), true), false);
            Asset("FloorSlab", go => MeshObject("FloorSlab_Stone", go.transform, m => m.Box(V(0, -0.025f, 0), V(0.495f, 0.05f, 0.495f), M("Wall_GreyStone"), 0.007f), true), false);
        }
    }
}
