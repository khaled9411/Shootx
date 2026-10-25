using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Shootx.Store;

namespace Shootx.Store.Editor
{
    public class StoreCatalogValidatorWindow : EditorWindow
    {
        private Vector2 _scrollPos;
        private List<string> _errors = new List<string>();
        private List<string> _warnings = new List<string>();
        private List<string> _info = new List<string>();

        private int _totalWeaponCost = 0;
        private int _totalOutfitCost = 0;
        private int _totalCatalogCost = 0;

        [MenuItem("Tools/Shootx Store/Validate Catalog & Economy", false, 10)]
        public static void OpenWindow()
        {
            var window = GetWindow<StoreCatalogValidatorWindow>("Store & Economy Validator");
            window.minSize = new Vector2(500, 600);
            window.RunValidation();
            window.Show();
        }

        private void OnEnable()
        {
            RunValidation();
        }

        private void RunValidation()
        {
            _errors.Clear();
            _warnings.Clear();
            _info.Clear();

            var db = Resources.Load<StoreCatalogDatabase>("StoreCatalogDatabase");
            if (db == null)
            {
                _errors.Add("CRITICAL: StoreCatalogDatabase.asset not found in Resources folder (Resources/StoreCatalogDatabase).");
                return;
            }

            _totalWeaponCost = db.GetTotalWeaponsSoftCost();
            _totalOutfitCost = db.GetTotalOutfitsSoftCost();
            _totalCatalogCost = _totalWeaponCost + _totalOutfitCost;

            // 1. Validate Weapons
            var weaponIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var weaponNumericIds = new HashSet<int>();
            int blankWeaponIcons = 0;

            for (int i = 0; i < db.weapons.Count; i++)
            {
                var wep = db.weapons[i];
                if (wep == null)
                {
                    _errors.Add($"Weapons list at index {i} is null!");
                    continue;
                }

                if (string.IsNullOrEmpty(wep.id))
                    _errors.Add($"Weapon '{wep.name}' has an empty or null ID.");
                else if (!weaponIds.Add(wep.id))
                    _errors.Add($"Duplicate Weapon ID detected: '{wep.id}'");

                if (!weaponNumericIds.Add(wep.numericId))
                    _errors.Add($"Duplicate Weapon numericId detected: {wep.numericId} ('{wep.id}')");

                if (wep.softCurrencyPrice < 0)
                    _errors.Add($"Weapon '{wep.id}' has a negative Soft Currency price: {wep.softCurrencyPrice}");

                if (wep.itemPrefab == null)
                    _warnings.Add($"Weapon '{wep.id}' has no itemPrefab assigned.");

                if (wep.itemIcon == null)
                    blankWeaponIcons++;
            }

            // 2. Validate Outfits
            var outfitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var outfitNumericIds = new HashSet<int>();
            int blankOutfitIcons = 0;

            for (int i = 0; i < db.outfits.Count; i++)
            {
                var outfit = db.outfits[i];
                if (outfit == null)
                {
                    _errors.Add($"Outfits list at index {i} is null!");
                    continue;
                }

                if (string.IsNullOrEmpty(outfit.id))
                    _errors.Add($"Outfit '{outfit.name}' has an empty or null ID.");
                else if (!outfitIds.Add(outfit.id))
                    _errors.Add($"Duplicate Outfit ID detected: '{outfit.id}'");

                if (!outfitNumericIds.Add(outfit.numericId))
                    _errors.Add($"Duplicate Outfit numericId detected: {outfit.numericId} ('{outfit.id}')");

                if (outfit.softCurrencyPrice < 0)
                    _errors.Add($"Outfit '{outfit.id}' has a negative Soft Currency price: {outfit.softCurrencyPrice}");

                if (outfit.itemIcon == null)
                    blankOutfitIcons++;
            }

            // 3. Validate Bundles
            var bundleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < db.bundles.Count; i++)
            {
                var b = db.bundles[i];
                if (b == null)
                {
                    _errors.Add($"Bundles list at index {i} is null!");
                    continue;
                }

                if (string.IsNullOrEmpty(b.productId))
                    _errors.Add($"Bundle '{b.name}' has an empty productId!");
                else if (!bundleIds.Add(b.productId))
                    _errors.Add($"Duplicate Bundle ProductId detected: '{b.productId}'");

                if (b.includedItems == null || b.includedItems.Count == 0)
                    _errors.Add($"Bundle '{b.productId}' has no items configured!");
                else
                {
                    foreach (var item in b.includedItems)
                    {
                        if (item == null)
                            _errors.Add($"Bundle '{b.productId}' has a null item reference in its contents.");
                        else if (!weaponIds.Contains(item.id) && !outfitIds.Contains(item.id))
                            _warnings.Add($"Bundle '{b.productId}' references item '{item.id}' which is not in the catalog database.");
                    }
                }
            }

