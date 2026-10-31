using UnityEngine;

namespace PurpleGlove
{
    /// <summary>Depth-tested TextMesh ink that follows Unity's dynamic font atlas.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(TextMesh))]
    public sealed class WorldDocumentText : MonoBehaviour
    {
        public Shader textShader;
        TextMesh textMesh;
        Renderer textRenderer;
        Material originalMaterial, inkMaterial;

        void OnEnable()
        {
            if (!textShader) return;
            textMesh = GetComponent<TextMesh>();
            textRenderer = GetComponent<Renderer>();
            if (!textMesh.font || !textRenderer) return;
            originalMaterial = textRenderer.sharedMaterial;
            inkMaterial = new Material(textShader) { name = "Document Ink (Runtime)", hideFlags = HideFlags.HideAndDontSave };
            inkMaterial.mainTexture = textMesh.font.material.mainTexture;
            textRenderer.sharedMaterial = inkMaterial;
            Font.textureRebuilt += RefreshAtlas;
        }

        void RefreshAtlas(Font font)
        {
            if (textMesh && textMesh.font == font && inkMaterial)
                inkMaterial.mainTexture = font.material.mainTexture;
        }

        void OnDisable()
        {
            Font.textureRebuilt -= RefreshAtlas;
            if (textRenderer && textRenderer.sharedMaterial == inkMaterial) textRenderer.sharedMaterial = originalMaterial;
            if (inkMaterial)
            {
                if (Application.isPlaying) Destroy(inkMaterial);
                else DestroyImmediate(inkMaterial);
            }
            inkMaterial = null;
        }
    }
}
