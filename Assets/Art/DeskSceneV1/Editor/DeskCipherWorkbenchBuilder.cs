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

            Solid("DecodeLinen", new Color(0.78f, 0.73f, 0.61f), 0, 0.1f);
            Solid("DecodeIvory", new Color(0.95f, 0.87f, 0.67f), 0, 0.16f);
            Solid("DecodeInk", new Color(0.055f, 0.032f, 0.018f), 0, 0);
            foreach (string key in new[] { "DecodeLinen", "DecodeIvory", "DecodeInk" })
            {
                M(key).SetFloat("_OutlineWidth", key == "DecodeInk" ? 0 : 0.45f);
                M(key).SetColor("_InspectionFill", key == "DecodeInk" ? Color.black
                    : key == "DecodeLinen" ? new Color(0.62f, 0.60f, 0.55f) : new Color(0.78f, 0.73f, 0.63f));
            }
            work.mat = Child(root, "DecodingMat", V(0.96f, 0.802f, -0.49f));
            MeshObject("Cipher_LinenMat", work.mat, m =>
            {
                m.Box(V(0, 0.004f, 0), V(0.76f, 0.008f, 0.68f), M("DecodeLinen"), 0.006f);
                foreach (float x in new[] { -0.361f, 0.361f })
                    m.Box(V(x, 0.0085f, 0), V(0.002f, 0.001f, 0.634f), M("Stitch"));
                foreach (float z in new[] { -0.319f, 0.319f })
                    m.Box(V(0, 0.0085f, z), V(0.724f, 0.001f, 0.002f), M("Stitch"));
            });
            AddCipherAction(Child(work.mat, "Interaction surface", Vector3.zero).gameObject, work, CipherWorkbenchAction.ActionKind.Focus,
                V(0, 0.005f, 0), V(0.76f, 0.01f, 0.68f));
            CipherText(work.mat, "Mat label", "解码工具", V(0, 0.010f, 0.287f), 0.0016f, work.uiFont);
            var slip = Child(work.mat, "Plaintext writing slip", V(0, 0.011f, -0.222f));
            MeshObject("Cipher_PlaintextSlip", slip, m =>
            {
                m.Box(V(0, 0, 0), V(0.60f, 0.002f, 0.165f), M("DecodeIvory"), 0.002f);
                for (int i = 0; i < 5; i++) CipherRule(m, V(0, 0.0018f, 0.025f - i * 0.022f), 0.55f, 0.0008f, M("Stitch"));
            });
            CipherText(slip, "Writing heading", "明文书写处", V(0, 0.002f, 0.062f), 0.0018f, work.uiFont);
            work.plaintextInk = CipherText(slip, "Plaintext ink", "", V(-0.275f, 0.002f, 0.039f), 0.0013f, work.uiFont);
            work.plaintextInk.anchor = TextAnchor.UpperLeft; work.plaintextInk.alignment = TextAlignment.Left;

            MoveWritingProp("InkBottle", V(1.35f, 0.801f, -0.08f), 8);
            MoveWritingProp("SandShaker", V(1.30f, 0.801f, 0.34f), 10);
            MoveWritingProp("Quill", V(1.38f, 0.802f, -0.43f), 90);
            MoveWritingProp("PaperKnife", V(0.73f, 0.802f, -0.08f), 0);

            work.tools = new GameObject[5]; work.storedTools = new GameObject[5];
            for (int i = 0; i < 5; i++) work.tools[i] = Child(work.mat, "Tool " + (CipherWorkbench.Tool)i, V(0, 0.015f, 0.065f)).gameObject;
            BuildCaesarTool(work);
            BuildPigpenTool(work);
            BuildSaintCyrTool(work);
            BuildCipherGrid(work, false);
            BuildCipherGrid(work, true);
            work.toolbox = AddCipherAction(box, work, CipherWorkbenchAction.ActionKind.Toolbox, V(0, 0.10f, 0), V(0.51f, 0.2f, 0.33f));
            work.toolbox.screenFill = 0.46f; work.toolbox.inspectionEuler = new Vector3(-55, 0, 0);
            work.toolbox.inspectionFill = new Color(0.70f, 0.63f, 0.50f);
            work.lid = box.transform.Find("LidPivot"); work.lid.localRotation = Quaternion.identity;
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
                work.storedTools[i] = miniature;
                CipherText(tray, "Slot " + i, (i + 1).ToString(), V(-0.176f + i * 0.088f, 0.008f, -0.105f), 0.0022f, documentFont);
                work.tools[i].SetActive(false);
            }
            CipherText(tray, "Toolbox title", "解密工具盒", V(0, 0.012f, 0.103f), 0.0018f, work.uiFont);
            var view = camera.GetComponent<DeskScenePreview>();
            view.decodeFocusAnchor = work.mat; view.decodeFocusHeight = 1.06f; view.decodeFocusFieldOfView = 45;
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
        }

        static void BuildPigpenTool(CipherWorkbench work)
        {
            var root = work.tools[1].transform;
            MeshObject("Cipher_PigpenCard", root, m =>
            {
                m.Box(Vector3.zero, V(0.57f, 0.004f, 0.29f), M("DecodeIvory"), 0.002f);
                for (int i = 0; i < 26; i++) DrawNotebookGlyph(m, (char)('A' + i), V(-0.252f + i % 13 * 0.042f, 0.004f, 0.055f - i / 13 * 0.12f), 0.012f);
            });
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
            });
            CipherText(root, "Title", rails ? "栅栏密码 · 两行纸条" : "仿射密码 · 对照关系", V(0, 0.003f, 0.088f), 0.0018f, work.uiFont);
            CipherText(root, "Instructions", rails ? "前半段填上行，后半段填下行；按列上下交替读。" : "上行明文 · 下行密文    y = (a × x + b) mod 26", V(0, 0.003f, -0.081f), 0.00125f, work.uiFont);
            var labels = new TextMesh[columns * 2];
            for (int row = 0; row < 2; row++)
            for (int col = 0; col < columns; col++)
            {
                labels[row * columns + col] = CipherText(root, "Cell " + row + " " + col, "", V(start + (col + 0.5f) * pitch, 0.003f, 0.02f - row * 0.04f), rails ? 0.0015f : 0.0019f, documentFont);
                if (row == 0 && (!rails || col % 5 == 0 || col == 40)) CipherText(root, "Index " + col, (rails ? col + 1 : col).ToString(), V(start + (col + 0.5f) * pitch, 0.003f, 0.053f), 0.0009f, documentFont);
            }
            if (rails) work.railLetters = labels; else work.affineLetters = labels;
        }

        static Transform Child(Transform parent, string name, Vector3 position)
        {
            var child = new GameObject(name).transform; child.SetParent(parent, false); child.localPosition = position; return child;
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
