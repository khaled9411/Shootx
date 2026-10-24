using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-authoritative health system for networked players.
///
/// Implements <see cref="INetworkDamageable"/> so both
/// <see cref="PlayerShooting"/> (via projectiles) and
/// <see cref="PlayerMelee"/> can deal damage through the same interface.
///
/// Features:
///   - Networked HP synced to all clients via NetworkVariable.
///   - Passive health regeneration with configurable delay after damage.
///   - Optional invulnerability frames (i-frames) after taking damage.
///   - Death / respawn flow with server authority.
///   - Rich C# events for UI, VFX, and animation hooks.
///
/// Attach this to the Player prefab alongside PlayerController,
/// PlayerShooting, and PlayerMelee.
/// </summary>
public class PlayerHealth : NetworkBehaviour, INetworkDamageable
{
    // ─── Inspector ──────────────────────────────────────────────
    [Header("Health Configuration")]
    [SerializeField] private HealthData healthData;

    // ─── Networked State ────────────────────────────────────────

    /// <summary>Current health (units, e.g. 3.0, 1.5, 0.5), synced to all clients. Only the server writes.</summary>
    private NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        3f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>Whether the player is currently dead. Prevents double-kills and gates regen.</summary>
    private NetworkVariable<bool> isDead = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ─── Events ─────────────────────────────────────────────────

    /// <summary>
    /// Fires on ALL clients whenever health changes.
    /// Passes (currentHealth, maxHealth).
    /// </summary>
    public event Action<float, float> OnHealthChanged;

    /// <summary>
    /// Fires on ALL clients when this player takes damage.
    /// Passes (damageAmount, attackerClientId).
    /// Use this for hit-flash, screen-shake, floating damage numbers, etc.
    /// </summary>
    public event Action<float, ulong> OnDamageTaken;

    /// <summary>
    /// Fires on ALL clients when this player is healed.
    /// Passes (healAmount).
    /// </summary>
    public event Action<float> OnHealed;

    /// <summary>
    /// Fires on ALL clients when this player dies.
    /// Passes (killerClientId).
    /// </summary>
    public event Action<ulong> OnDeath;

    /// <summary>
    /// Fires on ALL clients when this player respawns.
    /// </summary>
    public event Action OnRespawn;

    // ─── Local State (server only) ──────────────────────────────
    private float lastDamageTime;
    private float invulnerabilityEndTime;
    private float regenAccumulator;

    // ─── Properties ─────────────────────────────────────────────

    /// <summary>Read the current fractional health from any client.</summary>
    public float CurrentHealth => currentHealth.Value;

    /// <summary>Read max health (units) from the data asset.</summary>
    public float MaxHealth => healthData != null ? healthData.maxHealth : 3f;

    /// <summary>Respawn delay in seconds.</summary>
    public float RespawnDelay => healthData != null ? healthData.respawnDelay : 3f;

    /// <summary>Backward-compatibility integer health.</summary>
    public int CurrentHealthInt => Mathf.RoundToInt(currentHealth.Value);
    public int MaxHealthInt => healthData != null ? healthData.maxHealthInt : 3;

    /// <summary>Is the player currently dead?</summary>
    public bool IsDead => isDead.Value || currentHealth.Value <= 0.001f;

    // ═══════════════════════════════════════════════════════════
    // NetworkBehaviour Lifecycle
    // ═══════════════════════════════════════════════════════════

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Subscribe to synced value changes on all clients
        currentHealth.OnValueChanged += HandleHealthChanged;
        isDead.OnValueChanged += HandleDeadStateChanged;

        // Server initializes health
        if (IsServer)
        {
            float startingHealth = healthData != null ? healthData.maxHealth : 3f;
            currentHealth.Value = startingHealth;
            isDead.Value = false;
            lastDamageTime = float.MinValue;
            invulnerabilityEndTime = float.MinValue;
        }

        // Fire initial event so UI is in sync immediately
        OnHealthChanged?.Invoke(currentHealth.Value, MaxHealth);