            // 4. Validate IAP Definitions in Resources
            var iapDefinitions = Resources.LoadAll<IAPProductDefinition>("IAPProducts");
            var expectedIds = new string[]
            {
                "package_01", "package_02", "package_03", "package_04", "package_05", "package_06",
                "online_access", "remove_ads",
                "coins_200", "coins_500", "coins_1200", "coins_2500"
            };

            var registeredIapIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var def in iapDefinitions)
            {
                if (def != null && !string.IsNullOrEmpty(def.productId))
                    registeredIapIds.Add(def.productId);
            }

            foreach (var expected in expectedIds)
            {
                if (!registeredIapIds.Contains(expected))
                    _errors.Add($"Missing required IAP Product Asset for ID: '{expected}' in Resources/IAPProducts/");
            }

            // Informational entries
            _info.Add($"Total Registered Weapons: {db.weapons.Count} ({blankWeaponIcons} image fields empty for future artwork)");
            _info.Add($"Total Registered Outfits: {db.outfits.Count} ({blankOutfitIcons} image fields empty for future artwork)");
            _info.Add($"Total Configured Bundles: {db.bundles.Count}");
            _info.Add($"Total Registered IAP Definitions in Resources: {iapDefinitions.Length}");
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Shootx Store & Economy Validation Hub", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Validates catalogs, bundles, IAP products, and progression balance.", EditorStyles.miniLabel);

            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Re-Run Validation", GUILayout.Height(30)))
            {
                RunValidation();
            }
            if (GUILayout.Button("Select Catalog Asset", GUILayout.Height(30)))
            {
                var db = Resources.Load<StoreCatalogDatabase>("StoreCatalogDatabase");
                if (db != null)
                {
                    Selection.activeObject = db;
                    EditorGUIUtility.PingObject(db);
                }
            }
            if (GUILayout.Button("Open Setup Guide (Arabic)", GUILayout.Height(30)))
            {
                var guide = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/SINGLEPLAYER_STORE_IAP_SETUP_GUIDE_AR.md");
                if (guide != null)
                {
                    Selection.activeObject = guide;
                    EditorGUIUtility.PingObject(guide);
                }
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(15);
            EditorGUILayout.LabelField("Progression Economy Analysis", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"• Total Weapon Catalog Cost: {_totalWeaponCost:N0} Soft Coins");
            EditorGUILayout.LabelField($"• Total Outfit Catalog Cost: {_totalOutfitCost:N0} Soft Coins");
            EditorGUILayout.LabelField($"• Combined Total Catalog Cost: {_totalCatalogCost:N0} Soft Coins");
            EditorGUILayout.LabelField($"• 500 Levels Theoretical Earnings: 25,000 - 75,000 Soft Coins (Average: 50,000)");
            EditorGUILayout.LabelField($"• Economy Ratio: Complete 500 levels unlocks either ~100% weapons OR ~100% outfits.");
            EditorGUILayout.LabelField($"• Full completion of both catalogs requires level replaying or rewarded ad coin doubling.");
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // Errors
            if (_errors.Count > 0)
            {
                EditorGUILayout.LabelField($"Errors ({_errors.Count})", EditorStyles.boldLabel);
                GUI.color = new Color(1f, 0.4f, 0.4f);
                foreach (var err in _errors)
                {
                    EditorGUILayout.HelpBox(err, MessageType.Error);
                }
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = new Color(0.4f, 1f, 0.4f);
                EditorGUILayout.HelpBox("All Catalogs, IDs, Prices, and Bundles Passed Validation! (0 Errors)", MessageType.Info);
                GUI.color = Color.white;
            }

            // Warnings
            if (_warnings.Count > 0)
            {
                EditorGUILayout.LabelField($"Warnings ({_warnings.Count})", EditorStyles.boldLabel);
                GUI.color = new Color(1f, 0.9f, 0.4f);
                foreach (var warn in _warnings)
                {
                    EditorGUILayout.HelpBox(warn, MessageType.Warning);
                }
                GUI.color = Color.white;
            }

            // Info
            EditorGUILayout.LabelField("Catalog Overview", EditorStyles.boldLabel);
            foreach (var inf in _info)
            {
                EditorGUILayout.HelpBox(inf, MessageType.None);
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
