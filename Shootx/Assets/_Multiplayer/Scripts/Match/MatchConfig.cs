using UnityEngine;

/// <summary>
/// Central match configuration ScriptableObject.
/// Create via Assets > Create > Match > MatchConfig.
/// All match-tunable parameters live here — one source of truth.
/// </summary>
[CreateAssetMenu(fileName = "MatchConfig", menuName = "Match/MatchConfig", order = 1)]
public class MatchConfig : ScriptableObject
{
    [Header("Players")]
    [Tooltip("Maximum players allowed in one match.")]
    public int maxPlayers = 4;

    [Header("Win Conditions")]
    [Tooltip("Kills required to win immediately.")]
    public int killTarget = 10;

    [Tooltip("Match duration in seconds.")]
    public float matchDuration = 180f;

    [Header("Countdown")]
    [Tooltip("Pre-match countdown duration in seconds.")]
    public float preMatchCountdown = 3f;

    [Header("Respawn")]
    [Tooltip("Seconds before respawning after death.")]
    public float respawnDelay = 3f;

    [Tooltip("Spawn-protection invulnerability duration in seconds.")]
    public float spawnProtectionDuration = 1.5f;

    [Tooltip("Minimum preferred distance from living players when selecting a spawn point.")]
    public float minSpawnDistanceFromPlayers = 8f;

    [Header("Results")]
    [Tooltip("How long the results screen stays before auto-returning to lobby.")]
    public float resultsDisplayDuration = 8f;

    [Header("Map Loading")]
    [Tooltip("Seconds to wait for map initialization before timing out.")]
    public float mapLoadTimeout = 30f;

    [Tooltip("Avoid immediately repeating the previous map when possible.")]
    public bool avoidRepeatMap = true;
}
