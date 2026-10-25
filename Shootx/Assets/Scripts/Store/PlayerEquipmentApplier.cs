using System;
using System.Collections.Generic;
using UnityEngine;
using Shootx.Store;

/// <summary>
/// Single-player weapon equipment and character attachment controller.
/// Manages active character selection under Models/ and ensures the currently equipped
/// weapon is cleanly attached to the active character's right hand with data-driven offsets.
/// </summary>
public class PlayerEquipmentApplier : MonoBehaviour
{
    // ===================================================================
    #region Inspector Fields
    // ===================================================================

    [Header("=== Hierarchy References ===")]
    [Tooltip("Parent transform containing all character models (e.g. Models child)")]
    [SerializeField] private Transform modelsParent;

    [Tooltip("Reference to the single-player ShootingSystem")]
    [SerializeField] private ShootingSystem shootingSystem;

    [Tooltip("Reference to the single-player PlayerMovement")]
    [SerializeField] private PlayerMovement playerMovement;

    [Header("=== Data-Driven Attachment Profile ===")]
    [Tooltip("ScriptableObject containing character sockets, offsets, and muzzle configurations")]
    [SerializeField] private SinglePlayerWeaponAttachmentProfile attachmentProfile;

    // ===================================================================
    #endregion

    // ===================================================================
    #region Runtime State
    // ===================================================================

    private Transform _activeCharacterTransform;
    private Animator _activeAnimator;
    private Transform _activeHandSocket;
    private GameObject _currentSpawnedWeapon;
    private Transform _currentMuzzleTransform;

    private int _currentOutfitId = -1;
    private int _currentGunId = -1;
    private string _activeWeaponDefId = "";

    public Transform ActiveCharacterTransform => _activeCharacterTransform;
    public Animator ActiveAnimator => _activeAnimator;
    public Transform ActiveHandSocket => _activeHandSocket;
    public GameObject CurrentSpawnedWeapon => _currentSpawnedWeapon;
    public Transform CurrentMuzzleTransform => _currentMuzzleTransform;

    // ===================================================================
    #endregion

    // ===================================================================
    #region Unity Lifecycle
    // ===================================================================

    private void Awake()
    {
        ResolveCoreReferences();
    }

    private void Start()
    {
        ApplyCurrentLoadout();
    }

    private void OnEnable()
    {
        GameDataManager.OnEquipmentChanged += HandleEquipmentChanged;
        ApplyCurrentLoadout();
    }

    private void OnDisable()
    {
        GameDataManager.OnEquipmentChanged -= HandleEquipmentChanged;
    }

    private void OnDestroy()
    {
        GameDataManager.OnEquipmentChanged -= HandleEquipmentChanged;
        CleanupSpawnedWeapon();
    }

    private void ResolveCoreReferences()
    {
        if (shootingSystem == null)
            shootingSystem = GetComponent<ShootingSystem>() ?? GetComponentInParent<ShootingSystem>();

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>() ?? GetComponentInParent<PlayerMovement>();

        if (modelsParent == null)
        {
            Transform t = transform.Find("Models");
            if (t != null) modelsParent = t;
            else
            {
                // Recursive search for Models transform
                modelsParent = FindChildRecursive(transform, "Models");
            }
        }

        if (attachmentProfile == null)
        {
            attachmentProfile = SinglePlayerWeaponAttachmentProfile.DefaultProfile;
        }
    }

