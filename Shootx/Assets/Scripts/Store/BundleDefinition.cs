using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shootx.Store
{
    [CreateAssetMenu(fileName = "New Bundle", menuName = "ShootX/Store/Bundle Definition")]
    public class BundleDefinition : ScriptableObject
    {
        [Header("=== IAP Product Info ===")]
        [Tooltip("Exact IAP Product ID (package_01 to package_06)")]
        public string productId = "package_01";

        [Tooltip("Display name of the bundle")]
        public string bundleName = "Starter Pack";

        [TextArea(2, 4)]
        [Tooltip("Bundle marketing description")]
        public string description = "A pack containing entry-level weapons and outfits.";

        [Tooltip("Bundle artwork/icon. Left null until provided.")]
        public Sprite bundleIcon = null;

        [Header("=== Bundle Contents ===")]
        [Tooltip("List of weapon and outfit item definitions included in this bundle")]
        public List<StoreItemDefinition> includedItems = new List<StoreItemDefinition>();

        public void GrantBundleRewards()
        {
            if (includedItems == null || GameDataManager.Instance == null) return;

            foreach (var item in includedItems)
            {
                if (item == null) continue;

                if (item.itemType == StoreItemType.Weapon)
                {
                    GameDataManager.Instance.AddOwnedGun(item.numericId);
                    Debug.Log($"[Bundle] Unlocked weapon: {item.displayName} (ID: {item.numericId})");
                }
                else if (item.itemType == StoreItemType.Outfit)
                {
                    GameDataManager.Instance.AddOwnedOutfit(item.numericId);
                    Debug.Log($"[Bundle] Unlocked outfit: {item.displayName} (ID: {item.numericId})");
                }
            }
        }

        public int GetOwnedItemCount()
        {
            if (includedItems == null) return 0;
            int count = 0;
            foreach (var item in includedItems)
            {
                if (item != null && item.IsOwned())
                    count++;
            }
            return count;
        }

        public bool AreAllItemsOwned()
        {
            if (includedItems == null || includedItems.Count == 0) return false;
            return GetOwnedItemCount() == includedItems.Count;
        }
    }
}
