using UnityEngine;

/// <summary>
/// Data container for projectile properties.
/// Create instances via Assets > Create > Combat > ProjectileData.
/// </summary>
[CreateAssetMenu(fileName = "NewProjectileData", menuName = "Combat/ProjectileData", order = 0)]
public class ProjectileData : ScriptableObject
{
    [Header("Movement")]
    [Tooltip("Speed of the projectile in units/second.")]
    public float speed = 40f;

    [Header("Damage")]
    [Tooltip("Base damage dealt on hit.")]
    public int damage = 10;

    [Header("Lifetime")]
    [Tooltip("How long the projectile lives before being destroyed (seconds).")]
    public float lifetime = 3f;

    [Header("Physics")]
    [Tooltip("Radius of the projectile's sphere collider / overlap check.")]
    public float radius = 0.1f;

    [Tooltip("Layers the projectile can hit.")]
    public LayerMask hitLayers;
}
