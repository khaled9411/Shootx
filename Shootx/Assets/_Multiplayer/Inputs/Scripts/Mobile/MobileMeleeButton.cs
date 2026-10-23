using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Dedicated mobile melee attack button.
/// Triggers exactly one melee attack on tap without allowing held or repeated spam.
/// Isolates pointer ID to support simultaneous touch input with joysticks.
/// </summary>
public class MobileMeleeButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Input Dependency")]
    [SerializeField] private InputReader inputReader;

    [Header("UI References")]
    [SerializeField] private RectTransform buttonRect;
    [SerializeField] private Image buttonImage;
    [SerializeField] private Image iconImage;

    [Header("Visual Feedback")]
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.75f);
    [SerializeField] private Color pressedColor = new Color(1f, 0.6f, 0.2f, 1f);
    [SerializeField] private float punchScaleAmount = 0.15f;
    [SerializeField] private float punchDuration = 0.15f;

    private int activePointerId = -1;
    private Tween punchTween;
    private Tween colorTween;

    private void Awake()
    {
        if (buttonRect == null) buttonRect = GetComponent<RectTransform>();
        if (buttonImage == null) buttonImage = GetComponent<Image>();
        SetVisualNormal();
    }

    private void OnDisable()
    {
        ResetButton();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointerId != -1) return; // Dedicated finger only

        activePointerId = eventData.pointerId;

        // Visual press feedback
        if (buttonImage != null)
        {
            if (colorTween != null && colorTween.IsActive()) colorTween.Kill();
            buttonImage.color = pressedColor;
        }

        if (buttonRect != null)
        {
            if (punchTween != null && punchTween.IsActive()) punchTween.Kill();
            punchTween = buttonRect.DOPunchScale(Vector3.one * punchScaleAmount, punchDuration, 8, 1f).SetUpdate(true);
        }

        // Trigger melee attack immediately on pointer down for zero-latency feel
        if (inputReader != null)
        {
            inputReader.TriggerMelee();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId) return;

        activePointerId = -1;
        SetVisualNormal();
    }

    private void SetVisualNormal()
    {
        if (buttonImage != null)
        {
            if (colorTween != null && colorTween.IsActive()) colorTween.Kill();
            colorTween = buttonImage.DOColor(normalColor, 0.12f).SetUpdate(true);
        }
    }

    /// <summary>
    /// Resets the button state and visual feedback.
    /// Safe to call on death or scene transition.
    /// </summary>
    public void ResetButton()
    {
        activePointerId = -1;
        if (punchTween != null && punchTween.IsActive()) punchTween.Kill();
        if (colorTween != null && colorTween.IsActive()) colorTween.Kill();
        if (buttonRect != null) buttonRect.localScale = Vector3.one;
        if (buttonImage != null) buttonImage.color = normalColor;
    }
}
