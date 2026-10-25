using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shootx.Store
{
    [System.Serializable]
    public class CharacterAttachmentSetting
    {
        [Tooltip("Character model name (e.g. MainCharacter, RangerCharacter, Knight, Survivalist, etc.)")]
        public string characterName = "";

        [Tooltip("Custom socket or bone name to search on this character (e.g. 'handslot.r', 'Wrist_R')")]
        public string socketName = "";

        [Tooltip("Default local position offset for weapons attached to this character")]
        public Vector3 positionOffset = Vector3.zero;

        [Tooltip("Default local rotation offset (Euler angles) for weapons attached to this character")]
        public Vector3 rotationOffset = Vector3.zero;

        [Tooltip("Default local scale for weapons attached to this character")]
        public Vector3 scale = Vector3.one;
    }

    [System.Serializable]
    public class WeaponAttachmentSetting
    {
        [Tooltip("Weapon identifier or prefix (e.g. wep_pistol_01, SM_Wep_Pistol_01, 0)")]
        public string weaponId = "";

        [Tooltip("Target character name ('All' or empty applies to any character without specific override)")]
        public string characterName = "All";

        [Tooltip("Local position offset for this weapon")]
        public Vector3 positionOffset = Vector3.zero;

        [Tooltip("Local rotation offset (Euler angles) for this weapon")]
        public Vector3 rotationOffset = Vector3.zero;

        [Tooltip("Local scale for this weapon")]
        public Vector3 scale = Vector3.one;

        [Tooltip("Local position of the muzzle/FirePoint relative to this weapon's root")]
        public Vector3 muzzleOffset = new Vector3(0f, 0.08f, 0.35f);
    }

    [CreateAssetMenu(fileName = "SinglePlayerWeaponAttachmentProfile", menuName = "ShootX/Store/Weapon Attachment Profile")]
    public class SinglePlayerWeaponAttachmentProfile : ScriptableObject
    {
        private static SinglePlayerWeaponAttachmentProfile _cachedInstance;
        public static SinglePlayerWeaponAttachmentProfile DefaultProfile
        {
            get
            {
                if (_cachedInstance == null)
                {
                    _cachedInstance = Resources.Load<SinglePlayerWeaponAttachmentProfile>("SinglePlayerWeaponAttachmentProfile");
                }
                return _cachedInstance;
            }
        }

        [Header("=== Character Default Hand & Bone Settings ===")]
        [Tooltip("Default attachment settings per character model under Models/")]
        public List<CharacterAttachmentSetting> characterSettings = new List<CharacterAttachmentSetting>();

        [Header("=== Weapon Specific Offsets & Muzzles ===")]
        [Tooltip("Fine-grained weapon attachment and muzzle settings")]
        public List<WeaponAttachmentSetting> weaponSettings = new List<WeaponAttachmentSetting>();

        public CharacterAttachmentSetting GetCharacterSetting(string characterName)
        {
            if (string.IsNullOrEmpty(characterName) || characterSettings == null) return null;

            return characterSettings.Find(c =>
                c != null && (
                    c.characterName.Equals(characterName, StringComparison.OrdinalIgnoreCase) ||
                    characterName.StartsWith(c.characterName, StringComparison.OrdinalIgnoreCase) ||
                    c.characterName.StartsWith(characterName, StringComparison.OrdinalIgnoreCase)
                ));
        }

        public WeaponAttachmentSetting GetWeaponSetting(string weaponId, string characterName)
        {
            if (weaponSettings == null || string.IsNullOrEmpty(weaponId)) return null;

            // 1. Try exact character + weapon match
            if (!string.IsNullOrEmpty(characterName))
            {
                var specific = weaponSettings.Find(w =>
                    w != null &&
                    IsWeaponMatch(w.weaponId, weaponId) &&
                    w.characterName.Equals(characterName, StringComparison.OrdinalIgnoreCase));

                if (specific != null) return specific;
            }

            // 2. Try generic "All" character match for this weapon
            return weaponSettings.Find(w =>
                w != null &&
                IsWeaponMatch(w.weaponId, weaponId) &&
                (string.IsNullOrEmpty(w.characterName) || w.characterName.Equals("All", StringComparison.OrdinalIgnoreCase)));
        }

        private static bool IsWeaponMatch(string configId, string targetId)
        {
            if (string.IsNullOrEmpty(configId) || string.IsNullOrEmpty(targetId)) return false;

            return configId.Equals(targetId, StringComparison.OrdinalIgnoreCase) ||
                   targetId.IndexOf(configId, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   configId.IndexOf(targetId, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Computes the effective attachment transform and muzzle offsets for the given weapon and character.
        /// </summary>
        public void GetEffectiveOffsets(
            string weaponId,
            string characterName,
            out Vector3 localPosition,
            out Quaternion localRotation,
            out Vector3 localScale,
            out Vector3 muzzleOffset,
            out string preferredSocket)
        {
            // Default fallbacks
            localPosition = Vector3.zero;
            localRotation = Quaternion.identity;
            localScale = Vector3.one;
            muzzleOffset = new Vector3(0f, 0.08f, 0.35f);
            preferredSocket = "handslot.r";

            // Character defaults
            var charSetting = GetCharacterSetting(characterName);
            if (charSetting != null)
            {
                if (!string.IsNullOrEmpty(charSetting.socketName))
                    preferredSocket = charSetting.socketName;

                localPosition += charSetting.positionOffset;
                localRotation = Quaternion.Euler(charSetting.rotationOffset);
                if (charSetting.scale != Vector3.zero)
                    localScale = Vector3.Scale(localScale, charSetting.scale);
            }
            else
            {
                // Fallback socket determination
                if (characterName != null && characterName.IndexOf("MainCharacter", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    preferredSocket = "Wrist_R";
                    localRotation = Quaternion.Euler(-90f, 0f, 90f);
                    localPosition = new Vector3(0.05f, -0.04f, 0.12f);
                }
                else
                {
                    preferredSocket = "handslot.r";
                }
            }

            // Weapon specific overrides
            var wepSetting = GetWeaponSetting(weaponId, characterName);
            if (wepSetting != null)
            {
                localPosition += wepSetting.positionOffset;
                localRotation *= Quaternion.Euler(wepSetting.rotationOffset);

                if (wepSetting.scale != Vector3.zero)
                    localScale = Vector3.Scale(localScale, wepSetting.scale);

                if (wepSetting.muzzleOffset != Vector3.zero)
                    muzzleOffset = wepSetting.muzzleOffset;
            }
        }
    }
}
