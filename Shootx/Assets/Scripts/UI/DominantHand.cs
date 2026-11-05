using System;
using UnityEngine;

/// <summary>
/// Defines the player's dominant hand preference for mobile controls.
/// </summary>
public enum DominantHand
{
    RightHanded = 0,
    LeftHanded = 1
}

/// <summary>
/// Static helper and persistence manager for the Dominant Hand setting.
/// Uses PlayerPrefs with the key "settings_dominant_hand" (matching project conventions).
/// </summary>
public static class DominantHandSettings
{
    public const string PrefKey = "settings_dominant_hand";
    public const DominantHand DefaultDominantHand = DominantHand.RightHanded;

    /// <summary>
    /// Event triggered when the dominant hand setting changes.
    /// </summary>
    public static event Action<DominantHand> OnDominantHandChanged;

    /// <summary>
    /// Gets or sets the current dominant hand preference.
    /// Default is Right-handed (0) for new and existing players without a saved preference.
    /// Setting this immediately saves to PlayerPrefs and fires OnDominantHandChanged.
    /// </summary>
    public static DominantHand CurrentHand
    {
        get
        {
            int savedValue = PlayerPrefs.GetInt(PrefKey, (int)DefaultDominantHand);
            return (DominantHand)savedValue;
        }
        set
        {
            PlayerPrefs.SetInt(PrefKey, (int)value);
            PlayerPrefs.Save();
            OnDominantHandChanged?.Invoke(value);
        }
    }

    /// <summary>
    /// Resets the preference back to default (Right-handed), deleting the saved key.
    /// Useful for testing fresh player state.
    /// </summary>
    public static void ResetToDefault()
    {
        PlayerPrefs.DeleteKey(PrefKey);
        PlayerPrefs.Save();
        OnDominantHandChanged?.Invoke(DefaultDominantHand);
    }
}

