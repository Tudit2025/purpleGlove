using UnityEngine;

namespace PurpleGlove
{
    [ExecuteAlways, RequireComponent(typeof(Camera))]
    public sealed class PixelSceneView : MonoBehaviour
    {
        [Range(240, 1080)] public int verticalResolution = 540;
        public bool pixelate = true;
        [Tooltip("Render inspected document text at the display resolution for readable Chinese and cipher digits.")]
        public bool fullResolutionDocuments = true;
        [Header("Vignette")]
        public bool vignette = true;
        [Tooltip("Darkening at the four corners. The center keeps its original brightness.")]
        [Range(0, 1)] public float vignetteIntensity = 0.45f;
        [Tooltip("How far the soft corner gradient extends toward the center.")]
        [Range(0.01f, 1)] public float vignetteSmoothness = 0.75f;
        Material vignetteMaterial;
        SceneObjectInteractor interactor;
        SceneInputGate inputGate;
        DeskScenePreview view;

        void OnEnable()
        {
            if (Application.isPlaying) pixelate = GameSettings.Pixelation;
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!inputGate) inputGate = GetComponent<SceneInputGate>();
            if (!interactor) interactor = GetComponent<SceneObjectInteractor>();
            bool reading = fullResolutionDocuments && interactor && interactor.InspectedObject
                && interactor.InspectedObject.GetComponentInChildren<TextMesh>();
            if (!view) view = GetComponent<DeskScenePreview>();
            reading |= fullResolutionDocuments && view && view.CurrentView == DeskScenePreview.View.DecodeFocus;
            reading |= fullResolutionDocuments && inputGate && inputGate.WorkbenchModal;
            if (!pixelate || reading) { RenderOutput(source, destination); return; }
            int height = Mathf.Min(verticalResolution, source.height);
            int width = Mathf.Max(1, Mathf.RoundToInt(height * source.width / (float)source.height));
            var small = RenderTexture.GetTemporary(width, height, 0, source.format);
            small.filterMode = FilterMode.Point;
            var oldFilter = source.filterMode;
            try
            {
                source.filterMode = FilterMode.Point;
                Graphics.Blit(source, small);
                RenderOutput(small, destination);
            }
            finally { source.filterMode = oldFilter; RenderTexture.ReleaseTemporary(small); }
        }

        void RenderOutput(RenderTexture source, RenderTexture destination)
        {
            if (vignette && vignetteIntensity > 0)
            {
                if (!vignetteMaterial)
                {
                    // Resources keeps the shader available in player builds as well as the editor.
                    var shader = Resources.Load<Shader>("PHVignette");
                    if (shader && shader.isSupported)
                        vignetteMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                }
                if (vignetteMaterial)
                {
                    vignetteMaterial.SetFloat("_Intensity", Mathf.Clamp01(vignetteIntensity));
                    vignetteMaterial.SetFloat("_Smoothness", Mathf.Clamp(vignetteSmoothness, 0.01f, 1));
                    // Apply at the final output resolution so the gradient stays smooth when pixelating.
                    Graphics.Blit(source, destination, vignetteMaterial);
                    return;
                }
            }
            Graphics.Blit(source, destination);
        }

        void OnDisable()
        {
            if (!vignetteMaterial) return;
            if (Application.isPlaying) Destroy(vignetteMaterial);
            else DestroyImmediate(vignetteMaterial);
            vignetteMaterial = null;
        }
    }
}
