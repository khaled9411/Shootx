using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI component that displays health, damage flash, 3-segment representation, and death overlay.
/// Binds to the local player's PlayerHealth after spawn.
/// Upgraded to support fractional health (e.g. 1.5 / 3 units) and DOTween Pro animations.
/// </summary>
public class MultiplayerHealthUI : MonoBehaviour
{
    [Header("Health Bar (Slider Fallback)")]
    [Tooltip("Slider that represents the health bar. Value is normalised 0-1.")]
    [SerializeField] private Slider healthBar;

    [Tooltip("Optional fill image inside the slider — used to tint the bar colour.")]
    [SerializeField] private Image healthBarFill;

    [Header("Three-Segment Health Bars (HUD)")]
    [Tooltip("Optional 3 segment fill images representing 1 unit each (0-1, 1-2, 2-3).")]
    [SerializeField] private Image[] healthSegments = new Image[3];

    [Header("Health Text")]
    [Tooltip("Text element showing current HP (e.g. '1.5 / 3').")]
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

    [Header("Animation Durations")]
    [SerializeField] private float barAnimDuration = 0.25f;
    [SerializeField] private float flashDuration = 0.15f;

    // ─── Internal ───────────────────────────────────────────────
    private PlayerHealth playerHealth;
    private Tween _sliderTween;
    private Tween _flashTween;
    private readonly Tween[] _segmentTweens = new Tween[3];

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

            // Sync UI immediately
            UpdateHealthDisplay(playerHealth.CurrentHealth, playerHealth.MaxHealth, animate: false);
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

    private void OnDestroy()
    {
        KillAllTweens();

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= UpdateHealthDisplay;
            playerHealth.OnDamageTaken   -= HandleDamage;
            playerHealth.OnDeath         -= HandleDeath;
            playerHealth.OnRespawn       -= HandleRespawn;
        }
    }

    // ─── Event Handlers ─────────────────────────────────────────

    private void UpdateHealthDisplay(float current, float max)
    {
        UpdateHealthDisplay(current, max, animate: true);
    }

    public void UpdateHealthDisplay(float current, float max, bool animate)
    {
        float normalised = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        // Slider animation
        if (healthBar != null)
        {
            if (_sliderTween != null && _sliderTween.IsActive()) _sliderTween.Kill();
            if (animate)
            {
                _sliderTween = healthBar.DOValue(normalised, barAnimDuration).SetEase(Ease.OutQuad);
            }
            else
            {
                healthBar.value = normalised;
            }
        }

        // Tint fill color
        if (healthBarFill != null)
        {
            healthBarFill.color = GetHealthColor(normalised);
        }

        // 3-Segment fill calculation
        if (healthSegments != null && healthSegments.Length >= 3)
        {
            Color segColor = GetHealthColor(normalised);
            for (int i = 0; i < 3; i++)
            {
                Image segImg = healthSegments[i];
                if (segImg == null) continue;

                float segTarget = Mathf.Clamp01(current - i);
                segImg.color = segColor;

                if (_segmentTweens[i] != null && _segmentTweens[i].IsActive()) _segmentTweens[i].Kill();
                if (animate)
                {
                    _segmentTweens[i] = segImg.DOFillAmount(segTarget, barAnimDuration).SetEase(Ease.OutQuad);
                }
                else
                {
                    segImg.fillAmount = segTarget;
                }
            }
        }

        // Text representation (e.g. "1.5 / 3")
        if (healthText != null)
        {
            healthText.text = $"{current:0.#} / {max:0.#}";
        }
    }

    private void HandleDamage(float damage, ulong attackerClientId)
    {
        // Damage flash animation using DOTween
        if (damageFlashOverlay != null)
        {
            if (_flashTween != null && _flashTween.IsActive()) _flashTween.Kill();

            damageFlashOverlay.color = damageFlashColor;
            _flashTween = damageFlashOverlay.DOFade(0f, flashDuration).SetEase(Ease.InQuad);
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

        if (damageFlashOverlay != null)
        {
            damageFlashOverlay.color = Color.clear;
        }

        if (playerHealth != null)
        {
            UpdateHealthDisplay(playerHealth.MaxHealth, playerHealth.MaxHealth, animate: false);
        }
    }

    private Color GetHealthColor(float normalised)
    {
        if (normalised > 0.5f)
        {
            float t = (normalised - 0.5f) * 2f;
            return Color.Lerp(hurtColor, healthyColor, t);
        }
        else
        {
            float t = normalised * 2f;
            return Color.Lerp(criticalColor, hurtColor, t);
        }
    }

    private void KillAllTweens()
    {
        if (_sliderTween != null && _sliderTween.IsActive()) _sliderTween.Kill();
        if (_flashTween != null && _flashTween.IsActive()) _flashTween.Kill();

        for (int i = 0; i < _segmentTweens.Length; i++)
        {
            if (_segmentTweens[i] != null && _segmentTweens[i].IsActive())
            {
                _segmentTweens[i].Kill();
                _segmentTweens[i] = null;
            }
        }
    }
}
