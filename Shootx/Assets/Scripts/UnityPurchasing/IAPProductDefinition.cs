using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;

[CreateAssetMenu(fileName = "New IAP Product", menuName = "IAP/Product Definition")]
public class IAPProductDefinition : ScriptableObject
{
    [Header("=== Product Info ===")]
    public string productId;
    public ProductType productType = ProductType.Consumable;

    [Header("=== Rewards ===")]
    [Tooltip("Amount of Hard Coins to grant. Leave 0 if none.")]
    public int hardCoinsAmount;

    [Header("=== Permanent Unlocks ===")]
    public bool removesAds;
    public bool unlocksMultiplayer;

    [Header("=== Items (Bundles) ===")]
    [Tooltip("List of Gun IDs to unlock.")]
    public List<int> unlockGunIds;
    [Tooltip("List of Outfit IDs to unlock.")]
    public List<int> unlockOutfitIds;
}
