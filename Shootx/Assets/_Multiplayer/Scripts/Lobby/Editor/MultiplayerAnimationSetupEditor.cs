#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Automated Editor setup utility for the Multiplayer Character Animation System.
/// Configures:
///   1. MultiplayerPlayer.controller:
///      - Base Layer:
///        - Idle state with Idle_A.anim
///        - Running state with 2D Freeform Directional BlendTree (MoveX, MoveZ)
///          containing Forward (Running_A), Backward (Walking_Backwards),
///          Left Strafe (Running_Strafe_Left), and Right Strafe (Running_Strafe_Right)
///      - Aimming Layer (Upper Body):
///        - Assigned Upper.mask (excluding lower body/legs)
///        - Override blending mode, default weight 0
///        - Empty default state
///        - Aiming state with Ranged_1H_Aiming.anim
///        - Shoot state with Ranged_1H_Shoot.anim
///   2. Player.prefab:
///      - Adds and wires PlayerVisualManager with all 4 character configs
///        (ActionFigure, Driver, SpaceRanger, Survivalist)
///      - Configures weapon attachment to right hand with fine-tuned offsets
///      - Adds and wires PlayerAnimationController with InputReader
///      - Wires PlayerShooting and PlayerMelee animation callbacks
/// </summary>
public static class MultiplayerAnimationSetupEditor
{
    private const string MenuPath = "Tools/Multiplayer/";
    private const string ControllerPath = "Assets/_Multiplayer/Players/Animation/MultiplayerPlayer.controller";
    private const string UpperMaskPath = "Assets/_Multiplayer/Players/Animation/Upper.mask";
    private const string PlayerPrefabPath = "Assets/_Multiplayer/Players/Prefabs/Player.prefab";
    private const string InputReaderPath = "Assets/_Multiplayer/Inputs/SO/InputReader.asset";

    // Animation Clip Paths
    private const string RunningClipPath = "Assets/KayKit/Characters/Animations/Animations/Rig_Medium/Movement Basic/Running_A.anim";
    private const string WalkingBackClipPath = "Assets/KayKit/Characters/Animations/Animations/Rig_Medium/Movement Advanced/Walking_Backwards.anim";
    private const string StrafeLeftClipPath = "Assets/KayKit/Characters/Animations/Animations/Rig_Medium/Movement Advanced/Running_Strafe_Left.anim";
    private const string StrafeRightClipPath = "Assets/KayKit/Characters/Animations/Animations/Rig_Medium/Movement Advanced/Running_Strafe_Right.anim";
    private const string IdleClipPath = "Assets/KayKit/Characters/Animations/Animations/Rig_Medium/General/Idle_A.anim";
    private const string AimingClipPath = "Assets/KayKit/Characters/Animations/Animations/Rig_Medium/Combat Ranged/Ranged_1H_Aiming.anim";
    private const string ShootClipPath = "Assets/KayKit/Characters/Animations/Animations/Rig_Medium/Combat Ranged/Ranged_1H_Shoot.anim";

