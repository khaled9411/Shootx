using UnityEngine;

/// <summary>
/// Data container for weapon properties.
/// Create instances via Assets > Create > Combat > WeaponData.
/// </summary>
[CreateAssetMenu(fileName = "NewWeaponData", menuName = "Combat/WeaponData", order = 1)]
public class WeaponData : ScriptableObject
{
    [Header("Firing")]
    [Tooltip("Minimum time between shots (seconds). Default is 0.2s.")]
    public float fireCooldown = 0.2f;

    [Header("Hitscan & Ricochet")]
    [Tooltip("Maximum effective shot range in meters.")]
    public float maxShotRange = 30f;

    [Tooltip("Maximum wall ricochet bounces allowed (0 = no bounce).")]
    public int maxRicochets = 2;

    [Tooltip("Distance threshold for close-range damage (e.g. 6m).")]
    public float closeRangeDistance = 6f;

    [Tooltip("Distance threshold for medium-range damage (e.g. 14m).")]
    public float mediumRangeDistance = 14f;

    [Header("Distance Damage Values (Health Units)")]
    [Tooltip("Damage dealt at close range (<= closeRangeDistance).")]
    public float closeDamage = 3f;

    [Tooltip("Damage dealt at medium range (> closeRangeDistance, <= mediumRangeDistance).")]
    public float mediumDamage = 1.5f;

    [Tooltip("Damage dealt at long range (> mediumRangeDistance, <= maxShotRange).")]
    public float longDamage = 1f;

    [Header("Ammunition Settings")]
    [Tooltip("Basic ammunition capacity (default 3).")]
    public int basicAmmoCapacity = 3;

    [Header("Laser Visuals (Local Only)")]
    [Tooltip("Prefab for the local-only animated laser beam (with LaserBeamVisual component).")]
    public GameObject laserBeamPrefab;

    [Tooltip("Muzzle flash particle prefab (optional).")]
    public GameObject muzzleFlashPrefab;
}
