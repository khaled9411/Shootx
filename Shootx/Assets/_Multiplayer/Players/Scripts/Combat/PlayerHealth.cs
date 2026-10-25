using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : NetworkBehaviour, INetworkDamageable
{
    [Header("Health Configuration")]
    [SerializeField] private HealthData healthData;

    [Header("Damage Flash Settings (Local UI)")]
    [SerializeField] private string damageFlashObjectName = "DamageFlash";
    [SerializeField] private float flashDuration = 0.2f;
    [SerializeField] private Color flashColor = new Color(1f, 0f, 0f, 0.4f);

    [Header("World Visual Effects (All Clients)")]
    [SerializeField] private GameObject bloodEffectPrefab;
    [SerializeField] private Transform bloodSpawnPoint;

    private NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        3f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> isDead = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public event Action<float, float> OnHealthChanged;
    public event Action<float, ulong> OnDamageTaken;
    public event Action<float> OnHealed;
    public event Action<ulong> OnDeath;
    public event Action OnRespawn;

    private float lastDamageTime;
    private float invulnerabilityEndTime;

    private Image damageFlashImage;
    private Coroutine flashCoroutine;

    public float CurrentHealth => currentHealth.Value;
    public float MaxHealth => healthData != null ? healthData.maxHealth : 3f;
    public float RespawnDelay => healthData != null ? healthData.respawnDelay : 3f;
    public int CurrentHealthInt => Mathf.RoundToInt(currentHealth.Value);
    public int MaxHealthInt => healthData != null ? healthData.maxHealthInt : 3;
    public bool IsDead => isDead.Value || currentHealth.Value <= 0.001f;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        currentHealth.OnValueChanged += HandleHealthChanged;
        isDead.OnValueChanged += HandleDeadStateChanged;

        if (IsServer)
        {
            float startingHealth = healthData != null ? healthData.maxHealth : 3f;
            currentHealth.Value = startingHealth;
            isDead.Value = false;
            lastDamageTime = float.MinValue;
            invulnerabilityEndTime = float.MinValue;
        }

        OnHealthChanged?.Invoke(currentHealth.Value, MaxHealth);

        if (IsOwner)
        {
            FindAnyObjectByType<MultiplayerHealthUI>()?.Initialize(this);

            GameObject flashObj = GameObject.Find(damageFlashObjectName);
            if (flashObj != null)
            {
                damageFlashImage = flashObj.GetComponent<Image>();
                if (damageFlashImage != null)
                {
                    damageFlashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
                }
            }
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

        if (Time.time - lastDamageTime < healthData.regenDelay) return;
        if (currentHealth.Value >= healthData.maxHealth) return;

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
    // INetworkDamageable 
    // ═══════════════════════════════════════════════════════════

    public void TakeDamage(float damage, ulong attackerClientId)
    {
        if (!IsServer) return;
        if (isDead.Value) return;
        if (damage <= 0f) return;

        if (MatchManager.Instance != null && !MatchManager.Instance.IsDamageAllowed(OwnerClientId))
            return;

        if (healthData != null && healthData.invulnerabilityDuration > 0f && Time.time < invulnerabilityEndTime)
            return;

        float clampedDamage = Mathf.Min(damage, currentHealth.Value);
        currentHealth.Value = Mathf.Max(0f, currentHealth.Value - clampedDamage);
        lastDamageTime = Time.time;

        if (healthData != null && healthData.invulnerabilityDuration > 0f)
        {
            invulnerabilityEndTime = Time.time + healthData.invulnerabilityDuration;
        }

        NotifyDamageTakenClientRpc(clampedDamage, attackerClientId);

        if (currentHealth.Value <= 0.001f)
        {
            currentHealth.Value = 0f;
            HandleDeath(attackerClientId);
        }
    }

    public void TakeDamage(int damage, ulong attackerClientId)
    {
        TakeDamage((float)damage, attackerClientId);
    }

    // ═══════════════════════════════════════════════════════════
    // Healing
    // ═══════════════════════════════════════════════════════════

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

        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        GetComponent<PlayerVisualManager>()?.SetVisualsActive(false);
        GetComponent<PlayerWorldStatusUI>()?.SetStatusUIVisible(false);

        if (IsOwner)
        {
            GetComponent<PlayerController>()?.SetInputLocked(true);
        }
        else
        {
            LockControllerClientRpc();
        }

        OnDeath?.Invoke(killerClientId);
        NotifyDeathClientRpc(killerClientId);

        if (MatchManager.Instance != null && MatchManager.Instance.IsSpawned)
        {
            MatchManager.Instance.ReportPlayerDeath(OwnerClientId, killerClientId);
        }
        else
        {
            StartCoroutine(LegacyRespawnCoroutine());
        }
    }

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
    // NetworkVariable Callbacks
    // ═══════════════════════════════════════════════════════════

    private void HandleHealthChanged(float oldValue, float newValue)
    {
        OnHealthChanged?.Invoke(newValue, MaxHealth);
    }

    private void HandleDeadStateChanged(bool oldValue, bool newValue)
    {
    }

    // ═══════════════════════════════════════════════════════════
    // Client RPCs
    // ═══════════════════════════════════════════════════════════

    [ClientRpc]
    private void NotifyDamageTakenClientRpc(float damage, ulong attackerClientId)
    {
        OnDamageTaken?.Invoke(damage, attackerClientId);

        if (IsOwner && damageFlashImage != null)
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(DamageFlashRoutine());
        }

        SpawnBloodEffect();
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

        if (IsServer) return;
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

    // ═══════════════════════════════════════════════════════════
    // UI Effects & Visuals
    // ═══════════════════════════════════════════════════════════

    private IEnumerator DamageFlashRoutine()
    {
        float elapsed = 0f;
        damageFlashImage.color = flashColor;

        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(flashColor.a, 0f, elapsed / flashDuration);
            damageFlashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, alpha);

            yield return null;
        }

        damageFlashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
    }

    private void SpawnBloodEffect()
    {
        if (bloodEffectPrefab == null) return;

        Vector3 spawnPos = bloodSpawnPoint != null ? bloodSpawnPoint.position : transform.position + Vector3.up * 1f;

        GameObject bloodInstance = Instantiate(bloodEffectPrefab, spawnPos, Quaternion.identity);

        Destroy(bloodInstance, 2f);
    }
}