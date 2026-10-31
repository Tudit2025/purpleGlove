using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace PurpleGlove.Editor
{
    public static partial class DeskSceneBuilder
    {
        const string CipherGroup = "09 Decoding tools and writing mat";

        [MenuItem("Purple Glove/Scene/Install Cipher Toolbox")]
        public static void InstallCipherToolbox()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DeskSceneStartup.ScenePath);
            LoadSavedSceneAssets();
            BuildCipherWorkbench(Camera.main);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("CIPHER_TOOLBOX_SAVED");
        }

        static void BuildCipherWorkbench(Camera camera)
        {
            var box = GameObject.Find("PreservationBox");
            if (!camera || !box) throw new InvalidOperationException("Missing desk camera or preservation box.");
            var previous = GameObject.Find(CipherGroup);
            if (previous) UnityEngine.Object.DestroyImmediate(previous);
            var oldContents = box.transform.Find("Cipher tray");
            if (oldContents) UnityEngine.Object.DestroyImmediate(oldContents.gameObject);
            var root = Group(CipherGroup);
            var work = camera.GetComponent<CipherWorkbench>();
            if (!work) work = camera.gameObject.AddComponent<CipherWorkbench>();
            if (!camera.GetComponent<SceneInputGate>()) camera.gameObject.AddComponent<SceneInputGate>();
            work.uiFont = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/SourceHanSerifCN-Regular.otf");
            if (!work.uiFont) throw new InvalidOperationException("Missing bundled Chinese font.");
            work.takeSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/PH/PaperBook/book_setdown_01.wav");
            work.returnSound = work.takeSound; work.paperSound = work.takeSound;

            // Match the other desk props' textured, scene-lit leather and writing paper.
            // Keep these asset names so existing workbench surfaces retain their references.
            materials["DecodeLinen"] = SaveMaterial("DecodeLinen", new Material(M("Leather_Umber")));
            materials["DecodeIvory"] = SaveMaterial("DecodeIvory", new Material(M("Paper_RagIvory")));
            Solid("DecodeInk", new Color(0.055f, 0.032f, 0.018f), 0, 0);
            foreach (string key in new[] { "DecodeLinen", "DecodeIvory", "DecodeInk" })
                M(key).SetColor("_InspectionFill", Color.black);
            M("DecodeInk").SetFloat("_OutlineWidth", 0);
            // Thin mats and loose paper receive shadows without self-shadowing, like WritingMat/Paper_Blank.
            work.mat = Child(root, "DecodingMat", V(0.96f, 0.802f, -0.49f));
            MeshObject("Cipher_LinenMat", work.mat, m =>
            {
                m.Box(V(0, 0.004f, 0), V(0.76f, 0.008f, 0.68f), M("DecodeLinen"), 0.006f);
                foreach (float x in new[] { -0.361f, 0.361f })
                    m.Box(V(x, 0.0085f, 0), V(0.002f, 0.001f, 0.634f), M("Stitch"));
                foreach (float z in new[] { -0.319f, 0.319f })
                    m.Box(V(0, 0.0085f, z), V(0.724f, 0.001f, 0.002f), M("Stitch"));
            }).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            AddCipherAction(Child(work.mat, "Interaction surface", Vector3.zero).gameObject, work, CipherWorkbenchAction.ActionKind.Focus,
                V(0, 0.005f, 0), V(0.76f, 0.01f, 0.68f));
            CipherText(work.mat, "Mat label", "解码工具", V(0, 0.010f, 0.287f), 0.0016f, work.uiFont);
            var slip = Child(work.mat, "Plaintext writing slip", V(0, 0.011f, -0.222f));
            MeshObject("Cipher_PlaintextSlip", slip, m =>
            {
                m.Box(V(0, 0, 0), V(0.60f, 0.002f, 0.165f), M("DecodeIvory"), 0.002f);
                for (int i = 0; i < 5; i++) CipherRule(m, V(0, 0.0018f, 0.025f - i * 0.022f), 0.55f, 0.0008f, M("Stitch"));
            }).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            CipherText(slip, "Writing heading", "明文书写处", V(0, 0.002f, 0.062f), 0.0018f, work.uiFont);
            work.plaintextInk = CipherText(slip, "Plaintext ink", "", V(-0.275f, 0.002f, 0.039f), 0.0013f, work.uiFont);
            work.plaintextInk.anchor = TextAnchor.UpperLeft; work.plaintextInk.alignment = TextAlignment.Left;
            CipherHit(slip, "Write on paper", CipherWorkbenchTarget.Kind.Plaintext, V(0, 0.003f, 0), V(0.60f, 0.005f, 0.165f));

            MoveWritingProp("InkBottle", V(1.36f, 0.801f, 0.12f), 8);
            MoveWritingProp("SandShaker", V(1.30f, 0.801f, 0.34f), 10);
            MoveWritingProp("Quill", V(1.38f, 0.802f, -0.43f), 90);
            MoveWritingProp("PaperKnife", V(0.57f, 0.802f, 0.02f), 0);
            work.writingQuill = GameObject.Find("Quill")?.transform;

            work.tools = new GameObject[5]; work.storedTools = new GameObject[5];
            for (int i = 0; i < 5; i++) work.tools[i] = Child(work.mat, "Tool " + (CipherWorkbench.Tool)i, V(0, 0.015f, 0.065f)).gameObject;
            BuildCaesarTool(work);
            BuildPigpenTool(work);
            BuildSaintCyrTool(work);
            BuildCipherGrid(work, false);
            BuildCipherGrid(work, true);
            for (int i = 0; i < work.tools.Length; i++)
            {
                // A small sewn leather pull tab is the physical return grip on every tool.
                Vector3 center = i == 0 ? V(0.192f, 0.006f, 0) : V(0.30f, 0.012f, 0.10f);
                var grip = MeshObject("Cipher_ReturnGrip_" + i, work.tools[i].transform, m =>
                    m.Box(center, V(0.035f, 0.006f, 0.045f), M("Leather_Umber"), 0.002f));
                CipherHit(grip.transform, "Return grip", CipherWorkbenchTarget.Kind.ReturnTool, center, V(0.04f, 0.012f, 0.05f));
            }
            box.transform.position = V(0.96f, 0.801f, 0.36f);
            box.transform.rotation = Quaternion.identity;
            work.toolbox = AddCipherAction(box, work, CipherWorkbenchAction.ActionKind.Toolbox, V(0, 0.10f, 0), V(0.51f, 0.2f, 0.33f));
            work.toolbox.screenFill = 0.46f; work.toolbox.inspectionEuler = new Vector3(-55, 0, 0);
            work.toolbox.inspectionFill = new Color(0.30f, 0.26f, 0.20f);
            work.lid = box.transform.Find("LidPivot"); work.lid.localRotation = Quaternion.identity;
            var oldLidHit = work.lid.Find("Close physical lid");
            if (oldLidHit) UnityEngine.Object.DestroyImmediate(oldLidHit.gameObject);
            CipherHit(work.lid, "Close physical lid", CipherWorkbenchTarget.Kind.CloseBox, V(0, 0.01f, -0.15f), V(0.51f, 0.025f, 0.30f)).gameObject.SetActive(false);
            var tray = Child(box.transform, "Cipher tray", V(0, 0.13f, 0));
            MeshObject("Cipher_BoxTray", tray, m =>
            {
                m.Box(Vector3.zero, V(0.44f, 0.008f, 0.255f), M("Leather_Umber"), 0.002f);
                for (int i = 0; i < 4; i++) m.Box(V(-0.132f + i * 0.088f, 0.015f, 0), V(0.004f, 0.025f, 0.255f), M("Wood_HoneyOak"));
            });
            for (int i = 0; i < work.tools.Length; i++)
            {
                var miniature = UnityEngine.Object.Instantiate(work.tools[i], tray);
                miniature.name = "Stored " + (CipherWorkbench.Tool)i;
                miniature.transform.localPosition = V(-0.176f + i * 0.088f, 0.01f, 0);
                miniature.transform.localScale = Vector3.one * (i == 0 ? 0.20f : 0.13f);
                miniature.transform.localRotation = Quaternion.Euler(0, 90, 0);
                foreach (var target in miniature.GetComponentsInChildren<CipherWorkbenchTarget>(true)) UnityEngine.Object.DestroyImmediate(target.gameObject);
                // Miniatures also use metre UVs; scaling a full-size tool must not shrink the wood grain.
                foreach (var filter in miniature.GetComponentsInChildren<MeshFilter>(true))
                {
                    var source = filter.sharedMesh;
                    string path = Root + "/Meshes/Stored_" + i + "_" + source.name + ".asset";
                    var mesh = UnityEngine.Object.Instantiate(source); mesh.name = "Stored_" + i + "_" + source.name;
                    var uv = mesh.uv;
                    for (int u = 0; u < uv.Length; u++) uv[u] *= miniature.transform.localScale.x;
                    mesh.uv = uv;
                    var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (saved) { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); }
                    else { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
                    filter.sharedMesh = saved;
                }
                work.storedTools[i] = miniature;
                var pick = CipherHit(tray, "Take " + (CipherWorkbench.Tool)i, CipherWorkbenchTarget.Kind.StoredTool,
                    V(-0.176f + i * 0.088f, 0.030f, 0), V(0.080f, 0.035f, 0.19f));
                pick.tool = (CipherWorkbench.Tool)i;
                CipherText(tray, "Slot " + i, new[] { "转盘", "猪圈", "滑尺", "仿射", "栅栏" }[i],
                    V(-0.176f + i * 0.088f, 0.008f, -0.105f), 0.0018f, work.uiFont);
                work.tools[i].SetActive(false);
            }
            CipherText(tray, "Toolbox title", "解密工具盒", V(0, 0.012f, 0.103f), 0.0018f, work.uiFont);
            BuildLetterTray(root, work);
            var view = camera.GetComponent<DeskScenePreview>();
            view.decodeFocusAnchor = Child(root, "Decode camera anchor", V(0.96f, 0.802f, -0.16f));
            view.decodeDetailAnchor = work.mat;
            view.decodeFocusHeight = 1.68f; view.decodeFocusFieldOfView = 48;
            EditorUtility.SetDirty(view); EditorUtility.SetDirty(work);
        }

        static void BuildCaesarTool(CipherWorkbench work)
        {
            var root = work.tools[0].transform;
            MeshObject("Cipher_CaesarBody", root, m =>
            {
                m.Lathe(Vector3.zero, new[] { P(0.18f, 0), P(0.18f, 0.009f), P(0.176f, 0.012f) }, 52, M("Metal_AgedBrass"));
                m.Disc(V(0, 0.0125f, 0), 0.173f, 52, M("DecodeIvory"), Vector3.up);
                for (int i = 0; i < 26; i++)
                {
                    float angle = i * Mathf.PI * 2 / 26;
                    Vector3 radial = V(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                    m.Rod(radial * 0.131f + V(0, 0.014f, 0), radial * 0.139f + V(0, 0.014f, 0), 0.0005f, M("DecodeInk"), 4);
                }
            });
            work.caesarRotor = Child(root, "Rotating plaintext alphabet", Vector3.zero);
            MeshObject("Cipher_CaesarRotor", work.caesarRotor, m =>
            {
                m.Disc(V(0, 0.017f, 0), 0.128f, 52, M("Wood_HoneyOak"), Vector3.up);
                m.Disc(V(0, 0.018f, 0), 0.122f, 52, M("DecodeIvory"), Vector3.up);
                m.Disc(V(0, 0.020f, 0), 0.012f, 12, M("Metal_AgedBrass"), Vector3.up);
            });
            for (int i = 0; i < 26; i++)
            {
                float angle = i * Mathf.PI * 2 / 26;
                Vector3 radial = V(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                CipherText(root, "Cipher " + i, ((char)('A' + i)).ToString(), radial * 0.155f + V(0, 0.014f, 0), 0.0021f, documentFont);
                CipherText(work.caesarRotor, "Plain " + i, ((char)('A' + i)).ToString(), radial * 0.105f + V(0, 0.020f, 0), 0.0019f, documentFont);
            }
            CipherText(root, "Outer legend", "密文 · 外环", V(0, 0.025f, -0.047f), 0.0012f, work.uiFont);
            CipherText(root, "Inner legend", "明文 · 内环", V(0, 0.025f, 0.047f), 0.0012f, work.uiFont);
            // The square collider is inscribed in the disc, so its corners never grab the outer ring.
            CipherHit(work.caesarRotor, "Turn inner disc", CipherWorkbenchTarget.Kind.Rotor, V(0, 0.021f, 0), V(0.176f, 0.012f, 0.176f));
        }

        static void BuildPigpenTool(CipherWorkbench work)
        {
            var root = work.tools[1].transform;
            MeshObject("Cipher_PigpenCard", root, m =>
            {
                m.Box(Vector3.zero, V(0.57f, 0.004f, 0.29f), M("DecodeIvory"), 0.002f);
                for (int i = 0; i < 26; i++) DrawNotebookGlyph(m, (char)('A' + i), V(-0.252f + i % 13 * 0.042f, 0.004f, 0.055f - i / 13 * 0.12f), 0.012f);
            }).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            CipherText(root, "Title", "猪圈密码表 · 留意圆点与朝向", V(0, 0.004f, 0.118f), 0.0017f, work.uiFont);
            for (int i = 0; i < 26; i++) CipherText(root, "Letter " + i, ((char)('A' + i)).ToString(), V(-0.252f + i % 13 * 0.042f, 0.004f, 0.021f - i / 13 * 0.12f), 0.0022f, documentFont);
        }

        static void BuildSaintCyrTool(CipherWorkbench work)
        {
            var root = work.tools[2].transform;
            work.slidePitch = 0.010f;
            // The two complete alphabets remain on the physical slider; the frame covers their overhang.
            MeshObject("Cipher_SaintCyrFrame", root, m =>
            {
                m.Box(V(0, 0, 0), V(0.63f, 0.010f, 0.24f), M("Wood_HoneyOak"), 0.004f);
                m.Box(V(0, 0.007f, 0.087f), V(0.55f, 0.003f, 0.042f), M("DecodeIvory"), 0.001f);
                m.Box(V(0, 0.007f, -0.092f), V(0.35f, 0.003f, 0.032f), M("DecodeIvory"), 0.001f);
                m.Box(V(0, 0.012f, 0.015f), V(0.276f, 0.012f, 0.055f), M("DecodeIvory"), 0.001f);
                foreach (float x in new[] { -0.228f, 0.228f }) m.Box(V(x, 0.021f, -0.04f), V(0.174f, 0.022f, 0.058f), M("Wood_HoneyOak"), 0.002f);
                foreach (float z in new[] { 0.015f, -0.04f }) m.Box(V(-0.19f, 0.033f, z), V(0.046f, 0.001f, 0.027f), M("DecodeIvory"), 0.0001f);
                m.Box(V(-0.125f, 0.024f, -0.006f), V(0.001f, 0.001f, 0.05f), M("DecodeInk"));
            });
            CipherText(root, "Title", "维吉尼亚密码 · 圣西尔滑尺", V(0, 0.017f, 0.087f), 0.0017f, work.uiFont);
            CipherText(root, "Plain legend", "明文", V(-0.19f, 0.035f, 0.015f), 0.0014f, work.uiFont);
            CipherText(root, "Cipher legend", "密文", V(-0.19f, 0.035f, -0.04f), 0.0014f, work.uiFont);
            CipherText(root, "Key legend", "当前密钥对齐上行 A", V(0, 0.017f, -0.092f), 0.0014f, work.uiFont);
            work.saintCyrSlide = Child(root, "Sliding double alphabet", Vector3.zero);
            MeshObject("Cipher_SaintCyrSlide", work.saintCyrSlide, m => m.Box(V(0.13f, 0.013f, -0.04f), V(0.526f, 0.006f, 0.038f), M("DecodeIvory"), 0.001f));
            for (int i = 0; i < 26; i++) CipherText(root, "Plain " + i, ((char)('A' + i)).ToString(), V(-0.125f + i * work.slidePitch, 0.019f, 0.015f), 0.00135f, documentFont);
            for (int i = 0; i < 52; i++) CipherText(work.saintCyrSlide, "Cipher " + i, ((char)('A' + i % 26)).ToString(), V(-0.125f + i * work.slidePitch, 0.017f, -0.04f), 0.00135f, documentFont);
            CipherHit(root, "Slide window", CipherWorkbenchTarget.Kind.Slide, V(0, 0.022f, -0.04f), V(0.27f, 0.012f, 0.045f));
            var keyPaper = Child(root, "Keyword paper", V(0, 0.012f, -0.087f));
            root.Find("Key legend").gameObject.SetActive(false);
            work.keywordInk = CipherText(keyPaper, "Keyword ink", "________________________", V(0, 0.006f, 0), 0.0014f, documentFont);
            CipherHit(keyPaper, "Write keyword", CipherWorkbenchTarget.Kind.Keyword, V(0, 0.006f, 0), V(0.30f, 0.009f, 0.035f));
            foreach (int direction in new[] { -1, 1 })
            {
                var peg = MeshObject("Cipher_KeyPeg_" + (direction < 0 ? "Previous" : "Next"), root, m =>
                    m.Lathe(V(direction * 0.20f, 0.012f, -0.09f), new[] { P(0.012f, 0), P(0.012f, 0.009f), P(0.009f, 0.012f) }, 10, M("Metal_AgedBrass")));
                CipherText(root, "Key direction " + direction, direction < 0 ? "<" : ">", V(direction * 0.20f, 0.026f, -0.09f), 0.0016f, documentFont);
                CipherHit(peg.transform, "Key peg", direction < 0 ? CipherWorkbenchTarget.Kind.PreviousKey : CipherWorkbenchTarget.Kind.NextKey,
                    V(direction * 0.20f, 0.023f, -0.09f), V(0.03f, 0.022f, 0.03f));
            }
        }

        static void BuildCipherGrid(CipherWorkbench work, bool rails)
        {
            int columns = rails ? 41 : 26;
            float pitch = rails ? 0.013f : 0.020f;
            var root = work.tools[rails ? 4 : 3].transform;
            string id = rails ? "Rail" : "Affine";
            float width = columns * pitch, start = -width * 0.5f;
            MeshObject("Cipher_" + id + "Paper", root, m =>
            {
                m.Box(Vector3.zero, V(0.62f, 0.003f, 0.235f), M("DecodeIvory"), 0.002f);
                for (int i = 0; i <= columns; i++) CipherRule(m, V(start + i * pitch, 0.0025f, 0), 0.0009f, 0.08f, M("DecodeInk"));
                for (int i = 0; i <= 2; i++) CipherRule(m, V(0, 0.0025f, 0.04f - i * 0.04f), width, 0.0009f, M("DecodeInk"));
            }).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            CipherText(root, "Title", rails ? "栅栏密码 · 两行纸条" : "仿射密码 · 对照关系", V(0, 0.003f, 0.088f), 0.0018f, work.uiFont);
            CipherText(root, "Instructions", rails ? "前半段填上行，后半段填下行；按列上下交替读。" : "上行明文 · 下行密文    y = (a × x + b) mod 26", V(0, 0.003f, -0.081f), 0.00125f, work.uiFont);
            var labels = new TextMesh[columns * 2];
            for (int row = 0; row < 2; row++)
            for (int col = 0; col < columns; col++)
            {
                labels[row * columns + col] = CipherText(root, "Cell " + row + " " + col, "", V(start + (col + 0.5f) * pitch, 0.003f, 0.02f - row * 0.04f), rails ? 0.0015f : 0.0019f, documentFont);
                var cell = CipherHit(root, "Write cell " + row + " " + col, CipherWorkbenchTarget.Kind.Cell,
                    V(start + (col + 0.5f) * pitch, 0.004f, 0.02f - row * 0.04f), V(pitch * 0.98f, 0.006f, 0.039f));
                cell.index = row * columns + col;
                if (row == 0 && (!rails || col % 5 == 0 || col == 40)) CipherText(root, "Index " + col, (rails ? col + 1 : col).ToString(), V(start + (col + 0.5f) * pitch, 0.003f, 0.053f), 0.0009f, documentFont);
            }
            if (rails) work.railLetters = labels; else work.affineLetters = labels;
        }

        static Transform Child(Transform parent, string name, Vector3 position)
        {
            var child = new GameObject(name).transform; child.SetParent(parent, false); child.localPosition = position; return child;
        }
        static CipherWorkbenchTarget CipherHit(Transform parent, string name, CipherWorkbenchTarget.Kind kind, Vector3 center, Vector3 size)
        {
            var target = Child(parent, name, Vector3.zero).gameObject.AddComponent<CipherWorkbenchTarget>();
            target.kind = kind;
            var collider = target.GetComponent<BoxCollider>(); collider.center = center; collider.size = size;
            return target;
        }
        static void CipherRule(LowPolyMesh mesh, Vector3 center, float width, float depth, Material material)
        {
            mesh.Quad(center + V(-width / 2, 0, -depth / 2), center + V(width / 2, 0, -depth / 2),
                center + V(width / 2, 0, depth / 2), center + V(-width / 2, 0, depth / 2), material, Vector3.up);
        }
        static TextMesh CipherText(Transform parent, string name, string value, Vector3 position, float size, Font font)
        {
            PaperText(parent, name, value, position, 0.1f, font);
            var text = parent.Find(name).GetComponent<TextMesh>(); text.characterSize = size;
            text.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return text;
        }
        static SceneObject AddCipherAction(GameObject target, CipherWorkbench work, CipherWorkbenchAction.ActionKind action, Vector3 center, Vector3 size)
        {
            var item = target.GetComponent<SceneObject>(); if (!item) item = target.AddComponent<SceneObject>();
            item.hoverDistance = 0;
            var collider = target.GetComponent<BoxCollider>(); collider.center = center; collider.size = size;
            var handler = target.GetComponent<CipherWorkbenchAction>(); if (!handler) handler = target.AddComponent<CipherWorkbenchAction>();
            handler.workbench = work; handler.action = action;
            return item;
        }
        static void MoveWritingProp(string name, Vector3 position, float yaw)
        {
            var prop = GameObject.Find(name); if (!prop) return;
            prop.transform.position = position; prop.transform.rotation = Quaternion.Euler(0, yaw, 0);
        }
    }
}
