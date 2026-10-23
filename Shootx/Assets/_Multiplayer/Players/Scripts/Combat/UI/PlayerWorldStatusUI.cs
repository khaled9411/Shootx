using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space player status UI positioned strictly BELOW each character.
/// Displays synchronized player name, three-segment health bar (with fractional fill),
/// basic ammunition count, and extra ammunition count.
/// 
/// Billboards/locks rotation to maintain readable presentation for the top-down camera
/// regardless of the character's movement and aiming orientation.
/// </summary>
public class PlayerWorldStatusUI : MonoBehaviour
{
    [Header("Placement Configuration")]
    [Tooltip("Local position offset positioned strictly BELOW the character on the ground plane.")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.05f, -1.0f);

    [Tooltip("Lock rotation so that character spinning does not rotate the status UI.")]
    [SerializeField] private bool lockRotationToCamera = true;

    [Header("Player Name")]
    [SerializeField] private TextMeshProUGUI playerNameText;

    [Header("Three-Segment Health Bars")]
    [Tooltip("The three health fill images representing health units 0-1, 1-2, and 2-3.")]
    [SerializeField] private Image[] healthSegments = new Image[3];

    [Header("Health Bar Colors")]
    [SerializeField] private Color healthyColor = new Color(0.15f, 0.9f, 0.35f, 1f);
    [SerializeField] private Color hurtColor = new Color(0.95f, 0.75f, 0.15f, 1f);
    [SerializeField] private Color criticalColor = new Color(0.95f, 0.2f, 0.2f, 1f);

    [Header("Ammunition Displays")]
    [Tooltip("Displays basic ammunition (e.g. '3/3' or 'B: 3').")]
    [SerializeField] private TextMeshProUGUI basicAmmoText;

    [Tooltip("Displays extra ammunition (e.g. '+2 Extra').")]
    [SerializeField] private TextMeshProUGUI extraAmmoText;

    [Header("Animation")]
    [SerializeField] private float fillAnimDuration = 0.25f;
    [SerializeField] private float punchScaleAmount = 0.15f;

    // ─── Internal References ────────────────────────────────────
    private PlayerHealth _playerHealth;
    private PlayerShooting _playerShooting;
    private PlayerIdentity _playerIdentity;
    private Transform _followTarget;
    private Camera _mainCamera;
    private readonly Tween[] _healthTweens = new Tween[3];
    private Sequence _ammoSeq;

    private void Awake()
    {
        _mainCamera = Camera.main;
        _followTarget = transform.parent;

        if (healthSegments == null || healthSegments.Length < 3 || healthSegments[0] == null)
        {
            BuildDefaultStatusUI();
        }
    }

    private void Start()
    {
        InitializeComponents();
    }

