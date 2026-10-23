#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

/// <summary>
/// Automated Editor setup utility for the Multiplayer Dash Ability.
/// Configures:
///   1. Player.prefab:
///      - Adds and configures PlayerDash component.
///      - Wires InputReader (Assets/_Multiplayer/Inputs/SO/InputReader.asset).
///      - Creates DashTrail child GameObject with TrailRenderer and glowing neon cyan material.
///      - Wires trailRenderer on PlayerDash.
///   2. MobileControlsHUD.prefab:
///      - Configures MobileDashButton beside MeleeButton.
///      - Circular radial fill image, cooldown countdown text, and DOTween feedback.
///      - Wires MobileDashButton into MobileControlsHUD.
///   3. MainMultiplayer.unity scene:
///      - Ensures the scene has the updated MobileControlsHUD instance.
///
/// Can be executed via:
///   - Unity Menu: Tools -> Multiplayer -> Complete Dash Ability Setup
///   - Batchmode: DashAbilitySetupEditor.SetupAllNoDialog()
///   - Auto-runs on editor load if PlayerDash is missing on Player.prefab.
/// </summary>
public static class DashAbilitySetupEditor
{
    private const string MenuPath = "Tools/Multiplayer/";
    private const string PlayerPrefabPath = "Assets/_Multiplayer/Players/Prefabs/Player.prefab";
    private const string InputReaderPath = "Assets/_Multiplayer/Inputs/SO/InputReader.asset";
    private const string TrailMaterialPath = "Assets/_Multiplayer/Players/Prefabs/DashTrailMaterial.mat";
    private const string ScenePath = "Assets/_Multiplayer/Scenes/MainMultiplayer.unity";

    [InitializeOnLoadMethod]
    private static void AutoSetupOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (File.Exists(PlayerPrefabPath))
            {
                GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
                if (playerPrefab != null && playerPrefab.GetComponent<PlayerDash>() == null)
                {
                    Debug.Log("[DashSetup] PlayerDash missing on Player.prefab. Running automated Dash ability setup...");
                    SetupAllNoDialog();
                }
            }
        };
    }

    [MenuItem(MenuPath + "Complete Dash Ability Setup", false, 205)]
    public static void SetupAllMenu()
    {
        if (!EditorUtility.DisplayDialog("Dash Ability Setup",
            "This will configure the Multiplayer Dash Ability:\n\n" +
            "1. Configure Player.prefab with PlayerDash and DashTrail (TrailRenderer).\n" +
            "2. Configure MobileControlsHUD.prefab with MobileDashButton and circular radial cooldown.\n" +
            "3. Update MainMultiplayer.unity scene.\n\nProceed?",
            "Yes", "Cancel"))
            return;

        SetupAllNoDialog();

        EditorUtility.DisplayDialog("Setup Complete",
            "Multiplayer Dash Ability setup complete!\n\n" +
            "PC: Left Shift to Dash.\n" +
            "Mobile: Dedicated Dash Button beside Melee with 15s radial cooldown.\n" +
            "Check DASH_SYSTEM_SETUP_GUIDE_AR.md for full documentation.",
            "OK");
    }

    public static void SetupAllNoDialog()
    {
        Debug.Log("[DashSetup] Starting automated Dash Ability setup...");

        // 1. Create Trail Material
        Material trailMat = EnsureTrailMaterial();

        // 2. Configure Player.prefab
        SetupPlayerPrefab(trailMat);

        // 3. Configure Mobile Controls HUD Prefab
        MobileControlsSetupEditor.SetupMobileControlsPrefab();

        // 4. Ensure MainMultiplayer scene has updated mobile controls
        MobileControlsSetupEditor.EnsureSceneHasMobileControls();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[DashSetup] Automated Dash Ability setup finished successfully!");
    }

    private static Material EnsureTrailMaterial()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(TrailMaterialPath);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            mat = new Material(shader);
            mat.name = "DashTrailMaterial";
            Color neonCyan = new Color(0.1f, 0.85f, 1f, 0.85f);

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", neonCyan);
            else if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", neonCyan);

            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", neonCyan * 1.5f);
            }

            string dir = Path.GetDirectoryName(TrailMaterialPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            AssetDatabase.CreateAsset(mat, TrailMaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DashSetup] Created DashTrailMaterial at {TrailMaterialPath}");
        }
        return mat;
    }

    public static void SetupPlayerPrefab(Material trailMaterial)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogError($"[DashSetup] Could not load Player prefab at {PlayerPrefabPath}");
            return;
        }

        try
        {
            InputReader inputReader = AssetDatabase.LoadAssetAtPath<InputReader>(InputReaderPath);
            if (inputReader == null)
            {
                Debug.LogError($"[DashSetup] Could not find InputReader at: {InputReaderPath}");
            }

            // Ensure PlayerDash component exists
            PlayerDash dash = prefabRoot.GetComponent<PlayerDash>();
            if (dash == null)
            {
                dash = prefabRoot.AddComponent<PlayerDash>();
            }

            // Ensure DashTrail child exists with TrailRenderer
            Transform dashTrailT = prefabRoot.transform.Find("DashTrail");
            GameObject dashTrailGO;
            if (dashTrailT == null)
            {
                dashTrailGO = new GameObject("DashTrail");
                dashTrailGO.transform.SetParent(prefabRoot.transform, false);
                dashTrailGO.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            }
            else
            {
                dashTrailGO = dashTrailT.gameObject;
            }

            TrailRenderer tr = dashTrailGO.GetComponent<TrailRenderer>();
            if (tr == null)
            {
                tr = dashTrailGO.AddComponent<TrailRenderer>();
            }

            tr.time = 0.35f;
            tr.startWidth = 0.65f;
            tr.endWidth = 0.05f;
            tr.minVertexDistance = 0.1f;
            tr.autodestruct = false;
            tr.emitting = false;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.1f, 0.85f, 1f), 0.0f),
                    new GradientColorKey(new Color(0f, 0.5f, 1f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.85f, 0.0f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            tr.colorGradient = gradient;

            if (trailMaterial != null)
            {
                tr.sharedMaterial = trailMaterial;
            }

            // Configure PlayerDash properties via SerializedObject
            SerializedObject dashSO = new SerializedObject(dash);
            dashSO.FindProperty("inputReader").objectReferenceValue = inputReader;
            dashSO.FindProperty("dashCooldown").floatValue = 15f;
            dashSO.FindProperty("dashDuration").floatValue = 0.25f;
            dashSO.FindProperty("dashDistance").floatValue = 4f;
            dashSO.FindProperty("collisionSkinMargin").floatValue = 0.05f;
            dashSO.FindProperty("trailRenderer").objectReferenceValue = tr;
            dashSO.FindProperty("trailFadeDuration").floatValue = 0.35f;

            // Default collision layers: check Default (0), ground (9), obstacles (~((1<<16)|(1<<2)|(1<<5)))
            LayerMask mask = ~((1 << 16) | (1 << 2) | (1 << 5));
            dashSO.FindProperty("collisionLayers").intValue = mask.value;
            dashSO.ApplyModifiedProperties();

            // Save Prefab
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            Debug.Log($"[DashSetup] Player.prefab successfully updated and saved with PlayerDash and DashTrail!");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }
}
#endif
