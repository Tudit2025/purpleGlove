using System;
using UnityEngine;

namespace PurpleGlove
{
    [CreateAssetMenu(menuName = "Purple Glove/Dialogue/Event")]
    public sealed class DialogueEvent : ScriptableObject
    {
        [Serializable]
        public sealed class Line
        {
            public DialogueSpeaker speaker;
            [TextArea(2, 4)] public string text;
        }
        public string id, title;
        [TextArea] public string context;
        public bool testOnly;
        public DialogueSpeaker initialVisitor;
        public AudioClip arrivalSound;
        [Min(0)] public float arrivalDelay = 0.8f;
        public Line[] lines = Array.Empty<Line>();

        public bool TryValidate(out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(id)) error = "事件缺少 ID。";
            else if (lines == null || lines.Length == 0) error = "事件没有台词。";
            else for (int i = 0; i < lines.Length; i++)
                if (lines[i] == null || !lines[i].speaker || string.IsNullOrWhiteSpace(lines[i].text))
                { error = "第 " + (i + 1) + " 句缺少角色或文字。"; break; }
            return error == null;
        }
    }
}
