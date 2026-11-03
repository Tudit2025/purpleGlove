using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PurpleGlove.Editor
{
    /// <summary>Additive installer: preserves room objects and any already-authored speaker/event assets.</summary>
    public static class DeskDialogueBuilder
    {
        public const string DataRoot = "Assets/Data/Dialogue";
        const string ArtRoot = "Assets/Art/Dialogue";
        static readonly string[] SpeakerIds = { "luca", "clerk", "beatrice", "duke", "charles", "carafa", "matteo", "bartolo" };

        [MenuItem("Purple Glove/Dialogue/Install in Desk Room")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            InstallInScene(Camera.main);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("DIALOGUE_INSTALLED");
        }

        public static void InstallInScene(Camera camera)
        {
            if (!camera) throw new InvalidOperationException("Dialogue requires the desk camera.");
            Directory.CreateDirectory(DataRoot + "/Speakers"); Directory.CreateDirectory(DataRoot + "/Events");
            AssetDatabase.Refresh();
            ConfigureTextures();
            var speakers = BuildSpeakers();
            var font = AssetDatabase.LoadAssetAtPath<Font>(DeskSceneBuilder.Root + "/Fonts/SourceHanSerifCN-Regular.otf");
            if (!font) throw new InvalidOperationException("Missing Chinese dialogue font.");
            var dialogueFont = AssetDatabase.LoadAssetAtPath<Font>(DeskSceneBuilder.Root + "/Fonts/YinPinZhuSong.ttf");
            if (!dialogueFont) throw new InvalidOperationException("Missing YinPin ZhuSong dialogue font.");
            Add<SceneInputGate>(camera.gameObject);
            Add<DialogueVoice>(camera.gameObject);
            Add<DialogueOverlay>(camera.gameObject).font = dialogueFont;
            var controller = Add<DialogueController>(camera.gameObject);
            controller.speakers = SpeakerIds.Select(id => speakers[id]).ToArray();
            controller.events = BuildEvents(speakers);
            controller.eyeShutter = UnityEngine.Object.FindObjectsOfType<DoorMechanism>()
                .Single(d => d.mechanism == DoorMechanism.Mechanism.EyeShutter);
            var anchor = GameObject.Find("Visitor eye line - 1.66m");
            if (!anchor) throw new InvalidOperationException("Install visitation door before dialogue.");
            var visitor = Add<VisitorEyes>(anchor);
            var portrait = anchor.transform.Find("Pixel visitor eyes");
            if (!portrait)
            {
                portrait = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
                portrait.name = "Pixel visitor eyes";
                UnityEngine.Object.DestroyImmediate(portrait.GetComponent<Collider>());
                portrait.SetParent(anchor.transform, false);
            }
            // Anchor faces the room (-Z); retreat 40 cm into the corridor.
            portrait.localPosition = new Vector3(-0.06f, 0.02f, -0.40f);
            portrait.rotation = controller.eyeShutter.transform.rotation;
            portrait.localScale = new Vector3(0.25f, 0.375f, 1);
            portrait.gameObject.layer = 8;
            var material = AssetDatabase.LoadAssetAtPath<Material>(ArtRoot + "/VisitorEyes.mat");
            if (!material)
            {
                material = new Material(Shader.Find("PurpleGlove/Visitor Eyes"));
                AssetDatabase.CreateAsset(material, ArtRoot + "/VisitorEyes.mat");
            }
            visitor.portrait = portrait.GetComponent<MeshRenderer>();
            visitor.portrait.sharedMaterial = material;
            visitor.portrait.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            visitor.portrait.receiveShadows = true; visitor.portrait.enabled = false;
            // The corridor candle is behind the face. Approximate its weak bounce
            // from the doorway with a small local light, rather than emissive eyes.
            var bounce = anchor.transform.Find("Visitor reflected corridor light");
            if (!bounce)
            {
                bounce = new GameObject("Visitor reflected corridor light").transform;
                bounce.SetParent(anchor.transform, false);
            }
            bounce.localPosition = new Vector3(0.25f, 0.14f, -0.04f);
            var light = Add<Light>(bounce.gameObject);
            light.type = LightType.Point; light.color = new Color(1, 0.78f, 0.56f);
            light.intensity = 0.65f; light.range = 1.3f;
            light.cullingMask = 1 << 8; light.shadows = LightShadows.Soft;
            light.renderMode = LightRenderMode.ForcePixel; light.enabled = false;
            visitor.corridorBounce = light;
            controller.visitor = visitor;
            var audioObject = anchor.transform.Find("Visitor arrival audio");
            if (!audioObject)
            {
                audioObject = new GameObject("Visitor arrival audio").transform;
                audioObject.SetParent(anchor.transform, false);
            }
            controller.arrivalAudio = Add<AudioSource>(audioObject.gameObject);
            controller.arrivalAudio.playOnAwake = false; controller.arrivalAudio.spatialBlend = 0.7f;
            controller.arrivalAudio.volume = 0.6f; controller.arrivalAudio.minDistance = 2; controller.arrivalAudio.maxDistance = 9;
            controller.arrivalAudio.dopplerLevel = 0;
            Add<DialogueTestConsole>(camera.gameObject).font = font;
            DeskPrologueBuilder.InstallInScene(camera);
            DeskObjectMemoryBuilder.InstallInScene(camera);
            if (camera.GetComponent<PlaintextArchiveController>()) DeskFirstActFlowBuilder.InstallInScene(camera);
        }

        static void ConfigureTextures()
        {
            foreach (string id in SpeakerIds.Skip(1))
            {
                string path = ArtRoot + "/Textures/visitor_" + id + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (!importer) throw new InvalidOperationException("Missing visitor image: " + path);
                importer.textureType = TextureImporterType.Default; importer.npotScale = TextureImporterNPOTScale.None;
                importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 512; importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        static Dictionary<string, DialogueSpeaker> BuildSpeakers()
        {
            var result = new Dictionary<string, DialogueSpeaker>();
            string[] names = { "卢卡", "行会执事", "贝阿特丽切", "孔蒂公爵", "夏尔", "卡拉法", "马泰奥", "巴尔托洛" };
            string[] colours = { "F3DFAC", "C8D5D9", "90D5B0", "E4AF7F", "9CC9F0", "D2A4E6", "B9BFF0", "E5C889" };
            string[] descriptions = { "主角不在探视窗中显示。", "平直浓眉、榛色眼睛，沉稳而疲惫。", "锐利眉峰、橄榄绿眼睛，警觉克制。",
                "低压灰眉、灰色深眼窝，威严而审视。", "一侧眉毛微挑、淡蓝眼睛与雀斑，谨慎的外交官。", "不对称细眉、琥珀眼睛与眼角纹，精于算计。",
                "蓬松银眉、灰蓝眼睛与深纹，疲惫而温和；结局预留。", "凌乱浓眉、圆睁深棕眼睛与墨迹；仅测试预留，正篇不露面。" };
            float[] pitches = { 1.06f, 0.96f, 1.24f, 0.69f, 1.13f, 0.86f, 0.77f, 1.34f };
            float[] cutoffs = { 6200, 4300, 7600, 2100, 8700, 3300, 2700, 5100 };
            float[] speeds = { 24, 22, 28, 17, 23, 21, 16, 26 };
            float[] intervals = { .075f, .09f, .065f, .12f, .08f, .10f, .13f, .07f };
            for (int i = 0; i < SpeakerIds.Length; i++)
            {
                string id = SpeakerIds[i], path = DataRoot + "/Speakers/" + id + ".asset";
                var speaker = AssetDatabase.LoadAssetAtPath<DialogueSpeaker>(path);
                if (!speaker)
                {
                    speaker = ScriptableObject.CreateInstance<DialogueSpeaker>(); speaker.id = id; speaker.displayName = names[i];
                    speaker.isProtagonist = i == 0; ColorUtility.TryParseHtmlString("#" + colours[i], out speaker.textColor);
                    speaker.charactersPerSecond = speeds[i]; speaker.pitch = pitches[i]; speaker.lowPassHz = cutoffs[i];
                    speaker.soundInterval = intervals[i]; speaker.volume = i == 3 ? .65f : .52f; speaker.eyeDescription = descriptions[i];
                    if (i != 0) speaker.eyes = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/Textures/visitor_" + id + ".png");
                    speaker.typingClips = Enumerable.Range(0, 4).Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>(
                        "Assets/Audio/PH/Typewriter/Short/typewriter_key_short_" + ((i + n * 2) % 8 + 1).ToString("00") + ".wav")).ToArray();
                    if (speaker.typingClips.Any(c => !c)) throw new InvalidOperationException("Missing typewriter clips.");
                    AssetDatabase.CreateAsset(speaker, path);
                }
                // Migrate the shipped strips, preserving any custom portrait assignment.
                if (i != 0 && (!speaker.eyes || AssetDatabase.GetAssetPath(speaker.eyes) == ArtRoot + "/Textures/eyes_" + id + ".png"))
                {
                    speaker.eyes = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/Textures/visitor_" + id + ".png");
                    EditorUtility.SetDirty(speaker);
                }
                result.Add(id, speaker);
            }
            return result;
        }

        static DialogueEvent[] BuildEvents(Dictionary<string, DialogueSpeaker> s)
        {
            DialogueEvent.Line L(string speaker, string text) => new DialogueEvent.Line { speaker = s[speaker], text = text };
            return new[]
            {
                Event("arrival", "01 · 行会收件", "第一幕：交代封门与收件规则。", false, s["clerk"], 1,
                    L("clerk", "卢卡，三方的见证封条已经贴好。"),
                    L("luca", "今夜送进来的每份文件，都要登记来路。"),
                    L("clerk", "这是米兰方面交存的核验委托，封件编号一号。"),
                    L("luca", "请把编号留下，我会先检查封口。"),
                    L("clerk", "若要调档，把检索单送回投递口。"),
                    L("luca", "在核验完成前，这扇门保持封闭。")),
                Event("beatrice", "02 · 第五十七号封档", "第二幕：贝阿特丽切给出可核验的档案方向。", false, s["beatrice"], 2,
                    L("clerk", "威尼斯代表贝阿特丽切找你，她带了一份文件来。"),
                    L("beatrice", "你就是卢卡大师吧？我调查这个事情已经几天了，你的一个行会同事给了我一张字条。"),
                    L("beatrice", "他不想当证人牵扯进来，写的密码。你应该能够破解它。"),
                    L("beatrice", "我差不多知道里面会写什么，但是我的立场让我不能跟进这个线索了。"),
                    L("luca", "那把便条给我吧，我会自己确认的。"),
                    L("clerk", "威尼斯代表带来的资料登记好了，我给你送进去。")),
                Event("charles", "03 · 具名否认", "第三幕：正式否认仍需与具体文件相互核对。", false, s["clerk"], 1,
                    L("clerk", "法国代表夏尔已经登记，获准就命令来源答复。"),
                    L("charles", "法国没有签发这道越境命令。"),
                    L("luca", "你愿意把这句话写成具名的正式否认吗？"),
                    L("charles", "把你核出的文本和来源列明，我会签署答复。"),
                    L("luca", "你的否认会进入卷宗，换版记录也会一并保留。"),
                    L("charles", "那么，请让每一方都能复核同一份文字。")),
                Event("pressure", "04 · 原件与签名", "第三至四幕试演：公爵施压，卡拉法要求代领。", false, s["duke"], 3,
                    L("duke", "离晨会已经不远了，核验人。"),
                    L("luca", "时间不能替一份文件证明来源。"),
                    L("duke", "你师父在委托上签过名，你准备怎样解释？"),
                    L("luca", "我会把他的签名和换版记录分别列明。"),
                    L("carafa", "原件可以交给我代领，手续会简单得多。"),
                    L("luca", "请先解释，两次换版为什么都有你的签收。"),
                    L("carafa", "我签收的是封袋，里面的文字由别人负责。"),
                    L("luca", "这句话也会进入共同核验的记录。")),
                Event("archive", "05 · 共同封存", "第五幕：试演交接用语；不改变实际谜题、卷宗或结局状态。", false, s["clerk"], 2,
                    L("clerk", "三方的登记副本都已备齐。"),
                    L("luca", "原始签收页、旧试印存根和出库记录，一并共同封存。"),
                    L("clerk", "任何一方都不能单独取走原件。"),
                    L("luca", "回执必须写明实际处理结果。"),
                    L("clerk", "收到材料，不等于同意暂停转押。"),
                    L("luca", "那就等有效的回件，不替他们补上承诺。")),
                Event("matteo", "06 · 黎明后的师徒", "获救结局的眼部与声音预留试演，不自动触发结局。", true, s["matteo"], 1,
                    L("matteo", "卢卡，你没有替我删掉那一页。"),
                    L("luca", "我不能把救你，写成替你证明无错。"),
                    L("matteo", "那是我的签名，也该由我解释。"),
                    L("luca", "从这一句开始，我们把话说全。")),
                Event("bartolo", "07 · 印刷匠试音", "纯素材测试；正篇设定仍是巴尔托洛不露面。", true, s["bartolo"], 3,
                    L("bartolo", "纸边可以裁掉，换过的版却会留下痕迹。"),
                    L("luca", "先告诉我，原始签收页存在哪里。"),
                    L("bartolo", "行会的纠纷封存库，登记号和台账能对得上。"),
                    L("luca", "我会按正式手续调取。"))
            };
        }
        static DialogueEvent Event(string id, string title, string context, bool testOnly, DialogueSpeaker visitor, int knock,
            params DialogueEvent.Line[] lines)
        {
            string path = DataRoot + "/Events/" + id + ".asset";
            var sequence = AssetDatabase.LoadAssetAtPath<DialogueEvent>(path);
            if (sequence) return sequence;
            sequence = ScriptableObject.CreateInstance<DialogueEvent>();
            sequence.id = id; sequence.title = title; sequence.context = context; sequence.testOnly = testOnly;
            sequence.initialVisitor = visitor; sequence.lines = lines;
            sequence.arrivalSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/PH/Doors/door_knock_pattern_" + knock.ToString("00") + ".wav");
            sequence.arrivalDelay = sequence.arrivalSound ? sequence.arrivalSound.length : 0.8f;
            AssetDatabase.CreateAsset(sequence, path); return sequence;
        }
        static T Add<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component ? component : target.AddComponent<T>();
        }
    }
}
