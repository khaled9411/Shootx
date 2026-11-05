#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Editor utility to construct and wire the Dominant Hand setting UI inside the Solo Settings popup
/// in Assets/Scenes/Main.unity, matching the Cyberpunk RPG GUI style.
/// </summary>
public static class SettingsDominantHandSetupEditor
{
    private const string MenuPath = "Tools/Settings/";
    private const string MainScenePath = "Assets/Scenes/Main.unity";

    [InitializeOnLoadMethod]
    private static void AutoSetupOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            // Ensure MobileControlsHUD prefab is updated with new layout references
            MobileControlsSetupEditor.SetupMobileControlsPrefab();

            // Ensure Main.unity has Dominant Hand row
            EnsureDominantHandRowInScene();
        };
    }

    [MenuItem(MenuPath + "Setup Dominant Hand Setting in Main Scene", false, 20)]
    public static void SetupDominantHandUI()
    {
        bool success = EnsureDominantHandRowInScene();
        if (success)
        {
            EditorUtility.DisplayDialog("Settings Setup",
                "Dominant Hand UI has been successfully configured in Assets/Scenes/Main.unity!\n\n" +
                "Label: DOMINANT HAND\n" +
                "Options: Left-handed / Right-handed (Segmented Buttons)\n" +
                "Default: Right-handed\n" +
                "Saved key: settings_dominant_hand",
                "OK");
        }
    }

    public static bool EnsureDominantHandRowInScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;

        var currentScene = EditorSceneManager.GetActiveScene();
        bool needRevertScene = false;
        string originalScenePath = currentScene.path;

        if (currentScene.path != MainScenePath)
        {
            if (File.Exists(MainScenePath))
            {
                currentScene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
                needRevertScene = true;
            }
            else
            {
                Debug.LogWarning($"[SettingsSetup] Scene not found at {MainScenePath}");
                return false;
            }
        }

        SettingsPopupController controller = Object.FindAnyObjectByType<SettingsPopupController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            Debug.LogWarning("[SettingsSetup] SettingsPopupController not found in Main.unity");
            return false;
        }

        Transform settingsTransform = controller.transform;
        Transform existingRow = settingsTransform.Find("DominantHandRow");

        bool wasCreated = false;
        if (existingRow == null)
        {
            existingRow = CreateDominantHandRow(settingsTransform, controller);
            wasCreated = true;
        }
        else
        {
            WireControllerFields(controller, existingRow);
        }

        if (wasCreated || controller != null)
        {
            EditorUtility.SetDirty(controller.gameObject);
            EditorSceneManager.MarkSceneDirty(currentScene);
            EditorSceneManager.SaveScene(currentScene);
            Debug.Log("[SettingsSetup] Successfully ensured DominantHandRow in Main.unity!");
        }

        if (needRevertScene && !string.IsNullOrEmpty(originalScenePath) && originalScenePath != MainScenePath)
        {
            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
        }

        return true;
    }

    private static Transform CreateDominantHandRow(Transform parent, SettingsPopupController controller)
    {
        // 1. DominantHandRow root GameObject
        GameObject rowGO = new GameObject("DominantHandRow", typeof(RectTransform));
        rowGO.transform.SetParent(parent, false);
        rowGO.layer = LayerMask.NameToLayer("UI");

        RectTransform rowRT = rowGO.GetComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0.09374093f, 0.455f);
        rowRT.anchorMax = new Vector2(0.9250001f, 0.520f);
        rowRT.pivot = new Vector2(0.5f, 0.5f);
        rowRT.anchoredPosition = new Vector2(-0.72f, -2f);
        rowRT.sizeDelta = new Vector2(0.82f, 0f);
        rowRT.localScale = Vector3.one;

        // Try getting reference font and sprite from existing rows
        TMP_FontAsset refFont = null;
        Material refMat = null;
        Sprite btnSprite = null;

        Transform musicRow = parent.Find("MusicRow") ?? parent.Find("SoundsRow") ?? parent.Find("HapticRow");
        if (musicRow != null)
        {
            TextMeshProUGUI refTMP = musicRow.GetComponentInChildren<TextMeshProUGUI>();
            if (refTMP != null)
            {
                refFont = refTMP.font;
                refMat = refTMP.fontSharedMaterial;
            }
        }

        Transform supportBtnT = parent.Find("SupportButton") ?? parent.Find("RestorePurchasesButton");
        if (supportBtnT != null)
        {
            Image supImg = supportBtnT.GetComponent<Image>();
            if (supImg != null) btnSprite = supImg.sprite;
        }

        // 2. Row Label ("DOMINANT HAND")
        GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(rowGO.transform, false);
        labelGO.layer = LayerMask.NameToLayer("UI");

        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0.05f, 0.1f);
        labelRT.anchorMax = new Vector2(0.42f, 0.9f);
        labelRT.pivot = new Vector2(0f, 0.5f);
        labelRT.anchoredPosition = Vector2.zero;
        labelRT.sizeDelta = Vector2.zero;

        TextMeshProUGUI labelTMP = labelGO.GetComponent<TextMeshProUGUI>();
        labelTMP.text = "DOMINANT HAND";
        if (refFont != null)
        {
            labelTMP.font = refFont;
            labelTMP.fontSharedMaterial = refMat;
        }
        labelTMP.fontSize = 30f;
        labelTMP.enableAutoSizing = true;
        labelTMP.fontSizeMin = 20f;
        labelTMP.fontSizeMax = 32f;
        labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
        labelTMP.color = Color.white;
        labelTMP.raycastTarget = false;

        // 3. SegmentedContainer
        GameObject segGO = new GameObject("SegmentedContainer", typeof(RectTransform));
        segGO.transform.SetParent(rowGO.transform, false);
        segGO.layer = LayerMask.NameToLayer("UI");

        RectTransform segRT = segGO.GetComponent<RectTransform>();
        segRT.anchorMin = new Vector2(0.44f, 0.08f);
        segRT.anchorMax = new Vector2(0.96f, 0.92f);
        segRT.pivot = new Vector2(0.5f, 0.5f);
        segRT.anchoredPosition = Vector2.zero;
        segRT.sizeDelta = Vector2.zero;

        // 3a. Left-handed Button
        GameObject leftBtnGO = new GameObject("LeftHandButton", typeof(RectTransform), typeof(Image), typeof(Button));
        leftBtnGO.transform.SetParent(segGO.transform, false);
        leftBtnGO.layer = LayerMask.NameToLayer("UI");

        RectTransform leftBtnRT = leftBtnGO.GetComponent<RectTransform>();
        leftBtnRT.anchorMin = new Vector2(0f, 0f);
        leftBtnRT.anchorMax = new Vector2(0.485f, 1f);
        leftBtnRT.pivot = new Vector2(0.5f, 0.5f);
        leftBtnRT.anchoredPosition = Vector2.zero;
        leftBtnRT.sizeDelta = Vector2.zero;

        Image leftImg = leftBtnGO.GetComponent<Image>();
        leftImg.sprite = btnSprite;
        leftImg.type = btnSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        leftImg.color = new Color(0.08f, 0.12f, 0.18f, 0.75f); // Inactive color

        Button leftBtn = leftBtnGO.GetComponent<Button>();
        ColorBlock cbLeft = leftBtn.colors;
        cbLeft.normalColor = Color.white;
        cbLeft.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        cbLeft.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        leftBtn.colors = cbLeft;

        GameObject leftTxtGO = new GameObject("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI));
        leftTxtGO.transform.SetParent(leftBtnGO.transform, false);
        leftTxtGO.layer = LayerMask.NameToLayer("UI");

        RectTransform leftTxtRT = leftTxtGO.GetComponent<RectTransform>();
        leftTxtRT.anchorMin = Vector2.zero;
        leftTxtRT.anchorMax = Vector2.one;
        leftTxtRT.offsetMin = Vector2.zero;
        leftTxtRT.offsetMax = Vector2.zero;

        TextMeshProUGUI leftTMP = leftTxtGO.GetComponent<TextMeshProUGUI>();
        leftTMP.text = "Left-handed";
        if (refFont != null)
        {
            leftTMP.font = refFont;
            leftTMP.fontSharedMaterial = refMat;
        }
        leftTMP.fontSize = 22f;
        leftTMP.enableAutoSizing = true;
        leftTMP.fontSizeMin = 16f;
        leftTMP.fontSizeMax = 24f;
        leftTMP.alignment = TextAlignmentOptions.Center;
        leftTMP.color = new Color(0.65f, 0.75f, 0.85f, 0.65f); // Inactive text
        leftTMP.raycastTarget = false;

        // 3b. Right-handed Button
        GameObject rightBtnGO = new GameObject("RightHandButton", typeof(RectTransform), typeof(Image), typeof(Button));
        rightBtnGO.transform.SetParent(segGO.transform, false);
        rightBtnGO.layer = LayerMask.NameToLayer("UI");

        RectTransform rightBtnRT = rightBtnGO.GetComponent<RectTransform>();
        rightBtnRT.anchorMin = new Vector2(0.515f, 0f);
        rightBtnRT.anchorMax = new Vector2(1f, 1f);
        rightBtnRT.pivot = new Vector2(0.5f, 0.5f);
        rightBtnRT.anchoredPosition = Vector2.zero;
        rightBtnRT.sizeDelta = Vector2.zero;

        Image rightImg = rightBtnGO.GetComponent<Image>();
        rightImg.sprite = btnSprite;
        rightImg.type = btnSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        rightImg.color = new Color(0.12f, 0.75f, 0.95f, 0.95f); // Active color

        Button rightBtn = rightBtnGO.GetComponent<Button>();
        ColorBlock cbRight = rightBtn.colors;
        cbRight.normalColor = Color.white;
        cbRight.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        cbRight.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        rightBtn.colors = cbRight;

        GameObject rightTxtGO = new GameObject("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI));
        rightTxtGO.transform.SetParent(rightBtnGO.transform, false);
        rightTxtGO.layer = LayerMask.NameToLayer("UI");

        RectTransform rightTxtRT = rightTxtGO.GetComponent<RectTransform>();
        rightTxtRT.anchorMin = Vector2.zero;
        rightTxtRT.anchorMax = Vector2.one;
        rightTxtRT.offsetMin = Vector2.zero;
        rightTxtRT.offsetMax = Vector2.zero;

        TextMeshProUGUI rightTMP = rightTxtGO.GetComponent<TextMeshProUGUI>();
        rightTMP.text = "Right-handed";
        if (refFont != null)
        {
            rightTMP.font = refFont;
            rightTMP.fontSharedMaterial = refMat;
        }
        rightTMP.fontSize = 22f;
        rightTMP.enableAutoSizing = true;
        rightTMP.fontSizeMin = 16f;
        rightTMP.fontSizeMax = 24f;
        rightTMP.alignment = TextAlignmentOptions.Center;
        rightTMP.color = Color.white; // Active text
        rightTMP.raycastTarget = false;

        WireControllerFields(controller, rowGO.transform);

        return rowGO.transform;
    }

    private static void WireControllerFields(SettingsPopupController controller, Transform rowTransform)
    {
        Button rightBtn = rowTransform.Find("SegmentedContainer/RightHandButton")?.GetComponent<Button>();
        Button leftBtn = rowTransform.Find("SegmentedContainer/LeftHandButton")?.GetComponent<Button>();
        Image rightImg = rightBtn?.GetComponent<Image>();
        Image leftImg = leftBtn?.GetComponent<Image>();
        TextMeshProUGUI rightTMP = rightBtn?.GetComponentInChildren<TextMeshProUGUI>();
        TextMeshProUGUI leftTMP = leftBtn?.GetComponentInChildren<TextMeshProUGUI>();

        SerializedObject controllerSO = new SerializedObject(controller);
        if (rightBtn != null) controllerSO.FindProperty("rightHandButton").objectReferenceValue = rightBtn;
        if (leftBtn != null) controllerSO.FindProperty("leftHandButton").objectReferenceValue = leftBtn;
        if (rightImg != null) controllerSO.FindProperty("rightHandBackground").objectReferenceValue = rightImg;
        if (leftImg != null) controllerSO.FindProperty("leftHandBackground").objectReferenceValue = leftImg;
        if (rightTMP != null) controllerSO.FindProperty("rightHandText").objectReferenceValue = rightTMP;
        if (leftTMP != null) controllerSO.FindProperty("leftHandText").objectReferenceValue = leftTMP;
        controllerSO.ApplyModifiedProperties();
    }
}
#endif

