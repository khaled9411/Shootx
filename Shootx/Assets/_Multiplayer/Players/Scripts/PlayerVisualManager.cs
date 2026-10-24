using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-authoritative visual and character assignment manager for networked players.
/// 
/// Responsibilities:
///   1. Server deterministically assigns a distinct character visual (0..3) to each participating player.
///   2. Replicates selected character index to all clients via NetworkVariable.
///   3. Activates ONLY the assigned character model; disables the other 3 character models and their Animators.
///   4. Attaches the shared revolver weapon hierarchy to the active character's right hand socket.
///   5. Applies per-character weapon offsets and updates PlayerShooting's muzzlePoint.
///   6. Preserves character selection across death, respawn, and match lifecycle.
/// </summary>
public class PlayerVisualManager : NetworkBehaviour
{
    [System.Serializable]
    public class CharacterVisualConfig
    {
        public string characterName;
        public GameObject visualRoot;
        public Animator animator;
        [Tooltip("Optional explicit hand attachment bone or socket. If null, auto-searches for handslot.r or hand.r.")]
        public Transform customHandSocket;
        [Tooltip("Local position offset relative to the right hand socket.")]
        public Vector3 weaponPositionOffset = Vector3.zero;
        [Tooltip("Local rotation offset (Euler) relative to the right hand socket.")]
        public Vector3 weaponRotationOffset = Vector3.zero;
        [Tooltip("Local scale offset. Defaults to Vector3.one if set to zero.")]
        public Vector3 weaponScaleOffset = Vector3.one;
    }

    [Header("Character Configurations (ActionFigure, Driver, SpaceRanger, Survivalist)")]
    [SerializeField] private CharacterVisualConfig[] characterConfigs = new CharacterVisualConfig[4];

    [Header("Weapon Hierarchy Reference")]
    [Tooltip("The root Transform of the revolver hierarchy under Visuals.")]
    [SerializeField] private Transform weaponRoot;

    [Tooltip("The FirePoint transform at the muzzle.")]
    [SerializeField] private Transform firePoint;

    // ─── Networked Character Index ──────────────────────────────
    /// <summary>
    /// Index (0..3) of the character assigned by the server to this player.
    /// Replicated to all clients. Persists across death and respawn.
    /// </summary>
    private readonly NetworkVariable<int> selectedCharacterIndex = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ─── Events & Properties ────────────────────────────────────
    public event Action<int, Animator> OnCharacterAssigned;

    public int SelectedCharacterIndex => selectedCharacterIndex.Value;
    public Animator ActiveAnimator { get; private set; }
    public GameObject ActiveVisualObject { get; private set; }
    public Transform CurrentHandSocket { get; private set; }

