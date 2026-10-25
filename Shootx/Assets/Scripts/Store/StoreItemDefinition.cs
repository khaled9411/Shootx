using System;
using UnityEngine;

namespace Shootx.Store
{
    [CreateAssetMenu(fileName = "New Store Item", menuName = "ShootX/Store/Store Item Definition")]
    public class StoreItemDefinition : ScriptableObject
    {
        [Header("=== Identification ===")]
        [Tooltip("Unique stable string ID for this item (e.g. wep_pistol_01, outfit_clown)")]
        public string id = "";

        [Tooltip("Numeric ID corresponding to GameDataManager index / category ID")]
        public int numericId = 0;

        [Tooltip("User-facing display name")]
        public string displayName = "";

        [TextArea(2, 4)]
        [Tooltip("Optional store description")]
        public string description = "";

        [Tooltip("Category of the item: Weapon or Outfit")]
        public StoreItemType itemType = StoreItemType.Weapon;

        [Tooltip("Rarity tier of the item")]
        public ItemRarity rarity = ItemRarity.Common;

        [Header("=== Economy & Pricing ===")]
        [Tooltip("Primary currency required to purchase this item")]
        public CurrencyType purchaseCurrency = CurrencyType.SoftCurrency;

        [Tooltip("Price in Soft Coins (earned by completing levels)")]
        [Min(0)]
        public int softCurrencyPrice = 500;

        [Tooltip("Optional price in Hard Currency (purchased via IAP)")]
        [Min(0)]
        public int hardCurrencyPrice = 0;

        [Tooltip("If true, this item is unlocked and owned by default for every new player")]
        public bool isDefaultOwned = false;

        [Header("=== Visual & Gameplay Assets ===")]
        [Tooltip("Reference to the 3D model/prefab located in Store/Weapons or Store/Outfits")]
        public GameObject itemPrefab;

        [Tooltip("Item artwork/icon. Intentionally left empty/null until artwork is ready.")]
        public Sprite itemIcon = null;

        [Header("=== Attachment & Muzzle Overrides (Optional) ===")]
        [Tooltip("Optional custom socket name (e.g. 'handslot.r', 'Wrist_R'). Leave empty for automatic resolution.")]
        public string customSocketName = "";

        [Tooltip("Optional local position offset when equipped")]
        public Vector3 attachmentPositionOffset = Vector3.zero;

        [Tooltip("Optional local rotation offset (Euler angles) when equipped")]
        public Vector3 attachmentRotationOffset = Vector3.zero;

        [Tooltip("Optional local scale multiplier (Vector3.zero defaults to Vector3.one)")]
        public Vector3 attachmentScale = Vector3.zero;

        [Tooltip("Optional local muzzle/firepoint offset relative to this weapon. Leave Vector3.zero for automatic calculation.")]
        public Vector3 muzzleOffset = Vector3.zero;

        public bool IsOwned()
        {
            if (isDefaultOwned) return true;
            if (GameDataManager.Instance == null) return false;

            if (itemType == StoreItemType.Weapon)
            {
                return GameDataManager.Instance.IsGunOwned(numericId);
            }
            else
            {
                return GameDataManager.Instance.IsOutfitOwned(numericId);
            }
        }

        public bool IsEquipped()
        {
            if (GameDataManager.Instance == null) return false;

            if (itemType == StoreItemType.Weapon)
            {
                return GameDataManager.Instance.EquippedGunId == numericId;
            }
            else
            {
                return GameDataManager.Instance.EquippedOutfitId == numericId;
            }
        }
    }
}
