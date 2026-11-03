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
        [MenuItem("Purple Glove/Acts/Install Fourth Act Opening")]
        public static void InstallFourthActOpening()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Install act four outside Play Mode.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            LoadSavedSceneAssets();
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            if (!archive) throw new InvalidOperationException("Install the archive first.");
            var layout = archive.transform.Find(ActDocumentGroup);
            if (!layout) throw new InvalidOperationException("Missing authored document layout.");
            foreach (var item in archive.acts[3].documents) if (item) UnityEngine.Object.DestroyImmediate(item.gameObject);
            archive.acts[3].documents = Enumerable.Range(0, ActDocumentPages[3].Length).Select(i => CreateFourthActDocument(layout, i)).ToArray();
            ArrangeFourthActDocuments(archive);
            ConfigureFourthActDocumentReferences(archive);
            BuildFourthActPackets(Camera.main.GetComponent<SealedDeliveryController>());
            EditorUtility.SetDirty(archive); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("FOURTH_ACT_OPENING_INSTALLED");
        }

        static void ConfigureFourthActDocumentReferences(ActArchiveController archive)
        {
            archive.acts[3].deferredDocuments = Array.Empty<SceneObject>();
            var records = Camera.main.GetComponent<PlaintextArchiveController>();
            if (records && archive.acts[3].documents.Length == 5)
            {
                foreach (var entry in records.entries.Where(e => e.act == 4))
                    entry.sourceDocument = archive.acts[3].documents[entry.id == "rail" ? 4 : 2];
                EditorUtility.SetDirty(records);
            }
            ConfigureFourthActOpening(Camera.main);
            if (Camera.main.GetComponent<FifthActFlow>()) ConfigureFinalActs(Camera.main);
        }

        public static void ConfigureFourthActOpening(Camera camera)
        {
            var archive = UnityEngine.Object.FindFirstObjectByType<ActArchiveController>();
            var dialogue = camera.GetComponent<DialogueController>(); var delivery = camera.GetComponent<SealedDeliveryController>();
            var story = camera.GetComponent<PrologueSequence>();
            if (!archive || !dialogue || !delivery || !story) return;
            var opening = camera.GetComponent<FourthActOpening>() ?? camera.gameObject.AddComponent<FourthActOpening>();
            opening.archive = archive; opening.dialogue = dialogue; opening.delivery = delivery; opening.story = story;
            const string path = DeskDialogueBuilder.DataRoot + "/Events/act4_arrival.asset";
            if (!opening.introduction) opening.introduction = AssetDatabase.LoadAssetAtPath<DialogueEvent>(path);
            if (!opening.introduction)
            {
                var clerk = dialogue.speakers.Single(s => s.id == "clerk"); var luca = dialogue.speakers.Single(s => s.id == "luca");
                DialogueEvent.Line L(DialogueSpeaker s, string text) => new DialogueEvent.Line { speaker = s, text = text };
                var intro = ScriptableObject.CreateInstance<DialogueEvent>();
                intro.id = "act4_arrival"; intro.title = "第四幕 · 两份独立封件"; intro.initialVisitor = clerk;
                intro.context = "第四幕淡入完成后，逐包说明来源；关窗后送入两份独立封包，接收落桌后解锁三张文书与两张密文纸条。";
                intro.closeShutterOnComplete = true;
                intro.arrivalSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/PH/Doors/door_knock_pattern_01.wav");
                intro.arrivalDelay = intro.arrivalSound ? intro.arrivalSound.length : 0.1f;
                intro.lines = new[] {
                    L(clerk, "卢卡，这次有两份独立封件。"),
                    L(clerk, "灰白色的是你调的对应第十七号委托，批号七二六的原件存档。"),
                    L(clerk, "里面是商路通行协议之类的东西。"),
                    L(clerk, "棕包来自城北修道院。一个修士带过来，指名道姓说要给你，把东西给我之后他就回去了。"),
                    L(clerk, "我把棕包登记成“信件”了。"),
                    L(luca, "好的，请送进来吧。") };
                AssetDatabase.CreateAsset(intro, path); opening.introduction = intro;
            }
            if (!dialogue.events.Contains(opening.introduction)) dialogue.events = dialogue.events.Concat(new[] { opening.introduction }).ToArray();
            EditorUtility.SetDirty(opening); EditorUtility.SetDirty(dialogue);
        }

        static void ArrangeFourthActDocuments(ActArchiveController archive)
        {
            var mat = GameObject.Find("WritingMat").transform;
            for (int i = 0; i < archive.acts[3].documents.Length; i++)
            {
                var paper = archive.acts[3].documents[i].transform;
                // The affine slip stays on the mat; the longer rail slip uses the desk's front edge.
                float x = i == 0 || i == 3 ? -0.23f : 0.23f;
                float z = i < 2 ? 0.275f : i == 2 ? -0.075f : i == 4 ? -0.215f : -0.165f;
                paper.SetPositionAndRotation(mat.TransformPoint(V(x, 0.014f, z)), mat.rotation);
                if (i == 4) PositionRailCipherStrip(paper);
                paper.localScale = Vector3.one; paper.gameObject.SetActive(false);
            }
        }

        static SceneObject CreateFourthActDocument(Transform parent, int index)
        {
            actDocumentFont = RequiredDocumentFont("SourceHanSerifCN-Regular.otf");
            var paper = new GameObject("Act4_" + (index + 1).ToString("00") + "_" + ActDocumentPages[3][index][0]);
            paper.transform.SetParent(parent, false);
            var item = paper.AddComponent<SceneObject>(); item.screenFill = 0.82f; item.minimumViewDistance = 0.18f;
            item.inspectionFill = new Color(0.85f, 0.78f, 0.64f);
            bool strip = index == 2 || index == 4;
            float height = index == 4 ? RailStripHeight : strip ? 0.075f : 0.40f;
            float width = index == 4 ? RailStripWidth : index == 2 ? AffineStripWidth : 0.42f;
            var box = item.GetComponent<BoxCollider>(); box.center = V(0, 0.004f, 0); box.size = V(width, 0.012f, height);
            MeshObject(index < 2 ? "Act4_ArchivePaper" : strip ? "Act4_CipherStrip" : "Act4_MasterLetterPaper", paper.transform,
                m => m.Sheet(0.42f, strip ? 0.075f : height, M(index < 2 ? "Paper_RagIvory" : "Parchment_Aged"), 0.003f))
                .transform.localScale = V(width / 0.42f, 1, strip ? height / 0.075f : 1);
            if (index < 2) BuildFourthActArchivePaper(paper.transform, index);
            else if (strip) BuildFourthActCipher(paper.transform, index);
            else BuildFourthActLetter(paper.transform);
            if (index == 2 && Camera.main && Camera.main.GetComponent<DialogueController>())
                DeskObjectMemoryBuilder.InstallFourthActMemory(item, Camera.main.GetComponent<DialogueController>());
            foreach (var renderer in paper.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = ShadowCastingMode.Off;
            paper.SetActive(false); return item;
        }

        static void BuildFourthActArchivePaper(Transform paper, int index)
        {
            FirstActInk(paper, "Office", "印务行会 · 第五十七号封档原件", actDocumentFont, 0, 0.181f, 0.34f, 0.015f, true);
            FirstActInk(paper, "Title", ActDocumentPages[3][index][0], actDocumentFont, 0, 0.151f, 0.35f, 0.023f, true);
            FirstActInk(paper, "Reference", "米兰内廷第17号委托  /  批号726", PrintedChinese, 0, 0.117f, 0.35f, 0.014f, true);
            FirstActInk(paper, "Body", ActDocumentPages[3][index][1], PrintedChinese, -0.18f, 0.087f, 0.36f, index == 0 ? 0.150f : 0.187f);
            if (index == 0)
            {
                FirstActInk(paper, "French signature label", "商业原稿签收", PrintedChinese, -0.18f, -0.077f, 0.105f, 0.013f);
                FirstActInk(paper, "French signature", "Charles", notesFont, -0.059f, -0.071f, 0.12f, 0.023f);
                FirstActInk(paper, "Collector signature label", "经手领取签名", PrintedChinese, -0.18f, -0.113f, 0.105f, 0.013f);
                FirstActInk(paper, "Collector signature", "Carafa", notesFont, -0.059f, -0.107f, 0.12f, 0.023f);
            }
            else
            {
                FirstActInk(paper, "Change signatures", "I  Carafa    II  Carafa", notesFont, -0.18f, -0.108f, 0.35f, 0.020f);
                FirstActInk(paper, "Transcriber signature", "Matteo Ricci", notesFont, 0.034f, -0.135f, 0.145f, 0.022f);
            }
            FirstActInk(paper, "Custody record", "出库登记：两名保管见证人会同启封，原件送核。", PrintedChinese, -0.18f, -0.164f, 0.36f, 0.012f);
            FirstActInk(paper, "Folio", "第五十七号封档  /  IV · 0" + (index + 1), actDocumentFont, 0, -0.184f, 0.24f, 0.010f, true);
            MeshObject("Act4_ArchiveHeaderRule", paper, m => m.Box(V(0, 0.009f, 0.101f), V(0.36f, 0.0006f, 0.0007f), M("Ink")));
        }

        static void BuildFourthActCipher(Transform paper, int index)
        {
            CipherDocumentInk(paper, index == 2 ? "Affine cipher" : "Rail cipher",
                ActDocumentPages[3][index][1], 0, 0, (index == 2 ? AffineStripWidth : RailStripWidth) - 0.04f, true, true);
        }

        static void BuildFourthActLetter(Transform paper)
        {
            FirstActInk(paper, "Body", ActDocumentPages[3][3][1], HandwrittenChinese, -0.18f, 0.165f, 0.36f, 0.281f);
            FirstActInk(paper, "Signature", "Matteo", notesFont, 0.075f, -0.151f, 0.10f, 0.021f);
        }

        public static void BuildFourthActPackets(SealedDeliveryController delivery)
        {
            if (!delivery || !delivery.packet) return;
            LoadSavedSceneAssets();
            if (delivery.fourthActPacketsVisual) UnityEngine.Object.DestroyImmediate(delivery.fourthActPacketsVisual);
            Variant("Paper_FourthActBrownWrap", "Paper_AgedCream", new Color(0.53f, 0.31f, 0.16f));
            var pair = new GameObject("Act 4 - independently sealed archive and master packets").transform;
            pair.SetParent(delivery.packet.transform, false);
            // A visible gap and separate ties/seals distinguish the two registered sources.
            MeshObject("Act4_GuildArchivePacket", pair, m =>
            {
                m.Box(V(-0.071f, 0.018f, 0), V(0.123f, 0.032f, 0.128f), M("Paper_RagIvory"), 0.002f);
                m.Box(V(-0.071f, 0.035f, 0), V(0.007f, 0.002f, 0.129f), M("Stitch"));
                m.Box(V(-0.071f, 0.035f, 0), V(0.124f, 0.002f, 0.004f), M("Stitch"));
                foreach (float x in new[] { -0.091f, -0.051f })
                    m.Lathe(V(x, 0.037f, -0.014f), new[] { P(0.009f, 0), P(0.011f, 0.001f), P(0.009f, 0.004f) }, 10, M("Wax_Oxblood"));
            });
            MeshObject("Act4_RepairedMasterPacket", pair, m =>
            {
                m.Box(V(0.071f, 0.014f, 0), V(0.123f, 0.024f, 0.128f), M("Paper_FourthActBrownWrap"), 0.002f);
                m.Box(V(0.071f, 0.027f, 0), V(0.003f, 0.002f, 0.129f), M("Stitch"));
                m.Box(V(0.107f, 0.027f, 0.022f), V(0.036f, 0.001f, 0.060f), M("Paper_AgedCream"));
                for (int i = 0; i < 6; i++)
                    m.Box(V(0.107f, 0.029f, -0.002f + i * 0.009f), V(0.043f, 0.001f, 0.0015f), M("Stitch"));
                m.Lathe(V(0.071f, 0.029f, -0.025f), new[] { P(0.009f, 0), P(0.011f, 0.001f), P(0.009f, 0.004f) }, 9, M("Wax_Oxblood"));
            });
            var fill = new GameObject("Fourth act packet bounce light").AddComponent<Light>();
            fill.transform.SetParent(pair, false); fill.transform.localPosition = V(0, 0.18f, -0.12f);
            fill.type = LightType.Point; fill.range = 0.65f; fill.intensity = 1.35f; fill.color = new Color(1, 0.87f, 0.69f);
            fill.renderMode = LightRenderMode.ForcePixel; fill.shadows = LightShadows.None; fill.cullingMask = (1 << 8) | 1;
            delivery.fourthActPacketsVisual = pair.gameObject; pair.gameObject.SetActive(false); EditorUtility.SetDirty(delivery);
        }
    }
}
