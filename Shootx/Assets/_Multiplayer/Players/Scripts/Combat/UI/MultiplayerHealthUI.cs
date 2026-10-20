using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI component that displays health, damage flash, and death overlay.
/// Bind it to the local player's <see cref="PlayerHealth"/> after spawn.
///
/// Designed to work alongside <see cref="MultiplayerAmmoUI"/>.
/// </summary>
public class MultiplayerHealthUI : MonoBehaviour
{
    [Header("Health Bar")]
    [Tooltip("Slider that represents the health bar. Value is normalised 0-1.")]
    [SerializeField] private Slider healthBar;

    [Tooltip("Optional fill image inside the slider — used to tint the bar colour.")]
    [SerializeField] private Image healthBarFill;

    [Header("Health Text")]
    [Tooltip("Text element showing current HP (e.g. '80 / 100').")]
    [SerializeField] private TextMeshProUGUI healthText;

    [Header("Damage Flash")]
    [Tooltip("Full-screen overlay image that flashes red when damaged.")]
    [SerializeField] private Image damageFlashOverlay;

    [Tooltip("Colour of the damage flash.")]
    [SerializeField] private Color damageFlashColor = new Color(1f, 0f, 0f, 0.35f);

    [Header("Death Overlay")]
    [Tooltip("Panel shown when the player is dead.")]
    [SerializeField] private GameObject deathOverlay;

    [Tooltip("Optional respawn countdown text inside the death overlay.")]
    [SerializeField] private TextMeshProUGUI respawnTimerText;

    [Header("Health Bar Colours")]
    [SerializeField] private Color healthyColor = new Color(0.2f, 0.9f, 0.3f);
    [SerializeField] private Color hurtColor    = new Color(0.9f, 0.9f, 0.1f);
    [SerializeField] private Color criticalColor = new Color(0.9f, 0.15f, 0.1f);

    // ─── Internal ───────────────────────────────────────────────
    private PlayerHealth playerHealth;
    private float flashTimer;
    private float flashDuration = 0.15f;

    // ═══════════════════════════════════════════════════════════
    // Public API
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Called to bind this UI to a specific player's health component.
    /// Typically called after the local player spawns.
    /// </summary>
    public void Initialize(PlayerHealth health)
    {
        // Unsubscribe from previous
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= UpdateHealthDisplay;
            playerHealth.OnDamageTaken   -= HandleDamage;
            playerHealth.OnDeath         -= HandleDeath;
            playerHealth.OnRespawn       -= HandleRespawn;
        }

        playerHealth = health;

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += UpdateHealthDisplay;
            playerHealth.OnDamageTaken   += HandleDamage;
            playerHealth.OnDeath         += HandleDeath;
            playerHealth.OnRespawn       += HandleRespawn;

            flashDuration = health.MaxHealth > 0 ? 0.15f : 0.15f; // fallback

            // Sync UI immediately
            UpdateHealthDisplay(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }

        // Hide overlays initially
        if (damageFlashOverlay != null)
        {
            damageFlashOverlay.color = Color.clear;
        }

        if (deathOverlay != null)
        {
            deathOverlay.SetActive(false);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Unity Lifecycle
    // ═══════════════════════════════════════════════════════════

    private void Update()
    {
        // Fade out damage flash
        if (damageFlashOverlay != null && flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            float alpha = Mathf.Clamp01(flashTimer / flashDuration) * damageFlashColor.a;
            damageFlashOverlay.color = new Color(damageFlashColor.r, damageFlashColor.g, damageFlashColor.b, alpha);
        }
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= UpdateHealthDisplay;
            playerHealth.OnDamageTaken   -= HandleDamage;
            playerHealth.OnDeath         -= HandleDeath;
            playerHealth.OnRespawn       -= HandleRespawn;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Event Handlers
    // ═══════════════════════════════════════════════════════════

    private void UpdateHealthDisplay(int current, int max)
    {
        float normalised = max > 0 ? (float)current / max : 0f;

        // Slider
        if (healthBar != null)
        {
            healthBar.value = normalised;
        }

        // Colour
        if (healthBarFill != null)
        {
            healthBarFill.color = GetHealthColor(normalised);
        }

        // Text
        if (healthText != null)
        {
            healthText.text = $"{current} / {max}";
        }
    }

    private void HandleDamage(int damage, ulong attackerClientId)
    {
        // Trigger damage flash
        if (damageFlashOverlay != null)
        {
            flashTimer = flashDuration;
            damageFlashOverlay.color = damageFlashColor;
        }
    }

    private void HandleDeath(ulong killerClientId)
    {
        if (deathOverlay != null)
        {
            deathOverlay.SetActive(true);
        }
    }

    private void HandleRespawn()
    {
        if (deathOverlay != null)
        {
            deathOverlay.SetActive(false);
        }

        // Reset flash
        if (damageFlashOverlay != null)
        {
            damageFlashOverlay.color = Color.clear;
            flashTimer = 0f;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Returns a colour that interpolates from red → yellow → green
    /// based on the normalised health value.
    /// </summary>
    private Color GetHealthColor(float normalised)
    {
        if (normalised > 0.5f)
        {
            // Green → Yellow
            float t = (normalised - 0.5f) * 2f;
            return Color.Lerp(hurtColor, healthyColor, t);
        }
        else
        {
            // Yellow → Red
            float t = normalised * 2f;
            return Color.Lerp(criticalColor, hurtColor, t);
        }
    }
}
