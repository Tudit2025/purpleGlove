using System;
using UnityEngine;
using UnityEngine.UI;

namespace PurpleGlove
{
    /// <summary>Reusable chapter card. The scene swap happens only after the screen is fully black.</summary>
    [DisallowMultipleComponent]
    public sealed class ActTransitionOverlay : MonoBehaviour
    {
        public enum Phase { Idle, FadeOut, Black, TitleIn, TitleHold, TitleOut, FadeIn, FinalHold }
        public Font font;
        [Min(0.05f)] public float fadeOutSeconds = 0.8f, blackSeconds = 0.3f;
        [Min(0.05f)] public float titleFadeSeconds = 0.45f, titleHoldSeconds = 1.6f, fadeInSeconds = 1f;
        public Phase State { get; private set; }
        public bool IsActive => State != Phase.Idle;
        public float Opacity { get; private set; }
        public float TitleOpacity { get; private set; }
        public string Title => title ? title.text : "";
        Canvas canvas;
        Image curtain;
        Text title;
        float elapsed;
        Action covered, finished;
        bool finalFade;

        void Awake()
        {
            var root = new GameObject("Act transition overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.layer = 5; root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            curtain = Element("Black curtain", root.transform).gameObject.AddComponent<Image>();
            curtain.raycastTarget = false;
            title = Element("Chapter title", root.transform).gameObject.AddComponent<Text>();
            title.font = font; title.fontSize = 56; title.alignment = TextAnchor.MiddleCenter;
            title.raycastTarget = false; title.supportRichText = false;
            Cancel();
        }

        static RectTransform Element(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5; rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static string ActTitle(int act)
        {
            if (act == 6) return "第六幕 · 尾声";
            string[] numerals = { "零", "一", "二", "三", "四", "五" };
            return act >= 0 && act < numerals.Length ? "第" + numerals[act] + "幕" : "第" + act + "幕";
        }

        public bool Begin(int act, Action whenCovered, Action whenFinished)
        {
            if (act < 0 || act > 6) return false;
            return BeginCard(ActTitle(act), whenCovered, whenFinished);
        }

        public bool BeginCard(string caption, Action whenCovered, Action whenFinished)
        {
            if (!isActiveAndEnabled || IsActive || !canvas) return false;
            finalFade = false;
            title.text = caption; covered = whenCovered; finished = whenFinished;
            State = Phase.FadeOut; elapsed = 0; canvas.enabled = true; SetOpacity(0, 0);
            return true;
        }

        public bool BeginFinalFade(string caption, Action whenBlack)
        {
            if (!isActiveAndEnabled || IsActive || !canvas) return false;
            finalFade = true; title.text = caption; covered = whenBlack; finished = null;
            State = Phase.FadeOut; elapsed = 0; canvas.enabled = true; SetOpacity(0, 0); return true;
        }

        // The owner advances this clock, so console/focus pauses cover both the door and the curtain.
        public void Advance(float seconds)
        {
            if (!IsActive || State == Phase.FinalHold) return;
            elapsed += Mathf.Max(0, seconds);
            float duration = State == Phase.FadeOut ? fadeOutSeconds : State == Phase.Black ? blackSeconds
                : State == Phase.TitleHold ? titleHoldSeconds : State == Phase.FadeIn ? fadeInSeconds : titleFadeSeconds;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, duration));
            float smooth = Mathf.SmoothStep(0, 1, t);
            switch (State)
            {
                case Phase.FadeOut: SetOpacity(smooth, 0); break;
                case Phase.TitleIn: SetOpacity(1, smooth); break;
                case Phase.TitleOut: SetOpacity(1, 1 - smooth); break;
                case Phase.FadeIn: SetOpacity(1 - smooth, 0); break;
            }
            if (t < 1) return;
            elapsed = 0;
            switch (State)
            {
                case Phase.FadeOut:
                    if (finalFade)
                    {
                        State = Phase.FinalHold; SetOpacity(1, 1);
                        var ending = covered; covered = null; ending?.Invoke(); break;
                    }
                    State = Phase.Black; SetOpacity(1, 0);
                    var swap = covered; covered = null; swap?.Invoke(); break;
                case Phase.Black: State = Phase.TitleIn; break;
                case Phase.TitleIn: State = Phase.TitleHold; break;
                case Phase.TitleHold: State = Phase.TitleOut; break;
                case Phase.TitleOut: State = Phase.FadeIn; break;
                case Phase.FadeIn:
                    var done = finished; Cancel(); done?.Invoke(); break;
            }
        }

        void SetOpacity(float black, float text)
        {
            Opacity = black; TitleOpacity = text;
            if (curtain) curtain.color = new Color(0, 0, 0, black);
            if (title) title.color = new Color(0.94f, 0.88f, 0.75f, text);
        }

        public void Cancel()
        {
            State = Phase.Idle; elapsed = 0; covered = finished = null;
            finalFade = false;
            SetOpacity(0, 0); if (canvas) canvas.enabled = false;
        }
        // The debug console must remain visible and operable even in the opaque-black phase.
        public void SetConsoleVisible(bool visible)
        { if (canvas) canvas.enabled = IsActive && !visible; }
        void OnDisable() => Cancel();
    }
}
