using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerWorldStatusUI : MonoBehaviour
{
    [Header("Placement Configuration")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.05f, -1.0f);
    [SerializeField] private bool lockRotationToCamera = true;

    [Header("Player Name")]
    [SerializeField] private TextMeshProUGUI playerNameText;

    [Header("Three-Segment Health Bars")]
    [SerializeField] private Image[] healthSegments = new Image[3];

    [Header("Health Bar Colors")]
    [SerializeField] private Color healthyColor = new Color(0.15f, 0.9f, 0.35f, 1f);
    [SerializeField] private Color hurtColor = new Color(0.95f, 0.75f, 0.15f, 1f);
    [SerializeField] private Color criticalColor = new Color(0.95f, 0.2f, 0.2f, 1f);

    [Header("Ammunition Displays")]
    [SerializeField] private TextMeshProUGUI basicAmmoText;
    [SerializeField] private TextMeshProUGUI extraAmmoText;

    [Header("Ammunition Effects Colors")]
    [SerializeField] private Color ammoDefaultColor = new Color(0.3f, 0.8f, 1f, 1f);
    [SerializeField] private Color ammoIncreaseColor = new Color(0.2f, 1f, 0.4f, 1f);
    [SerializeField] private Color ammoDecreaseColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    [SerializeField] private Color ammoEmptyColor = Color.red;

    [Header("Animation")]
    [SerializeField] private float fillAnimDuration = 0.25f;

    // ─── Internal References ────────────────────────────────────
    private Canvas _worldCanvas;
    private PlayerHealth _playerHealth;
    private PlayerShooting _playerShooting;
    private PlayerIdentity _playerIdentity;
    private Transform _followTarget;
    private Camera _mainCamera;

    private readonly Tween[] _healthTweens = new Tween[3];
    private Sequence _ammoSeq;

    private int _lastTotalAmmo = -1;

    private void Awake()
    {
        _worldCanvas = GetComponent<Canvas>();
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
                GameObject bgObj = new GameObject($"HealthBg_{i}", typeof(RectTransform), typeof(Image));
                bgObj.transform.SetParent(barContainer.transform, false);
                RectTransform bgRt = bgObj.GetComponent<RectTransform>();
                bgRt.anchoredPosition = new Vector2(x, 0f);
                bgRt.sizeDelta = new Vector2(segWidth, 0.12f);
                Image bgImg = bgObj.GetComponent<Image>();
                bgImg.sprite = whiteSprite;
                bgImg.color = new Color(0.12f, 0.12f, 0.14f, 0.85f);

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
            basicAmmoText.color = ammoDefaultColor;
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

        if (_playerHealth != null)
        {
            UpdateHealthDisplay(_playerHealth.CurrentHealth, _playerHealth.MaxHealth, animate: false);
        }

        if (_playerShooting != null)
        {
            _lastTotalAmmo = _playerShooting.BasicAmmo + _playerShooting.ExtraAmmo;
            UpdateAmmoDisplay(_playerShooting.BasicAmmo, _playerShooting.ExtraAmmo, animate: false);

            if (_playerShooting.IsOwner)
            {
                AimFireJoystick.LocalPlayerHasAmmo = (_lastTotalAmmo) > 0;
            }
        }

        if (_playerIdentity != null)
        {
            UpdateNameDisplay(_playerIdentity.PlayerName);
        }

        SetStatusUIVisible(_playerHealth == null || !_playerHealth.IsDead);
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

            if (_playerShooting.IsOwner)
            {
                AimFireJoystick.OnLocalDryFireAttempt += TriggerEmptyAmmoEffect;
            }
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

            if (_playerShooting.IsOwner)
            {
                AimFireJoystick.OnLocalDryFireAttempt -= TriggerEmptyAmmoEffect;
            }
        }

        if (_playerIdentity != null)
        {
            _playerIdentity.OnPlayerNameChanged -= UpdateNameDisplay;
        }
    }

    private void LateUpdate()
    {
        if (_worldCanvas != null && !_worldCanvas.enabled) return;
        if (_followTarget == null) return;

        transform.position = _followTarget.position + localOffset;

        if (lockRotationToCamera)
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera != null)
            {
                transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            }
        }
    }

    // ─── Health Presentation ─────────────
    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        UpdateHealthDisplay(currentHealth, maxHealth, animate: true);
    }

    public void UpdateHealthDisplay(float currentHealth, float maxHealth, bool animate = true)
    {
        if (healthSegments == null || healthSegments.Length < 3) return;

        Color segmentColor = GetHealthColor(currentHealth, maxHealth);

        for (int i = 0; i < 3; i++)
        {
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

    // ─── Ammunition Presentation & Effects ────────────────────────
    private void HandleAmmoChanged(int basic, int extra)
    {
        UpdateAmmoDisplay(basic, extra, animate: true);

        if (_playerShooting != null && _playerShooting.IsOwner)
        {
            AimFireJoystick.LocalPlayerHasAmmo = (basic + extra) > 0;
        }
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

        int currentTotal = basic + extra;

        if (animate && basicAmmoText != null && _lastTotalAmmo != -1)
        {
            if (currentTotal > _lastTotalAmmo)
            {
                TriggerAmmoIncreaseEffect();
            }
            else if (currentTotal < _lastTotalAmmo)
            {
                TriggerAmmoDecreaseEffect();
            }
        }

        _lastTotalAmmo = currentTotal;
    }

    private void TriggerAmmoDecreaseEffect()
    {
        if (basicAmmoText == null) return;

        KillAmmoTween();
        _ammoSeq = DOTween.Sequence();

        _ammoSeq.Append(basicAmmoText.transform.DOPunchScale(Vector3.one * -0.15f, 0.15f, 1, 0.5f));
        _ammoSeq.Join(basicAmmoText.DOColor(ammoDecreaseColor, 0.05f));
        _ammoSeq.Append(basicAmmoText.DOColor(ammoDefaultColor, 0.15f));
    }

    private void TriggerAmmoIncreaseEffect()
    {
        if (basicAmmoText == null) return;

        KillAmmoTween();
        _ammoSeq = DOTween.Sequence();

        _ammoSeq.Append(basicAmmoText.transform.DOScale(1.3f, 0.15f));
        _ammoSeq.Join(basicAmmoText.DOColor(ammoIncreaseColor, 0.15f));

        _ammoSeq.Append(basicAmmoText.transform.DOScale(1f, 0.2f).SetEase(Ease.OutBounce));
        _ammoSeq.Join(basicAmmoText.DOColor(ammoDefaultColor, 0.2f));
    }

    private void HandleAmmoRefunded()
    {
        if (basicAmmoText == null) return;

        KillAmmoTween();
        _ammoSeq = DOTween.Sequence();

        _ammoSeq.Append(basicAmmoText.transform.DOScale(1.4f, 0.1f));
        _ammoSeq.Join(basicAmmoText.transform.DOPunchRotation(new Vector3(0, 0, 10f), 0.25f, 6, 1f));
        _ammoSeq.Join(basicAmmoText.DOColor(ammoIncreaseColor, 0.1f));

        _ammoSeq.Append(basicAmmoText.transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack));
        _ammoSeq.Join(basicAmmoText.DOColor(ammoDefaultColor, 0.2f));
    }

    private void TriggerEmptyAmmoEffect()
    {
        if (basicAmmoText == null) return;

        KillAmmoTween();
        _ammoSeq = DOTween.Sequence();

        _ammoSeq.Append(basicAmmoText.transform.DOScale(1.5f, 0.15f));
        _ammoSeq.Join(basicAmmoText.transform.DOPunchRotation(new Vector3(0, 0, 15f), 0.35f, 10, 1f));
        _ammoSeq.Join(basicAmmoText.DOColor(ammoEmptyColor, 0.15f));

        _ammoSeq.Append(basicAmmoText.transform.DOScale(1f, 0.15f));
        _ammoSeq.Join(basicAmmoText.DOColor(ammoDefaultColor, 0.15f));
    }

    // ────────────────────────────────────────────────────────────

    private void UpdateNameDisplay(string newName)
    {
        if (playerNameText != null)
        {
            playerNameText.text = string.IsNullOrWhiteSpace(newName) ? "Player" : newName;
        }
    }

    private void HandleDeath(ulong killerId)
    {
        UpdateHealthDisplay(0f, 3f, animate: false);
        SetStatusUIVisible(false);
    }

    private void HandleRespawn()
    {
        SetStatusUIVisible(true);

        if (_playerHealth != null)
        {
            UpdateHealthDisplay(_playerHealth.MaxHealth, _playerHealth.MaxHealth, animate: false);
        }

        if (_playerShooting != null)
        {
            _lastTotalAmmo = _playerShooting.BasicAmmo + _playerShooting.ExtraAmmo;
            UpdateAmmoDisplay(_playerShooting.BasicAmmo, _playerShooting.ExtraAmmo, animate: false);

            if (_playerShooting.IsOwner)
            {
                AimFireJoystick.LocalPlayerHasAmmo = _lastTotalAmmo > 0;
            }
        }
    }

    public void SetStatusUIVisible(bool visible)
    {
        if (_worldCanvas == null) _worldCanvas = GetComponent<Canvas>();
        if (_worldCanvas != null)
        {
            _worldCanvas.enabled = visible;
        }
    }

    private void KillAmmoTween()
    {
        if (_ammoSeq != null && _ammoSeq.IsActive())
        {
            _ammoSeq.Kill(complete: true);
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