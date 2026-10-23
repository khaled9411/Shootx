#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

/// <summary>
/// Automated Editor setup utility for the Complete Multiplayer Shooter Upgrade.
/// Creates the LaserBeamVisual prefab, AmmoPickup networked prefab, registers it in DefaultNetworkPrefabs,
/// configures the Player prefab with PlayerIdentity and PlayerWorldStatusUI (strictly below character),
/// and adds AmmoPickupSpawner into the MainMultiplayer scene.
/// 
/// Run via Unity menu: Tools -> Multiplayer -> Complete Hitscan Shooter Setup
/// or in batchmode via HitscanShooterSetupEditor.SetupAllNoDialog
/// </summary>
public static class HitscanShooterSetupEditor
{
    private const string MenuPath = "Tools/Multiplayer/";

    [InitializeOnLoadMethod]
    private static void AutoSetupOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            string prefabPath = "Assets/_Multiplayer/Prefabs/AmmoPickup.prefab";
            if (!File.Exists(prefabPath))
            {
                Debug.Log("[HitscanSetup] AmmoPickup missing. Automatically running HitscanShooterSetup...");
                SetupAllNoDialog();
            }
        };
    }

    [MenuItem(MenuPath + "Complete Hitscan Shooter Setup", false, 200)]
    public static void SetupAllMenu()
    {
        if (!EditorUtility.DisplayDialog("Hitscan Shooter Setup",
            "This will create/configure the Hitscan Laser, Ammo Pickups, Player World-Space UI (below character), and Scene Spawner.\n\nProceed?",
            "Yes", "Cancel"))
            return;

        SetupAllNoDialog();

        EditorUtility.DisplayDialog("Setup Complete",
            "Hitscan Shooter upgrade setup complete!\n\nCheck HITSCAN_SHOOTER_SETUP_GUIDE_AR.md for testing and configuration details.",
            "OK");
    }

    public static void SetupAllNoDialog()
    {
        Debug.Log("[HitscanSetup] Starting automated setup...");

        // Ensure directories exist
        EnsureDirectory("Assets/_Multiplayer/Prefabs");
        EnsureDirectory("Assets/_Multiplayer/Players/Weapons/Materials");
        EnsureDirectory("Assets/_Multiplayer/Players/Weapons/Projectiles");

        // 1. Create LaserBeamVisual Material & Prefab
        GameObject laserPrefab = CreateLaserBeamPrefab();

        // 2. Create AmmoPickup Material & Prefab
        GameObject pickupPrefab = CreateAmmoPickupPrefab();

        // 3. Register AmmoPickup in DefaultNetworkPrefabs
        RegisterInNetworkPrefabsList(pickupPrefab);

        // 4. Configure Player.prefab
        SetupPlayerPrefab(laserPrefab);

        // 5. Configure MainMultiplayer.unity scene
        SetupMainMultiplayerScene(pickupPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[HitscanSetup] Automated setup finished successfully!");
    }

    private static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 1. Laser Beam Prefab
    // ═══════════════════════════════════════════════════════════

    public static GameObject CreateLaserBeamPrefab()
    {
        string matPath = "Assets/_Multiplayer/Players/Weapons/Materials/LaserBeamMaterial.mat";
        string prefabPath = "Assets/_Multiplayer/Players/Weapons/Projectiles/LaserBeamVisual.prefab";

        // Create laser material
        Material laserMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (laserMat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            laserMat = new Material(shader);
            laserMat.name = "LaserBeamMaterial";
            Color cyanHdr = new Color(0f, 0.9f, 1f, 1f);
            if (laserMat.HasProperty("_BaseColor")) laserMat.SetColor("_BaseColor", cyanHdr);
            if (laserMat.HasProperty("_Color")) laserMat.SetColor("_Color", cyanHdr);

            AssetDatabase.CreateAsset(laserMat, matPath);
        }

        GameObject go = new GameObject("LaserBeamVisual");
        var lr = go.AddComponent<LineRenderer>();
        lr.material = laserMat;
        lr.useWorldSpace = true;
        lr.startWidth = 0.08f;
        lr.endWidth = 0.08f;
        lr.startColor = new Color(0f, 0.9f, 1f, 1f);
        lr.endColor = new Color(0f, 0.9f, 1f, 1f);
        lr.alignment = LineAlignment.View;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        var laserVisual = go.AddComponent<LaserBeamVisual>();
        var laserSO = new SerializedObject(laserVisual);
        var lrProp = laserSO.FindProperty("lineRenderer");
        if (lrProp != null) lrProp.objectReferenceValue = lr;
        laserSO.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);

        Debug.Log($"[HitscanSetup] Saved LaserBeamVisual prefab at: {prefabPath}");
        return prefab;
    }

    // ═══════════════════════════════════════════════════════════
    // 2. Ammo Pickup Prefab
    // ═══════════════════════════════════════════════════════════

    public static GameObject CreateAmmoPickupPrefab()
    {
        string matPath = "Assets/_Multiplayer/Players/Weapons/Materials/AmmoPickupMaterial.mat";
        string prefabPath = "Assets/_Multiplayer/Prefabs/AmmoPickup.prefab";

        // Create ammo material
        Material ammoMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (ammoMat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Standard");

            ammoMat = new Material(shader);
            ammoMat.name = "AmmoPickupMaterial";
            Color amberColor = new Color(1f, 0.72f, 0.1f, 1f);
            if (ammoMat.HasProperty("_BaseColor")) ammoMat.SetColor("_BaseColor", amberColor);
            if (ammoMat.HasProperty("_Color")) ammoMat.SetColor("_Color", amberColor);
            if (ammoMat.HasProperty("_EmissionColor"))
            {
                ammoMat.EnableKeyword("_EMISSION");
                ammoMat.SetColor("_EmissionColor", amberColor * 1.5f);
            }

            AssetDatabase.CreateAsset(ammoMat, matPath);
        }

        // Create root
        GameObject rootGO = new GameObject("AmmoPickup");
        rootGO.tag = "Untagged";

        // NGO NetworkObject
        var netObj = rootGO.AddComponent<NetworkObject>();
        netObj.AlwaysReplicateAsRoot = true;

        // Trigger Collider
        var col = rootGO.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.55f;
        col.center = new Vector3(0f, 0.35f, 0f);

        // AmmoPickup component
        rootGO.AddComponent<AmmoPickup>();

        // Child 3D visual mesh (styled as an ammunition box/cell)
        GameObject visualGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visualGO.name = "Visual";
        visualGO.transform.SetParent(rootGO.transform, false);
        visualGO.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        visualGO.transform.localScale = new Vector3(0.45f, 0.35f, 0.45f);

        // Remove primitive's collider so only the root trigger collider is active
        var cubeCol = visualGO.GetComponent<Collider>();
        if (cubeCol != null) Object.DestroyImmediate(cubeCol);

        var mr = visualGO.GetComponent<MeshRenderer>();
        if (mr != null) mr.sharedMaterial = ammoMat;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(rootGO, prefabPath);
        Object.DestroyImmediate(rootGO);

        Debug.Log($"[HitscanSetup] Saved AmmoPickup prefab at: {prefabPath}");
        return prefab;
    }

    // ═══════════════════════════════════════════════════════════
    // 3. Register in DefaultNetworkPrefabs
    // ═══════════════════════════════════════════════════════════

    public static void RegisterInNetworkPrefabsList(GameObject pickupPrefab)
    {
        string listPath = "Assets/DefaultNetworkPrefabs.asset";
        var prefabsList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(listPath);
        if (prefabsList == null || pickupPrefab == null)
        {
            Debug.LogWarning($"[HitscanSetup] Could not load NetworkPrefabsList at {listPath}");
            return;
        }

        SerializedObject so = new SerializedObject(prefabsList);
        SerializedProperty listProp = so.FindProperty("List");
        if (listProp == null) return;

        // Check if already registered
        bool alreadyRegistered = false;
        for (int i = 0; i < listProp.arraySize; i++)
        {
            SerializedProperty elem = listProp.GetArrayElementAtIndex(i);
            SerializedProperty prefabProp = elem.FindPropertyRelative("Prefab");
            if (prefabProp != null && prefabProp.objectReferenceValue == pickupPrefab)
            {
                alreadyRegistered = true;
                break;
            }
        }

        if (!alreadyRegistered)
        {
            int index = listProp.arraySize;
            listProp.InsertArrayElementAtIndex(index);
            SerializedProperty newElem = listProp.GetArrayElementAtIndex(index);
            SerializedProperty prefabProp = newElem.FindPropertyRelative("Prefab");
            if (prefabProp != null) prefabProp.objectReferenceValue = pickupPrefab;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(prefabsList);
            Debug.Log($"[HitscanSetup] Registered {pickupPrefab.name} in {listPath}");
        }
        else
        {
            Debug.Log($"[HitscanSetup] {pickupPrefab.name} is already registered in {listPath}");
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 4. Setup Player.prefab
    // ═══════════════════════════════════════════════════════════

    public static void SetupPlayerPrefab(GameObject laserPrefab)
    {
        string playerPath = "Assets/_Multiplayer/Players/Prefabs/Player.prefab";
        GameObject playerRoot = PrefabUtility.LoadPrefabContents(playerPath);
        if (playerRoot == null)
        {
            Debug.LogError($"[HitscanSetup] Failed to load {playerPath}");
            return;
        }

        // 1. Ensure PlayerIdentity component
        var identity = playerRoot.GetComponent<PlayerIdentity>();
        if (identity == null)
        {
            identity = playerRoot.AddComponent<PlayerIdentity>();
            Debug.Log("[HitscanSetup] Added PlayerIdentity to Player prefab.");
        }

        // 2. Configure PlayerShooting
        var shooting = playerRoot.GetComponent<PlayerShooting>();
        if (shooting != null)
        {
            SerializedObject shootSO = new SerializedObject(shooting);
            SetSerializedRef(shootSO, "laserBeamPrefab", laserPrefab);
            SetSerializedFloat(shootSO, "fireCooldown", 0.2f);
            SetSerializedFloat(shootSO, "maxShotRange", 30f);
            SetSerializedInt(shootSO, "maxRicochets", 2);
            SetSerializedFloat(shootSO, "closeRangeDistance", 6f);
            SetSerializedFloat(shootSO, "mediumRangeDistance", 14f);
            SetSerializedFloat(shootSO, "closeDamage", 3f);
            SetSerializedFloat(shootSO, "mediumDamage", 1.5f);
            SetSerializedFloat(shootSO, "longDamage", 1f);
            SetSerializedInt(shootSO, "basicAmmoCapacity", 3);
            SetSerializedFloat(shootSO, "surfaceOffset", 0.02f);
            shootSO.ApplyModifiedPropertiesWithoutUndo();
        }

        // 3. Setup WorldStatusUI strictly BELOW the player character
        Transform statusAnchor = playerRoot.transform.Find("StatusAnchor");
        if (statusAnchor == null)
        {
            GameObject anchorGO = new GameObject("StatusAnchor");
            anchorGO.transform.SetParent(playerRoot.transform, false);
            // Placed on ground plane slightly in front/below character for top-down visibility
            anchorGO.transform.localPosition = new Vector3(0f, 0.05f, -1.1f);
            anchorGO.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);
            statusAnchor = anchorGO.transform;
        }

        // Canvas setup on StatusAnchor
        var canvas = statusAnchor.GetComponent<Canvas>();
        if (canvas == null) canvas = statusAnchor.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rt = statusAnchor.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(300f, 110f);
        rt.localScale = Vector3.one * 0.005f;

        var scaler = statusAnchor.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = statusAnchor.gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        var statusUI = statusAnchor.GetComponent<PlayerWorldStatusUI>();
        if (statusUI == null) statusUI = statusAnchor.gameObject.AddComponent<PlayerWorldStatusUI>();

        // Build UI elements if not already present
        BuildWorldStatusUIHierarchy(statusAnchor, statusUI);

        PrefabUtility.SaveAsPrefabAsset(playerRoot, playerPath);
        PrefabUtility.UnloadPrefabContents(playerRoot);

        Debug.Log("[HitscanSetup] Configured Player prefab with PlayerIdentity and PlayerWorldStatusUI!");
    }

    private static void BuildWorldStatusUIHierarchy(Transform anchor, PlayerWorldStatusUI statusUI)
    {
        SerializedObject so = new SerializedObject(statusUI);

        // 1. Player Name Text
        Transform nameT = anchor.Find("PlayerNameText");
        TextMeshProUGUI nameTMP;
        if (nameT == null)
        {
            GameObject go = new GameObject("PlayerNameText");
            go.transform.SetParent(anchor, false);
            nameTMP = go.AddComponent<TextMeshProUGUI>();
            nameTMP.fontSize = 20;
            nameTMP.fontStyle = FontStyles.Bold;
            nameTMP.alignment = TextAlignmentOptions.Center;
            nameTMP.color = Color.white;
            nameTMP.text = "Player";
            RectTransform nrt = go.GetComponent<RectTransform>();
            nrt.anchorMin = new Vector2(0f, 0.65f);
            nrt.anchorMax = new Vector2(1f, 1f);
            nrt.offsetMin = Vector2.zero;
            nrt.offsetMax = Vector2.zero;
        }
        else
        {
            nameTMP = nameT.GetComponent<TextMeshProUGUI>();
        }
        SetSerializedRef(so, "playerNameText", nameTMP);

        // 2. Health Bar 3-Segment Container
        Transform barRoot = anchor.Find("HealthBarsContainer");
        if (barRoot == null)
        {
            GameObject go = new GameObject("HealthBarsContainer");
            go.transform.SetParent(anchor, false);
            RectTransform brt = go.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.05f, 0.32f);
            brt.anchorMax = new Vector2(0.95f, 0.62f);
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            barRoot = go.transform;
        }

        // 3 Segment Images
        SerializedProperty segmentsProp = so.FindProperty("healthSegments");
        if (segmentsProp != null) segmentsProp.arraySize = 3;

        for (int i = 0; i < 3; i++)
        {
            string segName = $"Segment_{i + 1}";
            Transform segT = barRoot.Find(segName);
            Image fillImg;
            if (segT == null)
            {
                // Slot Background
                GameObject slotGO = new GameObject(segName);
                slotGO.transform.SetParent(barRoot, false);
                var slotBg = slotGO.AddComponent<Image>();
                slotBg.color = new Color(0.1f, 0.1f, 0.12f, 0.85f);

                // Slot Fill (child)
                GameObject fillGO = new GameObject("Fill");
                fillGO.transform.SetParent(slotGO.transform, false);
                RectTransform frt = fillGO.AddComponent<RectTransform>();
                frt.anchorMin = Vector2.zero;
                frt.anchorMax = Vector2.one;
                frt.offsetMin = Vector2.zero;
                frt.offsetMax = Vector2.zero;

                fillImg = fillGO.AddComponent<Image>();
                fillImg.type = Image.Type.Filled;
                fillImg.fillMethod = Image.FillMethod.Horizontal;
                fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
                fillImg.fillAmount = 1f;
                fillImg.color = new Color(0.2f, 0.9f, 0.35f, 1f);
            }
            else
            {
                Transform fillT = segT.Find("Fill");
                fillImg = fillT != null ? fillT.GetComponent<Image>() : segT.GetComponent<Image>();
            }

            if (segmentsProp != null && fillImg != null)
            {
                segmentsProp.GetArrayElementAtIndex(i).objectReferenceValue = fillImg;
            }
        }

        // 3. Ammo Displays Container
        Transform ammoRoot = anchor.Find("AmmoContainer");
        TextMeshProUGUI basicTMP;
        TextMeshProUGUI extraTMP;

        if (ammoRoot == null)
        {
            GameObject go = new GameObject("AmmoContainer");
            go.transform.SetParent(anchor, false);
            RectTransform art = go.AddComponent<RectTransform>();
            art.anchorMin = new Vector2(0.05f, 0f);
            art.anchorMax = new Vector2(0.95f, 0.28f);
            art.offsetMin = Vector2.zero;
            art.offsetMax = Vector2.zero;

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            ammoRoot = go.transform;

            // Basic Text
            GameObject bGO = new GameObject("BasicAmmoText");
            bGO.transform.SetParent(ammoRoot, false);
            basicTMP = bGO.AddComponent<TextMeshProUGUI>();
            basicTMP.fontSize = 16;
            basicTMP.alignment = TextAlignmentOptions.Center;
            basicTMP.color = new Color(0.2f, 0.85f, 1f, 1f);
            basicTMP.text = "B: 3/3";

            // Extra Text
            GameObject eGO = new GameObject("ExtraAmmoText");
            eGO.transform.SetParent(ammoRoot, false);
            extraTMP = eGO.AddComponent<TextMeshProUGUI>();
            extraTMP.fontSize = 16;
            extraTMP.alignment = TextAlignmentOptions.Center;
            extraTMP.color = new Color(1f, 0.75f, 0.1f, 1f);
            extraTMP.text = "+0 EX";
            eGO.SetActive(false);
        }
        else
        {
            Transform bT = ammoRoot.Find("BasicAmmoText");
            Transform eT = ammoRoot.Find("ExtraAmmoText");
            basicTMP = bT != null ? bT.GetComponent<TextMeshProUGUI>() : null;
            extraTMP = eT != null ? eT.GetComponent<TextMeshProUGUI>() : null;
        }

        SetSerializedRef(so, "basicAmmoText", basicTMP);
        SetSerializedRef(so, "extraAmmoText", extraTMP);

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ═══════════════════════════════════════════════════════════
    // 5. Setup MainMultiplayer.unity Scene
    // ═══════════════════════════════════════════════════════════

    public static void SetupMainMultiplayerScene(GameObject pickupPrefab)
    {
        string scenePath = "Assets/_Multiplayer/Scenes/MainMultiplayer.unity";
        if (!File.Exists(scenePath))
        {
            Debug.LogWarning($"[HitscanSetup] Scene not found: {scenePath}");
            return;
        }

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var spawner = Object.FindAnyObjectByType<AmmoPickupSpawner>();
        if (spawner == null)
        {
            GameObject spawnerGO = new GameObject("AmmoPickupSpawner");
            spawner = spawnerGO.AddComponent<AmmoPickupSpawner>();
            Debug.Log("[HitscanSetup] Created AmmoPickupSpawner in MainMultiplayer scene.");
        }

        SerializedObject so = new SerializedObject(spawner);
        SetSerializedRef(so, "pickupPrefab", pickupPrefab);
        SetSerializedVector3(so, "spawnAreaCenter", new Vector3(-5f, 0.5f, 0f));
        SetSerializedVector3(so, "spawnAreaSize", new Vector3(14f, 2f, 10f));
        SetSerializedInt(so, "maxActivePickups", 4);
        SetSerializedFloat(so, "respawnInterval", 5f);
        SetSerializedInt(so, "maxPlacementAttempts", 15);
        SetSerializedFloat(so, "minPickupSpacing", 2.5f);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[HitscanSetup] Configured AmmoPickupSpawner in MainMultiplayer.unity!");
    }

    // ─── Helpers ────────────────────────────────────────────────

    private static void SetSerializedRef(SerializedObject so, string fieldName, Object val)
    {
        var prop = so.FindProperty(fieldName);
        if (prop != null) prop.objectReferenceValue = val;
    }

    private static void SetSerializedFloat(SerializedObject so, string fieldName, float val)
    {
        var prop = so.FindProperty(fieldName);
        if (prop != null) prop.floatValue = val;
    }

    private static void SetSerializedInt(SerializedObject so, string fieldName, int val)
    {
        var prop = so.FindProperty(fieldName);
        if (prop != null) prop.intValue = val;
    }

    private static void SetSerializedVector3(SerializedObject so, string fieldName, Vector3 val)
    {
        var prop = so.FindProperty(fieldName);
        if (prop != null) prop.vector3Value = val;
    }
}
#endif
