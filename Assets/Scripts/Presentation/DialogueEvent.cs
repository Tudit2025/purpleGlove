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
            public Choice[] choices = Array.Empty<Choice>();
            public bool HasChoices => choices != null && choices.Length > 0;
        }
        [Serializable]
        public sealed class Choice
        {
            public string id;
            [TextArea(1, 3)] public string text;
            [Tooltip("Zero-based destination line; -1 completes the event.")]
            public int nextLine = -1;
        }
        public string id, title;
        [TextArea] public string context;
        public bool testOnly;
        public DialogueSpeaker initialVisitor;
        public AudioClip arrivalSound;
        [Tooltip("Close the observation shutter on completion even if the player opened it before the visit.")]
        public bool closeShutterOnComplete;
        [Min(0)] public float arrivalDelay = 0.8f;
        public Line[] lines = Array.Empty<Line>();

        public bool TryValidate(out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(id)) error = "事件缺少 ID。";
            else if (lines == null || lines.Length == 0) error = "事件没有台词。";
            else for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] == null || !lines[i].speaker || string.IsNullOrWhiteSpace(lines[i].text))
                { error = "第 " + (i + 1) + " 句缺少角色或文字。"; break; }
                if (!lines[i].HasChoices) continue;
                if (lines[i].choices.Length < 2) { error = "选择至少需要两个选项。"; break; }
                var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
                foreach (var choice in lines[i].choices)
                    if (choice == null || string.IsNullOrWhiteSpace(choice.id) || string.IsNullOrWhiteSpace(choice.text)
                        || !ids.Add(choice.id) || choice.nextLine < -1 || choice.nextLine >= lines.Length)
                    { error = "第 " + (i + 1) + " 句的选项缺少唯一 ID、文字，或跳转行无效。"; break; }
                if (error != null) break;
            }
            return error == null;
        }
    }
}
