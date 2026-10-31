using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace PurpleGlove
{
    [DefaultExecutionOrder(-300), DisallowMultipleComponent]
    [RequireComponent(typeof(SceneInputGate), typeof(DialogueController))]
    public sealed class DialogueTestConsole : MonoBehaviour
    {
        public Font font;
        public bool enableInReleaseBuild;
        public bool IsOpen { get; private set; }
        public bool Available => Application.isEditor || Debug.isDebugBuild || enableInReleaseBuild;
        DialogueController dialogue;
        SceneInputGate gate;
        SceneObjectInteractor interactor;
        SealedDeliveryController delivery;
        readonly List<string> log = new List<string>();
        readonly List<string> history = new List<string>();
        string command = string.Empty;
        int historyIndex;
        Vector2 scroll;
        bool focusCommand;
        GUIStyle titleStyle, labelStyle, buttonStyle, fieldStyle;

        void Awake()
        {
            dialogue = GetComponent<DialogueController>(); gate = GetComponent<SceneInputGate>();
            interactor = GetComponent<SceneObjectInteractor>();
            delivery = GetComponent<SealedDeliveryController>();
            Write("输入 help 查看命令；点击按钮试播对话或收件。收件完成后解锁对应幕文件。");
        }
        void Update()
        {
            if (!Available) return;
            if (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.BackQuote)) SetOpen(!IsOpen);
            else if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) SetOpen(false);
        }
        public void SetOpen(bool open)
        {
            if (open && !Available) return;
            IsOpen = open; gate.SetConsole(open);
            if (open)
            {
                if (interactor) interactor.CancelInteraction();
                dialogue.Voice.Stop(); focusCommand = true;
            }
        }
        void OnDisable() { IsOpen = false; if (gate) gate.SetConsole(false); }

        public string Execute(string input)
        {
            string[] parts = (input ?? string.Empty).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return string.Empty;
            string result;
            switch (parts[0].ToLowerInvariant())
            {
                case "help":
                    result = "help / list / play <事件ID> / deliver <1–5> / next / stop / speed <0或8–60> / volume <0–1> / clear"; break;
                case "list":
                    var names = new List<string>();
                    foreach (var sequence in dialogue.events) if (sequence) names.Add(sequence.id + " · " + sequence.title);
                    for (int act = 1; act <= 5; act++) names.Add("deliver " + act + " · 第 " + act + " 幕封口袋收件");
                    result = string.Join("\n", names); break;
                case "play":
                    if (parts.Length != 2) result = "用法：play arrival";
                    else if (dialogue.IsActive) result = "已有对话；请先 stop，或继续当前对话。";
                    else if (delivery && delivery.IsActive) result = "正在收件；请先点击封口袋，或 stop 中止。";
                    else if (dialogue.Play(parts[1])) { SetOpen(false); result = "开始：" + parts[1]; }
                    else result = "无法开始：检查事件 ID、引用，或等待探视窗停止运动。";
                    break;
                case "deliver":
                    if (parts.Length != 2 || !int.TryParse(parts[1], out int actNumber) || actNumber < 1 || actNumber > 5)
                        result = "用法：deliver 1（幕号 1–5）";
                    else if (!delivery) result = "场景尚未安装封口袋收件事件。";
                    else if (delivery.archive && delivery.archive.IsAvailable(actNumber)) result = "该幕文件已解锁，无需重复收件。";
                    else if (delivery.BeginDelivery(actNumber)) { SetOpen(false); result = "开始第 " + actNumber + " 幕收件：活门转入后点击封口袋。"; }
                    else result = "无法收件：请先结束当前事件，或等待活门停止运动。";
                    break;
                case "next":
                    if (!dialogue.IsActive) result = "当前没有对话。";
                    else { dialogue.Advance(); result = "已推进（打字中先显示整句）。"; }
                    break;
                case "stop":
                    if (delivery && delivery.IsActive) { delivery.Stop(); result = "收件已中止，未解锁文件，已恢复原机位。"; }
                    else { dialogue.Stop(); result = "对话已结束，机位与探视窗已恢复。"; }
                    break;
                case "speed":
                    if (!Number(parts, out float speed) || (speed != 0 && (speed < 8 || speed > 60))) result = "速度范围：8–60 字/秒；0 使用角色默认值。";
                    else { dialogue.speedOverride = speed; result = "文字速度：" + speed.ToString(CultureInfo.InvariantCulture); }
                    break;
                case "volume":
                    if (!Number(parts, out float volume) || volume < 0 || volume > 1) result = "音量范围：0–1。";
                    else { dialogue.Voice.masterVolume = volume; dialogue.Voice.Stop(); result = "发言音量：" + volume.ToString(CultureInfo.InvariantCulture); }
                    break;
                case "clear": log.Clear(); result = "记录已清空。"; break;
                default: result = "未知命令；输入 help 查看帮助。"; break;
            }
            Write("> " + input + "\n" + result);
            return result;
        }
        static bool Number(string[] parts, out float number)
        {
            number = 0;
            return parts.Length == 2 && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out number)
                && !float.IsNaN(number) && !float.IsInfinity(number);
        }
        void Write(string message)
        {
            log.Add(message); if (log.Count > 50) log.RemoveAt(0);
            scroll.y = float.MaxValue;
        }
        void Submit()
        {
            if (string.IsNullOrWhiteSpace(command)) return;
            history.Add(command); if (history.Count > 50) history.RemoveAt(0);
            historyIndex = history.Count;
            string input = command; command = string.Empty; Execute(input); focusCommand = true;
        }

        void OnGUI()
        {
            if (!Available) return;
            if (labelStyle == null) Styles();
            GUI.depth = -1000;
            if (!IsOpen)
            {
                if (!dialogue.IsActive && (!delivery || !delivery.IsActive)) GUI.Label(new Rect(16, 12, 260, 26), "F1 / `  测试控制台", labelStyle);
                return;
            }
            var oldMatrix = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1080f, Screen.height / 760f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1000 * scale) * 0.5f,
                (Screen.height - 700 * scale) * 0.5f, 0), Quaternion.identity, Vector3.one * scale);
            var oldColor = GUI.color;
            GUI.color = new Color(0.035f, 0.031f, 0.026f, 0.98f);
            GUI.DrawTexture(new Rect(0, 0, 1000, 700), Texture2D.whiteTexture); GUI.color = oldColor;
            GUILayout.BeginArea(new Rect(26, 20, 948, 660));
            GUILayout.BeginHorizontal();
            GUILayout.Label("紫手套  /  事件测试", titleStyle);
            if (GUILayout.Button("关闭 · F1 / Esc", buttonStyle, GUILayout.Width(190))) SetOpen(false);
            GUILayout.EndHorizontal();
            string status = dialogue.IsActive ? dialogue.ActiveEvent.title + "  ·  "
                + (dialogue.LineIndex < 0 ? "来访中" : "第 " + (dialogue.LineIndex + 1) + " / " + dialogue.ActiveEvent.lines.Length + " 句") : "等待试播";
            if (delivery && delivery.IsActive) status = "第 " + delivery.ActiveAct + " 幕收件 · "
                + (delivery.State == SealedDeliveryController.Phase.AwaitingPickup ? "等待点击封口袋" : "交接中");
            GUILayout.Label(status, labelStyle);
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("继续 / 补全", buttonStyle)) dialogue.Advance();
            if (GUILayout.Button("中止当前事件", buttonStyle)) Execute("stop");
            GUILayout.EndHorizontal();
            for (int i = 0; i < dialogue.events.Length; i += 2)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + 2, dialogue.events.Length); j++)
                {
                    var sequence = dialogue.events[j]; if (!sequence) continue;
                    bool previous = GUI.enabled; GUI.enabled = !dialogue.IsActive && (!delivery || !delivery.IsActive);
                    if (GUILayout.Button(sequence.title + (sequence.testOnly ? " [预留测试]" : ""), buttonStyle, GUILayout.Width(469)))
                        Execute("play " + sequence.id);
                    GUI.enabled = previous;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(7);
            GUILayout.Label("封口袋收件 · 落到桌面后解锁文件", labelStyle);
            GUILayout.BeginHorizontal();
            for (int act = 1; act <= 5; act++)
            {
                bool previous = GUI.enabled;
                bool received = delivery && delivery.archive && delivery.archive.IsAvailable(act);
                GUI.enabled = delivery && delivery.CanDeliver(act);
                if (GUILayout.Button("第 " + act + " 幕" + (received ? " · 已收" : " · 收件"), buttonStyle)) Execute("deliver " + act);
                GUI.enabled = previous;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(7);
            GUILayout.Label("speed 0：角色默认速度    volume 0：静音    ↑ / ↓：命令历史", labelStyle);
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
            foreach (string entry in log) GUILayout.Label(entry, labelStyle);
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("DialogueCommand");
            command = GUILayout.TextField(command, fieldStyle, GUILayout.Height(40));
            if (GUILayout.Button("执行", buttonStyle, GUILayout.Width(90))) Submit();
            GUILayout.EndHorizontal();
            if (focusCommand) { GUI.FocusControl("DialogueCommand"); focusCommand = false; }
            var inputEvent = Event.current;
            if (inputEvent.type == EventType.KeyDown)
            {
                if (inputEvent.keyCode == KeyCode.Return || inputEvent.keyCode == KeyCode.KeypadEnter) { Submit(); inputEvent.Use(); }
                else if (inputEvent.keyCode == KeyCode.UpArrow || inputEvent.keyCode == KeyCode.DownArrow)
                {
                    historyIndex = Mathf.Clamp(historyIndex + (inputEvent.keyCode == KeyCode.UpArrow ? -1 : 1), 0, history.Count);
                    command = historyIndex == history.Count ? string.Empty : history[historyIndex]; inputEvent.Use();
                }
            }
            GUILayout.EndArea(); GUI.matrix = oldMatrix;
        }
        void Styles()
        {
            labelStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, wordWrap = true, richText = false };
            labelStyle.normal.textColor = new Color(0.82f, 0.78f, 0.67f);
            titleStyle = new GUIStyle(labelStyle) { fontSize = 28 };
            buttonStyle = new GUIStyle(GUI.skin.button) { font = font, fontSize = 19, fixedHeight = 40, richText = false };
            fieldStyle = new GUIStyle(GUI.skin.textField) { font = font, fontSize = 21 };
        }
    }
}
