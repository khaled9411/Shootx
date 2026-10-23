using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Dedicated mobile Dash ability button with circular radial cooldown indicator,
/// countdown text, DOTween scale punch, and pointer-ID touch isolation.
///
/// Designed to sit directly beside the MeleeButton in the MobileControlsHUD combat cluster.
/// </summary>
public class MobileDashButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    // ─── Inspector References ───────────────────────────────────
    [Header("Input Dependency")]
    [SerializeField] private InputReader inputReader;

    [Header("UI Component References")]
    [SerializeField] private RectTransform buttonRect;
    [SerializeField] private Image buttonBackground;
    [SerializeField] private Image cooldownRadialFill;
    [SerializeField] private Image dashIcon;
    [SerializeField] private TextMeshProUGUI cooldownText;

    [Header("Color States")]
    [Tooltip("Color of the button background when the ability is ready.")]
    [SerializeField] private Color readyColor = new Color(0.12f, 0.7f, 0.95f, 0.9f);

    [Tooltip("Color of the button background while dashing or on cooldown.")]
    [SerializeField] private Color cooldownColor = new Color(0.25f, 0.3f, 0.35f, 0.6f);

    [Tooltip("Color flashed when the button is actively pressed.")]
    [SerializeField] private Color pressedColor = new Color(0.4f, 0.95f, 1f, 1f);

    [Header("Animation Settings")]
    [SerializeField] private float punchScaleAmount = 0.15f;
    [SerializeField] private float punchDuration = 0.15f;
    [SerializeField] private float readyPulseAmount = 0.12f;

    // ─── Internal State ─────────────────────────────────────────
    private PlayerDash boundPlayerDash;
    private int activePointerId = -1;
    private bool wasOnCooldown = false;

    private Tween punchTween;
    private Tween colorTween;
    private Tween readyPulseTween;

    private void Awake()
    {
        if (buttonRect == null) buttonRect = GetComponent<RectTransform>();
        if (buttonBackground == null) buttonBackground = GetComponent<Image>();

        EnsureRadialFillConfiguration();
        SetVisualNormal();
    }

    private void OnEnable()
    {
        PlayerDash.OnLocalPlayerDashSpawned += HandleLocalPlayerDashSpawned;
        TryAutoBindLocalPlayer();
    }

    private void OnDisable()
    {
        PlayerDash.OnLocalPlayerDashSpawned -= HandleLocalPlayerDashSpawned;
        UnbindPlayer();
        ResetButton();
    }

    private void OnDestroy()
    {
        PlayerDash.OnLocalPlayerDashSpawned -= HandleLocalPlayerDashSpawned;
        UnbindPlayer();
        ResetButton();
    }

    private void EnsureRadialFillConfiguration()
    {
        if (cooldownRadialFill != null)
        {
            cooldownRadialFill.type = Image.Type.Filled;
            cooldownRadialFill.fillMethod = Image.FillMethod.Radial360;
            cooldownRadialFill.fillOrigin = (int)Image.Origin360.Top;
            cooldownRadialFill.fillClockwise = false;
        }
    }

    // ─── Player Binding ─────────────────────────────────────────

    private void HandleLocalPlayerDashSpawned(PlayerDash playerDash)
    {
        if (playerDash != null && playerDash.IsOwner)
        {
            BindPlayer(playerDash);
        }
    }

    private void TryAutoBindLocalPlayer()
    {
        if (boundPlayerDash != null) return;

        PlayerDash[] dashes = FindObjectsByType<PlayerDash>(FindObjectsSortMode.None);
        foreach (var dash in dashes)
        {
            if (dash.IsOwner)
            {
                BindPlayer(dash);
                break;
            }
        }
    }

    /// <summary>
    /// Binds the UI button to the local player's PlayerDash instance.
    /// </summary>
    public void BindPlayer(PlayerDash playerDash)
    {
        if (boundPlayerDash == playerDash) return;

        UnbindPlayer();
        boundPlayerDash = playerDash;

        if (boundPlayerDash != null)
        {
            boundPlayerDash.OnCooldownComplete += HandleCooldownComplete;
        }
    }

    /// <summary>
    /// Unbinds from the current PlayerDash instance.
    /// </summary>
    public void UnbindPlayer()
    {
        if (boundPlayerDash != null)
        {
            boundPlayerDash.OnCooldownComplete -= HandleCooldownComplete;
            boundPlayerDash = null;
        }
    }

    private void HandleCooldownComplete()
    {
        PlayReadyPulse();
    }

    // ─── Input Handling ─────────────────────────────────────────

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointerId != -1) return; // Dedicated finger only

        // Reject if ability is not ready
        if (boundPlayerDash != null && !boundPlayerDash.IsReady) return;

        activePointerId = eventData.pointerId;

        // Visual press punch
        if (buttonRect != null)
        {
            if (punchTween != null && punchTween.IsActive()) punchTween.Kill();
            punchTween = buttonRect.DOPunchScale(Vector3.one * punchScaleAmount, punchDuration, 8, 1f).SetUpdate(true);
        }

        if (buttonBackground != null)
        {
            if (colorTween != null && colorTween.IsActive()) colorTween.Kill();
            buttonBackground.color = pressedColor;
        }

        // Trigger centralized Dash request
        if (inputReader != null)
        {
            inputReader.TriggerDash();
        }
        else if (boundPlayerDash != null)
        {
            boundPlayerDash.TryDash();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId) return;

        activePointerId = -1;
        SetVisualNormal();
    }

    // ─── Visual Update Loop ─────────────────────────────────────

    private void Update()
    {
        if (boundPlayerDash == null)
        {
            TryAutoBindLocalPlayer();
            return;
        }

        bool isReady = boundPlayerDash.IsReady;

        if (isReady)
        {
            if (wasOnCooldown)
            {
                wasOnCooldown = false;
                PlayReadyPulse();
            }

            if (cooldownRadialFill != null)
            {
                cooldownRadialFill.fillAmount = 0f;
            }

            if (cooldownText != null && cooldownText.gameObject.activeSelf)
            {
                cooldownText.gameObject.SetActive(false);
            }

            if (activePointerId == -1 && buttonBackground != null)
            {
                buttonBackground.color = readyColor;
            }
        }
        else
        {
            wasOnCooldown = true;

            float remaining = boundPlayerDash.RemainingCooldown;
            float ratio = boundPlayerDash.CooldownProgress;

            if (cooldownRadialFill != null)
            {
                cooldownRadialFill.fillAmount = ratio;
            }

            if (cooldownText != null)
            {
                if (!cooldownText.gameObject.activeSelf)
                {
                    cooldownText.gameObject.SetActive(true);
                }

                // Format: 1 decimal place below 10s, integer above 10s
                cooldownText.text = remaining >= 10f
                    ? Mathf.CeilToInt(remaining).ToString()
                    : remaining.ToString("F1");
            }

            if (activePointerId == -1 && buttonBackground != null)
            {
                buttonBackground.color = cooldownColor;
            }
        }
    }

    private void SetVisualNormal()
    {
        if (buttonBackground != null)
        {
            if (colorTween != null && colorTween.IsActive()) colorTween.Kill();
            Color targetColor = (boundPlayerDash != null && !boundPlayerDash.IsReady) ? cooldownColor : readyColor;
            colorTween = buttonBackground.DOColor(targetColor, 0.12f).SetUpdate(true);
        }
    }

    private void PlayReadyPulse()
    {
        if (buttonRect == null) return;

        if (readyPulseTween != null && readyPulseTween.IsActive()) readyPulseTween.Kill();
        readyPulseTween = buttonRect.DOPunchScale(Vector3.one * readyPulseAmount, 0.25f, 6, 1f).SetUpdate(true);
    }

    /// <summary>
    /// Resets pointer state, visual scale, and kills active tweens safely.
    /// </summary>
    public void ResetButton()
    {
        activePointerId = -1;

        if (punchTween != null && punchTween.IsActive()) punchTween.Kill();
        if (colorTween != null && colorTween.IsActive()) colorTween.Kill();
        if (readyPulseTween != null && readyPulseTween.IsActive()) readyPulseTween.Kill();

        if (buttonRect != null) buttonRect.localScale = Vector3.one;
        if (buttonBackground != null) buttonBackground.color = readyColor;
        if (cooldownRadialFill != null) cooldownRadialFill.fillAmount = 0f;
        if (cooldownText != null) cooldownText.gameObject.SetActive(false);
    }
}
