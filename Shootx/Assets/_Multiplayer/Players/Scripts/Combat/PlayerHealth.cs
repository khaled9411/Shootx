using System;
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

    /// <summary>Current health, synced to all clients. Only the server writes.</summary>
    private NetworkVariable<int> currentHealth = new NetworkVariable<int>(
        0,
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
    public event Action<int, int> OnHealthChanged;

    /// <summary>
    /// Fires on ALL clients when this player takes damage.
    /// Passes (damageAmount, attackerClientId).
    /// Use this for hit-flash, screen-shake, floating damage numbers, etc.
    /// </summary>
    public event Action<int, ulong> OnDamageTaken;

    /// <summary>
    /// Fires on ALL clients when this player is healed.
    /// Passes (healAmount).
    /// </summary>
    public event Action<int> OnHealed;

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

    /// <summary>Read the current health from any client.</summary>
    public int CurrentHealth => currentHealth.Value;

    /// <summary>Read max health from the data asset.</summary>
    public int MaxHealth => healthData != null ? healthData.maxHealth : 100;

    /// <summary>Is the player currently dead?</summary>
    public bool IsDead => isDead.Value;

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
            currentHealth.Value = healthData.maxHealth;
            isDead.Value = false;
            lastDamageTime = float.MinValue;
            invulnerabilityEndTime = float.MinValue;
        }

        // Fire initial event so UI is in sync immediately
        OnHealthChanged?.Invoke(currentHealth.Value, healthData.maxHealth);

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
        if (!healthData.enableRegen) return;

        // Wait for regen delay after last damage
        if (Time.time - lastDamageTime < healthData.regenDelay) return;

        // Already at full health
        if (currentHealth.Value >= healthData.maxHealth) return;

        // Apply regen (accumulated fractionally to stay smooth)
        regenAccumulator += healthData.regenRate * Time.deltaTime;
        int regenInt = Mathf.FloorToInt(regenAccumulator);
        if (regenInt <= 0) return;
        regenAccumulator -= regenInt;


        int newHealth = Mathf.Min(currentHealth.Value + regenInt, healthData.maxHealth);
        if (newHealth != currentHealth.Value)
        {
            int healed = newHealth - currentHealth.Value;
            currentHealth.Value = newHealth;
            NotifyHealedClientRpc(healed);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // INetworkDamageable — called by projectiles AND melee
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Apply damage to this player. Called on the SERVER by
    /// <see cref="NetworkProjectile.OnHit"/> or <see cref="PlayerMelee.MeleeServerRpc"/>.
    /// </summary>
    public void TakeDamage(int damage, ulong attackerClientId)
    {
        if (!IsServer) return;
        if (isDead.Value) return;
        if (damage <= 0) return;

        // Invulnerability check
        if (healthData.invulnerabilityDuration > 0f && Time.time < invulnerabilityEndTime)
            return;

        // Apply damage
        int clampedDamage = Mathf.Min(damage, currentHealth.Value);
        currentHealth.Value -= clampedDamage;
        lastDamageTime = Time.time;

        // Start i-frames
        if (healthData.invulnerabilityDuration > 0f)
        {
            invulnerabilityEndTime = Time.time + healthData.invulnerabilityDuration;
        }

        // Notify all clients about the damage
        NotifyDamageTakenClientRpc(clampedDamage, attackerClientId);

        // Death check
        if (currentHealth.Value <= 0)
        {
            HandleDeath(attackerClientId);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Healing (public API — call from server-side pickups, etc.)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Heal the player by the specified amount. Server-only.
    /// </summary>
    public void Heal(int amount)
    {
        if (!IsServer) return;
        if (isDead.Value) return;
        if (amount <= 0) return;

        int newHealth = Mathf.Min(currentHealth.Value + amount, healthData.maxHealth);
        int actualHeal = newHealth - currentHealth.Value;

        if (actualHeal > 0)
        {
            currentHealth.Value = newHealth;
            NotifyHealedClientRpc(actualHeal);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Death & Respawn
    // ═══════════════════════════════════════════════════════════

    private void HandleDeath(ulong killerClientId)
    {
        isDead.Value = true;

        // Notify all clients
        NotifyDeathClientRpc(killerClientId);

        // Schedule respawn
        StartCoroutine(RespawnCoroutine());
    }

    private System.Collections.IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(healthData.respawnDelay);

        // Restore health
        currentHealth.Value = healthData.maxHealth;
        isDead.Value = false;

        // TODO: Reset position to a spawn point
        // transform.position = SpawnManager.GetRandomSpawnPoint();

        // Notify all clients
        NotifyRespawnClientRpc();
    }

    // ═══════════════════════════════════════════════════════════
    // NetworkVariable Callbacks (all clients)
    // ═══════════════════════════════════════════════════════════

    private void HandleHealthChanged(int oldValue, int newValue)
    {
        OnHealthChanged?.Invoke(newValue, healthData.maxHealth);
    }

    private void HandleDeadStateChanged(bool oldValue, bool newValue)
    {
        // This can be used to toggle ragdoll, disable controls, etc.
        // The specific events (OnDeath, OnRespawn) are fired via ClientRPC
        // so they carry additional info (killer ID).
    }

    // ═══════════════════════════════════════════════════════════
    // Client RPCs — event distribution
    // ═══════════════════════════════════════════════════════════

    [ClientRpc]
    private void NotifyDamageTakenClientRpc(int damage, ulong attackerClientId)
    {
        OnDamageTaken?.Invoke(damage, attackerClientId);
    }

    [ClientRpc]
    private void NotifyHealedClientRpc(int amount)
    {
        OnHealed?.Invoke(amount);
    }

    [ClientRpc]
    private void NotifyDeathClientRpc(ulong killerClientId)
    {
        OnDeath?.Invoke(killerClientId);
    }

    [ClientRpc]
    private void NotifyRespawnClientRpc()
    {
        OnRespawn?.Invoke();
    }
}
