using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PurpleGlove;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run only in Temp/performance-sync-validation. This reads a fresh copy of the
// saved scene; runtime variants are evaluated in memory and never saved.
public static class DocumentTextExport
{
    const string ScenePath = "Assets/Art/DeskSceneV1/Scenes/PH_DeskRoom.unity";
    [Serializable] public class Ink { public string path, text; public bool active; }
    [Serializable] public class Paper
    {
        public string id, category, path, answer, source;
        public int act;
        public bool deferred;
        public Ink[] ink;
        public NotebookBook.Spread[] spreads;
    }
    [Serializable] public class Variant
    {
        public string kind, condition, body;
        public Ink[] ink;
    }
    [Serializable] public class Export
    {
        public string scene = ScenePath;
        public List<Paper> papers = new List<Paper>();
        public List<Variant> variants = new List<Variant>();
        public Ink[] otherText;
        public string[] otherInspectableObjects;
    }
    static string ObjectPath(Transform t) => t.parent ? ObjectPath(t.parent) + "/" + t.name : t.name;
    static Ink[] ReadInk(Transform root) => root.GetComponentsInChildren<TextMesh>(true).Select(t => new Ink {
        path = ObjectPath(t.transform).Substring(ObjectPath(root).Length + 1), text = t.text,
        active = t.gameObject.activeSelf
    }).ToArray();
    static void Set(object target, string property, object value) => target.GetType().GetField("<" + property + ">k__BackingField",
        BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    public static void Run()
    {
        try
        {
            if (!Path.GetFullPath(".").Replace('\\', '/').EndsWith("/Temp/performance-sync-validation"))
                throw new Exception("Use the isolated validation project.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var archive = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ActArchiveController>(true)).Single();
            var records = Camera.main.GetComponent<PlaintextArchiveController>();
            var epilogue = Camera.main.GetComponent<EpilogueFlow>();
            var fifth = Camera.main.GetComponent<FifthActFlow>();
            var data = new Export();
            var roots = new List<Transform>();
            Paper Add(Transform root, string id, int act, string category)
            {
                roots.Add(root);
                var paper = new Paper { id = id, path = ObjectPath(root), act = act, category = category, ink = ReadInk(root) };
                data.papers.Add(paper); return paper;
            }
            for (int a = 0; a < archive.acts.Length; a++)
                for (int d = 0; d < archive.acts[a].documents.Length; d++)
                {
                    var item = archive.acts[a].documents[d];
                    var p = Add(item.transform, "act" + (a + 1) + "-" + (d + 1), a + 1, "original");
                    p.deferred = archive.acts[a].deferredDocuments.Contains(item);
                }
            foreach (var entry in records.entries)
            {
                entry.plaintextInk.text = PlaintextArchiveController.WrapAnswer(entry.answer);
                var p = Add(entry.document.transform, entry.id, entry.act, "plaintext");
                p.answer = entry.answer;
                p.source = entry.sourceDocument ? ObjectPath(entry.sourceDocument.transform) : "";
            }
            foreach (var shared in archive.sharedDocuments)
            {
                var notebook = shared.GetComponent<NotebookBook>();
                var book = shared.GetComponent<PrayerBookTool>();
                var p = Add(shared.transform, notebook ? "notebook" : book.sevenPetal ? "prayer-flower" : "prayer-old", 0, "reference");
                if (notebook) p.spreads = notebook.spreads;
            }
            Add(epilogue.reportReceipt, "report", 6, "receipt");
            Add(epilogue.reunionReceipt, "reunion", 6, "receipt");
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TextMesh>(true)).ToArray();
            data.otherText = all.Where(t => !roots.Any(r => t.transform == r || t.transform.IsChildOf(r))).Select(t => new Ink {
                path = ObjectPath(t.transform), text = t.text, active = t.gameObject.activeInHierarchy
            }).ToArray();
            data.otherInspectableObjects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SceneObject>(true))
                .Where(t => !roots.Contains(t.transform)).Select(t => ObjectPath(t.transform)).ToArray();

            // Evaluate the actual production methods for every paper-text state.
            typeof(ActArchiveController).GetMethod("Initialize", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(archive, null);
            Set(archive, "CurrentAct", 3);
            archive.acts[2].available = true;
            var verification = fifth.verification;
            Set(verification.flow, "State", ThirdActFlow.Phase.WaitingForSignature);
            var militaryPlaintext = records.entries.Single(e => e.id == "vigenere");
            foreach (string state in new[] { "blank", "restored", "signed" })
            {
                militaryPlaintext.saved = state != "blank";
                Set(verification, "Signed", state == "signed");
                verification.RefreshInk();
                data.variants.Add(new Variant { kind = "transcript", condition = state, ink = ReadInk(verification.transform) });
            }
            Set(archive, "CurrentAct", 5);
            foreach (bool guarantee in new[] { false, true })
            foreach (bool revised in new[] { false, true })
            {
                Set(fifth, "GuaranteesSource", guarantee); Set(fifth, "SourceRevised", revised);
                var form = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FifthActDocument>(true))
                    .Single(f => f.kind == FifthActDocument.Kind.Verification);
                form.RefreshInk();
                data.variants.Add(new Variant { kind = "verification", condition = "guarantee=" + guarantee + ";revised=" + revised, ink = ReadInk(form.transform) });
            }
            foreach (FifthActFlow.Custody custody in Enum.GetValues(typeof(FifthActFlow.Custody)))
            {
                Set(fifth, "OriginalCustody", custody);
                var form = fifth.transferApplication.GetComponent<FifthActDocument>(); form.RefreshInk();
                data.variants.Add(new Variant { kind = "transfer", condition = custody.ToString(), ink = ReadInk(form.transform) });
                foreach (bool guarantee in new[] { false, true })
                {
                    Set(fifth, "GuaranteesSource", guarantee);
                    var body = (string)typeof(EpilogueFlow).GetMethod("ReportText", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(epilogue, null);
                    data.variants.Add(new Variant { kind = "report", condition = custody + ";guarantee=" + guarantee, body = body });
                }
            }
            fifth.confession.GetComponent<FifthActDocument>().RefreshInk();
            data.variants.Add(new Variant { kind = "confession", condition = "act5", ink = ReadInk(fifth.confession.transform) });
            var output = Path.GetFullPath("../../output/document-text"); Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "scene-text.json"), JsonUtility.ToJson(data, true), new System.Text.UTF8Encoding(false));
            Debug.Log("DOCUMENT_TEXT_EXPORTED papers=" + data.papers.Count + " variants=" + data.variants.Count);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
