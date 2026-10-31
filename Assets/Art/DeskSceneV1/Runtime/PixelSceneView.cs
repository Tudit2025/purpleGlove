using UnityEngine;

namespace PurpleGlove
{
    [ExecuteAlways, RequireComponent(typeof(Camera))]
    public sealed class PixelSceneView : MonoBehaviour
    {
        [Range(240, 1080)] public int verticalResolution = 540;
        public bool pixelate = true;

        void Update()
        {
            if (Application.isPlaying && Input.GetKeyDown(KeyCode.P)) pixelate = !pixelate;
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!pixelate) { Graphics.Blit(source, destination); return; }
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