    private void BuildDefaultStatusUI()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
        }

        RectTransform rootRt = GetComponent<RectTransform>();
        if (rootRt == null) rootRt = gameObject.AddComponent<RectTransform>();
        rootRt.sizeDelta = new Vector2(1.6f, 0.6f);

        Sprite whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f, 100f);

        // 1. Player Name
        if (playerNameText == null)
        {
            GameObject nameObj = new GameObject("PlayerNameText", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameObj.transform.SetParent(transform, false);
            RectTransform nameRt = nameObj.GetComponent<RectTransform>();
            nameRt.anchoredPosition = new Vector2(0f, 0.22f);
            nameRt.sizeDelta = new Vector2(2.5f, 0.25f);
            playerNameText = nameObj.GetComponent<TextMeshProUGUI>();
            playerNameText.fontSize = 1.8f;
            playerNameText.alignment = TextAlignmentOptions.Center;
            playerNameText.color = Color.white;
            playerNameText.text = "Player";
        }

        // 2. Health Segment Bars
        if (healthSegments == null || healthSegments.Length < 3 || healthSegments[0] == null)
        {
            healthSegments = new Image[3];
            GameObject barContainer = new GameObject("HealthBars", typeof(RectTransform));
            barContainer.transform.SetParent(transform, false);
            RectTransform barContainerRt = barContainer.GetComponent<RectTransform>();
            barContainerRt.anchoredPosition = new Vector2(0f, 0.05f);
            barContainerRt.sizeDelta = new Vector2(1.5f, 0.15f);

            float segWidth = 0.44f;
            float segSpacing = 0.04f;
            float totalWidth = 3 * segWidth + 2 * segSpacing;
            float startX = -totalWidth / 2f + segWidth / 2f;

            for (int i = 0; i < 3; i++)
            {
                float x = startX + i * (segWidth + segSpacing);

                // Slot background
                GameObject bgObj = new GameObject($"HealthBg_{i}", typeof(RectTransform), typeof(Image));
                bgObj.transform.SetParent(barContainer.transform, false);
                RectTransform bgRt = bgObj.GetComponent<RectTransform>();
                bgRt.anchoredPosition = new Vector2(x, 0f);
                bgRt.sizeDelta = new Vector2(segWidth, 0.12f);
                Image bgImg = bgObj.GetComponent<Image>();
                bgImg.sprite = whiteSprite;
                bgImg.color = new Color(0.12f, 0.12f, 0.14f, 0.85f);

                // Fill image
                GameObject fillObj = new GameObject($"HealthFill_{i}", typeof(RectTransform), typeof(Image));
                fillObj.transform.SetParent(bgObj.transform, false);
                RectTransform fillRt = fillObj.GetComponent<RectTransform>();
                fillRt.anchorMin = Vector2.zero;
                fillRt.anchorMax = Vector2.one;
                fillRt.sizeDelta = Vector2.zero;
                Image fillImg = fillObj.GetComponent<Image>();
                fillImg.sprite = whiteSprite;
                fillImg.type = Image.Type.Filled;
                fillImg.fillMethod = Image.FillMethod.Horizontal;
                fillImg.fillOrigin = 0;
                fillImg.color = healthyColor;
                fillImg.fillAmount = 1f;

                healthSegments[i] = fillImg;
            }
        }

        // 3. Ammunition Text
        if (basicAmmoText == null)
        {
            GameObject ammoContainer = new GameObject("AmmoDisplay", typeof(RectTransform));
            ammoContainer.transform.SetParent(transform, false);
            RectTransform ammoContainerRt = ammoContainer.GetComponent<RectTransform>();
            ammoContainerRt.anchoredPosition = new Vector2(0f, -0.12f);
            ammoContainerRt.sizeDelta = new Vector2(1.5f, 0.2f);

            GameObject basicObj = new GameObject("BasicAmmoText", typeof(RectTransform), typeof(TextMeshProUGUI));
            basicObj.transform.SetParent(ammoContainer.transform, false);
            RectTransform basicRt = basicObj.GetComponent<RectTransform>();
            basicRt.anchoredPosition = new Vector2(-0.35f, 0f);
            basicRt.sizeDelta = new Vector2(0.8f, 0.2f);
            basicAmmoText = basicObj.GetComponent<TextMeshProUGUI>();
            basicAmmoText.fontSize = 1.4f;
            basicAmmoText.alignment = TextAlignmentOptions.Center;
            basicAmmoText.color = new Color(0.3f, 0.8f, 1f, 1f);
            basicAmmoText.text = "B: 3/3";

            GameObject extraObj = new GameObject("ExtraAmmoText", typeof(RectTransform), typeof(TextMeshProUGUI));
            extraObj.transform.SetParent(ammoContainer.transform, false);
            RectTransform extraRt = extraObj.GetComponent<RectTransform>();
            extraRt.anchoredPosition = new Vector2(0.35f, 0f);
            extraRt.sizeDelta = new Vector2(0.8f, 0.2f);
            extraAmmoText = extraObj.GetComponent<TextMeshProUGUI>();
            extraAmmoText.fontSize = 1.4f;
            extraAmmoText.alignment = TextAlignmentOptions.Center;
            extraAmmoText.color = new Color(1f, 0.8f, 0.2f, 1f);
            extraAmmoText.text = "+0 EX";
            extraObj.SetActive(false);
        }
    }

    public void Initialize(PlayerHealth health, PlayerShooting shooting, PlayerIdentity identity = null)
    {
        UnsubscribeEvents();

        _playerHealth = health;
        _playerShooting = shooting;
        _playerIdentity = identity != null ? identity : GetComponentInParent<PlayerIdentity>();

        SubscribeEvents();

        // Initial sync
        if (_playerHealth != null)
        {
            UpdateHealthDisplay(_playerHealth.CurrentHealth, _playerHealth.MaxHealth, animate: false);
        }

        if (_playerShooting != null)
        {
            UpdateAmmoDisplay(_playerShooting.BasicAmmo, _playerShooting.ExtraAmmo, animate: false);
        }

        if (_playerIdentity != null)
        {
            UpdateNameDisplay(_playerIdentity.PlayerName);
        }
    }

    private void InitializeComponents()
    {
        if (_playerHealth == null) _playerHealth = GetComponentInParent<PlayerHealth>();
        if (_playerShooting == null) _playerShooting = GetComponentInParent<PlayerShooting>();
        if (_playerIdentity == null) _playerIdentity = GetComponentInParent<PlayerIdentity>();

        if (_playerHealth != null && _playerShooting != null)
        {
            Initialize(_playerHealth, _playerShooting, _playerIdentity);
        }
    }

    private void SubscribeEvents()
    {
        if (_playerHealth != null)
        {
            _playerHealth.OnHealthChanged += HandleHealthChanged;
            _playerHealth.OnDeath += HandleDeath;
            _playerHealth.OnRespawn += HandleRespawn;
        }

        if (_playerShooting != null)
        {
            _playerShooting.OnAmmoChanged += HandleAmmoChanged;
            _playerShooting.OnBasicAmmoRefunded += HandleAmmoRefunded;
        }

        if (_playerIdentity != null)
        {
            _playerIdentity.OnPlayerNameChanged += UpdateNameDisplay;
        }
    }

    private void UnsubscribeEvents()
    {
        if (_playerHealth != null)
        {
            _playerHealth.OnHealthChanged -= HandleHealthChanged;
            _playerHealth.OnDeath -= HandleDeath;
            _playerHealth.OnRespawn -= HandleRespawn;
        }

        if (_playerShooting != null)
        {
            _playerShooting.OnAmmoChanged -= HandleAmmoChanged;
            _playerShooting.OnBasicAmmoRefunded -= HandleAmmoRefunded;
        }

        if (_playerIdentity != null)
        {
            _playerIdentity.OnPlayerNameChanged -= UpdateNameDisplay;
        }
    }

    private void LateUpdate()
    {
        if (_followTarget == null) return;

        // Position strictly below player root
        transform.position = _followTarget.position + localOffset;

        // Maintain fixed readable orientation facing camera
        if (lockRotationToCamera)
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera != null)
            {
                // Top-down camera: face directly upward / toward camera tilt
                transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            }
        }
    }

    // ─── Health Presentation (Fractional 3-Segment) ─────────────

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        UpdateHealthDisplay(currentHealth, maxHealth, animate: true);
    }

    /// <summary>
    /// Updates the three health segments with exact fractional fill.
    /// Segment 0: 0.0 -> 1.0
    /// Segment 1: 1.0 -> 2.0
    /// Segment 2: 2.0 -> 3.0
    /// </summary>
    public void UpdateHealthDisplay(float currentHealth, float maxHealth, bool animate = true)
    {
        if (healthSegments == null || healthSegments.Length < 3) return;

        Color segmentColor = GetHealthColor(currentHealth, maxHealth);

        for (int i = 0; i < 3; i++)
        {
            // Segment i covers the range [i, i + 1]
            float segmentTargetFill = Mathf.Clamp01(currentHealth - i);
            Image img = healthSegments[i];
            if (img == null) continue;

            img.color = segmentColor;

            if (_healthTweens[i] != null && _healthTweens[i].IsActive())
            {
                _healthTweens[i].Kill();
            }

            if (animate)
            {
                _healthTweens[i] = img.DOFillAmount(segmentTargetFill, fillAnimDuration).SetEase(Ease.OutQuad);
            }
            else
            {
                img.fillAmount = segmentTargetFill;
            }
        }
    }

    private Color GetHealthColor(float current, float max)
    {
        float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        if (ratio > 0.5f)
        {
            float t = (ratio - 0.5f) * 2f;
            return Color.Lerp(hurtColor, healthyColor, t);
        }
        else
        {
            float t = ratio * 2f;
            return Color.Lerp(criticalColor, hurtColor, t);
        }
    }

    // ─── Ammunition Presentation ────────────────────────────────

    private void HandleAmmoChanged(int basic, int extra)
    {
        UpdateAmmoDisplay(basic, extra, animate: true);
    }

    public void UpdateAmmoDisplay(int basic, int extra, bool animate = true)
    {
        if (basicAmmoText != null)
        {
            basicAmmoText.text = $"B: {basic}/3";
        }

        if (extraAmmoText != null)
        {
            if (extra > 0)
            {
                extraAmmoText.gameObject.SetActive(true);
                extraAmmoText.text = $"+{extra} EX";
            }
            else
            {
                extraAmmoText.gameObject.SetActive(false);
            }
        }

        if (animate && basicAmmoText != null)
        {
            KillAmmoTween();
            _ammoSeq = DOTween.Sequence();
            _ammoSeq.Append(basicAmmoText.transform.DOPunchScale(Vector3.one * punchScaleAmount, 0.15f, 5, 0.5f));
        }
    }

    private void HandleAmmoRefunded()
    {
        if (basicAmmoText != null)
        {
            KillAmmoTween();
            _ammoSeq = DOTween.Sequence();
            _ammoSeq.Append(basicAmmoText.transform.DOPunchScale(Vector3.one * (punchScaleAmount * 1.5f), 0.25f, 6, 0.6f));
        }
    }

    private void UpdateNameDisplay(string newName)
    {
        if (playerNameText != null)
        {
            playerNameText.text = string.IsNullOrWhiteSpace(newName) ? "Player" : newName;
        }
    }

    private void HandleDeath(ulong killerId)
    {
        UpdateHealthDisplay(0f, 3f, animate: true);
    }

    private void HandleRespawn()
    {
        if (_playerHealth != null)
        {
            UpdateHealthDisplay(_playerHealth.MaxHealth, _playerHealth.MaxHealth, animate: false);
        }

        if (_playerShooting != null)
        {
            UpdateAmmoDisplay(_playerShooting.BasicAmmo, _playerShooting.ExtraAmmo, animate: false);
        }
    }

    private void KillAmmoTween()
    {
        if (_ammoSeq != null && _ammoSeq.IsActive())
        {
            _ammoSeq.Kill();
            _ammoSeq = null;
        }
    }

    private void KillAllTweens()
    {
        KillAmmoTween();
        for (int i = 0; i < _healthTweens.Length; i++)
        {
            if (_healthTweens[i] != null && _healthTweens[i].IsActive())
            {
                _healthTweens[i].Kill();
                _healthTweens[i] = null;
            }
        }
    }

    private void OnDisable()
    {
        KillAllTweens();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
        KillAllTweens();
    }
}