    private void HandleEquipmentChanged()
    {
        ApplyCurrentLoadout();
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Loadout Application
    // ===================================================================

    /// <summary>
    /// Reads saved loadout from GameDataManager (with safe fallbacks) and applies both character and weapon.
    /// </summary>
    public void ApplyCurrentLoadout()
    {
        ResolveCoreReferences();

        int outfitId = 0;
        int gunId = 0;

        if (GameDataManager.Instance != null)
        {
            outfitId = GameDataManager.Instance.EquippedOutfitId;
            gunId = GameDataManager.Instance.EquippedGunId;
        }

        ApplyOutfit(outfitId);
        ApplyWeapon(gunId);
    }

    /// <summary>
    /// Activates the selected character model under Models/, finds its right-hand bone/socket,
    /// reattaches the current weapon to it, and updates animators and muzzles.
    /// </summary>
    public void ApplyOutfit(int outfitId)
    {
        ResolveCoreReferences();

        if (modelsParent == null)
        {
            Debug.LogWarning("[PlayerEquipmentApplier] modelsParent is null! Cannot apply outfit.");
            return;
        }

        StoreItemDefinition outfitDef = StoreCatalogDatabase.Instance != null
            ? StoreCatalogDatabase.Instance.GetOutfitByNumericId(outfitId)
            : null;

        Transform targetModel = ResolveCharacterModel(outfitId, outfitDef);

        if (targetModel == null)
        {
            Debug.LogWarning($"[PlayerEquipmentApplier] Could not resolve character model for outfitId {outfitId}. Falling back to default child.");
            if (modelsParent.childCount > 0)
                targetModel = modelsParent.GetChild(0);
        }

        if (targetModel == null) return;

        // Activate ONLY the target model, deactivate all other children under Models/
        for (int i = 0; i < modelsParent.childCount; i++)
        {
            Transform child = modelsParent.GetChild(i);
            if (child == null) continue;

            bool isTarget = child == targetModel;
            child.gameObject.SetActive(isTarget);
        }

        _activeCharacterTransform = targetModel;
        _currentOutfitId = outfitId;

        // Resolve active Animator
        _activeAnimator = targetModel.GetComponent<Animator>() ?? targetModel.GetComponentInChildren<Animator>();
        UpdateAnimatorsOnSystems(_activeAnimator);

        // Resolve active character's right-hand bone/socket
        string customSocketOverride = outfitDef != null ? outfitDef.customSocketName : null;
        _activeHandSocket = ResolveHandSocket(targetModel, targetModel.name, customSocketOverride);

        if (_activeHandSocket == null)
        {
            Debug.LogError($"[PlayerEquipmentApplier] Right-hand attachment socket NOT found on active model '{targetModel.name}'! " +
                           $"Please check bone hierarchy or assign a custom socket.");
            return;
        }

        // If a weapon is already equipped and spawned, re-attach it to the new character's right hand!
        if (_currentSpawnedWeapon != null)
        {
            AttachWeaponToHand(_currentSpawnedWeapon, _activeHandSocket, targetModel.name, _activeWeaponDefId);
        }
    }

    /// <summary>
    /// Equips the specified weapon by stable numeric ID, attaches it to the currently active character's right hand,
    /// applies appropriate offsets, and updates the shooting muzzle.
    /// </summary>
    public void ApplyWeapon(int gunId)
    {
        ResolveCoreReferences();

        StoreItemDefinition weaponDef = StoreCatalogDatabase.Instance != null
            ? StoreCatalogDatabase.Instance.GetWeaponByNumericId(gunId)
            : null;

        if (weaponDef == null || weaponDef.itemPrefab == null)
        {
            // Fallback: try weapon 0
            if (gunId != 0 && StoreCatalogDatabase.Instance != null)
            {
                weaponDef = StoreCatalogDatabase.Instance.GetWeaponByNumericId(0);
            }
        }

        if (weaponDef == null || weaponDef.itemPrefab == null)
        {
            Debug.LogWarning($"[PlayerEquipmentApplier] Weapon definition or prefab missing for gunId {gunId}.");
            return;
        }

        // Ensure active character and hand socket are resolved
        if (_activeCharacterTransform == null || _activeHandSocket == null)
        {
            int outfitId = GameDataManager.Instance != null ? GameDataManager.Instance.EquippedOutfitId : 0;
            ApplyOutfit(outfitId);
        }

        if (_activeHandSocket == null)
        {
            Debug.LogError("[PlayerEquipmentApplier] Cannot attach weapon: no active right-hand socket resolved!");
            return;
        }

        string charName = _activeCharacterTransform != null ? _activeCharacterTransform.name : "";
        string weaponPrefabName = weaponDef.itemPrefab.name;

        // If already equipped and parented properly, re-apply offsets and ensure active
        if (_currentSpawnedWeapon != null &&
            _currentSpawnedWeapon.name.StartsWith(weaponPrefabName) &&
            _currentSpawnedWeapon.transform.parent == _activeHandSocket)
        {
            _currentSpawnedWeapon.SetActive(true);
            ApplyTransformOffsets(_currentSpawnedWeapon.transform, charName, weaponDef.id, weaponDef);
            ResolveAndBindMuzzle(_currentSpawnedWeapon, weaponDef.id, charName, weaponDef);
            _currentGunId = gunId;
            _activeWeaponDefId = weaponDef.id;
            return;
        }

        // Clean up previous weapon instance safely
        CleanupSpawnedWeapon();

        // Clean up any stray/orphaned weapon children on the socket
        CleanupOrphanedWeaponsOnSocket(_activeHandSocket);

        // Instantiate new weapon prefab directly under the active right-hand socket
        _currentSpawnedWeapon = Instantiate(weaponDef.itemPrefab, _activeHandSocket);
        _currentSpawnedWeapon.name = weaponPrefabName;

        // Apply offsets
        ApplyTransformOffsets(_currentSpawnedWeapon.transform, charName, weaponDef.id, weaponDef);
        _currentSpawnedWeapon.SetActive(true);

        _currentGunId = gunId;
        _activeWeaponDefId = weaponDef.id;

        // Resolve muzzle and update shooter
        ResolveAndBindMuzzle(_currentSpawnedWeapon, weaponDef.id, charName, weaponDef);
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Character & Bone Resolution
    // ===================================================================

    private Transform ResolveCharacterModel(int outfitId, StoreItemDefinition outfitDef)
    {
        if (modelsParent == null) return null;

        // Strategy 1: Match by outfit itemPrefab name if definition exists
        if (outfitDef != null && outfitDef.itemPrefab != null)
        {
            string targetName = outfitDef.itemPrefab.name;
            Transform matched = FindModelChildByName(targetName);
            if (matched != null) return matched;
        }

        // Strategy 2: Match by outfit string ID (e.g. outfit_ranger -> Ranger, outfit_knight -> Knight)
        if (outfitDef != null && !string.IsNullOrEmpty(outfitDef.id))
        {
            string cleanId = outfitDef.id.Replace("outfit_", "").Replace("_", "");
            for (int i = 0; i < modelsParent.childCount; i++)
            {
                Transform child = modelsParent.GetChild(i);
                if (child == null) continue;
                string cleanChild = child.name.Replace("_", "");
                if (cleanChild.IndexOf(cleanId, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    cleanId.IndexOf(cleanChild, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return child;
                }
            }
        }

        // Strategy 3: Match by displayName
        if (outfitDef != null && !string.IsNullOrEmpty(outfitDef.displayName))
        {
            Transform matched = FindModelChildByName(outfitDef.displayName);
            if (matched != null) return matched;
        }

        // Strategy 4: Numeric ID defaults
        // numId 0 is Survivalist
        if (outfitId == 0)
        {
            Transform surv = FindModelChildByName("Survivalist");
            if (surv != null) return surv;
            Transform main = FindModelChildByName("MainCharacter");
            if (main != null) return main;
        }

        return null;
    }

    private Transform FindModelChildByName(string searchName)
    {
        if (modelsParent == null || string.IsNullOrEmpty(searchName)) return null;

        for (int i = 0; i < modelsParent.childCount; i++)
        {
            Transform child = modelsParent.GetChild(i);
            if (child == null) continue;

            if (child.name.Equals(searchName, StringComparison.OrdinalIgnoreCase) ||
                child.name.StartsWith(searchName, StringComparison.OrdinalIgnoreCase) ||
                searchName.StartsWith(child.name, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves the right-hand attachment socket or bone for the active character.
    /// Hierarchy order:
    /// 1. Configured custom socket override.
    /// 2. KayKit dedicated 'handslot.r'.
    /// 3. Humanoid RightHand bone via Animator.GetBoneTransform.
    /// 4. Known bone names: 'hand.r', 'Wrist_R', 'RightHand'.
    /// </summary>
    public Transform ResolveHandSocket(Transform characterModel, string characterName, string socketOverride = null)
    {
        if (characterModel == null) return null;

        // 1. Explicit override if provided
        if (!string.IsNullOrEmpty(socketOverride))
        {
            Transform custom = FindChildRecursive(characterModel, socketOverride);
            if (custom != null) return custom;
        }

        // 1b. Check profile for configured socket name
        if (attachmentProfile != null)
        {
            var charSetting = attachmentProfile.GetCharacterSetting(characterName);
            if (charSetting != null && !string.IsNullOrEmpty(charSetting.socketName))
            {
                Transform configured = FindChildRecursive(characterModel, charSetting.socketName);
                if (configured != null) return configured;
            }
        }

        // 2. Check for KayKit dedicated 'handslot.r' (exists on all KayKit characters)
        Transform handslot = FindChildRecursive(characterModel, "handslot.r");
        if (handslot != null) return handslot;

        // 3. Check Humanoid Animator RightHand bone
        if (_activeAnimator != null && _activeAnimator.isHuman)
        {
            Transform bone = _activeAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            if (bone != null && (bone.IsChildOf(characterModel) || bone == characterModel))
                return bone;
        }

        // 4. Fallback search for common right hand bone names
        string[] commonHandNames = { "hand.r", "Wrist_R", "RightHand", "hand_r", "handslot_r", "weapon_r" };
        foreach (var bName in commonHandNames)
        {
            Transform bone = FindChildRecursive(characterModel, bName);
            if (bone != null) return bone;
        }

        return null;
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Weapon Attachment & Offsets
    // ===================================================================

    private void AttachWeaponToHand(GameObject weaponObj, Transform handSocket, string characterName, string weaponId)
    {
        if (weaponObj == null || handSocket == null) return;

        weaponObj.transform.SetParent(handSocket, false);

        StoreItemDefinition def = StoreCatalogDatabase.Instance != null && _currentGunId >= 0
            ? StoreCatalogDatabase.Instance.GetWeaponByNumericId(_currentGunId)
            : null;

        ApplyTransformOffsets(weaponObj.transform, characterName, weaponId, def);
        ResolveAndBindMuzzle(weaponObj, weaponId, characterName, def);
    }

    private void ApplyTransformOffsets(Transform weaponTf, string characterName, string weaponId, StoreItemDefinition def)
    {
        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;
        Vector3 scale = Vector3.one;
        Vector3 muzzleOffset = new Vector3(0f, 0.08f, 0.35f);
        string preferredSocket = "handslot.r";

        if (attachmentProfile != null)
        {
            attachmentProfile.GetEffectiveOffsets(weaponId, characterName, out pos, out rot, out scale, out muzzleOffset, out preferredSocket);
        }
        else
        {
            // Safe built-in fallback rules
            if (characterName.IndexOf("MainCharacter", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                pos = new Vector3(0.05f, -0.04f, 0.12f);
                rot = Quaternion.Euler(-90f, 0f, 90f);
            }
        }

        // Definition-level overrides if specified
        if (def != null)
        {
            if (def.attachmentPositionOffset != Vector3.zero) pos += def.attachmentPositionOffset;
            if (def.attachmentRotationOffset != Vector3.zero) rot *= Quaternion.Euler(def.attachmentRotationOffset);
            if (def.attachmentScale != Vector3.zero) scale = Vector3.Scale(scale, def.attachmentScale);
        }

        weaponTf.localPosition = pos;
        weaponTf.localRotation = rot;
        weaponTf.localScale = scale;
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Muzzle & ShootingSystem Binding
    // ===================================================================

    private void ResolveAndBindMuzzle(GameObject weaponObj, string weaponId, string characterName, StoreItemDefinition def)
    {
        if (weaponObj == null) return;

        // 1. Check for existing Muzzle / FirePoint child in weapon
        Transform muzzle = weaponObj.transform.Find("Muzzle") ??
                           weaponObj.transform.Find("FirePoint") ??
                           weaponObj.transform.Find("nozzle") ??
                           FindChildRecursive(weaponObj.transform, "Muzzle") ??
                           FindChildRecursive(weaponObj.transform, "FirePoint");

        // 2. If no muzzle child exists, create or update a dedicated Muzzle child
        if (muzzle == null)
        {
            Vector3 muzzlePos = Vector3.zero;

            if (def != null && def.muzzleOffset != Vector3.zero)
            {
                muzzlePos = def.muzzleOffset;
            }
            else if (attachmentProfile != null)
            {
                var wepSetting = attachmentProfile.GetWeaponSetting(weaponId, characterName);
                if (wepSetting != null && wepSetting.muzzleOffset != Vector3.zero)
                    muzzlePos = wepSetting.muzzleOffset;
            }

            if (muzzlePos == Vector3.zero)
            {
                muzzlePos = CalculateMuzzlePositionFromBounds(weaponObj);
            }

            GameObject muzzleGo = new GameObject("Muzzle");
            muzzleGo.transform.SetParent(weaponObj.transform, false);
            muzzleGo.transform.localPosition = muzzlePos;
            muzzleGo.transform.localRotation = Quaternion.identity;
            muzzleGo.transform.localScale = Vector3.one;

            muzzle = muzzleGo.transform;
        }

        _currentMuzzleTransform = muzzle;

        // Update single-player ShootingSystem
        if (shootingSystem != null && muzzle != null)
        {
            shootingSystem.WeaponFirePoint = muzzle;
        }
    }

    private static Vector3 CalculateMuzzlePositionFromBounds(GameObject weaponObj)
    {
        var renderers = weaponObj.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers == null || renderers.Length == 0)
            return new Vector3(0f, 0.08f, 0.35f);

        float maxZ = 0.25f;
        float barrelY = 0.08f;
        float centerX = 0f;

        Transform root = weaponObj.transform;

        foreach (var r in renderers)
        {
            var filter = r.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;

            Mesh m = filter.sharedMesh;
            Bounds b = m.bounds;

            Vector3[] localCorners = new Vector3[8]
            {
                new Vector3(b.min.x, b.min.y, b.min.z),
                new Vector3(b.min.x, b.min.y, b.max.z),
                new Vector3(b.min.x, b.max.y, b.min.z),
                new Vector3(b.min.x, b.max.y, b.max.z),
                new Vector3(b.max.x, b.min.y, b.min.z),
                new Vector3(b.max.x, b.min.y, b.max.z),
                new Vector3(b.max.x, b.max.y, b.min.z),
                new Vector3(b.max.x, b.max.y, b.max.z),
            };

            for (int i = 0; i < 8; i++)
            {
                Vector3 worldPt = r.transform.TransformPoint(localCorners[i]);
                Vector3 inWeaponSpace = root.InverseTransformPoint(worldPt);

                if (inWeaponSpace.z > maxZ)
                {
                    maxZ = inWeaponSpace.z;
                    barrelY = inWeaponSpace.y;
                    centerX = inWeaponSpace.x;
                }
            }
        }

        return new Vector3(centerX, barrelY, maxZ + 0.02f);
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Animators & Cleanups
    // ===================================================================

    private void UpdateAnimatorsOnSystems(Animator activeAnim)
    {
        if (shootingSystem != null)
            shootingSystem.PlayerAnimator = activeAnim;

        if (playerMovement != null)
            playerMovement.PlayerAnimator = activeAnim;

        if (activeAnim != null && activeAnim.gameObject.activeInHierarchy)
        {
            activeAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            activeAnim.Play("Idle", 0, 0f);
        }
    }

    private void CleanupSpawnedWeapon()
    {
        if (_currentSpawnedWeapon != null)
        {
            if (Application.isPlaying)
                Destroy(_currentSpawnedWeapon);
            else
                DestroyImmediate(_currentSpawnedWeapon);

            _currentSpawnedWeapon = null;
            _currentMuzzleTransform = null;
        }
    }

    private void CleanupOrphanedWeaponsOnSocket(Transform socket)
    {
        if (socket == null) return;

        for (int i = socket.childCount - 1; i >= 0; i--)
        {
            Transform child = socket.GetChild(i);
            if (child == null) continue;

            // Only clean up weapon models (SM_Wep_* or cloned weapons)
            if (child.name.StartsWith("SM_Wep_", StringComparison.OrdinalIgnoreCase) ||
                child.name.StartsWith("wep_", StringComparison.OrdinalIgnoreCase) ||
                child.name.Equals("Muzzle", StringComparison.OrdinalIgnoreCase))
            {
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName)) return null;
        if (parent.name.Equals(childName, StringComparison.OrdinalIgnoreCase)) return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildRecursive(parent.GetChild(i), childName);
            if (found != null) return found;
        }

        return null;
    }

    // ===================================================================
    #endregion
}
