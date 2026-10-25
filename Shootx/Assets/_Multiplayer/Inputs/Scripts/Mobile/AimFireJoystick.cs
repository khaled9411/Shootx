using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AimFireJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public enum AimState
    {
        Idle,
        Aiming,
        Armed,
        Canceled
    }

    [Header("Input Architecture Reference")]
    [SerializeField] private InputReader inputReader;

    [Header("UI Rect References")]
    [SerializeField] private RectTransform backgroundRect;
    [SerializeField] private RectTransform handleRect;
    [SerializeField] private RectTransform armedIndicatorRect;

    [Header("Visual Feedback (Images)")]
    [SerializeField] private Image handleImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image armedIndicatorImage;

    [Header("Hysteresis & Aim Thresholds")]
    [Range(0.5f, 1.0f)]
    [SerializeField] private float armingThreshold = 0.90f;

    [Range(0.2f, 0.95f)]
    [SerializeField] private float disarmThreshold = 0.75f;

    [Range(0f, 0.4f)]
    [SerializeField] private float deadZone = 0.15f;

    [SerializeField] private float handleRange = 90f;
    [SerializeField] private float resetDuration = 0.12f;

    [Header("Color Palette Feedback")]
    [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] private Color aimingColor = new Color(0.2f, 0.85f, 1f, 0.9f);
    [SerializeField] private Color armedColor = new Color(1f, 0.35f, 0.15f, 1f);
    [SerializeField] private Color canceledColor = new Color(0.55f, 0.55f, 0.6f, 0.5f);

    // ─── Public Properties ──────────────────────────────────────
    public AimState CurrentState { get; private set; } = AimState.Idle;
    public Vector2 RawInput { get; private set; } = Vector2.zero;
    public Vector3 CurrentWorldAimDirection { get; private set; } = Vector3.forward;
    public bool IsArmed => CurrentState == AimState.Armed;
    public bool IsDragging => activePointerId != -1;

    // ─── Global State For Local Player Ammo ─────────────────────
    public static bool LocalPlayerHasAmmo = true;
    public static event Action OnLocalDryFireAttempt;

    // ─── Internal State ─────────────────────────────────────────
    private int activePointerId = -1;
    private Canvas parentCanvas;
    private Camera canvasCamera;
    private Camera mainCamera;
    private Tween handleTween;
    private Tween colorTween;
    private Tween punchTween;
    private Tween indicatorTween;

    private void Awake()
    {
        if (backgroundRect == null) backgroundRect = GetComponent<RectTransform>();
        if (handleRect == null && backgroundRect != null && backgroundRect.childCount > 0)
        {
            handleRect = backgroundRect.GetChild(0) as RectTransform;
        }

        if (handleImage == null && handleRect != null)
        {
            handleImage = handleRect.GetComponent<Image>();
        }

        parentCanvas = GetComponentInParent<Canvas>();
        mainCamera = Camera.main;
        UpdateCanvasCamera();
        ApplyStateVisuals(AimState.Idle, true);
    }

    private void OnValidate()
    {
        if (disarmThreshold >= armingThreshold)
        {
            disarmThreshold = Mathf.Max(0.1f, armingThreshold - 0.1f);
        }
    }

    private void OnEnable()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        UpdateCanvasCamera();
        ResetJoystick();
    }

    private void OnDisable()
    {
        ResetJoystick();
    }

    private void UpdateCanvasCamera()
    {
        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            canvasCamera = parentCanvas.worldCamera;
        }
        else
        {
            canvasCamera = null;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointerId != -1) return; // Dedicated finger only

        if (!LocalPlayerHasAmmo)
        {
            if (backgroundRect != null)
            {
                backgroundRect.DOComplete();
                backgroundRect.DOPunchPosition(new Vector3(15f, 0f, 0f), 0.3f, 10, 1f).SetUpdate(true);
            }
            if (handleImage != null)
            {
                handleImage.DOComplete();
                handleImage.DOColor(Color.red, 0.15f).OnComplete(() => handleImage.color = idleColor).SetUpdate(true);
            }

            OnLocalDryFireAttempt?.Invoke();
            return;
        }

        activePointerId = eventData.pointerId;
        KillTweens();
        SetState(AimState.Aiming);
        ProcessDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId) return;
        ProcessDrag(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId) return;
        HandleRelease();
    }

    private void ProcessDrag(PointerEventData eventData)
    {
        if (backgroundRect == null || handleRect == null) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            backgroundRect,
            eventData.position,
            canvasCamera,
            out Vector2 localPoint))
        {
            float radius = Mathf.Max(10f, handleRange);
            Vector2 clampedPoint = Vector2.ClampMagnitude(localPoint, radius);
            handleRect.anchoredPosition = clampedPoint;

            RawInput = clampedPoint / radius;
            float rawMag = RawInput.magnitude;

            if (rawMag <= deadZone)
            {
                if (CurrentState == AimState.Armed)
                {
                    SetState(AimState.Canceled);
                }
                return;
            }

            UpdateWorldAimDirection(RawInput);

            if (CurrentState == AimState.Aiming || CurrentState == AimState.Canceled)
            {
                if (rawMag >= armingThreshold)
                {
                    SetState(AimState.Armed);
                }
            }
            else if (CurrentState == AimState.Armed)
            {
                if (rawMag < disarmThreshold)
                {
                    SetState(AimState.Canceled);
                }
            }

            if (inputReader != null)
            {
                bool armed = (CurrentState == AimState.Armed);
                inputReader.SetMobileAim(CurrentWorldAimDirection, isAiming: true, isArmed: armed);
            }
        }
    }

    private void UpdateWorldAimDirection(Vector2 stickVector)
    {
        if (mainCamera == null) mainCamera = Camera.main;

        if (mainCamera != null)
        {
            Vector3 camFwd = mainCamera.transform.forward;
            camFwd.y = 0f;
            camFwd.Normalize();

            Vector3 camRight = mainCamera.transform.right;
            camRight.y = 0f;
            camRight.Normalize();

            Vector3 worldDir = (camRight * stickVector.x + camFwd * stickVector.y);
            worldDir.y = 0f;

            if (worldDir.sqrMagnitude > 0.001f)
            {
                CurrentWorldAimDirection = worldDir.normalized;
            }
        }
        else
        {
            Vector3 worldDir = new Vector3(stickVector.x, 0f, stickVector.y);
            if (worldDir.sqrMagnitude > 0.001f)
            {
                CurrentWorldAimDirection = worldDir.normalized;
            }
        }
    }

    private void HandleRelease()
    {
        activePointerId = -1;

        if (CurrentState == AimState.Armed)
        {
            if (inputReader != null)
            {
                inputReader.RequestMobileFire(CurrentWorldAimDirection);
            }

            if (backgroundRect != null)
            {
                backgroundRect.DOPunchScale(Vector3.one * 0.1f, 0.15f, 8, 1f).SetUpdate(true);
            }
        }

        SetState(AimState.Idle);

        if (handleRect != null)
        {
            if (resetDuration > 0f)
            {
                handleTween = handleRect.DOAnchorPos(Vector2.zero, resetDuration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);
            }
            else
            {
                handleRect.anchoredPosition = Vector2.zero;
            }
        }

        if (inputReader != null)
        {
            inputReader.SetMobileAim(Vector3.zero, isAiming: false, isArmed: false);
        }
    }

    private void SetState(AimState newState)
    {
        if (CurrentState == newState) return;

        CurrentState = newState;
        ApplyStateVisuals(newState, false);

        if (inputReader != null)
        {
            bool armed = (newState == AimState.Armed);
            bool aiming = (newState != AimState.Idle);
            inputReader.SetMobileAim(CurrentWorldAimDirection, aiming, armed);
        }
    }

    private void ApplyStateVisuals(AimState state, bool immediate)
    {
        Color targetColor = idleColor;
        float indicatorAlpha = 0f;
        float indicatorScale = 0.85f;

        switch (state)
        {
            case AimState.Idle:
                targetColor = idleColor;
                indicatorAlpha = 0f;
                indicatorScale = 0.85f;
                break;
            case AimState.Aiming:
                targetColor = aimingColor;
                indicatorAlpha = 0.25f;
                indicatorScale = 0.95f;
                break;
            case AimState.Armed:
                targetColor = armedColor;
                indicatorAlpha = 1f;
                indicatorScale = 1.12f;

                if (!immediate && handleRect != null)
                {
                    if (punchTween != null && punchTween.IsActive()) punchTween.Kill();
                    punchTween = handleRect.DOPunchScale(Vector3.one * 0.15f, 0.2f, 10, 1f).SetUpdate(true);
                }
                break;
            case AimState.Canceled:
                targetColor = canceledColor;
                indicatorAlpha = 0f;
                indicatorScale = 0.85f;
                break;
        }

        if (handleImage != null)
        {
            if (colorTween != null && colorTween.IsActive()) colorTween.Kill();
            if (immediate)
            {
                handleImage.color = targetColor;
            }
            else
            {
                colorTween = handleImage.DOColor(targetColor, 0.15f).SetUpdate(true);
            }
        }

        if (armedIndicatorImage != null)
        {
            if (indicatorTween != null && indicatorTween.IsActive()) indicatorTween.Kill();
            if (immediate)
            {
                Color c = armedIndicatorImage.color;
                c.a = indicatorAlpha;
                armedIndicatorImage.color = c;
            }
            else
            {
                indicatorTween = armedIndicatorImage.DOFade(indicatorAlpha, 0.15f).SetUpdate(true);
            }
        }

        if (armedIndicatorRect != null)
        {
            if (immediate)
            {
                armedIndicatorRect.localScale = Vector3.one * indicatorScale;
            }
            else
            {
                armedIndicatorRect.DOScale(indicatorScale, 0.15f).SetEase(Ease.OutQuad).SetUpdate(true);
            }
        }
    }

    private void KillTweens()
    {
        if (handleTween != null && handleTween.IsActive()) handleTween.Kill();
        if (colorTween != null && colorTween.IsActive()) colorTween.Kill();
        if (punchTween != null && punchTween.IsActive()) punchTween.Kill();
        if (indicatorTween != null && indicatorTween.IsActive()) indicatorTween.Kill();
    }

    public void ResetJoystick()
    {
        activePointerId = -1;
        RawInput = Vector2.zero;
        KillTweens();
        SetState(AimState.Idle);

        if (handleRect != null) handleRect.anchoredPosition = Vector2.zero;
        if (inputReader != null) inputReader.SetMobileAim(Vector3.zero, isAiming: false, isArmed: false);

        ApplyStateVisuals(AimState.Idle, true);
    }
}