using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Shootx.Store.Editor
{
    public static class SinglePlayerAttachmentProfileGenerator
    {
        private const string ProfileResourcePath = "Assets/Resources/SinglePlayerWeaponAttachmentProfile.asset";

        [MenuItem("ShootX/Store/Generate Single-Player Attachment Profile", priority = 25)]
        public static void GenerateOrUpdateProfileMenu()
        {
            GenerateOrUpdateProfile(logSuccess: true);
        }

        [InitializeOnLoadMethod]
        private static void AutoEnsureProfile()
        {
            // Run automatically on load if asset does not exist yet
            if (!File.Exists(ProfileResourcePath))
            {
                GenerateOrUpdateProfile(logSuccess: false);
            }
        }

        public static SinglePlayerWeaponAttachmentProfile GenerateOrUpdateProfile(bool logSuccess = false)
        {
            // Ensure Resources directory exists
            if (!Directory.Exists("Assets/Resources"))
            {
                Directory.CreateDirectory("Assets/Resources");
                AssetDatabase.Refresh();
            }

            SinglePlayerWeaponAttachmentProfile profile = AssetDatabase.LoadAssetAtPath<SinglePlayerWeaponAttachmentProfile>(ProfileResourcePath);

            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<SinglePlayerWeaponAttachmentProfile>();
                AssetDatabase.CreateAsset(profile, ProfileResourcePath);
            }

            // Populate all 16 character model configurations
            profile.characterSettings = new List<CharacterAttachmentSetting>
            {
                new CharacterAttachmentSetting
                {
                    characterName = "MainCharacter",
                    socketName = "Wrist_R",
                    positionOffset = new Vector3(0.05f, -0.04f, 0.12f),
                    rotationOffset = new Vector3(-90f, 0f, 90f),
                    scale = Vector3.one
                },
                new CharacterAttachmentSetting
                {
                    characterName = "RangerCharacter",
                    socketName = "handslot.r",
                    positionOffset = Vector3.zero,
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one
                },
                new CharacterAttachmentSetting
                {
                    characterName = "Barbarian",
                    socketName = "handslot.r",
                    positionOffset = Vector3.zero,
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one
                },
                new CharacterAttachmentSetting
                {
                    characterName = "Druid",
                    socketName = "handslot.r",
                    positionOffset = Vector3.zero,
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one
                },
                new CharacterAttachmentSetting
                {
                    characterName = "Knight",
                    socketName = "handslot.r",
                    positionOffset = Vector3.zero,
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one
                },
                new CharacterAttachmentSetting
                {
                    characterName = "Rogue_Hooded",
                    socketName = "handslot.r",
                    positionOffset = Vector3.zero,
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one
                },
                new CharacterAttachmentSetting
                {
                    characterName = "Animatronic_Creepy",
                    socketName = "handslot.r",
                    positionOffset = Vector3.zero,
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one
                },
                new CharacterAttachmentSetting
                {
                    characterName = "Animatronic_Normal",
                    socketName = "handslot.r",
                    positionOffset = Vector3.zero,
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one
                },
                new CharacterAttachmentSetting
                {
                    characterName = "Clown",
                    socketName = "handslot.r",
                    positionOffset = Vector3.zero,
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one
                },
                new CharacterAttachmentSetting
                {
                    characterName = "Monster",
                    socketName = "handslot.r",
                    positionOffset = Vector3.zero,
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one
                },
                new CharacterAttachmentSetting
                {
                    characterName = "MonsterCostume",
                    socketName = "handslot.r",
                    positionOffset = Vector3.zero,
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one
                },
                new CharacterAttachmentSetting
                {
                    characterName = "Paladin",
                    socketName = "handslot.r",
                    positionOffset = Vector3.zero,
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one
                },
                new CharacterAttachmentSetting
                {
                    characterName = "Paladin_with_Helmet",
                    socketName = "handslot.r",
                    positionOffset = Vector3.zero,
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one
                },
                new CharacterAttachmentSetting
                {
                    characterName = "Survivalist",
                    socketName = "handslot.r",
                    positionOffset = Vector3.zero,
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one
                },
                new CharacterAttachmentSetting
                {
                    characterName = "Werewolf_Man",
                    socketName = "handslot.r",
                    positionOffset = Vector3.zero,
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one
                },
                new CharacterAttachmentSetting
                {
                    characterName = "Werewolf_Wolf",
                    socketName = "handslot.r",
                    positionOffset = Vector3.zero,
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one
                }
            };

            // Populate all 14 weapon attachment & muzzle configurations
            profile.weaponSettings = new List<WeaponAttachmentSetting>
            {
                new WeaponAttachmentSetting
                {
                    weaponId = "wep_pistol_01",
                    characterName = "All",
                    positionOffset = Vector3.zero,
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one,
                    muzzleOffset = new Vector3(0f, 0.08f, 0.28f)
                },
                new WeaponAttachmentSetting
                {
                    weaponId = "wep_revolver_01",
                    characterName = "All",
                    positionOffset = Vector3.zero,
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one,
                    muzzleOffset = new Vector3(0f, 0.09f, 0.32f)
                },
                new WeaponAttachmentSetting
                {
                    weaponId = "wep_machine_pistol_gen1",
                    characterName = "All",
                    positionOffset = Vector3.zero,
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one,
                    muzzleOffset = new Vector3(0f, 0.08f, 0.35f)
                },
                new WeaponAttachmentSetting
                {
                    weaponId = "wep_machine_pistol_gen2",
                    characterName = "All",
                    positionOffset = Vector3.zero,
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one,
                    muzzleOffset = new Vector3(0f, 0.08f, 0.35f)
                },
                new WeaponAttachmentSetting
                {
                    weaponId = "wep_rifle_small_01",
                    characterName = "All",
                    positionOffset = new Vector3(0f, 0f, 0.02f),
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one,
                    muzzleOffset = new Vector3(0f, 0.08f, 0.50f)
                },
                new WeaponAttachmentSetting
                {
                    weaponId = "wep_smg_01",
                    characterName = "All",
                    positionOffset = Vector3.zero,
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one,
                    muzzleOffset = new Vector3(0f, 0.08f, 0.42f)
                },
                new WeaponAttachmentSetting
                {
                    weaponId = "wep_rifle_base_01",
                    characterName = "All",
                    positionOffset = new Vector3(0f, 0f, 0.03f),
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one,
                    muzzleOffset = new Vector3(0f, 0.08f, 0.70f)
                },
                new WeaponAttachmentSetting
                {
                    weaponId = "wep_syringe_gun_01",
                    characterName = "All",
                    positionOffset = Vector3.zero,
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one,
                    muzzleOffset = new Vector3(0f, 0.08f, 0.38f)
                },
                new WeaponAttachmentSetting
                {
                    weaponId = "wep_sniper_01",
                    characterName = "All",
                    positionOffset = new Vector3(0f, 0f, 0.05f),
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one,
                    muzzleOffset = new Vector3(0f, 0.09f, 0.98f)
                },
                new WeaponAttachmentSetting
                {
                    weaponId = "wep_rifle_laser_01",
                    characterName = "All",
                    positionOffset = new Vector3(0f, 0f, 0.03f),
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one,
                    muzzleOffset = new Vector3(0f, 0.08f, 0.65f)
                },
                new WeaponAttachmentSetting
                {
                    weaponId = "wep_rifle_plasma_01",
                    characterName = "All",
                    positionOffset = new Vector3(0f, 0f, 0.03f),
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one,
                    muzzleOffset = new Vector3(0f, 0.08f, 0.68f)
                },
                new WeaponAttachmentSetting
                {
                    weaponId = "wep_rifle_plasma_02",
                    characterName = "All",
                    positionOffset = new Vector3(0f, 0f, 0.03f),
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one,
                    muzzleOffset = new Vector3(0f, 0.08f, 0.70f)
                },
                new WeaponAttachmentSetting
                {
                    weaponId = "wep_rifle_plasma_03",
                    characterName = "All",
                    positionOffset = new Vector3(0f, 0f, 0.03f),
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one,
                    muzzleOffset = new Vector3(0f, 0.08f, 0.72f)
                },
                new WeaponAttachmentSetting
                {
                    weaponId = "wep_sniper_plasma_02",
                    characterName = "All",
                    positionOffset = new Vector3(0f, 0f, 0.05f),
                    rotationOffset = Vector3.zero,
                    scale = Vector3.one,
                    muzzleOffset = new Vector3(0f, 0.09f, 0.95f)
                }
            };

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            if (logSuccess)
            {
                Debug.Log($"<color=#00FF88>[SinglePlayerAttachmentProfileGenerator] Successfully generated and configured SinglePlayerWeaponAttachmentProfile with 16 characters and 14 weapons at {ProfileResourcePath}!</color>");
            }

            return profile;
        }
    }
}
