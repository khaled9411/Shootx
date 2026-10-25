using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Purchasing;
using Shootx.Store;

namespace Shootx.Store.Editor
{
    [InitializeOnLoad]
    public static class StoreCatalogGenerator
    {
        private const string StoreDataDir = "Assets/Data/Store";
        private const string WeaponsDataDir = "Assets/Data/Store/Weapons";
        private const string OutfitsDataDir = "Assets/Data/Store/Outfits";
        private const string BundlesDataDir = "Assets/Data/Store/Bundles";
        private const string IAPProductsDir = "Assets/Resources/IAPProducts";

        static StoreCatalogGenerator()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists($"{StoreDataDir}/StoreCatalogDatabase.asset"))
                {
                    GenerateStoreAssets();
                }
            };
        }

        [MenuItem("ShootX/Store/Generate All Store & IAP Assets")]
        public static void GenerateStoreAssets()
        {
            EnsureDirectories();

            var weapons = GenerateWeapons();
            var outfits = GenerateOutfits();
            var bundles = GenerateBundles(weapons, outfits);
            GenerateIAPDefinitions(weapons, outfits);
            GenerateDatabase(weapons, outfits, bundles);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=#00FF88>[StoreCatalogGenerator] Successfully generated all 14 Weapon definitions, 13 Outfit definitions, 6 Bundle definitions, 12 IAP Product definitions, and StoreCatalogDatabase!</color>");
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(StoreDataDir)) Directory.CreateDirectory(StoreDataDir);
            if (!Directory.Exists(WeaponsDataDir)) Directory.CreateDirectory(WeaponsDataDir);
            if (!Directory.Exists(OutfitsDataDir)) Directory.CreateDirectory(OutfitsDataDir);
            if (!Directory.Exists(BundlesDataDir)) Directory.CreateDirectory(BundlesDataDir);
            if (!Directory.Exists(IAPProductsDir)) Directory.CreateDirectory(IAPProductsDir);
        }

        private static List<StoreItemDefinition> GenerateWeapons()
        {
            var list = new List<StoreItemDefinition>();

            var weaponConfigs = new[]
            {
                new { id = "wep_pistol_01", num = 0, name = "Standard Pistol", rarity = ItemRarity.Common, price = 0, defOwned = true, prefab = "SM_Wep_Pistol_01" },
                new { id = "wep_revolver_01", num = 1, name = "Heavy Revolver", rarity = ItemRarity.Common, price = 400, defOwned = false, prefab = "SM_Wep_Revolver_01" },
                new { id = "wep_machine_pistol_gen1", num = 2, name = "Machine Pistol Gen 1", rarity = ItemRarity.Common, price = 600, defOwned = false, prefab = "SM_Wep_MachinePistol_Gen1_01" },
                new { id = "wep_machine_pistol_gen2", num = 3, name = "Machine Pistol Gen 2", rarity = ItemRarity.Uncommon, price = 1000, defOwned = false, prefab = "SM_Wep_MachinePistol_Gen2_01" },
                new { id = "wep_rifle_small_01", num = 4, name = "Tactical Compact Rifle", rarity = ItemRarity.Uncommon, price = 1200, defOwned = false, prefab = "SM_Wep_Rifle_Small_01" },
                new { id = "wep_smg_01", num = 5, name = "Rapid SMG", rarity = ItemRarity.Uncommon, price = 1500, defOwned = false, prefab = "SM_Wep_SMG_01" },
                new { id = "wep_rifle_base_01", num = 6, name = "Assault Rifle", rarity = ItemRarity.Rare, price = 2000, defOwned = false, prefab = "SM_Wep_Rifle_Base_01" },
                new { id = "wep_syringe_gun_01", num = 7, name = "Bio Syringe Gun", rarity = ItemRarity.Rare, price = 2500, defOwned = false, prefab = "SM_Wep_Syringe_Gun_01" },
                new { id = "wep_sniper_01", num = 8, name = "Marksman Sniper Rifle", rarity = ItemRarity.Rare, price = 2800, defOwned = false, prefab = "SM_Wep_Sniper_01" },
                new { id = "wep_rifle_laser_01", num = 9, name = "High-Tech Laser Rifle", rarity = ItemRarity.Epic, price = 4000, defOwned = false, prefab = "SM_Wep_Rifle_Laser_01" },
                new { id = "wep_rifle_plasma_01", num = 10, name = "Plasma Rifle Alpha", rarity = ItemRarity.Epic, price = 4500, defOwned = false, prefab = "SM_Wep_Rifle_Plasma_01" },
                new { id = "wep_rifle_plasma_02", num = 11, name = "Plasma Rifle Beta", rarity = ItemRarity.Epic, price = 5000, defOwned = false, prefab = "SM_Wep_Rifle_Plasma_02" },
                new { id = "wep_rifle_plasma_03", num = 12, name = "Plasma Rifle Omega", rarity = ItemRarity.Legendary, price = 7500, defOwned = false, prefab = "SM_Wep_Rifle_Plasma_03" },
                new { id = "wep_sniper_plasma_02", num = 13, name = "Heavy Plasma Sniper", rarity = ItemRarity.Legendary, price = 9000, defOwned = false, prefab = "SM_Wep_Sniper_Plasma_02" }
            };

            foreach (var cfg in weaponConfigs)
            {
                string assetPath = $"{WeaponsDataDir}/{cfg.id}.asset";
                StoreItemDefinition item = AssetDatabase.LoadAssetAtPath<StoreItemDefinition>(assetPath);
                if (item == null)
                {
                    item = ScriptableObject.CreateInstance<StoreItemDefinition>();
                    AssetDatabase.CreateAsset(item, assetPath);
                }

                item.id = cfg.id;
                item.numericId = cfg.num;
                item.displayName = cfg.name;
                item.itemType = StoreItemType.Weapon;
                item.rarity = cfg.rarity;
                item.purchaseCurrency = CurrencyType.SoftCurrency;
                item.softCurrencyPrice = cfg.price;
                item.hardCurrencyPrice = 0;
                item.isDefaultOwned = cfg.defOwned;

                string prefabPath = $"Assets/Store/Weapons/{cfg.prefab}.prefab";
                item.itemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                item.itemIcon = null; // Blank as required

                EditorUtility.SetDirty(item);
                list.Add(item);
            }

            return list;
        }

        private static List<StoreItemDefinition> GenerateOutfits()
        {
            var list = new List<StoreItemDefinition>();

            var outfitConfigs = new[]
            {
                new { id = "outfit_survivalist", num = 0, name = "Survivalist", rarity = ItemRarity.Common, price = 0, defOwned = true, prefab = "Survivalist" },
                new { id = "outfit_monster_costume", num = 1, name = "Monster Costume", rarity = ItemRarity.Common, price = 500, defOwned = false, prefab = "MonsterCostume" },
                new { id = "outfit_clown", num = 2, name = "Clown", rarity = ItemRarity.Common, price = 700, defOwned = false, prefab = "Clown" },
                new { id = "outfit_ranger", num = 3, name = "Ranger", rarity = ItemRarity.Uncommon, price = 1100, defOwned = false, prefab = "Ranger" },
                new { id = "outfit_rogue_hooded", num = 4, name = "Hooded Rogue", rarity = ItemRarity.Uncommon, price = 1400, defOwned = false, prefab = "Rogue_Hooded" },
                new { id = "outfit_druid", num = 5, name = "Forest Druid", rarity = ItemRarity.Rare, price = 2200, defOwned = false, prefab = "Druid" },
                new { id = "outfit_knight", num = 6, name = "Armored Knight", rarity = ItemRarity.Rare, price = 2600, defOwned = false, prefab = "Knight" },
                new { id = "outfit_monster", num = 7, name = "Underworld Monster", rarity = ItemRarity.Rare, price = 2900, defOwned = false, prefab = "Monster" },
                new { id = "outfit_paladin", num = 8, name = "Holy Paladin", rarity = ItemRarity.Epic, price = 4200, defOwned = false, prefab = "Paladin" },
                new { id = "outfit_animatronic_normal", num = 9, name = "Animatronic Normal", rarity = ItemRarity.Epic, price = 4800, defOwned = false, prefab = "Animatronic_Normal" },
                new { id = "outfit_werewolf_wolf", num = 10, name = "Feral Werewolf", rarity = ItemRarity.Epic, price = 5500, defOwned = false, prefab = "Werewolf_Wolf" },
                new { id = "outfit_paladin_with_helmet", num = 11, name = "Vanguard Paladin", rarity = ItemRarity.Legendary, price = 7500, defOwned = false, prefab = "Paladin_with_Helmet" },
                new { id = "outfit_animatronic_creepy", num = 12, name = "Creepy Animatronic", rarity = ItemRarity.Legendary, price = 8500, defOwned = false, prefab = "Animatronic_Creepy" }
            };

            foreach (var cfg in outfitConfigs)
            {
                string assetPath = $"{OutfitsDataDir}/{cfg.id}.asset";
                StoreItemDefinition item = AssetDatabase.LoadAssetAtPath<StoreItemDefinition>(assetPath);
                if (item == null)
                {
                    item = ScriptableObject.CreateInstance<StoreItemDefinition>();
                    AssetDatabase.CreateAsset(item, assetPath);
                }

                item.id = cfg.id;
                item.numericId = cfg.num;
                item.displayName = cfg.name;
                item.itemType = StoreItemType.Outfit;
                item.rarity = cfg.rarity;
                item.purchaseCurrency = CurrencyType.SoftCurrency;
                item.softCurrencyPrice = cfg.price;
                item.hardCurrencyPrice = 0;
                item.isDefaultOwned = cfg.defOwned;

                string prefabPath = $"Assets/Store/Outfits/{cfg.prefab}.prefab";
                item.itemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                item.itemIcon = null;

                EditorUtility.SetDirty(item);
                list.Add(item);
            }

            return list;
        }

        private static List<BundleDefinition> GenerateBundles(List<StoreItemDefinition> weapons, List<StoreItemDefinition> outfits)
        {
            var list = new List<BundleDefinition>();

            var bundleConfigs = new[]
            {
                new {
                    pid = "package_01",
                    name = "Starter Pack",
                    desc = "A balanced entry bundle containing starter weapons and outfits.",
                    weps = new[] { "wep_revolver_01", "wep_machine_pistol_gen1" },
                    outs = new[] { "outfit_monster_costume", "outfit_clown" }
                },
                new {
                    pid = "package_02",
                    name = "Advanced Pack",
                    desc = "A solid collection of mid-tier weapons and outfits.",
                    weps = new[] { "wep_machine_pistol_gen2", "wep_rifle_small_01" },
                    outs = new[] { "outfit_ranger", "outfit_rogue_hooded" }
                },
                new {
                    pid = "package_03",
                    name = "Elite Pack",
                    desc = "High-tier combat equipment and elite outfits.",
                    weps = new[] { "wep_smg_01", "wep_rifle_base_01", "wep_syringe_gun_01" },
                    outs = new[] { "outfit_druid", "outfit_knight" }
                },
                new {
                    pid = "package_04",
                    name = "Character Pack",
                    desc = "Exclusive character visuals and costumes.",
                    weps = new string[0],
                    outs = new[] { "outfit_monster", "outfit_paladin", "outfit_animatronic_normal", "outfit_werewolf_wolf" }
                },
                new {
                    pid = "package_05",
                    name = "Legendary Pack",
                    desc = "Top-tier legendary weapons and prestigious outfits.",
                    weps = new[] { "wep_rifle_laser_01", "wep_rifle_plasma_01", "wep_rifle_plasma_03" },
                    outs = new[] { "outfit_paladin_with_helmet", "outfit_animatronic_creepy" }
                },
                new {
                    pid = "package_06",
                    name = "Legendary Weapons Pack",
                    desc = "An exclusive armory of top-tier legendary weapons.",
                    weps = new[] { "wep_sniper_01", "wep_rifle_plasma_02", "wep_sniper_plasma_02" },
                    outs = new string[0]
                }
            };

            foreach (var cfg in bundleConfigs)
            {
                string assetPath = $"{BundlesDataDir}/{cfg.pid}.asset";
                BundleDefinition bundle = AssetDatabase.LoadAssetAtPath<BundleDefinition>(assetPath);
                if (bundle == null)
                {
                    bundle = ScriptableObject.CreateInstance<BundleDefinition>();
                    AssetDatabase.CreateAsset(bundle, assetPath);
                }

                bundle.productId = cfg.pid;
                bundle.bundleName = cfg.name;
                bundle.description = cfg.desc;
                bundle.bundleIcon = null;
                bundle.includedItems.Clear();

                foreach (var wId in cfg.weps)
                {
                    var found = weapons.Find(w => w.id == wId);
                    if (found != null) bundle.includedItems.Add(found);
                }

                foreach (var oId in cfg.outs)
                {
                    var found = outfits.Find(o => o.id == oId);
                    if (found != null) bundle.includedItems.Add(found);
                }

                EditorUtility.SetDirty(bundle);
                list.Add(bundle);
            }

            return list;
        }

        private static void GenerateIAPDefinitions(List<StoreItemDefinition> weapons, List<StoreItemDefinition> outfits)
        {
            var iapConfigs = new[]
            {
                new { id = "coins_200", type = ProductType.Consumable, coins = 200, noAds = false, online = false, wep = new int[0], outf = new int[0] },
                new { id = "coins_500", type = ProductType.Consumable, coins = 500, noAds = false, online = false, wep = new int[0], outf = new int[0] },
                new { id = "coins_1200", type = ProductType.Consumable, coins = 1200, noAds = false, online = false, wep = new int[0], outf = new int[0] },
                new { id = "coins_2500", type = ProductType.Consumable, coins = 2500, noAds = false, online = false, wep = new int[0], outf = new int[0] },

                new { id = "online_access", type = ProductType.NonConsumable, coins = 0, noAds = false, online = true, wep = new int[0], outf = new int[0] },
                new { id = "remove_ads", type = ProductType.NonConsumable, coins = 0, noAds = true, online = true, wep = new int[0], outf = new int[0] },

                new { id = "package_01", type = ProductType.NonConsumable, coins = 0, noAds = false, online = false, wep = new[] { 1, 2 }, outf = new[] { 1, 2 } },
                new { id = "package_02", type = ProductType.NonConsumable, coins = 0, noAds = false, online = false, wep = new[] { 3, 4 }, outf = new[] { 3, 4 } },
                new { id = "package_03", type = ProductType.NonConsumable, coins = 0, noAds = false, online = false, wep = new[] { 5, 6, 7 }, outf = new[] { 5, 6 } },
                new { id = "package_04", type = ProductType.NonConsumable, coins = 0, noAds = false, online = false, wep = new int[0], outf = new[] { 7, 8, 9, 10 } },
                new { id = "package_05", type = ProductType.NonConsumable, coins = 0, noAds = false, online = false, wep = new[] { 9, 10, 12 }, outf = new[] { 11, 12 } },
                new { id = "package_06", type = ProductType.NonConsumable, coins = 0, noAds = false, online = false, wep = new[] { 8, 11, 13 }, outf = new int[0] }
            };

            foreach (var cfg in iapConfigs)
            {
                string path = $"{IAPProductsDir}/{cfg.id}.asset";
                IAPProductDefinition def = AssetDatabase.LoadAssetAtPath<IAPProductDefinition>(path);
                if (def == null)
                {
                    def = ScriptableObject.CreateInstance<IAPProductDefinition>();
                    AssetDatabase.CreateAsset(def, path);
                }

                def.productId = cfg.id;
                def.productType = cfg.type;
                def.hardCoinsAmount = cfg.coins;
                def.removesAds = cfg.noAds;
                def.unlocksMultiplayer = cfg.online;
                def.unlockGunIds = new List<int>(cfg.wep);
                def.unlockOutfitIds = new List<int>(cfg.outf);

                EditorUtility.SetDirty(def);
            }
        }

        private static void GenerateDatabase(List<StoreItemDefinition> weapons, List<StoreItemDefinition> outfits, List<BundleDefinition> bundles)
        {
            string dbPath = $"{StoreDataDir}/StoreCatalogDatabase.asset";
            StoreCatalogDatabase db = AssetDatabase.LoadAssetAtPath<StoreCatalogDatabase>(dbPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<StoreCatalogDatabase>();
                AssetDatabase.CreateAsset(db, dbPath);
            }

            // Using SerializedObject to safely assign serialized private fields
            SerializedObject so = new SerializedObject(db);
            SerializedProperty wepProp = so.FindProperty("weapons");
            SerializedProperty outProp = so.FindProperty("outfits");
            SerializedProperty bunProp = so.FindProperty("bundles");

            wepProp.ClearArray();
            for (int i = 0; i < weapons.Count; i++)
            {
                wepProp.InsertArrayElementAtIndex(i);
                wepProp.GetArrayElementAtIndex(i).objectReferenceValue = weapons[i];
            }

            outProp.ClearArray();
            for (int i = 0; i < outfits.Count; i++)
            {
                outProp.InsertArrayElementAtIndex(i);
                outProp.GetArrayElementAtIndex(i).objectReferenceValue = outfits[i];
            }

            bunProp.ClearArray();
            for (int i = 0; i < bundles.Count; i++)
            {
                bunProp.InsertArrayElementAtIndex(i);
                bunProp.GetArrayElementAtIndex(i).objectReferenceValue = bundles[i];
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(db);
        }
    }
}
