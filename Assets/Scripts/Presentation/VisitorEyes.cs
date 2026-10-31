using UnityEngine;

namespace PurpleGlove
{
    /// <summary>A depth-tested flat portrait behind the actual aperture, never a screen overlay.</summary>
    [DisallowMultipleComponent]
    public sealed class VisitorEyes : MonoBehaviour
    {
        public MeshRenderer portrait;
        [Tooltip("A faint corridor bounce, enabled only while a visitor is present.")]
        public Light corridorBounce;
        public DialogueSpeaker CurrentVisitor { get; private set; }
        MaterialPropertyBlock properties;
        public void Show(DialogueSpeaker speaker)
        {
            CurrentVisitor = speaker;
            if (corridorBounce) corridorBounce.enabled = speaker && speaker.eyes;
            if (!portrait) return;
            portrait.enabled = speaker && speaker.eyes;
            if (!portrait.enabled) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            properties.SetTexture("_MainTex", speaker.eyes);
            portrait.SetPropertyBlock(properties);
        }
        void Awake() => Show(null);
        void OnDisable() => Show(null);
    }
}
