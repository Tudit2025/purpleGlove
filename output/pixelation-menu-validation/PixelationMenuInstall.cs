using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PurpleGlove;
using PurpleGlove.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class PixelationMenuInstall
{
    [Serializable] sealed class Delta { public long[] changed, added; }
    static long Id(UnityEngine.Object obj) => (long)GlobalObjectId.GetGlobalObjectIdSlow(obj).targetObjectId;
    public static void Install()
    {
        if (!Path.GetFullPath(".").Replace('\\', '/').EndsWith("/Temp/guild-letter-layout"))
            throw new Exception("Use the isolated validation project.");
        PlayerSettings.companyName = "PurpleGloveValidation";
        PlayerSettings.productName = "PixelationMenu";
        PlayerPrefs.DeleteKey("PurpleGlove.Pixelation");
        var scene = EditorSceneManager.OpenScene(MainMenuController.ScenePath);
        var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        if (menu.pixelationButton) throw new Exception("Pixelation control already installed.");
        var settings = menu.settingsPanel.GetComponent<RectTransform>();
        var fullscreen = menu.fullscreenButton.GetComponent<RectTransform>();
        var back = menu.backButton.GetComponent<RectTransform>();
        var changed = new UnityEngine.Object[] { menu, settings, fullscreen, menu.fullscreenLabel.rectTransform, back, menu.backLabel.rectTransform };
        typeof(MainMenuBuilder).GetField("chinese", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, menu.chineseFont);
        var arguments = new object[] { "Pixelation", settings, "像素化：开", 196f, 795f, 500f, 50f, null };
        menu.pixelationButton = (Button)typeof(MainMenuBuilder).GetMethod("Button", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, arguments);
        menu.pixelationLabel = (Text)arguments[7];
        menu.pixelationButton.transform.SetSiblingIndex(fullscreen.GetSiblingIndex() + 1);
        fullscreen.anchoredPosition = new Vector2(196, -735); fullscreen.sizeDelta = new Vector2(500, 50);
        back.anchoredPosition = new Vector2(196, -855); back.sizeDelta = new Vector2(500, 50);
        menu.fullscreenLabel.rectTransform.sizeDelta = menu.backLabel.rectTransform.sizeDelta = new Vector2(488, 46);
        foreach (var obj in changed) EditorUtility.SetDirty(obj);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        var added = menu.pixelationButton.GetComponentsInChildren<Transform>(true)
            .SelectMany(t => t.GetComponents<Component>().Cast<UnityEngine.Object>().Append(t.gameObject)).Select(Id).ToArray();
        Directory.CreateDirectory("../../output/pixelation-menu-validation");
        File.WriteAllText("../../output/pixelation-menu-validation/scene-delta.json", JsonUtility.ToJson(new Delta { changed = changed.Select(Id).ToArray(), added = added }, true));
    }
}
