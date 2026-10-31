using UnityEngine;

namespace PurpleGlove
{
    /// <summary>Three short voices overlap naturally; throttling prevents bursts at low frame rates.</summary>
    [DisallowMultipleComponent]
    public sealed class DialogueVoice : MonoBehaviour
    {
        [Range(0, 1)] public float masterVolume = 0.75f;
        AudioSource[] sources;
        AudioLowPassFilter[] filters;
        DialogueSpeaker speaker;
        int nextSource, lastClip = -1;
        float nextSound;
        public int PlayedCount { get; private set; }
        public int LastClip => lastClip;

        void Awake()
        {
            sources = new AudioSource[3]; filters = new AudioLowPassFilter[3];
            for (int i = 0; i < sources.Length; i++)
            {
                var child = new GameObject("Typewriter voice " + (i + 1));
                child.transform.SetParent(transform, false);
                sources[i] = child.AddComponent<AudioSource>();
                sources[i].playOnAwake = false; sources[i].spatialBlend = 0;
                sources[i].dopplerLevel = 0;
                filters[i] = child.AddComponent<AudioLowPassFilter>();
            }
        }

        public void BeginLine(DialogueSpeaker next)
        {
            Stop();
            if (speaker != next) lastClip = -1;
            speaker = next; nextSound = 0;
        }

        public void Tick(char character)
        {
            if (!speaker || sources == null || char.IsWhiteSpace(character) || char.IsPunctuation(character)
                || char.IsControl(character) || Time.unscaledTime < nextSound) return;
            var clips = speaker.typingClips;
            if (clips == null || clips.Length == 0) return;
            int index = clips.Length == 1 ? 0 : Random.Range(0, clips.Length - (lastClip >= 0 ? 1 : 0));
            if (clips.Length > 1 && lastClip >= 0 && index >= lastClip) index++;
            lastClip = index;
            if (!clips[index]) return;
            var source = sources[nextSource];
            filters[nextSource].cutoffFrequency = speaker.lowPassHz;
            source.pitch = speaker.pitch * Random.Range(0.98f, 1.02f);
            source.volume = masterVolume * speaker.volume;
            source.clip = clips[index]; source.Play();
            nextSource = (nextSource + 1) % sources.Length;
            nextSound = Time.unscaledTime + speaker.soundInterval;
            PlayedCount++;
        }

        public void Stop()
        {
            if (sources != null) foreach (var source in sources) if (source) source.Stop();
        }
        void OnDisable() => Stop();
    }
}
