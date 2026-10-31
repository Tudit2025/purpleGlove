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
        SceneObjectInteractor interactor;
        SceneInputGate inputGate;
        DeskScenePreview view;
        CipherWorkbench workbench;

        void Update()
        {
            if (!inputGate) inputGate = GetComponent<SceneInputGate>();
            if (!workbench) workbench = GetComponent<CipherWorkbench>();
            if (Application.isPlaying && (!inputGate || !inputGate.BlocksWorldInput)
                && (!workbench || !workbench.UsesDecodeKeyboard) && Input.GetKeyDown(KeyCode.P)) pixelate = !pixelate;
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!interactor) interactor = GetComponent<SceneObjectInteractor>();
            bool reading = fullResolutionDocuments && interactor && interactor.InspectedObject
                && interactor.InspectedObject.GetComponentInChildren<TextMesh>();
            if (!view) view = GetComponent<DeskScenePreview>();
            reading |= fullResolutionDocuments && view && view.CurrentView == DeskScenePreview.View.DecodeFocus;
            reading |= fullResolutionDocuments && inputGate && inputGate.WorkbenchModal;
            if (!pixelate || reading) { Graphics.Blit(source, destination); return; }
            int height = Mathf.Min(verticalResolution, source.height);
            int width = Mathf.Max(1, Mathf.RoundToInt(height * source.width / (float)source.height));
            var small = RenderTexture.GetTemporary(width, height, 0, source.format);
            small.filterMode = FilterMode.Point;
            var oldFilter = source.filterMode;
            try
            {
                source.filterMode = FilterMode.Point;
                Graphics.Blit(source, small);
                Graphics.Blit(small, destination);
            }
            finally { source.filterMode = oldFilter; RenderTexture.ReleaseTemporary(small); }
        }
    }
}
