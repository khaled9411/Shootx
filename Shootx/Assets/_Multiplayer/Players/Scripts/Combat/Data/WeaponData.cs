using UnityEngine;

/// <summary>
/// Data container for weapon properties.
/// Create instances via Assets > Create > Combat > WeaponData.
/// </summary>
[CreateAssetMenu(fileName = "NewWeaponData", menuName = "Combat/WeaponData", order = 1)]
public class WeaponData : ScriptableObject
{
    [Header("Firing")]
    [Tooltip("Minimum time between shots (seconds). For semi-auto this prevents spam-clicking.")]
    public float fireCooldown = 0.25f;

    [Header("Ammo")]
    [Tooltip("Maximum rounds in a single magazine.")]
    public int magazineSize = 12;

    [Tooltip("Time it takes to reload (seconds).")]
    public float reloadTime = 1.5f;

    [Header("Projectile")]
    [Tooltip("Reference to the projectile data this weapon fires.")]
    public ProjectileData projectileData;

    [Header("Prefabs")]
    [Tooltip("The networked projectile prefab (must have NetworkObject).")]
    public GameObject networkProjectilePrefab;

    [Tooltip("The local dummy projectile prefab (no NetworkObject needed).")]
    public GameObject dummyProjectilePrefab;

    [Header("Visuals")]
    [Tooltip("Muzzle flash particle prefab (optional).")]
    public GameObject muzzleFlashPrefab;
}
