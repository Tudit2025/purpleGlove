using System;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

namespace PurpleGlove
{
    /// <summary>Turns completed plaintext into the act's own immutable, inspectable record.</summary>
    [DefaultExecutionOrder(-150), DisallowMultipleComponent]
    public sealed class PlaintextArchiveController : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            public int act;
            public string id, answer;
            public SceneObject document;
            public SceneObject sourceDocument;
            public TextMesh plaintextInk;
            [NonSerialized] public bool saved;
            [NonSerialized] public string writtenText;
        }

        public CipherWorkbench workbench;
        public ActArchiveController archive;
        public DialogueController dialogue;
        public DialogueEvent completionLine;
        public Entry[] entries = Array.Empty<Entry>();
        public UnityEvent<int> plaintextPreserved = new UnityEvent<int>();
        [Min(0)] public float settleSeconds = 0.25f;
        readonly string[] drafts = new string[6];
        readonly bool[] announced = new bool[6];
        SceneInputGate gate;
        SceneObjectInteractor interactor;
        int writingAct = -1;
        string observed = "";
        float quietSeconds;

        public int WritingAct => writingAct;
        public string DraftForAct(int act) => act >= 0 && act < drafts.Length ? drafts[act] ?? "" : "";
        void Awake() { gate = GetComponent<SceneInputGate>(); interactor = GetComponent<SceneObjectInteractor>(); }
        void Update() { if (Application.isFocused) AdvanceTime(Time.unscaledDeltaTime); }

        void AdvanceTime(float seconds)
        {
            if (!workbench || !archive || !dialogue || !gate || !workbench.isActiveAndEnabled || !archive.isActiveAndEnabled) return;
            int act = archive.DeskAct;
            if (act < 0 || act >= drafts.Length) return;
            if (act != writingAct)
            {
                if (writingAct >= 0) drafts[writingAct] = workbench.Plaintext;
                writingAct = act;
                // Keep the initial unsorted scratch pad, then separate every delivered act's draft.
                if (act == 0 && drafts[0] == null) drafts[0] = workbench.Plaintext;
                workbench.SetPlaintext(drafts[act] ?? "");
                observed = workbench.Plaintext; quietSeconds = 0;
                return;
            }
            if (observed != workbench.Plaintext)
            {
                drafts[act] = observed = workbench.Plaintext; quietSeconds = 0;
            }
            else quietSeconds += Mathf.Max(0, seconds);
            if (act < 1 || act > 4 || !archive.IsAvailable(act) || gate.BlocksWorldInput
                || workbench.IsManipulating || workbench.HasPendingPlaintextStamps
                || (interactor && interactor.InspectedObject) || quietSeconds < settleSeconds) return;

            string remaining = Normalize(observed);
            int consumed = 0;
            // A second solution may follow the first on the same sheet, or replace the draft entirely.
            bool removed;
            do
            {
                removed = false;
                foreach (var entry in entries)
                {
                    if (entry.act != act || !entry.saved) continue;
                    string written = Normalize(entry.writtenText);
                    int preferredLength = remaining.StartsWith(written, StringComparison.Ordinal) ? written.Length : 0;
                    if (!TryMatchNormalized(remaining, Normalize(entry.answer), true, out int length, preferredLength)) continue;
                    remaining = remaining.Substring(length); consumed += length; removed = true; break;
                }
            } while (removed && remaining.Length > 0);

            foreach (var entry in entries)
            {
                if (entry.act != act || entry.saved || (entry.sourceDocument && !archive.IsDocumentAvailable(act, entry.sourceDocument)) || string.IsNullOrEmpty(entry.answer)
                    || !TryMatchNormalized(remaining, Normalize(entry.answer), false, out _) || !entry.document || !entry.plaintextInk) continue;
                // Register the original scene object so shelving retains its content and identity.
                entry.plaintextInk.text = WrapAnswer(entry.answer);
                if (!archive.RegisterDocument(act, entry.document)) { entry.plaintextInk.text = ""; continue; }
                entry.saved = true;
                entry.writtenText = AfterLetters(observed, consumed).Trim();
                // Clear both the visible sheet and its cached draft before story listeners run.
                workbench.SetPlaintext("");
                drafts[act] = observed = "";
                quietSeconds = 0;
                plaintextPreserved.Invoke(act);
                break;
            }
            if (announced[act] || !completionLine) return;
            foreach (var entry in entries)
                if (entry.act == act && entry.saved && dialogue.PlayInPlace(completionLine))
                { announced[act] = true; break; }
        }

        // Used only on a freshly reloaded scene; do not replay earlier completion dialogue/events.
        internal void PrepareCompletedActForTesting(int act)
        {
            if (act < 1 || act > 4) return;
            foreach (var entry in entries)
            {
                if (entry.act != act) continue;
                if (!entry.document || !entry.plaintextInk || !archive.RegisterDocument(act, entry.document))
                    throw new InvalidOperationException("Cannot prepare plaintext: " + entry.id);
                entry.plaintextInk.text = WrapAnswer(entry.answer);
                entry.saved = true; entry.writtenText = entry.answer;
            }
            drafts[act] = ""; announced[act] = true;
        }

        public static string Normalize(string text)
        {
            var result = new StringBuilder();
            foreach (char c in text ?? "") if (char.IsLetterOrDigit(c)) result.Append(char.ToUpperInvariant(c));
            return result.ToString();
        }

        public static bool MatchesAnswer(string text, string answer)
            => TryMatchNormalized(Normalize(text), Normalize(answer), false, out _);

        static bool TryMatchNormalized(string text, string answer, bool prefix, out int matchedLength, int preferredLength = 0)
        {
            matchedLength = 0;
            // Each substituted, missing or extra letter/digit costs one edit. Round down so
            // the total never exceeds 10% of the normalized answer, even for short answers.
            int allowed = answer.Length / 10;
            int minimumLength = answer.Length - allowed;
            if (answer.Length == 0 || text.Length < minimumLength
                || (!prefix && text.Length > answer.Length + allowed)) return false;
            int length = prefix ? Math.Min(text.Length, answer.Length + allowed) : text.Length;
            var previous = new int[length + 1];
            var current = new int[length + 1];
            for (int j = 0; j <= length; j++) previous[j] = j;
            for (int i = 1; i <= answer.Length; i++)
            {
                current[0] = i;
                int rowMinimum = current[0];
                for (int j = 1; j <= length; j++)
                {
                    int substitution = previous[j - 1] + (answer[i - 1] == text[j - 1] ? 0 : 1);
                    current[j] = Math.Min(substitution, Math.Min(previous[j] + 1, current[j - 1] + 1));
                    rowMinimum = Math.Min(rowMinimum, current[j]);
                }
                if (rowMinimum > allowed) return false;
                var swap = previous; previous = current; current = swap;
            }
            // Saved answers can contain errors too. Remove their best matching prefix before
            // checking the next solution. On ties, retain the original submitted boundary
            // when available; otherwise prefer the longer prefix.
            int best = allowed + 1;
            for (int j = prefix ? minimumLength : length; j <= length; j++)
                if (previous[j] <= allowed && (previous[j] < best || (previous[j] == best && matchedLength != preferredLength)))
                { best = previous[j]; matchedLength = j; }
            return best <= allowed;
        }

        static string AfterLetters(string text, int consumed)
        {
            int index = 0;
            while (index < text.Length && consumed > 0)
                if (char.IsLetterOrDigit(text[index++])) consumed--;
            return text.Substring(index);
        }
        public static string WrapAnswer(string text)
        {
            var result = new StringBuilder();
            var lines = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                // Preserve authored line breaks on short slips, then wrap any longer lines.
                if (i > 0) result.Append('\n');
                int width = 0;
                foreach (string word in lines[i].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (width > 0 && width + 1 + word.Length > 34) { result.Append('\n'); width = 0; }
                    else if (width > 0) { result.Append(' '); width++; }
                    result.Append(word); width += word.Length;
                }
            }
            return result.ToString();
        }
    }
}
