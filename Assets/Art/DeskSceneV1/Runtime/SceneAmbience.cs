using UnityEngine;

namespace PurpleGlove
{
    [DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
    public sealed class SceneAmbience : MonoBehaviour
    {
        [Tooltip("Ambient volume, independent of future dialogue and interaction sounds.")]
        [Range(0, 1)] public float volume = 0.8f;
        [Tooltip("Seconds to fade in on entry, or fade out/in when muted/unmuted.")]
        [Min(0)] public float fadeSeconds = 1.25f;
        public bool muted;

        AudioSource source;
        float gain;

        void OnEnable()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            gain = 0;
            source.volume = 0;
            if (source.clip) source.Play();
            else Debug.LogWarning("Scene ambience needs an AudioClip.", this);
        }

        void Update()
        {
            // Keep one continuous loop through camera changes and mute transitions.
            // Unscaled time also lets settings fade the sound while gameplay is paused.
            float target = muted ? 0 : 1;
            gain = fadeSeconds <= 0 ? target : Mathf.MoveTowards(gain, target, Time.unscaledDeltaTime / fadeSeconds);
            source.volume = Mathf.Clamp01(volume) * gain;
        }

        void OnDisable()
        {
            if (!source) return;
            source.volume = 0;
            source.Stop();
        }
    }
}
