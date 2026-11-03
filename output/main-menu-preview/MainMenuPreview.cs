using System;
using System.IO;
using PurpleGlove;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Composition study only. Run in an isolated project; never saves the scene.
public static class MainMenuPreview
{
    const int Width = 1920, Height = 1080;
    static readonly string Output = Path.GetFullPath("../../output/main-menu-preview");
    static Font chinese, latin;
    static Transform canvasRoot;

    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene("Assets/Art/DeskSceneV1/Scenes/PH_DeskRoom.unity");
            var original = Camera.main;
            var story = original.GetComponent<PrologueSequence>();
            foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>(true)) canvas.enabled = false;
            foreach (var camera in UnityEngine.Object.FindObjectsOfType<Camera>(true)) camera.enabled = false;

            // Existing corridor geometry and door, viewed from its right-hand corner.
            var shot = new GameObject("Main menu composition camera").AddComponent<Camera>();
            shot.CopyFrom(original); shot.enabled = false;
            shot.aspect = Width / (float)Height;
            shot.fieldOfView = 62;
            shot.nearClipPlane = 0.035f;
            shot.cullingMask &= ~(1 << 5);
            Vector3 position = story.doorLeaf.TransformPoint(new Vector3(1.567f, 1.31f, 1.73f));
            Vector3 target = story.doorLeaf.TransformPoint(new Vector3(1.267f, 1.21f, 0.08f));
            shot.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
            var pixel = shot.gameObject.AddComponent<PixelSceneView>();
            pixel.pixelate = true; pixel.verticalResolution = 540;
            if (story.exteriorLight)
            {
                story.exteriorLight.enabled = true;
                story.exteriorLight.transform.position = story.doorLeaf.TransformPoint(new Vector3(0.06f, 1.76f, 0.73f));
                story.exteriorLight.intensity = 0.95f;
                story.exteriorLight.range = 2.4f;
                story.exteriorLight.color = new Color(1f, 0.75f, 0.48f);
            }
            Physics.SyncTransforms();

            chinese = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/DeskSceneV1/Fonts/YinPinZhuSong.ttf");
            latin = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/DeskSceneV1/Fonts/EBGaramond-Medium.otf");
            if (!chinese || !latin) throw new InvalidOperationException("Missing project fonts.");
            var ui = new GameObject("Menu layout camera").AddComponent<Camera>();
            ui.enabled = false; ui.clearFlags = CameraClearFlags.Depth; ui.cullingMask = 1 << 5;
            ui.orthographic = true; ui.orthographicSize = Height * 0.5f;
            ui.nearClipPlane = 0.01f; ui.farClipPlane = 100; ui.aspect = shot.aspect;
            ui.transform.position = new Vector3(0, 0, -20);
            var canvasObject = new GameObject("Menu layout", typeof(RectTransform), typeof(Canvas));
            canvasObject.layer = 5; canvasRoot = canvasObject.transform;
            var canvasUi = canvasObject.GetComponent<Canvas>();
            canvasUi.renderMode = RenderMode.ScreenSpaceCamera; canvasUi.worldCamera = ui;
            canvasUi.planeDistance = 1; canvasUi.pixelPerfect = true;

            var cream = new Color(0.86f, 0.79f, 0.65f);
            var quiet = new Color(0.55f, 0.49f, 0.40f);
            Label("English title", "T H E   P U R P L E   G L O V E", 194, 242, 600, 42, 26, quiet, latin);
            Label("Title", "紫手套", 180, 302, 630, 225, 176, cream, chinese);
            Line("Title rule", 196, 526, 440, 1, new Color(0.51f, 0.43f, 0.33f, 0.45f));
            Line("Purple accent", 196, 523, 44, 5, new Color(0.42f, 0.27f, 0.40f));
            Label("Start", "开始游戏", 208, 592, 340, 58, 34, cream, chinese);
            Line("Selected marker", 181, 616, 8, 8, cream);
            Label("Settings", "设置", 208, 674, 260, 52, 29, quiet, chinese);
            Label("Quit", "退出", 208, 746, 260, 52, 29, quiet, chinese);
            Label("Language", "中文  /  ENGLISH", 195, 969, 400, 36, 20, quiet, chinese);

            var texture = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                shot.targetTexture = texture; ui.targetTexture = texture;
                canvasUi.enabled = false;
                shot.Render(); Save(texture, "corridor-background.png");
                canvasUi.enabled = true; Canvas.ForceUpdateCanvases();
                shot.Render(); ui.Render(); Save(texture, "corridor-menu.png");
                File.WriteAllText(Path.Combine(Output, "camera.txt"),
                    $"Existing PH_DeskRoom scene; unsaved composition study.\nCamera position: {position:F4}\n"
                    + $"Look at: {target:F4}\nEuler: {shot.transform.eulerAngles:F4}\nVertical FOV: {shot.fieldOfView}\n"
                    + "Resolution: 1920 x 1080; scene sampled at 960 x 540; native-resolution title/menu.\n"
                    + "Title: project YinPinZhuSong; English: EB Garamond.\n"
                    + "Existing prologue light repositioned and dimmed for this preview only.\n");
            }
            finally
            {
                shot.targetTexture = ui.targetTexture = null;
                RenderTexture.active = previous; RenderTexture.ReleaseTemporary(texture);
            }
            Debug.Log("MAIN_MENU_PREVIEW_PASS"); EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    static RectTransform Rect(string name, float x, float y, float width, float height, params Type[] components)
    {
        var go = new GameObject(name, components); go.layer = 5;
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(canvasRoot, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        return rect;
    }
    static void Label(string name, string value, float x, float y, float width, float height, int size, Color color, Font font)
    {
        var rect = Rect(name, x, y, width, height, typeof(RectTransform), typeof(Text));
        var text = rect.GetComponent<Text>(); text.font = font; text.text = value; text.fontSize = size;
        text.color = color; text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
    }
    static void Line(string name, float x, float y, float width, float height, Color color)
    {
        var rect = Rect(name, x, y, width, height, typeof(RectTransform), typeof(Image));
        rect.GetComponent<Image>().color = color; rect.GetComponent<Image>().raycastTarget = false;
    }
    static void Save(RenderTexture source, string name)
    {
        RenderTexture.active = source;
        var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(Output, name), image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
    }
}
