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
                    string answer = Normalize(entry.answer);
                    if (entry.act != act || !entry.saved || answer.Length == 0 || !remaining.StartsWith(answer, StringComparison.Ordinal)) continue;
                    remaining = remaining.Substring(answer.Length); consumed += answer.Length; removed = true; break;
                }
            } while (removed && remaining.Length > 0);

            foreach (var entry in entries)
            {
                if (entry.act != act || entry.saved || (entry.sourceDocument && !archive.IsDocumentAvailable(act, entry.sourceDocument)) || string.IsNullOrEmpty(entry.answer)
                    || remaining != Normalize(entry.answer) || !entry.document || !entry.plaintextInk) continue;
                // Register the original scene object so shelving retains its content and identity.
                entry.plaintextInk.text = WrapAnswer(entry.answer);
                if (!archive.RegisterDocument(act, entry.document)) { entry.plaintextInk.text = ""; continue; }
                entry.saved = true;
                entry.writtenText = AfterLetters(observed, consumed).Trim();
                plaintextPreserved.Invoke(act);
                break;
            }
            if (announced[act] || !completionLine) return;
            foreach (var entry in entries)
                if (entry.act == act && entry.saved && dialogue.PlayInPlace(completionLine))
                { announced[act] = true; break; }
        }

        public static string Normalize(string text)
        {
            var result = new StringBuilder();
            foreach (char c in text ?? "") if (char.IsLetterOrDigit(c)) result.Append(char.ToUpperInvariant(c));
            return result.ToString();
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
            var result = new StringBuilder(); int width = 0;
            foreach (string word in (text ?? "").Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (width > 0 && width + 1 + word.Length > 34) { result.Append('\n'); width = 0; }
                else if (width > 0) { result.Append(' '); width++; }
                result.Append(word); width += word.Length;
            }
            return result.ToString();
        }
    }
}
