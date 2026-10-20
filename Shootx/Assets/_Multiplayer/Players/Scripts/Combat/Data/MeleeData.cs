using UnityEngine;

/// <summary>
/// Data container for melee attack properties.
/// Create instances via Assets > Create > Combat > MeleeData.
/// </summary>
[CreateAssetMenu(fileName = "NewMeleeData", menuName = "Combat/MeleeData", order = 2)]
public class MeleeData : ScriptableObject
{
    [Header("Damage")]
    [Tooltip("Amount of damage the melee attack deals.")]
    public int damage = 25;

    [Header("Range & Detection")]
    [Tooltip("Maximum distance from the attacker to detect a target (units).")]
    public float attackRange = 1.8f;

    [Tooltip("Half-angle of the attack arc in front of the player (degrees). " +
             "180 = full circle, 90 = cone in front.")]
    [Range(10f, 180f)]
    public float attackAngle = 120f;

    [Header("Timing")]
    [Tooltip("Minimum time between melee swings (seconds). Prevents spam.")]
    public float attackCooldown = 0.6f;

    [Tooltip("Delay from swing start to when the hit is actually registered (seconds). " +
             "Should match the animation's impact frame.")]
    public float hitRegistrationDelay = 0.15f;

    [Header("Layers")]
    [Tooltip("Which layers count as valid melee targets.")]
    public LayerMask hitLayers;

    [Header("Visuals / Feedback")]
    [Tooltip("Local-only VFX prefab shown immediately on the owner (client prediction).")]
    public GameObject localSlashVfxPrefab;

    [Tooltip("Networked VFX prefab shown on all clients when the server confirms the hit.")]
    public GameObject networkHitVfxPrefab;

    [Tooltip("Screen-shake magnitude on the attacker when the swing lands.")]
    public float hitShakeMagnitude = 0.15f;
}
