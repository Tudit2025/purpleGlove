using System;
using UnityEngine;

namespace PurpleGlove
{
    [CreateAssetMenu(menuName = "Purple Glove/Story music")]
    public sealed class StoryMusicSettings : ScriptableObject
    {
        [Serializable] public sealed class Cue
        {
            public AudioClip intro, loop, outro;
        }
        public Cue investigation = new Cue(), pressure = new Cue(), reflection = new Cue(), reunion = new Cue(), loss = new Cue();
        [Tooltip("Optional purchased music-only loop for acts one and two; Phoenix remains the fallback.")]
        public AudioClip candlelitMonastery;
        [Range(0, 1)] public float volume = 0.4f;
        [Min(0.1f)] public float crossfadeSeconds = 4;
        [Range(0, 1)] public float dialogueGain = 0.65f, deliveryGain = 0.5f;
        [TextArea(4, 10)] public string credits;
    }
}
