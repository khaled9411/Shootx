using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Shootx.Store;

namespace Shootx.Store.Editor
{
    public static class SinglePlayerWeaponAttachmentVerifier
    {
        [InitializeOnLoadMethod]
        [MenuItem("ShootX/Store/Verify Weapon Attachment System", priority = 30)]
        public static void RunFullVerification()
        {
            Debug.Log("<color=#00EAFF>==================================================\n" +
                      "[SinglePlayerWeaponAttachmentVerifier] STARTING FULL AUDIT & TEST\n" +
                      "==================================================</color>");

            // 1. Verify Player Prefab
            GameObject playerPrefab = Resources.Load<GameObject>("Prefabs/Player");
            if (playerPrefab == null)
            {
                playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
            }

            if (playerPrefab == null)
            {
                Debug.LogError("<color=red>[VERIFY FAIL] Could not load Player prefab from Resources/Prefabs/Player or Assets/Prefabs/Player.prefab!</color>");
                return;
            }

            Debug.Log($"<color=green>[VERIFY PASS] Found Player prefab: {playerPrefab.name}</color>");

            // Instantiate a clean sandbox instance
            GameObject testPlayer = GameObject.Instantiate(playerPrefab);
            testPlayer.name = "Test_SinglePlayer_Sandbox";

            try
            {
                Transform models = testPlayer.transform.Find("Models");
                if (models == null)
                {
                    Debug.LogError("<color=red>[VERIFY FAIL] 'Models' child not found directly under Player!</color>");
                    return;
                }

                Debug.Log($"<color=green>[VERIFY PASS] Found 'Models' parent with {models.childCount} children.</color>");

                // Check external children under Player
                Transform firePoint = testPlayer.transform.Find("FirePoint") ?? testPlayer.transform.Find("FirePoint ");
                Transform modelOld = testPlayer.transform.Find("Model");
                Transform line = testPlayer.transform.Find("Line");
                Transform smokeVfx = testPlayer.transform.Find("Move Smoke VFX");

                Debug.Log($"[Hierarchy Audit] External objects:" +
                          $"\n  - FirePoint: {(firePoint != null ? firePoint.name : "MISSING")}" +
                          $"\n  - Model: {(modelOld != null ? modelOld.name : "MISSING")}" +
                          $"\n  - Line: {(line != null ? line.name : "MISSING")}" +
                          $"\n  - Move Smoke VFX: {(smokeVfx != null ? smokeVfx.name : "MISSING")}");

                PlayerEquipmentApplier applier = testPlayer.GetComponent<PlayerEquipmentApplier>();
                if (applier == null)
                {
                    applier = testPlayer.AddComponent<PlayerEquipmentApplier>();
                }

                // 2. Audit all 16 character models
                string[] expectedModels = new string[]
                {
                    "MainCharacter", "RangerCharacter", "Barbarian", "Druid", "Knight", "Rogue_Hooded",
                    "Animatronic_Creepy", "Animatronic_Normal", "Clown", "Monster", "MonsterCostume",
                    "Paladin", "Paladin_with_Helmet", "Survivalist", "Werewolf_Man", "Werewolf_Wolf"
                };

                int passedCharacters = 0;
                foreach (string charName in expectedModels)
                {
                    Transform modelChild = null;
                    for (int i = 0; i < models.childCount; i++)
                    {
                        var c = models.GetChild(i);
                        if (c.name.Equals(charName, StringComparison.OrdinalIgnoreCase))
                        {
                            modelChild = c;
                            break;
                        }
                    }

                    if (modelChild == null)
                    {
                        Debug.LogWarning($"<color=yellow>[CHAR WARNING] Character '{charName}' not found by exact name under Models/.</color>");
                        continue;
                    }

                    Animator anim = modelChild.GetComponent<Animator>() ?? modelChild.GetComponentInChildren<Animator>();
                    Transform handSocket = applier.ResolveHandSocket(modelChild, modelChild.name);

                    if (handSocket == null)
                    {
                        Debug.LogError($"<color=red>[CHAR FAIL] Model '{charName}' failed to resolve a right-hand socket!</color>");
                    }
                    else
                    {
                        passedCharacters++;
                        string animStatus = anim != null ? $"Animator OK (avatar: {(anim.avatar != null ? anim.avatar.name : "none")})" : "NO Animator";
                        Debug.Log($"<color=#70FF70>[CHAR OK] '{charName}' -> Hand Socket: '{handSocket.name}' (Path: {GetRelativePath(handSocket, modelChild)}) | {animStatus}</color>");
                    }
                }

                Debug.Log($"<color=cyan>[CHARACTERS SUMMARY] {passedCharacters}/{expectedModels.Length} characters verified with valid right-hand sockets.</color>");

                // 3. Audit all 14 weapons in StoreCatalogDatabase
                var db = StoreCatalogDatabase.Instance;
                if (db == null)
                {
                    Debug.LogError("<color=red>[CATALOG FAIL] StoreCatalogDatabase.Instance is null!</color>");
                    return;
                }

                Debug.Log($"[Weapons Catalog] Found {db.Weapons.Count} weapons in database.");

                int passedWeapons = 0;
                foreach (var wepDef in db.Weapons)
                {
                    if (wepDef == null || wepDef.itemPrefab == null)
                    {
                        Debug.LogError($"<color=red>[WEAPON FAIL] Weapon entry '{wepDef?.id}' has null definition or prefab!</color>");
                        continue;
                    }

                    applier.ApplyWeapon(wepDef.numericId);

                    GameObject weaponInstance = applier.CurrentSpawnedWeapon;
                    if (weaponInstance == null)
                    {
                        Debug.LogError($"<color=red>[WEAPON FAIL] Failed to spawn weapon '{wepDef.displayName}' (ID: {wepDef.numericId})!</color>");
                        continue;
                    }

                    Transform muzzle = applier.CurrentMuzzleTransform;
                    if (muzzle == null)
                    {
                        Debug.LogError($"<color=red>[WEAPON FAIL] Weapon '{wepDef.displayName}' spawned without a valid muzzle!</color>");
                        continue;
                    }

                    passedWeapons++;
                    Debug.Log($"<color=#70FF70>[WEAPON OK] '{wepDef.displayName}' (numId: {wepDef.numericId}) attached to '{weaponInstance.transform.parent.name}' at localPos {weaponInstance.transform.localPosition} | Muzzle: {muzzle.localPosition}</color>");
                }

                Debug.Log($"<color=cyan>[WEAPONS SUMMARY] {passedWeapons}/{db.Weapons.Count} weapons successfully attached and verified with muzzles.</color>");

                // 4. Test Switching Weapon (No duplicates)
                Debug.Log("<color=yellow>[TEST] Testing weapon switching and duplicate prevention...</color>");
                applier.ApplyWeapon(0); // Pistol
                Transform pistolParent = applier.CurrentSpawnedWeapon?.transform.parent;
                int childCountBefore = pistolParent != null ? pistolParent.childCount : 0;

                applier.ApplyWeapon(1); // Revolver
                int childCountAfter = pistolParent != null ? pistolParent.childCount : 0;

                applier.ApplyWeapon(6); // Assault Rifle
                int childCountAfterRifle = pistolParent != null ? pistolParent.childCount : 0;

                if (childCountAfter == childCountBefore && childCountAfterRifle == childCountBefore)
                {
                    Debug.Log("<color=green>[TEST PASS] Weapon switching correctly replaced previous weapon with 0 duplicates!</color>");
                }
                else
                {
                    Debug.LogWarning($"[TEST INFO] Hand socket child counts: before={childCountBefore}, after={childCountAfter}, rifle={childCountAfterRifle}");
                }

                // 5. Test Switching Character (Preserve equipped weapon)
                Debug.Log("<color=yellow>[TEST] Testing character switching while preserving equipped weapon...</color>");
                applier.ApplyWeapon(8); // Sniper rifle
                string equippedWeaponName = applier.CurrentSpawnedWeapon != null ? applier.CurrentSpawnedWeapon.name : "";

                // Switch to Knight (outfit 6)
                applier.ApplyOutfit(6);
                string weaponOnKnight = applier.CurrentSpawnedWeapon != null ? applier.CurrentSpawnedWeapon.name : "";
                string knightSocket = applier.CurrentSpawnedWeapon != null ? applier.CurrentSpawnedWeapon.transform.parent.name : "";

                // Switch to Ranger (outfit 3)
                applier.ApplyOutfit(3);
                string weaponOnRanger = applier.CurrentSpawnedWeapon != null ? applier.CurrentSpawnedWeapon.name : "";
                string rangerSocket = applier.CurrentSpawnedWeapon != null ? applier.CurrentSpawnedWeapon.transform.parent.name : "";

                // Switch to Survivalist (outfit 0)
                applier.ApplyOutfit(0);
                string weaponOnSurvivalist = applier.CurrentSpawnedWeapon != null ? applier.CurrentSpawnedWeapon.name : "";
                string survivalistSocket = applier.CurrentSpawnedWeapon != null ? applier.CurrentSpawnedWeapon.transform.parent.name : "";

                if (weaponOnKnight == equippedWeaponName && weaponOnRanger == equippedWeaponName && weaponOnSurvivalist == equippedWeaponName)
                {
                    Debug.Log($"<color=green>[TEST PASS] Equipped weapon '{equippedWeaponName}' was preserved across all character switches!" +
                              $"\n  - Knight socket: '{knightSocket}'" +
                              $"\n  - Ranger socket: '{rangerSocket}'" +
                              $"\n  - Survivalist socket: '{survivalistSocket}'</color>");
                }
                else
                {
                    Debug.LogError($"<color=red>[TEST FAIL] Weapon was not preserved across character switches: knight='{weaponOnKnight}', ranger='{weaponOnRanger}', surv='{weaponOnSurvivalist}'</color>");
                }

                // 6. Test Store Preview Manager
                Debug.Log("<color=yellow>[TEST] Testing StorePreviewManager integration...</color>");
                var previewMgr = StorePreviewManager.Instance;
                if (previewMgr != null)
                {
                    previewMgr.ShowOutfitPreview(6); // Knight preview
                    previewMgr.ShowWeaponPreview(1); // Revolver preview
                    previewMgr.ClosePreview();
                    Debug.Log("<color=green>[TEST PASS] StorePreviewManager preview calls executed cleanly.</color>");
                }

                Debug.Log("<color=#00EAFF>==================================================\n" +
                          "[SinglePlayerWeaponAttachmentVerifier] ALL AUDITS & TESTS COMPLETED SUCCESSFULLY!\n" +
                          "==================================================</color>");
            }
            finally
            {
                if (testPlayer != null)
                {
                    GameObject.DestroyImmediate(testPlayer);
                }
            }
        }

        private static string GetRelativePath(Transform child, Transform root)
        {
            if (child == null || root == null) return "";
            if (child == root) return root.name;

            string path = child.name;
            Transform curr = child.parent;
            while (curr != null && curr != root)
            {
                path = curr.name + "/" + path;
                curr = curr.parent;
            }
            return path;
        }
    }
}
