using UnityEngine;

namespace PurpleGlove
{
    public static class GameSettings
    {
        const string VolumeKey = "PurpleGlove.MasterVolume", FullscreenKey = "PurpleGlove.Fullscreen";
        const string PixelationKey = "PurpleGlove.Pixelation";
        public static event System.Action PixelationChanged;
        public static float Volume => Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1));
        public static bool Pixelation => PlayerPrefs.GetInt(PixelationKey, 1) != 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetEvents() => PixelationChanged = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
            AudioListener.volume = Volume;
            if (PlayerPrefs.HasKey(FullscreenKey)) Screen.fullScreen = PlayerPrefs.GetInt(FullscreenKey) != 0;
        }

        public static void SetVolume(float value)
        {
            AudioListener.volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, AudioListener.volume);
        }
        public static void SetFullscreen(bool value)
        {
            Screen.fullScreen = value;
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0); PlayerPrefs.Save();
        }
        public static void SetPixelation(bool value)
        {
            PlayerPrefs.SetInt(PixelationKey, value ? 1 : 0);
            foreach (var view in Object.FindObjectsByType<PixelSceneView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                view.pixelate = value;
            PixelationChanged?.Invoke();
            PlayerPrefs.Save();
        }
        public static void Save() => PlayerPrefs.Save();
    }
}