        if (IsOwner)
        {
            FindAnyObjectByType<MultiplayerHealthUI>()?.Initialize(this);
        }
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= HandleHealthChanged;
        isDead.OnValueChanged -= HandleDeadStateChanged;
    }

    // ═══════════════════════════════════════════════════════════
    // Update — Server-side regeneration
    // ═══════════════════════════════════════════════════════════

    private void Update()
    {
        if (!IsServer) return;
        if (isDead.Value) return;
        if (healthData == null || !healthData.enableRegen) return;

        // Wait for regen delay after last damage
        if (Time.time - lastDamageTime < healthData.regenDelay) return;

        // Already at full health
        if (currentHealth.Value >= healthData.maxHealth) return;

        // Apply regen smoothly
        float healAmount = healthData.regenRate * Time.deltaTime;
        float newHealth = Mathf.Min(currentHealth.Value + healAmount, healthData.maxHealth);
        if (newHealth > currentHealth.Value)
        {
            float actualHeal = newHealth - currentHealth.Value;
            currentHealth.Value = newHealth;
            NotifyHealedClientRpc(actualHeal);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // INetworkDamageable — called by hitscan Raycast, projectiles, and melee
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Apply fractional damage to this player (server-authoritative).
    /// </summary>
    public void TakeDamage(float damage, ulong attackerClientId)
    {
        if (!IsServer) return;
        if (isDead.Value) return;
        if (damage <= 0f) return;

        // ─── Match-state gate ──────────────────────────────────────────────
        // Reject damage if match isn’t active or spawn protection is active
        if (MatchManager.Instance != null && !MatchManager.Instance.IsDamageAllowed(OwnerClientId))
            return;

        // Invulnerability check (i-frames from HealthData)
        if (healthData != null && healthData.invulnerabilityDuration > 0f && Time.time < invulnerabilityEndTime)
            return;

        // Apply clamped damage
        float clampedDamage = Mathf.Min(damage, currentHealth.Value);
        currentHealth.Value = Mathf.Max(0f, currentHealth.Value - clampedDamage);
        lastDamageTime = Time.time;

        // Start i-frames
        if (healthData != null && healthData.invulnerabilityDuration > 0f)
        {
            invulnerabilityEndTime = Time.time + healthData.invulnerabilityDuration;
        }

        // Notify all clients about the damage
        NotifyDamageTakenClientRpc(clampedDamage, attackerClientId);

        // Death check
        if (currentHealth.Value <= 0.001f)
        {
            currentHealth.Value = 0f;
            HandleDeath(attackerClientId);
        }
    }

    /// <summary>
    /// Backward-compatibility integer damage method.
    /// </summary>
    public void TakeDamage(int damage, ulong attackerClientId)
    {
        TakeDamage((float)damage, attackerClientId);
    }

    // ═══════════════════════════════════════════════════════════
    // Healing (public API — call from server-side pickups, etc.)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Heal the player by the specified fractional amount. Server-only.
    /// </summary>
    public void Heal(float amount)
    {
        if (!IsServer) return;
        if (isDead.Value) return;
        if (amount <= 0f) return;

        float maxHp = MaxHealth;
        float newHealth = Mathf.Min(currentHealth.Value + amount, maxHp);
        float actualHeal = newHealth - currentHealth.Value;

        if (actualHeal > 0f)
        {
            currentHealth.Value = newHealth;
            NotifyHealedClientRpc(actualHeal);
        }
    }

    public void Heal(int amount)
    {
        Heal((float)amount);
    }

    // ═══════════════════════════════════════════════════════════
    // Death & Respawn
    // ═══════════════════════════════════════════════════════════

    private void HandleDeath(ulong killerClientId)
    {
        if (isDead.Value) return;
        isDead.Value = true;

        // Disable collision immediately so dead body doesn't block players or projectiles
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        // Hide visuals and status UI immediately on server
        GetComponent<PlayerVisualManager>()?.SetVisualsActive(false);
        GetComponent<PlayerWorldStatusUI>()?.SetStatusUIVisible(false);

        // Lock local player input immediately
        if (IsOwner)
        {
            GetComponent<PlayerController>()?.SetInputLocked(true);
        }
        else
        {
            // On server (host-client), lock via RPC
            LockControllerClientRpc();
        }

        // Fire death events on server directly
        OnDeath?.Invoke(killerClientId);

        // Notify all clients
        NotifyDeathClientRpc(killerClientId);

        // Delegate respawn directly to MatchManager on server
        if (MatchManager.Instance != null && MatchManager.Instance.IsSpawned)
        {
            MatchManager.Instance.ReportPlayerDeath(OwnerClientId, killerClientId);
        }
        else
        {
            StartCoroutine(LegacyRespawnCoroutine());
        }
    }

    /// <summary>
    /// Restores health on respawn. Called by MatchManager on server.
    /// </summary>
    public void ServerRespawnRestore()
    {
        if (!IsServer) return;
        currentHealth.Value = MaxHealth;
        isDead.Value = false;
        invulnerabilityEndTime = float.MinValue;
        lastDamageTime = float.MinValue;

        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = true;

        GetComponent<PlayerVisualManager>()?.SetVisualsActive(true);
        GetComponent<PlayerWorldStatusUI>()?.SetStatusUIVisible(true);
        GetComponent<PlayerController>()?.SetInputLocked(false);
        NotifyRespawnClientRpc();
    }

    /// <summary>
    /// Called on the owner client after a server-confirmed respawn.
    /// Unlocks controller for animation/visual hooks.
    /// </summary>
    public void NotifyRespawnClient()
    {
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = true;

        GetComponent<PlayerVisualManager>()?.SetVisualsActive(true);
        GetComponent<PlayerWorldStatusUI>()?.SetStatusUIVisible(true);

        OnRespawn?.Invoke();
    }

    private IEnumerator LegacyRespawnCoroutine()
    {
        float delay = healthData != null ? healthData.respawnDelay : 3f;
        yield return new WaitForSeconds(delay);

        currentHealth.Value = MaxHealth;
        isDead.Value = false;

        Vector3 spawnPos = MultiplayerSpawnManager.GetRandomSpawnPosition();
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.position = spawnPos;
        if (cc != null) cc.enabled = true;

        if (TryGetComponent<PlayerShooting>(out var shooting))
            shooting.HandleRespawnServer();

        NotifyRespawnClientRpc();
    }

    [ClientRpc]
    private void LockControllerClientRpc()
    {
        GetComponent<PlayerController>()?.SetInputLocked(true);
    }

    // ═══════════════════════════════════════════════════════════
    // NetworkVariable Callbacks (all clients)
    // ═══════════════════════════════════════════════════════════

    private void HandleHealthChanged(float oldValue, float newValue)
    {
        OnHealthChanged?.Invoke(newValue, MaxHealth);
    }

    private void HandleDeadStateChanged(bool oldValue, bool newValue)
    {
        // Network state synced
    }

    // ═══════════════════════════════════════════════════════════
    // Client RPCs — event distribution
    // ═══════════════════════════════════════════════════════════

    [ClientRpc]
    private void NotifyDamageTakenClientRpc(float damage, ulong attackerClientId)
    {
        OnDamageTaken?.Invoke(damage, attackerClientId);
    }

    [ClientRpc]
    private void NotifyHealedClientRpc(float amount)
    {
        OnHealed?.Invoke(amount);
    }

    [ClientRpc]
    private void NotifyDeathClientRpc(ulong killerClientId)
    {
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        GetComponent<PlayerVisualManager>()?.SetVisualsActive(false);
        GetComponent<PlayerWorldStatusUI>()?.SetStatusUIVisible(false);

        if (IsServer) return; // Server already invoked OnDeath locally
        OnDeath?.Invoke(killerClientId);
    }

    [ClientRpc]
    private void NotifyRespawnClientRpc()
    {
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = true;

        GetComponent<PlayerVisualManager>()?.SetVisualsActive(true);
        GetComponent<PlayerWorldStatusUI>()?.SetStatusUIVisible(true);

        OnRespawn?.Invoke();
    }
}
