#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

/// <summary>
/// Editor utility to construct and wire the Mobile Controls UI hierarchy,
/// generate the MobileControlsHUD prefab, and inject it into the MainMultiplayer scene.
/// </summary>
public static class MobileControlsSetupEditor
{
    private const string MenuPath = "Tools/Mobile Controls/";
    private const string PrefabPath = "Assets/_Multiplayer/Prefabs/MobileControlsHUD.prefab";
    private const string ScenePath = "Assets/_Multiplayer/Scenes/MainMultiplayer.unity";
    private const string InputReaderPath = "Assets/_Multiplayer/Inputs/SO/InputReader.asset";

    [InitializeOnLoadMethod]
    private static void AutoSetupOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(PrefabPath))
            {
                Debug.Log("[MobileControls] Prefab missing. Generating MobileControlsHUD prefab...");
                SetupMobileControlsPrefab();
            }

            EnsureSceneHasMobileControls();
        };
    }

    [MenuItem(MenuPath + "Build Mobile Controls Prefab & Setup Scene", false, 10)]
    public static void SetupAll()
    {
        GameObject prefab = SetupMobileControlsPrefab();
        EnsureSceneHasMobileControls();
        EditorUtility.DisplayDialog("Mobile Controls Setup",
            "Mobile Controls UI prefab and MainMultiplayer scene have been configured successfully!\n\n" +
            "Left Joystick: Movement\n" +
            "Right Joystick: Aim & Release-to-Fire (Hysteresis: 0.90 Armed / 0.75 Disarm)\n" +
            "Melee Button: Melee attack\n" +
            "Desktop controls remain 100% functional.",
            "OK");
    }

    [MenuItem(MenuPath + "Generate Prefab Only", false, 11)]
    public static GameObject SetupMobileControlsPrefab()
    {
        InputReader inputReader = AssetDatabase.LoadAssetAtPath<InputReader>(InputReaderPath);
        if (inputReader == null)
        {
            Debug.LogError($"[MobileControls] Could not find InputReader at: {InputReaderPath}");
            return null;
        }

        // Sprites
        Sprite circleBgSprite = LoadSprite("Assets/Cyberpunk RPG GUI Pack/Cyberpunk RPG GUI Resources/SlicedElements/22Components3/bg_RangeCircle.png");
        Sprite handleTargetSprite = LoadSprite("Assets/Cyberpunk RPG GUI Pack/Cyberpunk RPG GUI Resources/SlicedElements/22Components3/Icon_target.png");
        Sprite handleAimSprite = LoadSprite("Assets/Cyberpunk RPG GUI Pack/Cyberpunk RPG GUI Resources/SlicedElements/22Components3/Icon_Front sight1.png");
        Sprite meleeBtnSprite = LoadSprite("Assets/Cyberpunk RPG GUI Pack/Cyberpunk RPG GUI Resources/SlicedElements/22Components3/Btn_Attack0_n.png");
        Sprite meleeIconSprite = LoadSprite("Assets/Cyberpunk RPG GUI Pack/Cyberpunk RPG GUI Resources/SlicedElements/22Components3/Icon_Attacl0.png");

        // 1. Root GameObject
        GameObject rootGO = new GameObject("MobileControlsRoot", typeof(RectTransform), typeof(CanvasGroup), typeof(MobileControlsHUD));
        RectTransform rootRT = rootGO.GetComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        MobileControlsHUD hud = rootGO.GetComponent<MobileControlsHUD>();
        CanvasGroup canvasGroup = rootGO.GetComponent<CanvasGroup>();

        // 2. SafeAreaRoot
        GameObject safeAreaGO = new GameObject("SafeAreaRoot", typeof(RectTransform), typeof(SafeArea));
        safeAreaGO.transform.SetParent(rootGO.transform, false);
        RectTransform safeAreaRT = safeAreaGO.GetComponent<RectTransform>();
        safeAreaRT.anchorMin = Vector2.zero;
        safeAreaRT.anchorMax = Vector2.one;
        safeAreaRT.offsetMin = Vector2.zero;
        safeAreaRT.offsetMax = Vector2.zero;

        // 3. Left Movement Joystick
        GameObject moveJoyGO = new GameObject("MovementJoystick", typeof(RectTransform), typeof(FixedJoystick));
        moveJoyGO.transform.SetParent(safeAreaGO.transform, false);
        RectTransform moveJoyRT = moveJoyGO.GetComponent<RectTransform>();
        moveJoyRT.anchorMin = new Vector2(0f, 0f);
        moveJoyRT.anchorMax = new Vector2(0f, 0f);
        moveJoyRT.pivot = new Vector2(0.5f, 0.5f);
        moveJoyRT.anchoredPosition = new Vector2(180f, 180f);
        moveJoyRT.sizeDelta = new Vector2(240f, 240f);

        GameObject moveBgGO = new GameObject("JoystickBackground", typeof(RectTransform), typeof(Image));
        moveBgGO.transform.SetParent(moveJoyGO.transform, false);
        RectTransform moveBgRT = moveBgGO.GetComponent<RectTransform>();
        moveBgRT.anchorMin = Vector2.zero;
        moveBgRT.anchorMax = Vector2.one;
        moveBgRT.offsetMin = Vector2.zero;
        moveBgRT.offsetMax = Vector2.zero;
        Image moveBgImg = moveBgGO.GetComponent<Image>();
        moveBgImg.sprite = circleBgSprite;
        moveBgImg.color = new Color(0.12f, 0.18f, 0.28f, 0.65f);
        moveBgImg.raycastTarget = true;

        GameObject moveHandleGO = new GameObject("JoystickHandle", typeof(RectTransform), typeof(Image));
        moveHandleGO.transform.SetParent(moveBgGO.transform, false);
        RectTransform moveHandleRT = moveHandleGO.GetComponent<RectTransform>();
        moveHandleRT.anchorMin = new Vector2(0.5f, 0.5f);
        moveHandleRT.anchorMax = new Vector2(0.5f, 0.5f);
        moveHandleRT.pivot = new Vector2(0.5f, 0.5f);
        moveHandleRT.anchoredPosition = Vector2.zero;
        moveHandleRT.sizeDelta = new Vector2(100f, 100f);
        Image moveHandleImg = moveHandleGO.GetComponent<Image>();
        moveHandleImg.sprite = handleTargetSprite;
        moveHandleImg.color = new Color(0.2f, 0.85f, 1f, 0.85f);
        moveHandleImg.raycastTarget = false;

        FixedJoystick fixedJoy = moveJoyGO.GetComponent<FixedJoystick>();
        var fixedJoySO = new SerializedObject(fixedJoy);
        fixedJoySO.FindProperty("inputReader").objectReferenceValue = inputReader;
        fixedJoySO.FindProperty("backgroundRect").objectReferenceValue = moveBgRT;
        fixedJoySO.FindProperty("handleRect").objectReferenceValue = moveHandleRT;
        fixedJoySO.FindProperty("handleRange").floatValue = 85f;
        fixedJoySO.FindProperty("deadZone").floatValue = 0.1f;
        fixedJoySO.ApplyModifiedProperties();

        // 4. Combat Controls Container (Right Side)
        GameObject combatGO = new GameObject("CombatControls", typeof(RectTransform));
        combatGO.transform.SetParent(safeAreaGO.transform, false);
        RectTransform combatRT = combatGO.GetComponent<RectTransform>();
        combatRT.anchorMin = new Vector2(1f, 0f);
        combatRT.anchorMax = new Vector2(1f, 0f);
        combatRT.pivot = new Vector2(1f, 0f);
        combatRT.anchoredPosition = Vector2.zero;
        combatRT.sizeDelta = new Vector2(400f, 600f);

        // 5. Right Aim & Fire Joystick
        GameObject aimJoyGO = new GameObject("AimFireJoystick", typeof(RectTransform), typeof(AimFireJoystick));
        aimJoyGO.transform.SetParent(combatGO.transform, false);
        RectTransform aimJoyRT = aimJoyGO.GetComponent<RectTransform>();
        aimJoyRT.anchorMin = new Vector2(1f, 0f);
        aimJoyRT.anchorMax = new Vector2(1f, 0f);
        aimJoyRT.pivot = new Vector2(0.5f, 0.5f);
        aimJoyRT.anchoredPosition = new Vector2(-180f, 180f);
        aimJoyRT.sizeDelta = new Vector2(240f, 240f);

        GameObject aimBgGO = new GameObject("JoystickBackground", typeof(RectTransform), typeof(Image));
        aimBgGO.transform.SetParent(aimJoyGO.transform, false);
        RectTransform aimBgRT = aimBgGO.GetComponent<RectTransform>();
        aimBgRT.anchorMin = Vector2.zero;
        aimBgRT.anchorMax = Vector2.one;
        aimBgRT.offsetMin = Vector2.zero;
        aimBgRT.offsetMax = Vector2.zero;
        Image aimBgImg = aimBgGO.GetComponent<Image>();
        aimBgImg.sprite = circleBgSprite;
        aimBgImg.color = new Color(0.12f, 0.18f, 0.28f, 0.65f);
        aimBgImg.raycastTarget = true;

        GameObject armedIndGO = new GameObject("ArmedIndicatorRing", typeof(RectTransform), typeof(Image));
        armedIndGO.transform.SetParent(aimBgGO.transform, false);
        RectTransform armedIndRT = armedIndGO.GetComponent<RectTransform>();
        armedIndRT.anchorMin = new Vector2(0.5f, 0.5f);
        armedIndRT.anchorMax = new Vector2(0.5f, 0.5f);
        armedIndRT.pivot = new Vector2(0.5f, 0.5f);
        armedIndRT.anchoredPosition = Vector2.zero;
        armedIndRT.sizeDelta = new Vector2(265f, 265f);
        Image armedIndImg = armedIndGO.GetComponent<Image>();
        armedIndImg.sprite = circleBgSprite;
        armedIndImg.color = new Color(1f, 0.35f, 0.15f, 0f);
        armedIndImg.raycastTarget = false;

        GameObject aimHandleGO = new GameObject("JoystickHandle", typeof(RectTransform), typeof(Image));
        aimHandleGO.transform.SetParent(aimBgGO.transform, false);
        RectTransform aimHandleRT = aimHandleGO.GetComponent<RectTransform>();
        aimHandleRT.anchorMin = new Vector2(0.5f, 0.5f);
        aimHandleRT.anchorMax = new Vector2(0.5f, 0.5f);
        aimHandleRT.pivot = new Vector2(0.5f, 0.5f);
        aimHandleRT.anchoredPosition = Vector2.zero;
        aimHandleRT.sizeDelta = new Vector2(105f, 105f);
        Image aimHandleImg = aimHandleGO.GetComponent<Image>();
        aimHandleImg.sprite = handleAimSprite != null ? handleAimSprite : handleTargetSprite;
        aimHandleImg.color = new Color(1f, 1f, 1f, 0.7f);
        aimHandleImg.raycastTarget = false;

        AimFireJoystick aimJoy = aimJoyGO.GetComponent<AimFireJoystick>();
        var aimJoySO = new SerializedObject(aimJoy);
        aimJoySO.FindProperty("inputReader").objectReferenceValue = inputReader;
        aimJoySO.FindProperty("backgroundRect").objectReferenceValue = aimBgRT;
        aimJoySO.FindProperty("handleRect").objectReferenceValue = aimHandleRT;
        aimJoySO.FindProperty("armedIndicatorRect").objectReferenceValue = armedIndRT;
        aimJoySO.FindProperty("handleImage").objectReferenceValue = aimHandleImg;
        aimJoySO.FindProperty("backgroundImage").objectReferenceValue = aimBgImg;
        aimJoySO.FindProperty("armedIndicatorImage").objectReferenceValue = armedIndImg;
        aimJoySO.FindProperty("armingThreshold").floatValue = 0.90f;
        aimJoySO.FindProperty("disarmThreshold").floatValue = 0.75f;
        aimJoySO.FindProperty("deadZone").floatValue = 0.15f;
        aimJoySO.FindProperty("handleRange").floatValue = 90f;
        aimJoySO.ApplyModifiedProperties();

        // 6. Melee Button (Above AimFireJoystick)
        GameObject meleeGO = new GameObject("MeleeButton", typeof(RectTransform), typeof(Image), typeof(MobileMeleeButton));
        meleeGO.transform.SetParent(combatGO.transform, false);
        RectTransform meleeRT = meleeGO.GetComponent<RectTransform>();
        meleeRT.anchorMin = new Vector2(1f, 0f);
        meleeRT.anchorMax = new Vector2(1f, 0f);
        meleeRT.pivot = new Vector2(0.5f, 0.5f);
        meleeRT.anchoredPosition = new Vector2(-180f, 375f);
        meleeRT.sizeDelta = new Vector2(125f, 125f);
        Image meleeBgImg = meleeGO.GetComponent<Image>();
        meleeBgImg.sprite = meleeBtnSprite != null ? meleeBtnSprite : circleBgSprite;
        meleeBgImg.color = new Color(1f, 0.35f, 0.2f, 0.9f);
        meleeBgImg.raycastTarget = true;

        GameObject meleeIconGO = new GameObject("ButtonIcon", typeof(RectTransform), typeof(Image));
        meleeIconGO.transform.SetParent(meleeGO.transform, false);
        RectTransform meleeIconRT = meleeIconGO.GetComponent<RectTransform>();
        meleeIconRT.anchorMin = new Vector2(0.5f, 0.5f);
        meleeIconRT.anchorMax = new Vector2(0.5f, 0.5f);
        meleeIconRT.pivot = new Vector2(0.5f, 0.5f);
        meleeIconRT.anchoredPosition = Vector2.zero;
        meleeIconRT.sizeDelta = new Vector2(80f, 80f);
        Image meleeIconImg = meleeIconGO.GetComponent<Image>();
        meleeIconImg.sprite = meleeIconSprite;
        meleeIconImg.color = Color.white;
        meleeIconImg.raycastTarget = false;

        MobileMeleeButton meleeBtn = meleeGO.GetComponent<MobileMeleeButton>();
        var meleeBtnSO = new SerializedObject(meleeBtn);
        meleeBtnSO.FindProperty("inputReader").objectReferenceValue = inputReader;
        meleeBtnSO.FindProperty("buttonRect").objectReferenceValue = meleeRT;
        meleeBtnSO.FindProperty("buttonImage").objectReferenceValue = meleeBgImg;
        meleeBtnSO.FindProperty("iconImage").objectReferenceValue = meleeIconImg;
        meleeBtnSO.ApplyModifiedProperties();

        // Wire HUD
        var hudSO = new SerializedObject(hud);
        hudSO.FindProperty("inputReader").objectReferenceValue = inputReader;
        hudSO.FindProperty("movementJoystick").objectReferenceValue = fixedJoy;
        hudSO.FindProperty("aimFireJoystick").objectReferenceValue = aimJoy;
        hudSO.FindProperty("meleeButton").objectReferenceValue = meleeBtn;
        hudSO.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
        hudSO.FindProperty("forceMobileInEditor").boolValue = true;
        hudSO.ApplyModifiedProperties();

        // Save as Prefab
        string dir = Path.GetDirectoryName(PrefabPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(rootGO, PrefabPath);
        Object.DestroyImmediate(rootGO);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[MobileControls] Saved prefab to {PrefabPath}");
        return savedPrefab;
    }

    [MenuItem(MenuPath + "Inject Mobile Controls into MainMultiplayer Scene", false, 12)]
    public static void EnsureSceneHasMobileControls()
    {
        bool wasDirty = false;
        var currentScene = EditorSceneManager.GetActiveScene();

        // If not already in MainMultiplayer, open it if necessary
        bool needRevertScene = false;
        string originalScenePath = currentScene.path;

        if (currentScene.path != ScenePath)
        {
            if (File.Exists(ScenePath))
            {
                currentScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                needRevertScene = true;
            }
            else
            {
                return;
            }
        }

        // Check if MobileControlsRoot or MobileControlsHUD already exists in scene
        MobileControlsHUD existingHUD = Object.FindAnyObjectByType<MobileControlsHUD>();
        if (existingHUD == null)
        {
            // Find OverlayCanves (existing Canvas)
            Canvas overlayCanvas = null;
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in canvases)
            {
                if (c.gameObject.name.Contains("Overlay") || c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    overlayCanvas = c;
                    break;
                }
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                prefab = SetupMobileControlsPrefab();
            }

            if (prefab != null)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (overlayCanvas != null)
                {
                    instance.transform.SetParent(overlayCanvas.transform, false);
                }

                RectTransform rt = instance.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                }

                EditorUtility.SetDirty(instance);
                wasDirty = true;
                Debug.Log("[MobileControls] Injected MobileControlsHUD into scene!");
            }
        }

        if (wasDirty)
        {
            EditorSceneManager.MarkSceneDirty(currentScene);
            EditorSceneManager.SaveScene(currentScene);
            Debug.Log("[MobileControls] Saved scene changes to MainMultiplayer.unity!");
        }

        if (needRevertScene && !string.IsNullOrEmpty(originalScenePath) && originalScenePath != ScenePath)
        {
            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
        }
    }

    private static Sprite LoadSprite(string assetPath)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite == null)
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex != null)
            {
                // Ensure texture is imported as sprite
                string path = AssetDatabase.GetAssetPath(tex);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.SaveAndReimport();
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                }
            }
        }
        return sprite;
    }
}
#endif
