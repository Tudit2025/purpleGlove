using UnityEngine;

namespace PurpleGlove
{
    /// <summary>A small, warm light that replaces the mouse pointer while playing.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Purple Glove/Glow Cursor")]
    public sealed class GlowCursor : MonoBehaviour
    {
        const int TextureSize = 32;
        static readonly Vector2 Hotspot = new Vector2(TextureSize / 2f, TextureSize / 2f);
        static GlowCursor instance;

        [SerializeField, ColorUsage(false)] Color coreColor = new Color(1f, 0.97f, 0.87f);
        [SerializeField, ColorUsage(false)] Color glowColor = new Color(1f, 0.78f, 0.43f);
        [SerializeField, Range(1f, 5f)] float coreRadius = 2.5f;
        [SerializeField, Range(7f, 15f)] float glowRadius = 13f;

        Texture2D cursorTexture;
        bool ownsCursor;
        bool previousVisibility;
        CursorLockMode previousLockMode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            // Also permits a scene-authored instance with custom colors and size.
            if (instance || FindFirstObjectByType<GlowCursor>()) return;
            new GameObject("Glow Cursor").AddComponent<GlowCursor>();
        }

        void Awake()
        {
            if (instance && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;
            // The automatically created object is a root and survives scene changes.
            if (!transform.parent) DontDestroyOnLoad(gameObject);
        }

        void OnEnable()
        {
            if (instance != this) return;
            if (!cursorTexture) cursorTexture = CreateTexture();
            if (Application.isFocused) ApplyCursor();
        }

        void OnApplicationFocus(bool focused)
        {
            if (instance != this || !isActiveAndEnabled) return;
            if (focused) ApplyCursor();
            else RestoreCursor();
        }

        void OnDisable()
        {
            if (instance == this) RestoreCursor();
        }

        void OnApplicationQuit()
        {
            if (instance == this) RestoreCursor();
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                RestoreCursor();
                instance = null;
            }

            if (cursorTexture) Destroy(cursorTexture);
        }

        void ApplyCursor()
        {
            if (!cursorTexture) return;
            if (!ownsCursor)
            {
                previousVisibility = Cursor.visible;
                previousLockMode = Cursor.lockState;
                ownsCursor = true;
            }

            // A native cursor stays responsive and sharp over the pixelated scene.
            // Keep it visible: SetCursor replaces the arrow with this texture.
            Cursor.lockState = CursorLockMode.None;
            Cursor.SetCursor(cursorTexture, Hotspot, CursorMode.Auto);
            Cursor.visible = true;
        }

        void RestoreCursor()
        {
            if (!ownsCursor) return;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            Cursor.lockState = previousLockMode;
            Cursor.visible = previousVisibility;
            ownsCursor = false;
        }

        Texture2D CreateTexture()
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "Glow Cursor (Runtime)",
                hideFlags = HideFlags.HideAndDontSave,
                alphaIsTransparency = true,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[TextureSize * TextureSize];
            float core = Mathf.Clamp(coreRadius, 1f, 5f);
            float glow = Mathf.Clamp(glowRadius, 7f, 15f);
            var outlineColor = new Color(0.12f, 0.09f, 0.055f);

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Hotspot);
                    float falloff = Mathf.Clamp01(1f - distance / glow);
                    var pixel = glowColor;
                    pixel.a = falloff * falloff * 0.45f;

                    // A fine dark edge keeps the bright core legible over pale paper.
                    float outline = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(core + 0.25f, core + 1.25f, distance));
                    float center = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(core - 0.75f, core + 0.25f, distance));
                    pixel = Composite(pixel, outlineColor, outline * 0.7f);
                    pixel = Composite(pixel, coreColor, center);
                    pixels[y * TextureSize + x] = pixel;
                }
            }

            texture.SetPixels(pixels);
            // Cursor.SetCursor needs readable RGBA32 pixels without mipmaps.
            texture.Apply(false, false);
            return texture;
        }

        static Color Composite(Color background, Color foreground, float alpha)
        {
            float outputAlpha = alpha + background.a * (1f - alpha);
            if (outputAlpha <= 0f) return Color.clear;
            Color result = (foreground * alpha + background * (background.a * (1f - alpha))) / outputAlpha;
            result.a = outputAlpha;
            return result;
        }
    }
}
