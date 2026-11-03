using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace PurpleGlove.Editor
{
    public static partial class DeskSceneBuilder
    {
        [MenuItem("Purple Glove/Acts/Install Third Act Work Paper")]
        public static void InstallThirdActWorkPaper()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Install the work paper outside Play Mode.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            LoadSavedSceneAssets();
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            if (!archive || archive.acts[2].documents.Length < 5)
                throw new InvalidOperationException("Install the third-act packet first.");
            var layout = archive.transform.Find(ActDocumentGroup);
            if (!layout) throw new InvalidOperationException("Missing authored document layout.");
            var documents = archive.acts[2].documents.ToList();
            if (documents.Count == 5) documents.Add(CreateThirdActDocument(layout, 5));
            archive.acts[2].documents = documents.ToArray();
            PositionThirdActDocument(documents[5], 5);
            EditorUtility.SetDirty(archive);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("THIRD_ACT_WORK_PAPER_INSTALLED");
        }

        [MenuItem("Purple Glove/Acts/Install Third Act Packet")]
        [MenuItem("Purple Glove/Acts/Install Third Act Progression")]
        public static void InstallThirdActPacket()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Install third-act documents outside Play Mode.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            LoadSavedSceneAssets();
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            if (!archive) throw new InvalidOperationException("Install the archive first.");
            var layout = archive.transform.Find(ActDocumentGroup);
            if (!layout) throw new InvalidOperationException("Missing authored document layout.");
            foreach (var item in archive.acts[2].documents)
                if (item) UnityEngine.Object.DestroyImmediate(item.gameObject);
            archive.acts[2].documents = Enumerable.Range(0, ActDocumentPages[2].Length)
                .Select(i => CreateThirdActDocument(layout, i)).ToArray();
            ArrangeThirdActDocuments(archive);
            ConfigureThirdActDocumentReferences(archive);
            ConfigureSecondActEnding(Camera.main);
            ConfigureOperationalDocuments(archive);
            EditorUtility.SetDirty(archive);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("THIRD_ACT_PACKET_INSTALLED");
        }

        static void ConfigureThirdActDocumentReferences(ActArchiveController archive)
        {
            // Only Charles's later receipt is deferred; the French original and work paper arrive in the first packet.
            archive.acts[2].deferredDocuments = new[] { archive.acts[2].documents[3] };
            var records = Camera.main.GetComponent<PlaintextArchiveController>();
            if (!records) return;
            var entry = records.entries.FirstOrDefault(e => e.id == "vigenere");
            if (entry != null) entry.sourceDocument = archive.acts[2].documents[1];
            ConfigureThirdAct(Camera.main);
            EditorUtility.SetDirty(records);
        }

        static void ArrangeThirdActDocuments(ActArchiveController archive)
        {
            for (int i = 0; i < archive.acts[2].documents.Length; i++)
                PositionThirdActDocument(archive.acts[2].documents[i], i);
        }

        static void PositionThirdActDocument(SceneObject document, int index)
        {
            var mat = GameObject.Find("WritingMat").transform;
            // The small work paper sits just below the cipher without covering either line of ink.
            var positions = new[] { V(-0.315f, 0.014f, 0.245f), V(0.235f, 0.014f, -0.225f),
                V(0.315f, 0.014f, 0.245f), V(-0.235f, 0.014f, -0.225f), V(0, 0.014f, 0.245f),
                V(0.235f, 0.014f, -0.435f) };
            var angles = new[] { -1.5f, -3f, 1.5f, 2f, 0f, 4f };
            var paper = document.transform;
            paper.SetPositionAndRotation(mat.TransformPoint(positions[index]), mat.rotation * Quaternion.Euler(0, angles[index], 0));
            paper.localScale = Vector3.one * (index == 1 || index == 3 || index == 5 ? 1f : 0.70f);
            paper.gameObject.SetActive(false);
        }

        static SceneObject CreateThirdActDocument(Transform parent, int index)
        {
            actDocumentFont = RequiredDocumentFont("SourceHanSerifCN-Regular.otf");
            var paper = new GameObject("Act3_" + (index + 1).ToString("00") + "_" + ActDocumentPages[2][index][0]);
            paper.transform.SetParent(parent, false);
            var item = paper.AddComponent<SceneObject>();
            item.screenFill = 0.82f; item.minimumViewDistance = 0.18f;
            item.inspectionFill = new Color(0.85f, 0.78f, 0.64f);
            var box = item.GetComponent<BoxCollider>(); box.center = V(0, 0.004f, 0);
            bool workPaper = index == 5;
            bool slip = index == 1 || index == 3 || workPaper;
            float width = workPaper ? 0.22f : 0.42f;
            float depth = workPaper ? 0.12f : slip ? 0.27f : 0.60f;
            box.size = V(width, 0.012f, depth);
            MeshObject(workPaper ? "Act3_WorkPaper" : slip ? "Act3_CipherPaper" : "Act3_FormPaper", paper.transform,
                m => m.Sheet(width, depth, M(slip ? "Paper_AgedCream" : "Paper_RagIvory"), 0.003f));
            if (index == 0) BuildThirdActCommission(paper.transform);
            else if (index == 1) BuildThirdActCipher(paper.transform);
            else if (index == 2) BuildThirdActVerification(paper.transform);
            else if (index == 3) BuildCharlesReceipt(paper.transform);
            else if (index == 4) BuildFrenchOriginal(paper.transform);
            else FirstActInk(paper.transform, "Keyword", ActDocumentPages[2][5][1], notesFont, 0, 0.015f, 0.13f, 0.032f, true);
            foreach (var renderer in paper.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = ShadowCastingMode.Off;
            paper.SetActive(false); return item;
        }

        static void BuildThirdActCommission(Transform paper)
        {
            FirstActInk(paper, "Office", "米 兰 内 廷  ·  印 务 行 会", actDocumentFont, 0, 0.267f, 0.32f, 0.019f, true);
            FirstActInk(paper, "Title", "争议军情 · 译录委托", actDocumentFont, 0, 0.225f, 0.32f, 0.027f, true);
            FirstActInk(paper, "Body", ActDocumentPages[2][0][1], actDocumentFont, -0.177f, 0.176f, 0.354f, 0.202f);
            FirstActInk(paper, "Signer role", "承办译录人", actDocumentFont, -0.177f, -0.201f, 0.087f, 0.017f);
            FirstActInk(paper, "Matteo signature", "Matteo Ricci", notesFont, -0.060f, -0.191f, 0.22f, 0.035f);
            MeshObject("Act3_CommissionRules", paper, m =>
            {
                m.Box(V(0, 0.009f, 0.192f), V(0.356f, 0.0006f, 0.0007f), M("Ink"));
                m.Box(V(0.058f, 0.009f, -0.232f), V(0.24f, 0.0006f, 0.0007f), M("Ink"));
            });
            FirstActInk(paper, "Folio", "726  /  III · 01  /  原委托随件", PrintedChinese, 0, -0.271f, 0.30f, 0.015f, true);
        }

        static void BuildThirdActCipher(Transform paper)
        {
            FirstActInk(paper, "Title", "马泰奥 · 军情密文译录", HandwrittenChinese, -0.18f, 0.111f, 0.25f, 0.020f);
            FirstActInk(paper, "Reference", "第17号委托 · 726 · 标称来源：法方", actDocumentFont, -0.18f, 0.078f, 0.36f, 0.016f);
            CipherDocumentInk(paper, "Cipher", ActDocumentPages[2][1][1], 0, 0.020f, 0.36f);
            FirstActInk(paper, "Folio", "726  /  III · 02", PrintedChinese, 0, -0.106f, 0.17f, 0.014f, true);
        }

        static void BuildThirdActVerification(Transform paper)
        {
            FirstActInk(paper, "Office", "印 务 行 会  ·  三 方 核 验 所", actDocumentFont, 0, 0.267f, 0.32f, 0.019f, true);
            FirstActInk(paper, "Title", "军 情 核 验 表", actDocumentFont, 0, 0.226f, 0.24f, 0.028f, true);
            FirstActInk(paper, "Reference", "米兰内廷第17号委托  /  批号726", actDocumentFont, -0.18f, 0.183f, 0.36f, 0.016f);
            FirstActInk(paper, "Transcript heading", "一、明文还原", actDocumentFont, -0.166f, 0.139f, 0.14f, 0.021f);
            FirstActInk(paper, "Transcript prompt", "还原文字：", actDocumentFont, -0.166f, 0.101f, 0.09f, 0.016f);
            FirstActInk(paper, "Transcript signature label", "明文还原签名：", actDocumentFont, -0.166f, -0.006f, 0.125f, 0.018f);
            FirstActInk(paper, "Source heading", "二、来源确认", actDocumentFont, -0.166f, -0.061f, 0.14f, 0.021f);
            FirstActInk(paper, "Source options", "已证实  /  待核  /  存在冲突", actDocumentFont, -0.166f, -0.102f, 0.29f, 0.018f);
            FirstActInk(paper, "Evidence prompt", "核验依据：", actDocumentFont, -0.166f, -0.139f, 0.09f, 0.016f);
            FirstActInk(paper, "Source signature label", "来源确认签名：", actDocumentFont, -0.166f, -0.213f, 0.125f, 0.018f);
            FirstActInk(paper, "Folio", "分别填写、分别签名  /  III · 03", actDocumentFont, 0, -0.272f, 0.31f, 0.015f, true);
            var verification = paper.gameObject.AddComponent<ThirdActVerification>();
            verification.transcriptInk = FirstActInk(paper, "Restored transcript", "法国将于六月十八日越境。", HandwrittenChinese, -0.163f, 0.053f, 0.32f, 0.022f);
            verification.signatureInk = FirstActInk(paper, "Luca transcript signature", "Luca", notesFont, -0.025f, 0.005f, 0.16f, 0.025f);
            verification.signPrompt = FirstActInk(paper, "Sign transcript prompt", "点击签署明文还原", HandwrittenChinese, -0.025f, -0.006f, 0.185f, 0.018f);
            verification.transcriptInk.text = verification.signatureInk.text = verification.signPrompt.text = "";
            MeshObject("Act3_VerificationTableRules", paper, m =>
            {
                void Rule(float left, float right, float z) => m.Box(V((left + right) / 2, 0.009f, z), V(right - left, 0.0006f, 0.0007f), M("Ink"));
                foreach (float z in new[] { 0.155f, -0.041f, -0.246f }) Rule(-0.182f, 0.182f, z);
                foreach (float x in new[] { -0.182f, 0.182f })
                    m.Box(V(x, 0.009f, -0.0455f), V(0.0007f, 0.0006f, 0.401f), M("Ink"));
                foreach (float z in new[] { 0.059f, 0.026f, -0.176f }) Rule(-0.163f, 0.163f, z);
                Rule(-0.030f, 0.163f, -0.027f);
                Rule(-0.030f, 0.163f, -0.234f);
            });
        }

        static void BuildCharlesReceipt(Transform paper)
        {
            FirstActInk(paper, "Title", "夏尔 · 补充凭条", actDocumentFont, 0, 0.113f, 0.30f, 0.022f, true);
            FirstActInk(paper, "Reference", "第17号委托 / 批号726 / 法方预备说明", actDocumentFont, -0.18f, 0.079f, 0.36f, 0.014f);
            FirstActInk(paper, "Body", ActDocumentPages[2][3][1], HandwrittenChinese, -0.18f, 0.050f, 0.36f, 0.088f);
            FirstActInk(paper, "Charles signature", "Charles", notesFont, 0.08f, -0.048f, 0.10f, 0.024f);
            FirstActInk(paper, "Folio", "具名预备说明，非正式反证  /  III · 04", actDocumentFont, 0, -0.104f, 0.35f, 0.014f, true);
        }

        static void BuildFrenchOriginal(Transform paper)
        {
            FirstActInk(paper, "Office", "MISSION DE FRANCE", documentFont, 0, 0.258f, 0.30f, 0.023f, true);
            FirstActInk(paper, "Title", "DÉPÊCHE MILITAIRE", documentFont, 0, 0.203f, 0.33f, 0.031f, true);
            FirstActInk(paper, "Address", "À la chancellerie de Milan", documentFont, -0.174f, 0.133f, 0.30f, 0.023f);
            FirstActInk(paper, "French original", ActDocumentPages[2][4][1], documentFont,
                -0.174f, 0.047f, 0.348f, 0.072f);
            FirstActInk(paper, "Signer role", "Pour la mission de France", documentFont, -0.174f, -0.133f, 0.28f, 0.020f);
            FirstActInk(paper, "French signature", "Charles", notesFont, 0.015f, -0.177f, 0.155f, 0.040f);
            MeshObject("Act3_FrenchOriginalRules", paper, m =>
            {
                m.Box(V(0, 0.009f, 0.163f), V(0.348f, 0.0006f, 0.0007f), M("Ink"));
                m.Box(V(0.09f, 0.009f, -0.226f), V(0.17f, 0.0006f, 0.0007f), M("Ink"));
            });
            FirstActInk(paper, "Registration", "行会收件登记：第17号委托 / 批号726", actDocumentFont,
                -0.174f, -0.263f, 0.348f, 0.015f);
        }
    }
}
