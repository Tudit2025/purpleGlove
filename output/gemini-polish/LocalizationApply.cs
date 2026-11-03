using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PurpleGlove;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run only in the isolated project copy under Temp: -executeMethod LocalizationApply.Run
// Writes the polished Chinese into dialogue assets, prefabs and the saved desk scene; a longer
// revision is shrunk to the footprint of the text it replaces, so no paper overflows.
public static class LocalizationApply
{
    const string Scene = "Assets/Art/DeskSceneV1/Scenes/PH_DeskRoom.unity";
    static readonly string Pack = Path.GetFullPath("../../output/gemini-polish");
    [Serializable] sealed class Entry { public string zh, text; }
    [Serializable] sealed class Table { public Entry[] entries; }
    [Serializable] sealed class Line { public string id, choice, old, text; public int line; }
    [Serializable] sealed class Lines { public Line[] entries; }
    static Dictionary<string, string> revision;
    static readonly StringBuilder report = new StringBuilder();
    static int inks, shrunk, fields, lines;

    public static void Run()
    {
        try
        {
            revision = JsonUtility.FromJson<Table>(File.ReadAllText(Pack + "/zh-revision.json")).entries
                .ToDictionary(e => e.zh, e => e.text, StringComparer.Ordinal);
            ReviseDialogue();
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Art/DeskSceneV1/Prefabs" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try { if (ReviseHierarchy(root.GetComponentsInChildren<Transform>(true), path)) PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var scene = EditorSceneManager.OpenScene(Scene);
            var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            ReviseHierarchy(objects, Scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            File.WriteAllText(Pack + "/apply-report.txt", $"PASS: {lines} dialogue lines/choices, {inks} world texts ({shrunk} shrunk to fit), {fields} other serialized strings.\n\n" + report);
            Debug.Log("LOCALIZATION_APPLY_PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            File.WriteAllText(Pack + "/apply-report.txt", "FAIL: " + error);
            EditorApplication.Exit(1);
        }
    }

    static string Revise(string value)
    {
        if (string.IsNullOrEmpty(value) || !Localization.HasChinese(value)) return value;
        if (revision.TryGetValue(value, out var whole)) return whole;
        if (value.IndexOf('\n') < 0) return value;
        return string.Join("\n", value.Split('\n').Select(l => revision.TryGetValue(l, out var r) ? r : l));
    }

    static void ReviseDialogue()
    {
        var events = AssetDatabase.FindAssets("t:DialogueEvent", new[] { "Assets/Data/Dialogue/Events" })
            .Select(g => AssetDatabase.LoadAssetAtPath<DialogueEvent>(AssetDatabase.GUIDToAssetPath(g))).ToDictionary(e => e.id);
        foreach (var item in JsonUtility.FromJson<Lines>(File.ReadAllText(Pack + "/dialogue-revision.json")).entries)
        {
            var line = events[item.id].lines[item.line];
            if (string.IsNullOrEmpty(item.choice))
            {
                if (line.text != item.old) throw new InvalidDataException($"{item.id}[{item.line}] changed since export");
                line.text = item.text;
            }
            else
            {
                var choice = line.choices.Single(c => c.id == item.choice);
                if (choice.text != item.old) throw new InvalidDataException($"{item.id}.{item.choice} changed since export");
                choice.text = item.text;
            }
            EditorUtility.SetDirty(events[item.id]); lines++;
        }
    }

    static bool ReviseHierarchy(IEnumerable<Transform> objects, string where)
    {
        bool changed = false;
        foreach (var item in objects)
        {
            foreach (var text in item.GetComponents<TextMesh>())
            {
                string revised = Revise(text.text);
                if (revised == text.text) continue;
                float scale = Localization.FitScale(text, text.text, revised);
                if (scale < 1) { text.characterSize *= scale; shrunk++; }
                report.AppendLine($"{where} | {PathOf(text.transform)} | scale {scale:0.000}\n  {text.text.Replace("\n", "⏎")}\n  {revised.Replace("\n", "⏎")}");
                text.text = revised; inks++; changed = true;
                EditorUtility.SetDirty(text);
                if (PrefabUtility.IsPartOfPrefabInstance(text)) PrefabUtility.RecordPrefabInstancePropertyModifications(text);
            }
            foreach (var behaviour in item.GetComponents<MonoBehaviour>())
            {
                if (!behaviour) continue;
                var serialized = new SerializedObject(behaviour);
                var property = serialized.GetIterator();
                bool dirty = false;
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.String) continue;
                    string revised = Revise(property.stringValue);
                    if (revised == property.stringValue) continue;
                    report.AppendLine($"{where} | {PathOf(item)} | {behaviour.GetType().Name}.{property.propertyPath}\n  {property.stringValue.Replace("\n", "⏎")}\n  {revised.Replace("\n", "⏎")}");
                    property.stringValue = revised; dirty = true; fields++;
                }
                if (dirty) { serialized.ApplyModifiedPropertiesWithoutUndo(); changed = true; }
            }
        }
        return changed;
    }

    static string PathOf(Transform item) => item.parent ? PathOf(item.parent) + "/" + item.name : item.name;
}