    [InitializeOnLoadMethod]
    private static void AutoSetupOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (File.Exists(PlayerPrefabPath))
            {
                GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
                if (playerPrefab != null && (playerPrefab.GetComponent<PlayerVisualManager>() == null || playerPrefab.GetComponent<PlayerAnimationController>() == null))
                {
                    Debug.Log("[AnimationSetup] Missing animation components on Player.prefab. Running automated Animation System setup...");
                    SetupAllNoDialog();
                }
            }
        };
    }

    [MenuItem(MenuPath + "Complete Animation System Setup", false, 206)]
    public static void SetupAllMenu()
    {
        SetupAllNoDialog();

        EditorUtility.DisplayDialog("Setup Complete",
            "Multiplayer Character Animation System setup complete!\n\n" +
            "1. Animator Controller configured with 2D Directional Blend Tree & Upper-body Aim/Shoot.\n" +
            "2. Upper.mask assigned to Aimming layer (Override blending).\n" +
            "3. PlayerVisualManager configured with 4 characters & weapon attachment.\n" +
            "4. PlayerAnimationController wired and synchronized.\n\n" +
            "Check MULTIPLAYER_ANIMATION_SETUP_GUIDE_AR.md for complete details.",
            "OK");
    }

    public static void SetupAllNoDialog()
    {
        Debug.Log("[AnimationSetup] Starting automated Animation System setup...");

        // 1. Configure Animator Controller
        ConfigureAnimatorController();

        // 2. Configure Player Prefab
        ConfigurePlayerPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[AnimationSetup] Animation System setup finished successfully!");
    }

    public static void ConfigureAnimatorController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[AnimationSetup] Could not load AnimatorController at '{ControllerPath}'!");
            return;
        }

        AvatarMask upperMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperMaskPath);
        AnimationClip idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath);
        AnimationClip runningClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(RunningClipPath);
        AnimationClip walkingBackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkingBackClipPath);
        AnimationClip strafeLeftClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(StrafeLeftClipPath);
        AnimationClip strafeRightClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(StrafeRightClipPath);
        AnimationClip aimingClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AimingClipPath);
        AnimationClip shootClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ShootClipPath);

        // Ensure parameters MoveX and MoveZ exist
        EnsureParameter(controller, "MoveX", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "MoveZ", AnimatorControllerParameterType.Float);

        // ─── Layer 0: Base Layer ────────────────────────────────
        var layers = controller.layers;
        if (layers.Length > 0)
        {
            var baseLayer = layers[0];
            var sm = baseLayer.stateMachine;

            // Find or configure Idle
            AnimatorState idleState = FindState(sm, "Idle");
            if (idleState == null) idleState = sm.AddState("Idle", new Vector3(300, 100, 0));
            if (idleClip != null) idleState.motion = idleClip;

            // Find or configure Running as 2D Blend Tree
            AnimatorState runningState = FindState(sm, "Running");
            if (runningState == null) runningState = sm.AddState("Running", new Vector3(300, 200, 0));

            BlendTree blendTree;
            if (runningState.motion is BlendTree existingTree)
            {
                blendTree = existingTree;
            }
            else
            {
                blendTree = new BlendTree
                {
                    name = "DirectionalLocomotion",
                    blendType = BlendTreeType.FreeformDirectional2D,
                    blendParameter = "MoveX",
                    blendParameterY = "MoveZ",
                    hideFlags = HideFlags.HideInHierarchy
                };
                AssetDatabase.AddObjectToAsset(blendTree, controller);
                runningState.motion = blendTree;
            }

            blendTree.blendType = BlendTreeType.FreeformDirectional2D;
            blendTree.blendParameter = "MoveX";
            blendTree.blendParameterY = "MoveZ";

            // Clear and add children: Forward, Backward, Left, Right
            while (blendTree.children.Length > 0)
            {
                blendTree.RemoveChild(0);
            }

            if (runningClip != null) blendTree.AddChild(runningClip, new Vector2(0f, 1f));
            if (walkingBackClip != null) blendTree.AddChild(walkingBackClip, new Vector2(0f, -1f));
            if (strafeLeftClip != null) blendTree.AddChild(strafeLeftClip, new Vector2(-1f, 0f));
            if (strafeRightClip != null) blendTree.AddChild(strafeRightClip, new Vector2(1f, 0f));

            sm.defaultState = idleState;
        }

        // ─── Layer 1: Aimming Layer ─────────────────────────────
        AnimatorControllerLayer aimLayer = null;
        int aimLayerIndex = -1;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i].name.Equals("Aimming", StringComparison.OrdinalIgnoreCase) ||
                layers[i].name.Equals("Aiming", StringComparison.OrdinalIgnoreCase))
            {
                aimLayer = layers[i];
                aimLayerIndex = i;
                break;
            }
        }

        if (aimLayer == null)
        {
            controller.AddLayer("Aimming");
            layers = controller.layers;
            aimLayerIndex = layers.Length - 1;
            aimLayer = layers[aimLayerIndex];
        }

        aimLayer.name = "Aimming"; // preserve exact asset spelling
        aimLayer.avatarMask = upperMask;
        aimLayer.blendingMode = AnimatorLayerBlendingMode.Override;
        aimLayer.defaultWeight = 0f;

        var aimSM = aimLayer.stateMachine;

        // Ensure states: Empty (default), Aiming, Shoot
        AnimatorState emptyState = FindState(aimSM, "Empty");
        if (emptyState == null) emptyState = aimSM.AddState("Empty", new Vector3(300, 100, 0));
        emptyState.motion = null;

        AnimatorState aimState = FindState(aimSM, "Aiming");
        if (aimState == null) aimState = aimSM.AddState("Aiming", new Vector3(300, 200, 0));
        if (aimingClip != null) aimState.motion = aimingClip;

        AnimatorState shootState = FindState(aimSM, "Shoot");
        if (shootState == null) shootState = aimSM.AddState("Shoot", new Vector3(300, 300, 0));
        if (shootClip != null) shootState.motion = shootClip;

        aimSM.defaultState = emptyState;

        // Apply updated layers
        controller.layers = layers;

        EditorUtility.SetDirty(controller);
        Debug.Log("[AnimationSetup] MultiplayerPlayer.controller successfully configured!");
    }

    private static void EnsureParameter(AnimatorController controller, string paramName, AnimatorControllerParameterType type)
    {
        foreach (var p in controller.parameters)
        {
            if (p.name.Equals(paramName, StringComparison.OrdinalIgnoreCase)) return;
        }
        controller.AddParameter(paramName, type);
    }

    private static AnimatorState FindState(AnimatorStateMachine sm, string stateName)
    {
        foreach (var child in sm.states)
        {
            if (child.state.name.Equals(stateName, StringComparison.OrdinalIgnoreCase))
                return child.state;
        }
        return null;
    }

    public static void ConfigurePlayerPrefab()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogError($"[AnimationSetup] Could not load Player prefab at '{PlayerPrefabPath}'!");
            return;
        }

        try
        {
            InputReader inputReader = AssetDatabase.LoadAssetAtPath<InputReader>(InputReaderPath);

            // 1. Configure PlayerVisualManager
            PlayerVisualManager visualManager = prefabRoot.GetComponent<PlayerVisualManager>();
            if (visualManager == null)
            {
                visualManager = prefabRoot.AddComponent<PlayerVisualManager>();
            }

            Transform visualsTransform = prefabRoot.transform.Find("Visuals");
            if (visualsTransform == null)
            {
                Debug.LogError("[AnimationSetup] Could not find 'Visuals' child in Player.prefab!");
                return;
            }

            Transform actionFigure = visualsTransform.Find("ActionFigure");
            Transform driver = visualsTransform.Find("Driver");
            Transform spaceRanger = visualsTransform.Find("SpaceRanger");
            Transform survivalist = visualsTransform.Find("Survivalist");
            Transform weapon = visualsTransform.Find("Weapon");
            Transform firePoint = weapon != null ? weapon.Find("FirePoint") : null;

            // Weapon offset configuration for KayKit handslot.r socket
            // When attached to handslot.r: position offset (0, 0, 0), rotation (0, 0, 0), scale (1, 1, 1)
            // If attached to hand.r: small local adjustment
            PlayerVisualManager.CharacterVisualConfig[] configs = new PlayerVisualManager.CharacterVisualConfig[4]
            {
                new PlayerVisualManager.CharacterVisualConfig
                {
                    characterName = "ActionFigure",
                    visualRoot = actionFigure != null ? actionFigure.gameObject : null,
                    animator = actionFigure != null ? actionFigure.GetComponent<Animator>() : null,
                    customHandSocket = null,
                    weaponPositionOffset = Vector3.zero,
                    weaponRotationOffset = Vector3.zero,
                    weaponScaleOffset = Vector3.one
                },
                new PlayerVisualManager.CharacterVisualConfig
                {
                    characterName = "Driver",
                    visualRoot = driver != null ? driver.gameObject : null,
                    animator = driver != null ? driver.GetComponent<Animator>() : null,
                    customHandSocket = null,
                    weaponPositionOffset = Vector3.zero,
                    weaponRotationOffset = Vector3.zero,
                    weaponScaleOffset = Vector3.one
                },
                new PlayerVisualManager.CharacterVisualConfig
                {
                    characterName = "SpaceRanger",
                    visualRoot = spaceRanger != null ? spaceRanger.gameObject : null,
                    animator = spaceRanger != null ? spaceRanger.GetComponent<Animator>() : null,
                    customHandSocket = null,
                    weaponPositionOffset = Vector3.zero,
                    weaponRotationOffset = Vector3.zero,
                    weaponScaleOffset = Vector3.one
                },
                new PlayerVisualManager.CharacterVisualConfig
                {
                    characterName = "Survivalist",
                    visualRoot = survivalist != null ? survivalist.gameObject : null,
                    animator = survivalist != null ? survivalist.GetComponent<Animator>() : null,
                    customHandSocket = null,
                    weaponPositionOffset = Vector3.zero,
                    weaponRotationOffset = Vector3.zero,
                    weaponScaleOffset = Vector3.one
                }
            };

            visualManager.SetConfigs(configs, weapon, firePoint);

            // 2. Configure PlayerAnimationController
            PlayerAnimationController animController = prefabRoot.GetComponent<PlayerAnimationController>();
            if (animController == null)
            {
                animController = prefabRoot.AddComponent<PlayerAnimationController>();
            }

            // Wire inputReader
            var inputField = typeof(PlayerAnimationController).GetField("inputReader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (inputField != null && inputReader != null)
            {
                inputField.SetValue(animController, inputReader);
            }

            // 3. Configure PlayerShooting muzzlePoint
            PlayerShooting shooting = prefabRoot.GetComponent<PlayerShooting>();
            if (shooting != null && firePoint != null)
            {
                shooting.SetMuzzlePoint(firePoint);
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            Debug.Log("[AnimationSetup] Player.prefab successfully configured and saved!");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }
}
#endif
