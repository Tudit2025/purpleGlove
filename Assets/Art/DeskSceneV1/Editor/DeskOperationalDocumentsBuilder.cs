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
        [MenuItem("Purple Glove/Scene/Install Operational Document Rack")]
        public static void InstallOperationalDocumentRack()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Install the working-form rack outside Play Mode.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            LoadSavedSceneAssets();
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            if (!archive) throw new InvalidOperationException("Install act documents first.");
            ConfigureOperationalDocuments(archive);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("OPERATIONAL_DOCUMENT_RACK_INSTALLED");
        }

        static void ConfigureOperationalDocuments(ActArchiveController archive)
        {
            var rack = GameObject.Find("CorrespondenceRack"); var mat = GameObject.Find("WritingMat");
            if (!rack || !mat) throw new InvalidOperationException("Missing the desk rack or writing mat.");
            var layout = archive.transform.Find(ActDocumentGroup);
            if (!layout) return;
            // Keep the current authored letters intact; the new application is an additional first-act enclosure.
            var transfer = archive.acts[0].documents.FirstOrDefault(p => p && p.name.EndsWith("行会档案转交申请表"));
            if (!transfer)
            {
                transfer = CreateFirstActDocument(layout, 3);
                archive.acts[0].documents = archive.acts[0].documents.Concat(new[] { transfer }).ToArray();
            }
            // The old prop-sized picking box covered the individual files. The rack now serves as their fixed holder.
            var rackObject = rack.GetComponent<SceneObject>();
            if (rackObject) { rackObject.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(rackObject); }
            foreach (var collider in rack.GetComponents<Collider>())
            { collider.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(collider); }
            var anchors = rack.transform.Find("Working form slots");
            if (!anchors) { anchors = new GameObject("Working form slots").transform; anchors.SetParent(rack.transform, false); }
            var desktop = mat.transform.Find("Working form interaction surface");
            if (!desktop)
            {
                desktop = new GameObject("Working form interaction surface").transform; desktop.SetParent(mat.transform, false);
                desktop.localPosition = V(0, 0.055f, 0);
            }
            void Attach(SceneObject paper, int act, string title, int column, int row, int rackFromAct = 1)
            {
                if (!paper) return;
                var slot = anchors.Find(title);
                if (!slot) { slot = new GameObject(title).transform; slot.SetParent(anchors, false); }
                slot.localPosition = V((column - 1) * 0.211f, 0.162f + row * 0.045f, -0.075f + row * 0.073f);
                slot.localRotation = Quaternion.Euler(-78, 0, 0); slot.localScale = Vector3.one;
                var form = paper.GetComponent<DeskOperationalDocument>() ?? paper.gameObject.AddComponent<DeskOperationalDocument>();
                var size = paper.GetComponent<BoxCollider>().size;
                form.archive = archive; form.receivedInAct = act; form.storageAnchor = slot; form.desktopAnchor = desktop;
                form.rackFromAct = rackFromAct;
                form.storageScale = V(0.172f / size.x, 0.4f, 0.25f / size.z); form.desktopScale = Vector3.one;
                paper.hoverDistance = 0.025f;
                if (form.rackTab) UnityEngine.Object.DestroyImmediate(form.rackTab);
                var tab = new GameObject("Rack tab").transform; tab.SetParent(paper.transform, false);
                tab.localPosition = V(0, 0.016f, size.z / 2);
                float width = 0.166f / form.storageScale.x, height = 0.045f / form.storageScale.z;
                MeshObject("WorkingFormTab_" + title, tab, m => m.Box(V(0, 0, 0), V(width, 0.002f, height), M("Paper_RagIvory"), 0.001f));
                var ink = FirstActInk(tab, "Tab label", title, PrintedChinese, 0, height * 0.28f, width * 0.89f, height * 0.55f, true);
                var box = tab.gameObject.AddComponent<BoxCollider>(); box.center = V(0, 0.003f, 0); box.size = V(width, 0.015f, height);
                form.rackTab = tab.gameObject;
                foreach (var renderer in tab.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = ShadowCastingMode.Off;
                form.Stow(false); paper.gameObject.SetActive(false);
                EditorUtility.SetDirty(form); EditorUtility.SetDirty(paper);
            }
            Attach(transfer, 1, "转交申请", 0, 0);
            Attach(archive.acts[2].documents.FirstOrDefault(p => p && p.GetComponent<ThirdActVerification>()), 3, "核验签字", 1, 0);
            var records = Camera.main.GetComponent<PlaintextArchiveController>();
            var confession = records ? records.entries.FirstOrDefault(e => e.id == "rail") : null;
            if (confession != null) Attach(confession.document, 4, "师父自述", 2, 0);
            ConfigureTransferApplicationArrival(Camera.main.GetComponent<DialogueController>());
            ConfigureFinalActs(Camera.main);
            EditorUtility.SetDirty(archive);
        }

        internal static void ConfigureTransferApplicationArrival(DialogueController dialogue)
        {
            if (!dialogue) return;
            var opening = dialogue.events.FirstOrDefault(e => e && e.id == "act1_arrival");
            if (!opening || opening.lines.Any(l => l.text.Contains("档案转交申请表"))) return;
            var clerk = dialogue.speakers.Single(s => s.id == "clerk");
            var lines = opening.lines.ToList();
            lines.Insert(Mathf.Max(0, lines.Count - 1), new DialogueEvent.Line { speaker = clerk,
                text = "另附一张行会档案转交申请表，申请把行会保管的档案交给指定受领方时使用。先收入桌面文件立夹，核验整理完毕后再办理。" });
            opening.lines = lines.ToArray(); EditorUtility.SetDirty(opening);
        }

        static void BuildTransferApplication(Transform paper)
        {
            actDocumentFont = RequiredDocumentFont("SourceHanSerifCN-Regular.otf");
            MeshObject("Act1_ArchiveTransferForm", paper, m => m.Sheet(0.42f, 0.60f, M("Paper_RagIvory"), 0.003f));
            FirstActInk(paper, "Office", "印 务 行 会 · 档 案 保 管 所", actDocumentFont, 0, 0.266f, 0.34f, 0.019f, true);
            FirstActInk(paper, "Title", "行会档案转交申请表", actDocumentFont, 0, 0.223f, 0.35f, 0.026f, true);
            FirstActInk(paper, "Purpose", ActDocumentPages[0][3][1], actDocumentFont, -0.177f, 0.177f, 0.354f, 0.043f);
            string[] labels = { "档案编号：", "档案名称：", "转交清单及页数：", "指定受领方：", "转交事由：", "申请人签名：", "行会核准及交接登记：" };
            float[] rows = { 0.102f, 0.057f, 0.012f, -0.053f, -0.104f, -0.169f, -0.219f };
            for (int i = 0; i < labels.Length; i++)
                FirstActInk(paper, "Field " + i, labels[i], actDocumentFont, -0.174f, rows[i], 0.21f, 0.017f);
            FirstActInk(paper, "Recipient choices", "米兰  /  威尼斯  /  法国", PrintedChinese, -0.04f, -0.053f, 0.21f, 0.016f);
            FirstActInk(paper, "Footer", "申请不等同交接；以实际签收凭据为准。", actDocumentFont, 0, -0.272f, 0.34f, 0.014f, true);
            MeshObject("Act1_ArchiveTransferFormRules", paper, m =>
            {
                foreach (float z in new[] { 0.122f, -0.251f }) m.Box(V(0, 0.009f, z), V(0.36f, 0.0006f, 0.0007f), M("Ink"));
                foreach (float z in new[] { 0.080f, 0.035f, -0.023f, -0.137f, -0.194f, -0.242f })
                    m.Box(V(0.025f, 0.009f, z), V(0.30f, 0.0006f, 0.0007f), M("Ink"));
            });
            foreach (var renderer in paper.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
