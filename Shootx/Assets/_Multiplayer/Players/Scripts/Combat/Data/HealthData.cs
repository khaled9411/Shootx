using UnityEngine;

/// <summary>
/// Data container for player health properties.
/// Create instances via Assets > Create > Combat > HealthData.
/// </summary>
[CreateAssetMenu(fileName = "NewHealthData", menuName = "Combat/HealthData", order = 3)]
public class HealthData : ScriptableObject
{
    [Header("Health")]
    [Tooltip("Maximum health points.")]
    public int maxHealth = 100;

    [Header("Regeneration")]
    [Tooltip("Enable passive health regeneration over time.")]
    public bool enableRegen = true;

    [Tooltip("Health points recovered per second during regen.")]
    public float regenRate = 5f;

    [Tooltip("Seconds after taking damage before regen kicks in.")]
    public float regenDelay = 4f;

    [Header("Damage Feedback")]
    [Tooltip("Duration of the damage flash visual effect (seconds).")]
    public float damageFlashDuration = 0.15f;

    [Tooltip("Duration of the invulnerability window after taking damage (seconds). " +
             "Set to 0 to disable i-frames.")]
    public float invulnerabilityDuration = 0f;

    [Header("Death & Respawn")]
    [Tooltip("Time in seconds before the player respawns after death.")]
    public float respawnDelay = 3f;
}
