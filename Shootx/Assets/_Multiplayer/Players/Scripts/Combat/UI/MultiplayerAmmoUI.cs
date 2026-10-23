using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI component that displays the local player's basic and extra ammunition.
/// Shows clear visual distinction between recoverable basic ammo and permanent extra ammo.
/// Includes DOTween Pro feedback transitions on consumption, hits/refunds, pickups, and respawns.
/// </summary>
public class MultiplayerAmmoUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Primary text showing ammo status (e.g. 'Basic: 3/3 | Extra: 2').")]
    [SerializeField] private TextMeshProUGUI ammoText;

    [Tooltip("Dedicated text element for basic ammo (optional).")]
    [SerializeField] private TextMeshProUGUI basicAmmoText;

    [Tooltip("Dedicated text element for extra ammo (optional).")]
    [SerializeField] private TextMeshProUGUI extraAmmoText;

    [Tooltip("Floating/temporary feedback banner for refunds and pickups.")]
    [SerializeField] private TextMeshProUGUI feedbackNoticeText;

    [Header("Basic Ammo Pips (Optional)")]
    [Tooltip("3 visual icons/pips for basic ammunition units.")]
    [SerializeField] private Image[] basicPips = new Image[3];

    [Header("Colors")]
    [SerializeField] private Color basicActiveColor = new Color(0.2f, 0.85f, 1f, 1f);
    [SerializeField] private Color basicEmptyColor = new Color(0.3f, 0.3f, 0.35f, 0.4f);
    [SerializeField] private Color extraActiveColor = new Color(1f, 0.7f, 0.1f, 1f);
    [SerializeField] private Color refundColor = new Color(0.2f, 1f, 0.4f, 1f);

    [Header("Animation Settings")]
    [SerializeField] private float punchScale = 0.2f;
    [SerializeField] private float feedbackDuration = 1.0f;

    // ─── Legacy References (Preserved for Inspector safety) ────
    [Header("Legacy Elements")]
    [SerializeField] private Slider reloadBar;
    [SerializeField] private Button reloadButton;
    [SerializeField] private GameObject reloadIndicator;

    private PlayerShooting playerShooting;
    private Sequence _feedbackSeq;
    private Sequence _punchSeq;

    private void Awake()
    {
        if (ammoText == null && basicAmmoText == null)
        {
            BuildDefaultAmmoUI();
        }
    }

    private void BuildDefaultAmmoUI()
    {
        GameObject panel = new GameObject("AmmoHUDPanel", typeof(RectTransform));
        panel.transform.SetParent(transform, false);
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-40f, 40f);
        rt.sizeDelta = new Vector2(300f, 80f);

        GameObject textObj = new GameObject("AmmoText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(panel.transform, false);
        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        ammoText = textObj.GetComponent<TextMeshProUGUI>();
        ammoText.fontSize = 28f;
        ammoText.alignment = TextAlignmentOptions.BottomRight;
        ammoText.color = Color.white;
        ammoText.text = "Basic: 3/3";

        // Feedback notice
        GameObject noticeObj = new GameObject("FeedbackNotice", typeof(RectTransform), typeof(TextMeshProUGUI));
        noticeObj.transform.SetParent(panel.transform, false);
        RectTransform noticeRt = noticeObj.GetComponent<RectTransform>();
        noticeRt.anchorMin = new Vector2(0f, 1f);
        noticeRt.anchorMax = new Vector2(1f, 1f);
        noticeRt.pivot = new Vector2(0.5f, 0f);
        noticeRt.anchoredPosition = new Vector2(0f, 10f);
        noticeRt.sizeDelta = new Vector2(300f, 40f);

        feedbackNoticeText = noticeObj.GetComponent<TextMeshProUGUI>();
        feedbackNoticeText.fontSize = 22f;
        feedbackNoticeText.alignment = TextAlignmentOptions.Center;
        feedbackNoticeText.color = refundColor;
        feedbackNoticeText.alpha = 0f;
        feedbackNoticeText.gameObject.SetActive(false);
    }

    public void Initialize(PlayerShooting shooting)
    {
        // Unsubscribe from previous
        if (playerShooting != null)
        {
            playerShooting.OnAmmoChanged -= UpdateAmmoDisplay;
            playerShooting.OnBasicAmmoRefunded -= HandleBasicRefunded;
            playerShooting.OnAmmoPickupCollected -= HandlePickupCollected;
        }

        playerShooting = shooting;

        if (playerShooting != null)
        {
            playerShooting.OnAmmoChanged += UpdateAmmoDisplay;
            playerShooting.OnBasicAmmoRefunded += HandleBasicRefunded;
            playerShooting.OnAmmoPickupCollected += HandlePickupCollected;

            // Sync initial state
            UpdateAmmoDisplay(playerShooting.BasicAmmo, playerShooting.ExtraAmmo, animate: false);
        }

        // Hide feedback text initially
        if (feedbackNoticeText != null)
        {
            feedbackNoticeText.alpha = 0f;
            feedbackNoticeText.gameObject.SetActive(false);
        }

        if (reloadIndicator != null) reloadIndicator.SetActive(false);
        if (reloadBar != null) reloadBar.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        KillAllTweens();

        if (playerShooting != null)
        {
            playerShooting.OnAmmoChanged -= UpdateAmmoDisplay;
            playerShooting.OnBasicAmmoRefunded -= HandleBasicRefunded;
            playerShooting.OnAmmoPickupCollected -= HandlePickupCollected;
        }
    }

    private void UpdateAmmoDisplay(int basic, int extra)
    {
        UpdateAmmoDisplay(basic, extra, animate: true);
    }

    public void UpdateAmmoDisplay(int basic, int extra, bool animate)
    {
        // Combined text fallback
        if (ammoText != null)
        {
            if (extra > 0)
            {
                ammoText.text = $"Basic: {basic}/3 <color=#FFB703>| Extra: +{extra}</color>";
            }
            else
            {
                ammoText.text = $"Basic: {basic}/3";
            }
        }

        // Dedicated basic text
        if (basicAmmoText != null)
        {
            basicAmmoText.text = $"{basic}/3";
        }

        // Dedicated extra text
        if (extraAmmoText != null)
        {
            if (extra > 0)
            {
                extraAmmoText.gameObject.SetActive(true);
                extraAmmoText.text = $"+{extra} EX";
                extraAmmoText.color = extraActiveColor;
            }
            else
            {
                extraAmmoText.gameObject.SetActive(false);
            }
        }

        // Pips update
        if (basicPips != null)
        {
            for (int i = 0; i < basicPips.Length; i++)
            {
                if (basicPips[i] == null) continue;
                bool isFull = i < basic;
                basicPips[i].color = isFull ? basicActiveColor : basicEmptyColor;
            }
        }

        // Punch transition
        if (animate)
        {
            Transform target = basicAmmoText != null ? basicAmmoText.transform : (ammoText != null ? ammoText.transform : null);
            if (target != null)
            {
                if (_punchSeq != null && _punchSeq.IsActive()) _punchSeq.Kill();
                _punchSeq = DOTween.Sequence();
                _punchSeq.Append(target.DOPunchScale(Vector3.one * punchScale, 0.15f, 5, 0.5f));
            }
        }
    }

    private void HandleBasicRefunded()
    {
        ShowFeedbackNotice("+1 BULLET REFUNDED!", refundColor);

        // Green punch animation on ammo display
        Transform target = basicAmmoText != null ? basicAmmoText.transform : (ammoText != null ? ammoText.transform : null);
        if (target != null)
        {
            if (_punchSeq != null && _punchSeq.IsActive()) _punchSeq.Kill();
            _punchSeq = DOTween.Sequence();
            _punchSeq.Append(target.DOPunchScale(Vector3.one * (punchScale * 1.5f), 0.25f, 6, 0.6f));
        }
    }

    private void HandlePickupCollected()
    {
        ShowFeedbackNotice("+1 AMMO PICKUP!", extraActiveColor);

        Transform target = extraAmmoText != null ? extraAmmoText.transform : (ammoText != null ? ammoText.transform : null);
        if (target != null)
        {
            if (_punchSeq != null && _punchSeq.IsActive()) _punchSeq.Kill();
            _punchSeq = DOTween.Sequence();
            _punchSeq.Append(target.DOPunchScale(Vector3.one * (punchScale * 1.4f), 0.2f, 5, 0.5f));
        }
    }

    private void ShowFeedbackNotice(string message, Color color)
    {
        if (feedbackNoticeText == null) return;

        if (_feedbackSeq != null && _feedbackSeq.IsActive()) _feedbackSeq.Kill();

        feedbackNoticeText.gameObject.SetActive(true);
        feedbackNoticeText.text = message;
        feedbackNoticeText.color = color;
        feedbackNoticeText.alpha = 0f;
        feedbackNoticeText.transform.localScale = Vector3.one * 0.8f;

        _feedbackSeq = DOTween.Sequence();
        _feedbackSeq.Append(feedbackNoticeText.DOFade(1f, 0.15f));
        _feedbackSeq.Join(feedbackNoticeText.transform.DOScale(1f, 0.15f).SetEase(Ease.OutBack));
        _feedbackSeq.AppendInterval(feedbackDuration);
        _feedbackSeq.Append(feedbackNoticeText.DOFade(0f, 0.25f));
        _feedbackSeq.OnComplete(() =>
        {
            if (feedbackNoticeText != null)
            {
                feedbackNoticeText.gameObject.SetActive(false);
            }
        });
    }

    private void UpdateReloadState(bool reloading)
    {
        if (reloadIndicator != null) reloadIndicator.SetActive(reloading);
        if (reloadBar != null) reloadBar.gameObject.SetActive(reloading);
    }

    private void KillAllTweens()
    {
        if (_punchSeq != null && _punchSeq.IsActive())
        {
            _punchSeq.Kill();
            _punchSeq = null;
        }

        if (_feedbackSeq != null && _feedbackSeq.IsActive())
        {
            _feedbackSeq.Kill();
            _feedbackSeq = null;
        }
    }
}
