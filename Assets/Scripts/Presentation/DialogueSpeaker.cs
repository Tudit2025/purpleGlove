using System;
using UnityEngine;

namespace PurpleGlove
{
    [CreateAssetMenu(menuName = "Purple Glove/Dialogue/Speaker")]
    public sealed class DialogueSpeaker : ScriptableObject
    {
        public string id, displayName;
        public bool isProtagonist;
        public Color textColor = Color.white;
        [Range(8, 60)] public float charactersPerSecond = 22;
        public Texture2D eyes;
        [TextArea] public string eyeDescription;
        [Header("Typewriter voice")]
        public AudioClip[] typingClips = Array.Empty<AudioClip>();
        [Range(0.5f, 2)] public float pitch = 1;
        [Range(0, 1)] public float volume = 0.5f;
        [Range(500, 12000)] public float lowPassHz = 6000;
        [Range(0.045f, 0.2f)] public float soundInterval = 0.08f;
    }
}
