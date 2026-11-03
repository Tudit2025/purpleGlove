using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PurpleGlove;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Copy into an isolated project's Assets/Validation/Editor; run PlaintextToleranceValidation.Run.
[InitializeOnLoad]
public static class PlaintextToleranceValidation
{
    const string Key = "PlaintextToleranceValidation.Play";
    const string ScenePath = "Assets/Art/DeskSceneV1/Scenes/PH_DeskRoom.unity";
    static readonly string Output = Path.GetFullPath("../../output/plaintext-tolerance-validation");
    static int checks, preserved;
    static bool done;
    static double started;
    static PlaintextArchiveController records;
    static CipherWorkbench work;
    static ActArchiveController archive;
    static DialogueController dialogue;
    static SceneInputGate gate;
    static readonly BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    static PlaintextToleranceValidation() { if (SessionState.GetBool(Key, false)) Install(); }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Instance).Invoke(target, args);
    static void Ungate() => typeof(SceneInputGate).GetField("ignoreThroughFrame", Instance).SetValue(gate, -1);
    static void Step(float seconds = 1) => Call(records, "AdvanceTime", seconds);
    static void SetDraft(string text) { work.SetPlaintext(text); Step(0); Step(); }
    static void CheckCleared(int act)
    {
        Check(work.Plaintext == "", "Confirmed plaintext cleared for act " + act);
        Check(work.plaintextInk.text == "", "Writing surface cleared for act " + act);
        Check(records.DraftForAct(act) == "", "Cached draft cleared for act " + act);
    }
    static void Receive(int act)
    {
        Check(archive.ReceiveAct(act), "Receive act " + act);
        Ungate(); Step();
        Check(records.WritingAct == act && work.Plaintext == "", "Fresh draft for act " + act);
    }
    static void EndLine()
    {
        Check(dialogue.ActiveEvent == records.completionLine, "One completion line per act");
        dialogue.Stop(); Ungate();
    }
    static void Complete(int act)
    {
        Check(archive.CompleteAct(act), "Complete act " + act); Ungate(); Step();
    }

    public static void Run()
    {
        Directory.CreateDirectory(Output);
        var scene = EditorSceneManager.OpenScene(ScenePath);
        // Isolate archive/keyboard behavior from automatic chapter transitions in the copy.
        foreach (var behaviour in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
            if (behaviour is PrologueSequence || behaviour is FirstActFlow || behaviour is SecondActFlow
                || behaviour is ThirdActFlow || behaviour is FourthActFlow || behaviour is FourthActOpening
                || behaviour is FifthActFlow || behaviour is EpilogueFlow || behaviour is DialogueTestConsole)
                behaviour.enabled = false;
        EditorSceneManager.SaveScene(scene);
        records = Camera.main.GetComponent<PlaintextArchiveController>();
        try { ValidateMatching(); }
        catch (Exception e) { Finish(false, e.ToString()); return; }
        SessionState.SetInt(Key + ".Checks", checks);
        SessionState.SetBool(Key, true); Install(); EditorApplication.isPlaying = true;
    }

    static void ValidateMatching()
    {
        foreach (var entry in records.entries)
        {
            string answer = PlaintextArchiveController.Normalize(entry.answer);
            int allowed = answer.Length / 10;
            Check(PlaintextArchiveController.MatchesAnswer(entry.answer.ToLowerInvariant() + "!\n", entry.answer), "Formatting " + entry.id);
            Check(!PlaintextArchiveController.MatchesAnswer(null, entry.answer), "Null draft " + entry.id);
            Check(!PlaintextArchiveController.MatchesAnswer(" \n!?", entry.answer), "Blank draft " + entry.id);
            foreach (int count in new[] { allowed, allowed + 1 })
            {
                bool accepted = count <= allowed;
                Check(PlaintextArchiveController.MatchesAnswer(new string('0', count) + answer.Substring(count), entry.answer) == accepted, "Substitution boundary " + entry.id);
                Check(PlaintextArchiveController.MatchesAnswer(answer.Substring(count), entry.answer) == accepted, "Omission boundary " + entry.id);
                Check(PlaintextArchiveController.MatchesAnswer(answer + new string('0', count), entry.answer) == accepted, "Insertion boundary " + entry.id);
            }
            for (int index = 0; index < answer.Length; index++)
            {
                Check(PlaintextArchiveController.MatchesAnswer(answer.Remove(index, 1), entry.answer) == (allowed >= 1), "Missing character at " + entry.id + ":" + index);
                Check(PlaintextArchiveController.MatchesAnswer(answer.Insert(index, "0"), entry.answer) == (allowed >= 1), "Extra character at " + entry.id + ":" + index);
                Check(PlaintextArchiveController.MatchesAnswer(answer.Remove(index, 1).Insert(index, "0"), entry.answer) == (allowed >= 1), "Wrong character at " + entry.id + ":" + index);
            }
        }
        Check(!PlaintextArchiveController.MatchesAnswer("", ""), "Empty answer must not match");
        Check(!PlaintextArchiveController.MatchesAnswer("X", null), "Null answer must not match");
        Check(!PlaintextArchiveController.MatchesAnswer("123", "!?"), "Punctuation-only answer must not match");
        Check(!PlaintextArchiveController.MatchesAnswer("LEFTGLOV", "LEFT GLOVE"), "Nine letters cannot round up to one error");
        Check(PlaintextArchiveController.MatchesAnswer("ABCDEFGHI", "ABCDEFGHIJ"), "Exactly ten percent omission");
        Check(!PlaintextArchiveController.MatchesAnswer("ABCDEFGHIJKLMNOPQ", "ABCDEFGHIJKLMNOPQRS"), "Nineteen letters cannot round up to two errors");
        Check(PlaintextArchiveController.MatchesAnswer("ZCDEFGHIJKLMNOPQRST", "ABCDEFGHIJKLMNOPQRST"), "Mixed replacement and omission share two-edit budget");
        Check(!PlaintextArchiveController.MatchesAnswer("ZCDEFGHIJKLMNOPQRST0", "ABCDEFGHIJKLMNOPQRST"), "Mixed errors exceed two-edit budget");
        Check(!PlaintextArchiveController.MatchesAnswer("BACDEFGHIJ", "ABCDEFGHIJ"), "Two swapped letters cost two edits");
        Check(!PlaintextArchiveController.MatchesAnswer(new string('A', 600), "KEEP THE ORIGINAL"), "Long unrelated draft rejected");
        foreach (var pair in new[] { new[] { "book", "pigpen" }, new[] { "affine", "rail" }, new[] { "rail", "affine" } })
        {
            string first = PlaintextArchiveController.Normalize(records.entries.Single(e => e.id == pair[0]).answer);
            string second = PlaintextArchiveController.Normalize(records.entries.Single(e => e.id == pair[1]).answer);
            foreach (string prefix in new[] { first, "0" + first.Substring(1), first.Remove(3, 1), first.Insert(3, "0"), first.Substring(0, first.Length - 1) })
            {
                object[] args = { prefix + second, first, true, 0, prefix.Length };
                bool matched = (bool)typeof(PlaintextArchiveController).GetMethod("TryMatchNormalized", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
                Check(matched && (int)args[3] == prefix.Length, "Saved prefix boundary " + pair[0] + " -> " + pair[1] + ": " + prefix);
            }
        }
    }

    static void Install()
    {
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
    }
    static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Finish(false, message + "\n" + stack); }
    static void Tick()
    {
        if (done) return;
        if (EditorApplication.timeSinceStartup - started > 120) { Finish(false, "Play Mode timeout"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.frameCount < 12) return;
        try
        {
            records = Camera.main.GetComponent<PlaintextArchiveController>();
            work = records.workbench; archive = records.archive; dialogue = records.dialogue;
            gate = Camera.main.GetComponent<SceneInputGate>();
            records.plaintextPreserved.AddListener(act => { preserved++; CheckCleared(act); });
            Ungate(); Step();
            SetDraft("KEEP THE ORIGINA"); Check(!records.entries.Any(e => e.saved), "No answer before delivery");
            Receive(1);
            SetDraft("KEEP THE ORIGIN"); Check(!records.entries[0].saved, "Over-budget omission rejected by controller");
            Check(work.Plaintext == "KEEP THE ORIGIN", "Rejected draft remains editable");
            work.SetPlaintext("KEEP THE ORIGINA"); Step(0); Step(0.24f);
            Check(!records.entries[0].saved, "Wait for typing to settle");
            Call(work, "StampLetter", 'A'); Step(1);
            Check(!records.entries[0].saved && work.HasPendingPlaintextStamps, "Wait for final stamp return");
            Check(work.Plaintext == "KEEP THE ORIGINA", "Pending stamps do not clear the draft");
            Call(work, "AnimateLetterStamps", 0.5f); Step();
            var caesar = records.entries[0];
            Check(caesar.saved && caesar.writtenText == "KEEP THE ORIGINA", "Within-budget omission saved");
            Check(caesar.plaintextInk.text == PlaintextArchiveController.WrapAnswer(caesar.answer), "Canonical text preserved");
            int firstId = caesar.document.GetInstanceID(), firstCount = archive.acts[0].documents.Length;
            EndLine(); SetDraft(caesar.answer);
            Check(archive.acts[0].documents.Length == firstCount && preserved == 1 && !dialogue.IsActive, "No duplicate record or completion");
            SetDraft("NEXT DRAFT");
            Complete(1);

            Receive(2);
            var book = records.entries.Single(e => e.id == "book");
            var pigpen = records.entries.Single(e => e.id == "pigpen");
            string bookText = PlaintextArchiveController.Normalize(book.answer);
            string correctedTail = "0" + bookText.Substring(1);
            SetDraft(correctedTail.Substring(0, correctedTail.Length - 1));
            Check(book.saved && !pigpen.saved, "Mixed error and omission saved"); EndLine();
            SetDraft("left glove");
            Check(!pigpen.saved && !archive.IsDocumentAvailable(2, pigpen.sourceDocument), "Second-act source remains locked until delivery");
            Check(work.Plaintext == "left glove", "Locked-source draft retained until confirmation");
            Check(archive.ReceiveSupplement(2), "Receive pigpen source"); Ungate(); Step();
            Check(pigpen.saved && pigpen.writtenText == "left glove", "Next solution starts on the cleared sheet");
            Check(!dialogue.IsActive && preserved == 3, "Only one completion line in act two");
            Complete(2);

            Receive(3);
            var vigenere = records.entries.Single(e => e.id == "vigenere");
            SetDraft("KEEP THE ORIGINAL"); Check(!vigenere.saved, "Other act answer rejected");
            gate.SetConsole(true);
            SetDraft("0" + PlaintextArchiveController.Normalize(vigenere.answer).Substring(1));
            Check(!vigenere.saved, "Console defers tolerant completion");
            Check(work.Plaintext.Length > 0, "Console does not discard deferred input");
            gate.SetConsole(false); Ungate(); Step();
            Check(vigenere.saved, "Deferred tolerant completion resumed"); EndLine(); Complete(3);

            Receive(4);
            var affine = records.entries.Single(e => e.id == "affine");
            var rail = records.entries.Single(e => e.id == "rail");
            string affineText = PlaintextArchiveController.Normalize(affine.answer).Insert(4, "0");
            SetDraft(affineText); Check(affine.saved, "Within-budget extra character saved"); EndLine();
            string railText = PlaintextArchiveController.Normalize(rail.answer);
            railText = railText.Substring(railText.Length / 10);
            bool sourceLocked = rail.sourceDocument && !archive.IsDocumentAvailable(4, rail.sourceDocument);
            SetDraft(affineText + "\n" + railText);
            if (sourceLocked)
            {
                Check(!rail.saved, "Locked source cannot be solved early");
                Check(archive.ReceiveSupplement(4), "Receive deferred source"); Ungate(); Step();
            }
            Check(rail.saved && rail.writtenText == railText, "Long answer at omission limit after saved typo prefix");
            Check(rail.plaintextInk.text == PlaintextArchiveController.WrapAnswer(rail.answer), "Long canonical answer preserved");
            Check(preserved == 6 && !dialogue.IsActive, "Six unique records, one completion per act"); Complete(4);
            Receive(5); SetDraft(caesar.answer);
            Check(preserved == 6 && !dialogue.IsActive, "No fifth-act solution");
            Check(archive.ShowActOnDesk(1), "Revisit first act"); Ungate(); Step();
            Check(caesar.document.GetInstanceID() == firstId && caesar.plaintextInk.text == PlaintextArchiveController.WrapAnswer(caesar.answer), "Archive retains original canonical paper");
            Check(work.Plaintext == "NEXT DRAFT" && records.DraftForAct(5) == caesar.answer, "Unconfirmed act drafts retained");
            foreach (int act in new[] { 2, 3, 4 })
            {
                Check(archive.ShowActOnDesk(act), "Revisit completed act " + act); Ungate(); Step();
                CheckCleared(act); Step(); CheckCleared(act);
            }
            Finish(true, "PASS: " + (checks + SessionState.GetInt(Key + ".Checks", 0)) + " assertions in Unity 2022.3.62f2.\n"
                + "All six current scene answers: exact, formatting, replacement/omission/insertion at and above the 10% limit; every character position.\n"
                + "Short-answer rounding, mixed error budgets, blank/null/long input, saved-prefix boundaries in both fourth-act orders.\n"
                + "Play Mode: delivery/source gates, quiet delay, letter flight, tolerant save, canonical paper, consecutive/appended answers, per-act dialogue, duplicate protection, shelving/revisit.\n"
                + "Every confirmation clears the visible sheet and cached draft before callbacks; unconfirmed/deferred input is retained; cleared drafts stay empty on revisit.\n"
                + "Isolated project copy; synthetic frame/keyboard state, not OS input testing.\n");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Finish(bool success, string message)
    {
        if (done) return; done = true; SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Path.Combine(Output, "validation.txt"), message);
        if (success) Debug.Log("PLAINTEXT_TOLERANCE_VALIDATION_PASS"); else Debug.LogError(message);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
