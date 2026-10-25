using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shootx.Store
{
    [CreateAssetMenu(fileName = "StoreCatalogDatabase", menuName = "ShootX/Store/Store Catalog Database")]
    public class StoreCatalogDatabase : ScriptableObject
    {
        private static StoreCatalogDatabase _instance;
        public static StoreCatalogDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<StoreCatalogDatabase>("StoreCatalogDatabase");
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("=== Catalog Items ===")]
        public List<StoreItemDefinition> weapons = new List<StoreItemDefinition>();
        public List<StoreItemDefinition> outfits = new List<StoreItemDefinition>();
        public List<BundleDefinition> bundles = new List<BundleDefinition>();

        public IReadOnlyList<StoreItemDefinition> Weapons => weapons;
        public IReadOnlyList<StoreItemDefinition> Outfits => outfits;
        public IReadOnlyList<BundleDefinition> Bundles => bundles;

        public int GetTotalWeaponsSoftCost() => CalculateTotalWeaponSoftCost();
        public int GetTotalOutfitsSoftCost() => CalculateTotalOutfitSoftCost();

        public void Initialize()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            if (Instance == null)
                Instance = this;
        }

        public StoreItemDefinition GetWeaponById(string id)
        {
            return weapons.Find(w => w != null && w.id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }

        public StoreItemDefinition GetWeaponByNumericId(int numId)
        {
            return weapons.Find(w => w != null && w.numericId == numId);
        }

        public StoreItemDefinition GetOutfitById(string id)
        {
            return outfits.Find(o => o != null && o.id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }

        public StoreItemDefinition GetOutfitByNumericId(int numId)
        {
            return outfits.Find(o => o != null && o.numericId == numId);
        }

        public BundleDefinition GetBundleByProductId(string productId)
        {
            return bundles.Find(b => b != null && b.productId.Equals(productId, StringComparison.OrdinalIgnoreCase));
        }

        public int CalculateTotalWeaponSoftCost()
        {
            int total = 0;
            foreach (var w in weapons)
            {
                if (w != null && w.purchaseCurrency == CurrencyType.SoftCurrency)
                    total += w.softCurrencyPrice;
            }
            return total;
        }

        public int CalculateTotalOutfitSoftCost()
        {
            int total = 0;
            foreach (var o in outfits)
            {
                if (o != null && o.purchaseCurrency == CurrencyType.SoftCurrency)
                    total += o.softCurrencyPrice;
            }
            return total;
        }

        public List<string> ValidateCatalog()
        {
            var errors = new List<string>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var w in weapons)
            {
                if (w == null)
                {
                    errors.Add("Null weapon entry in catalog.");
                    continue;
                }
                if (string.IsNullOrEmpty(w.id)) errors.Add($"Weapon with numericId {w.numericId} has empty ID.");
                else if (!seenIds.Add(w.id)) errors.Add($"Duplicate weapon ID: {w.id}");

                if (w.softCurrencyPrice < 0) errors.Add($"Weapon {w.id} has negative soft price.");
                if (w.itemPrefab == null) errors.Add($"Weapon {w.id} is missing its 3D itemPrefab reference.");
            }

            foreach (var o in outfits)
            {
                if (o == null)
                {
                    errors.Add("Null outfit entry in catalog.");
                    continue;
                }
                if (string.IsNullOrEmpty(o.id)) errors.Add($"Outfit with numericId {o.numericId} has empty ID.");
                else if (!seenIds.Add(o.id)) errors.Add($"Duplicate outfit ID: {o.id}");

                if (o.softCurrencyPrice < 0) errors.Add($"Outfit {o.id} has negative soft price.");
                if (o.itemPrefab == null) errors.Add($"Outfit {o.id} is missing its 3D itemPrefab reference.");
            }

            foreach (var b in bundles)
            {
                if (b == null)
                {
                    errors.Add("Null bundle entry in catalog.");
                    continue;
                }
                if (string.IsNullOrEmpty(b.productId)) errors.Add($"Bundle '{b.bundleName}' has empty productId.");
                if (b.includedItems == null || b.includedItems.Count == 0) errors.Add($"Bundle '{b.productId}' has no included items.");
            }

            return errors;
        }
    }
}
