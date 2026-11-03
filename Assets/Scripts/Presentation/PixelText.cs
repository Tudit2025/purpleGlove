using UnityEngine;
using UnityEngine.UI;

namespace PurpleGlove
{
    /// <summary>Pixel lettering without lowering the resolution of the canvas or its hit areas.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Text))]
    public sealed class PixelText : MonoBehaviour, IMeshModifier
    {
        [Tooltip("Size of a lettering pixel at 1080p; rounded to whole display pixels.")]
        [Range(1, 4)] public float pixelSize = 2;
        Text label;
        Material originalMaterial, pixelMaterial;

        public static void Apply(Text text, float size = 2)
        {
            if (!text) return;
            var effect = text.GetComponent<PixelText>();
            if (!effect) effect = text.gameObject.AddComponent<PixelText>();
            effect.pixelSize = size;
            effect.Refresh();
        }

        void OnEnable()
        {
            label = GetComponent<Text>();
            originalMaterial = label.material;
            // Resources ensures the shader is also included in player builds.
            var shader = Resources.Load<Shader>("PHPixelText");
            if (shader && shader.isSupported)
                pixelMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            GameSettings.PixelationChanged += Refresh;
            label.SetVerticesDirty();
            Refresh();
        }

        public void ModifyMesh(Mesh mesh)
        {
            using (var vertices = new VertexHelper(mesh))
            {
                ModifyMesh(vertices);
                vertices.FillMesh(mesh);
            }
        }

        public void ModifyMesh(VertexHelper vertices)
        {
            if (!isActiveAndEnabled || !label) return;
            if (label.canvas) label.canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            // Each Text glyph is a quad. Carry its atlas bounds to the shader so larger
            // pixel blocks cannot pick up ink from a neighbouring packed glyph.
            var vertex = new UIVertex();
            for (int start = 0; start + 3 < vertices.currentVertCount; start += 4)
            {
                var min = new Vector2(float.MaxValue, float.MaxValue);
                var max = new Vector2(float.MinValue, float.MinValue);
                for (int corner = 0; corner < 4; corner++)
                {
                    vertices.PopulateUIVertex(ref vertex, start + corner);
                    min = Vector2.Min(min, vertex.uv0); max = Vector2.Max(max, vertex.uv0);
                }
                for (int corner = 0; corner < 4; corner++)
                {
                    vertices.PopulateUIVertex(ref vertex, start + corner);
                    vertex.uv1 = new Vector4(min.x, min.y, max.x, max.y);
                    vertices.SetUIVertex(vertex, start + corner);
                }
            }
        }

        void Refresh()
        {
            if (!label || !isActiveAndEnabled) return;
            if (pixelMaterial) pixelMaterial.SetFloat("_PixelSize", Mathf.Clamp(pixelSize, 1, 4));
            label.material = GameSettings.Pixelation && pixelMaterial ? pixelMaterial : originalMaterial;
        }

        void OnDisable()
        {
            GameSettings.PixelationChanged -= Refresh;
            if (label && label.material == pixelMaterial) label.material = originalMaterial;
            if (label) label.SetVerticesDirty();
            if (pixelMaterial) Destroy(pixelMaterial);
            pixelMaterial = null;
        }
    }
}