    private PlayerHealth playerHealth;
    private PlayerShooting playerShooting;
    private PlayerAnimationController animationController;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerShooting = GetComponent<PlayerShooting>();
        animationController = GetComponent<PlayerAnimationController>();
        EnsureConfigsInitialized();
    }

    /// <summary>
    /// Self-healing initialization: automatically discovers the 4 character models,
    /// Weapon, and FirePoint under the Visuals hierarchy if not already assigned in Inspector.
    /// </summary>
    public void EnsureConfigsInitialized()
    {
        Transform visuals = transform.Find("Visuals");
        if (visuals == null) return;

        string[] names = { "ActionFigure", "Driver", "SpaceRanger", "Survivalist" };
        if (characterConfigs == null || characterConfigs.Length != 4)
        {
            characterConfigs = new CharacterVisualConfig[4];
        }

        for (int i = 0; i < 4; i++)
        {
            if (characterConfigs[i] == null)
            {
                characterConfigs[i] = new CharacterVisualConfig();
            }

            if (string.IsNullOrEmpty(characterConfigs[i].characterName))
            {
                characterConfigs[i].characterName = names[i];
            }

            if (characterConfigs[i].visualRoot == null)
            {
                Transform child = visuals.Find(names[i]);
                if (child != null)
                {
                    characterConfigs[i].visualRoot = child.gameObject;
                    characterConfigs[i].animator = child.GetComponent<Animator>();
                }
            }
        }

        if (weaponRoot == null)
        {
            Transform wep = visuals.Find("Weapon");
            if (wep != null) weaponRoot = wep;
        }

        if (firePoint == null && weaponRoot != null)
        {
            Transform fp = weaponRoot.Find("FirePoint");
            if (fp != null) firePoint = fp;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        selectedCharacterIndex.OnValueChanged += HandleCharacterIndexChanged;

        if (IsServer)
        {
            int assignedIndex = AssignDistinctCharacterIndex();
            selectedCharacterIndex.Value = assignedIndex;
        }


        if (playerHealth != null)
        {
            playerHealth.OnDeath += HandleDeath;
            playerHealth.OnRespawn += HandleRespawn;
        }

        // Apply immediately if already set (e.g. late-joining clients or server local init)
        if (playerHealth != null && playerHealth.IsDead)
        {
            SetVisualsActive(false);
        }
        else if (selectedCharacterIndex.Value >= 0)
        {
            ApplyCharacterVisual(selectedCharacterIndex.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        selectedCharacterIndex.OnValueChanged -= HandleCharacterIndexChanged;

        if (playerHealth != null)
        {
            playerHealth.OnDeath -= HandleDeath;
            playerHealth.OnRespawn -= HandleRespawn;
        }

        base.OnNetworkDespawn();
    }

    private void HandleCharacterIndexChanged(int previousIndex, int newIndex)
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            return;
        }

        if (newIndex >= 0)
        {
            ApplyCharacterVisual(newIndex);
        }
    }

    private void HandleDeath(ulong killerClientId)
    {
        SetVisualsActive(false);
    }

    private void HandleRespawn()
    {
        SetVisualsActive(true);
    }

    /// <summary>
    /// Shows or hides the active character model and weapon (e.g. during death / respawn).
    /// </summary>
    public void SetVisualsActive(bool active)
    {
        if (!active)
        {
            if (characterConfigs != null)
            {
                for (int i = 0; i < characterConfigs.Length; i++)
                {
                    if (characterConfigs[i] != null)
                    {
                        if (characterConfigs[i].visualRoot != null)
                        {
                            characterConfigs[i].visualRoot.SetActive(false);
                        }
                        if (characterConfigs[i].animator != null)
                        {
                            characterConfigs[i].animator.enabled = false;
                        }
                    }
                }
            }

            if (weaponRoot != null)
            {
                weaponRoot.gameObject.SetActive(false);
            }
        }
        else
        {
            int index = selectedCharacterIndex.Value >= 0 ? selectedCharacterIndex.Value : 0;
            ApplyCharacterVisual(index);

            if (weaponRoot != null)
            {
                weaponRoot.gameObject.SetActive(true);
            }
        }
    }

    /// <summary>
    /// Server-authoritative logic to select a distinct character visual for this player.
    /// Checks all other living/spawned players and picks the first available index (0..3).
    /// </summary>
    private int AssignDistinctCharacterIndex()
    {
        if (characterConfigs == null || characterConfigs.Length == 0) return 0;

        HashSet<int> usedIndices = new HashSet<int>();

        // Check all currently spawned PlayerVisualManager instances in the match
        var allPlayers = FindObjectsByType<PlayerVisualManager>(FindObjectsSortMode.None);
        foreach (var pvm in allPlayers)
        {
            if (pvm == this) continue;
            int taken = pvm.SelectedCharacterIndex;
            if (taken >= 0 && taken < characterConfigs.Length)
            {
                usedIndices.Add(taken);
            }
        }

        // Find first unused character index
        for (int i = 0; i < characterConfigs.Length; i++)
        {
            if (!usedIndices.Contains(i))
            {
                Debug.Log($"[PlayerVisualManager] Server assigned unique character index {i} ({characterConfigs[i].characterName}) to ClientId {OwnerClientId}.");
                return i;
            }
        }

        // Fallback if all 4 are taken (e.g. match with > 4 players)
        int fallbackIndex = (int)(OwnerClientId % (ulong)characterConfigs.Length);
        Debug.Log($"[PlayerVisualManager] All characters assigned, using modulo fallback index {fallbackIndex} for ClientId {OwnerClientId}.");
        return fallbackIndex;
    }

    /// <summary>
    /// Activates the selected character model, disables the other 3 models and their Animators,
    /// attaches the weapon to the right hand socket, and binds the Animator to PlayerAnimationController.
    /// </summary>
    public void ApplyCharacterVisual(int index)
    {
        if (characterConfigs == null || characterConfigs.Length == 0) return;
        if (index < 0 || index >= characterConfigs.Length) index = 0;

        for (int i = 0; i < characterConfigs.Length; i++)
        {
            var config = characterConfigs[i];
            bool isActive = (i == index);

            if (config.visualRoot != null)
            {
                config.visualRoot.SetActive(isActive);
            }

            if (config.animator != null)
            {
                config.animator.enabled = isActive;
            }

            if (isActive)
            {
                ActiveVisualObject = config.visualRoot;
                ActiveAnimator = config.animator;
                AttachWeaponToHand(config);
            }
        }

        if (ActiveAnimator != null)
        {
            if (animationController == null) animationController = GetComponent<PlayerAnimationController>();
            if (animationController != null)
            {
                animationController.BindAnimator(ActiveAnimator);
            }

            OnCharacterAssigned?.Invoke(index, ActiveAnimator);
        }
    }

    /// <summary>
    /// Parents the weapon hierarchy to the active character's right hand socket
    /// and applies configured position, rotation, and scale offsets.
    /// </summary>
    private void AttachWeaponToHand(CharacterVisualConfig config)
    {
        if (weaponRoot == null || config.visualRoot == null) return;

        Transform handSocket = FindHandSocket(config);
        CurrentHandSocket = handSocket;

        if (handSocket != null)
        {
            weaponRoot.SetParent(handSocket, false);

            weaponRoot.localPosition = config.weaponPositionOffset;
            weaponRoot.localRotation = Quaternion.Euler(config.weaponRotationOffset);
            weaponRoot.localScale = (config.weaponScaleOffset == Vector3.zero) ? Vector3.one : config.weaponScaleOffset;

            // Ensure FirePoint remains at muzzle and notify PlayerShooting
            if (firePoint != null)
            {
                if (playerShooting == null) playerShooting = GetComponent<PlayerShooting>();
                if (playerShooting != null)
                {
                    playerShooting.SetMuzzlePoint(firePoint);
                }
            }
        }
        else
        {
            Debug.LogWarning($"[PlayerVisualManager] Could not find right hand bone/socket on character '{config.characterName}'.");
        }
    }

    /// <summary>
    /// Finds the best right-hand attachment target:
    /// 1. Configured custom socket.
    /// 2. 'handslot.r' (KayKit dedicated weapon socket).
    /// 3. HumanBodyBones.RightHand from Animator.
    /// 4. 'hand.r' bone transform.
    /// </summary>
    public Transform FindHandSocket(CharacterVisualConfig config)
    {
        if (config.customHandSocket != null) return config.customHandSocket;
        if (config.visualRoot == null) return null;

        // 1. Check for KayKit dedicated 'handslot.r'
        Transform handslot = FindChildRecursive(config.visualRoot.transform, "handslot.r");
        if (handslot != null) return handslot;

        // 2. Check Humanoid Animator RightHand bone
        if (config.animator != null && config.animator.isHuman)
        {
            Transform bone = config.animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (bone != null) return bone;
        }

        // 3. Fallback search for 'hand.r'
        Transform handR = FindChildRecursive(config.visualRoot.transform, "hand.r");
        if (handR != null) return handR;

        return config.visualRoot.transform;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null) return null;
        if (parent.name.Equals(childName, StringComparison.OrdinalIgnoreCase)) return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildRecursive(parent.GetChild(i), childName);
            if (found != null) return found;
        }

        return null;
    }

    // ─── Inspector Helper Configuration ─────────────────────────
    public void SetConfigs(CharacterVisualConfig[] configs, Transform weapon, Transform muzzle)
    {
        characterConfigs = configs;
        weaponRoot = weapon;
        firePoint = muzzle;
    }
}
