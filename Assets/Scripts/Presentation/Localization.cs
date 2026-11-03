using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PurpleGlove
{
    /// <summary>Chinese stays the authored text everywhere; English is looked up by that text when shown.</summary>
    public static class Localization
    {
        public enum Language { Chinese, English }
        public const string TablePath = "PHLocalization/English";
        const string PreferenceKey = "PurpleGlove.Language";

        [Serializable] sealed class Entry { public string zh, text; }
        [Serializable] sealed class Table { public Entry[] entries = Array.Empty<Entry>(); }

        sealed class Ink
        {
            public string chinese, format, shown;
            public object[] args;
            public float size;
            public bool english;
            // The owner lays this text out itself and refreshes it on Changed (notebook pages).
            public bool external;
        }

        static readonly Dictionary<TextMesh, Ink> inks = new Dictionary<TextMesh, Ink>();
        static Dictionary<string, string> english;
        static Language current;
        static bool initialised;

        public static event Action Changed;
        public static Language Current { get { Initialise(); return current; } }
        // Edit-mode builders also call page and ink refreshes; saved scenes always keep the Chinese source.
        public static bool IsEnglish => Application.isPlaying && Current == Language.English;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() { inks.Clear(); Changed = null; initialised = false; english = null; }

        // sceneLoaded also fires for the first scene when subscribed here, after Awake and before Start.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install() { SceneManager.sceneLoaded -= RegisterScene; SceneManager.sceneLoaded += RegisterScene; }

        static void Initialise()
        {
            if (initialised) return;
            initialised = true;
            current = PlayerPrefs.GetInt(PreferenceKey, 0) == (int)Language.English ? Language.English : Language.Chinese;
        }

        static Dictionary<string, string> English
        {
            get
            {
                if (english != null) return english;
                english = new Dictionary<string, string>(StringComparer.Ordinal);
                var asset = Resources.Load<TextAsset>(TablePath);
                if (!asset) { Debug.LogWarning("Missing English text table: Resources/" + TablePath); return english; }
                foreach (var entry in JsonUtility.FromJson<Table>(asset.text).entries) english[entry.zh] = entry.text;
                return english;
            }
        }

        public static void SetLanguage(Language language)
        {
            Initialise();
            if (current == language) return;
            current = language;
            PlayerPrefs.SetInt(PreferenceKey, (int)language);
            PlayerPrefs.Save();
            foreach (var pair in new List<KeyValuePair<TextMesh, Ink>>(inks))
            {
                if (!pair.Key) { inks.Remove(pair.Key); continue; }
                if (pair.Value.external) continue;
                // A writer outside this registry replaced the text: adopt it as the new Chinese source.
                if (pair.Key.text != pair.Value.shown)
                {
                    pair.Value.chinese = pair.Key.text; pair.Value.format = null; pair.Value.size = pair.Key.characterSize;
                }
                Apply(pair.Key, pair.Value);
            }
            Changed?.Invoke();
        }

        /// <summary>The shown form of authored Chinese: whole text first, then line by line.</summary>
        public static string Text(string chinese)
        {
            if (string.IsNullOrEmpty(chinese) || !IsEnglish) return chinese;
            var table = English;
            if (table.TryGetValue(chinese, out var whole)) return whole;
            if (chinese.IndexOf('\n') < 0) return chinese;
            var lines = chinese.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                if (table.TryGetValue(lines[i], out var line)) lines[i] = line;
            return string.Join("\n", lines);
        }

        public static string Format(string chineseFormat, params object[] args) => string.Format(Text(chineseFormat), args);

        /// <summary>Writes authored Chinese into world ink, shrinking a translation to the Chinese text's footprint.</summary>
        public static void SetText(TextMesh text, string chinese) => Set(text, chinese ?? string.Empty, null, null);
        public static void SetFormat(TextMesh text, string chineseFormat, params object[] args)
            => Set(text, string.Format(chineseFormat, args), chineseFormat, args);

        public static void Exclude(TextMesh text) { if (text) inks[text] = new Ink { external = true }; }

        static void Set(TextMesh text, string chinese, string format, object[] args)
        {
            if (!text) return;
            if (!inks.TryGetValue(text, out var ink)) inks[text] = ink = new Ink { size = text.characterSize };
            else if (ink.external) { text.text = format != null ? Format(format, args) : Text(chinese); return; }
            else if (ink.chinese == chinese && ink.english == IsEnglish && text.text == ink.shown) return;
            ink.chinese = chinese; ink.format = format; ink.args = args;
            Apply(text, ink);
        }

        static void RegisterScene(Scene scene, LoadSceneMode mode)
        {
            foreach (var stale in new List<TextMesh>(inks.Keys)) if (!stale) inks.Remove(stale);
            foreach (var text in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (text.gameObject.scene != scene || inks.ContainsKey(text) || !HasChinese(text.text)) continue;
                var ink = new Ink { chinese = text.text, shown = text.text, size = text.characterSize };
                inks[text] = ink;
                if (IsEnglish) Apply(text, ink);
            }
        }

        static void Apply(TextMesh text, Ink ink)
        {
            string shown = ink.format != null ? Format(ink.format, ink.args) : Text(ink.chinese);
            float scale = 1;
            if (shown != ink.chinese)
            {
                Vector2 box = Measure(text, ink.chinese), need = Measure(text, shown);
                // A centred single line, such as a title, may also use the free width of the sheet beneath it.
                if (box.y == 1 && need.y == 1) box.x = Mathf.Max(box.x, FreeWidth(text, ink.size));
                if (box.x > 0 && need.x > 0) scale = Mathf.Min(1, box.x / need.x, box.y / need.y);
            }
            text.characterSize = ink.size * scale;
            text.text = shown;
            ink.shown = shown; ink.english = IsEnglish;
        }

        /// <summary>Uniform scale that keeps a replacement inside the footprint of the original text.</summary>
        public static float FitScale(TextMesh text, string original, string replacement)
        {
            Vector2 box = Measure(text, original), size = Measure(text, replacement);
            if (box.x <= 0 || size.x <= 0) return 1;
            return Mathf.Min(1, box.x / size.x, box.y / size.y);
        }

        /// <summary>
        /// Width, in font advance units at <paramref name="size"/>, that a centred line can occupy on the nearest
        /// sheet or object beneath it, keeping a margin from both edges; zero when the text is not centred.
        /// </summary>
        public static float FreeWidth(TextMesh text, float size)
        {
            if (!text || size <= 0 || (text.anchor != TextAnchor.UpperCenter && text.anchor != TextAnchor.MiddleCenter
                && text.anchor != TextAnchor.LowerCenter)) return 0;
            Vector3 origin = text.transform.position, right = text.transform.right;
            float span = 0;
            var level = text.transform.parent;
            // The nearest level with geometry under the text wins, so a shelf never lends a paper its width.
            for (int depth = 0; level && depth < 2 && span <= 0; depth++, level = level.parent)
                foreach (var filter in level.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!filter.sharedMesh || filter.GetComponent<TextMesh>()) continue;
                    var bounds = filter.sharedMesh.bounds;
                    float min = float.MaxValue, max = float.MinValue;
                    for (int i = 0; i < 8; i++)
                    {
                        var corner = bounds.center + Vector3.Scale(bounds.extents,
                            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        float along = Vector3.Dot(filter.transform.TransformPoint(corner) - origin, right);
                        min = Mathf.Min(min, along); max = Mathf.Max(max, along);
                    }
                    if (min < 0 && max > 0) span = Mathf.Max(span, 2 * Mathf.Min(-min, max));
                }
            float scale = Mathf.Abs(text.transform.lossyScale.x);
            // TextMesh lays out one advance unit as a tenth of the character size in local space.
            return span <= 0 || scale <= 0 ? 0 : span * 0.9f / scale / (size * 0.1f);
        }

        /// <summary>Widest line advance in font units and the line count; independent of character size.</summary>
        public static Vector2 Measure(TextMesh text, string value)
        {
            var font = text ? text.font : null;
            if (!font || string.IsNullOrEmpty(value)) return new Vector2(0, 1);
            font.RequestCharactersInTexture(value, text.fontSize, text.fontStyle);
            float widest = 0, line = 0;
            int lines = 1;
            foreach (char c in value)
            {
                if (c == '\n') { widest = Mathf.Max(widest, line); line = 0; lines++; continue; }
                if (font.GetCharacterInfo(c, out var info, text.fontSize, text.fontStyle)) line += info.advance;
            }
            return new Vector2(Mathf.Max(widest, line), lines);
        }

        public static bool HasChinese(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (char c in value) if (c >= '一' && c <= '鿿') return true;
            return false;
        }
    }
}
