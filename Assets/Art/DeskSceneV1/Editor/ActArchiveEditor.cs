using UnityEditor;
using UnityEngine;

namespace PurpleGlove.Editor
{
    [CustomEditor(typeof(ActArchiveController))]
    public sealed class ActArchiveEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var archive = (ActArchiveController)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("剧情在幕末调用 EndAct(1–5) 或 CompleteCurrentAct()。运行时可在此模拟，不会写入场景初始状态。", MessageType.Info);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                EditorGUILayout.LabelField("当前进度", archive.AllActsCompleted ? "全部结束" : "第 " + archive.CurrentAct + " 幕");
                EditorGUILayout.LabelField("桌面资料", archive.DeskAct == 0 ? "已全部归档" : "第 " + archive.DeskAct + " 幕");
                using (new EditorGUI.DisabledScope(archive.AllActsCompleted))
                    if (GUILayout.Button("模拟结束当前幕并归档")) archive.CompleteCurrentAct();
                for (int act = 1; act <= 5; act++)
                    using (new EditorGUI.DisabledScope(!archive.IsAvailable(act)))
                        if (GUILayout.Button("取回第 " + act + " 幕资料")) archive.ShowActOnDesk(act);
            }
        }

        [MenuItem("Purple Glove/Acts/Simulate End of Current Act")]
        static void SimulateEnd()
        {
            var archive = Object.FindFirstObjectByType<ActArchiveController>();
            if (archive) archive.CompleteCurrentAct();
        }

        [MenuItem("Purple Glove/Acts/Simulate End of Current Act", true)]
        static bool CanSimulateEnd() => Application.isPlaying && Object.FindFirstObjectByType<ActArchiveController>();
    }
}
