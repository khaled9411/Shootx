using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// High-reliability fixed virtual joystick for 2D movement.
/// Feeds normalized movement vectors to InputReader.
/// Supports multi-touch tracking by isolating touch pointer IDs,
/// deadzone filtering, and smooth return-to-center tweening.
/// </summary>
public class FixedJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Dependencies")]
    [SerializeField] private InputReader inputReader;

    [Header("UI Rect References")]
    [SerializeField] private RectTransform backgroundRect;
    [SerializeField] private RectTransform handleRect;

    [Header("Joystick Settings")]
    [Tooltip("Maximum distance handle can move from center in pixels/canvas units.")]
    [SerializeField] private float handleRange = 85f;

    [Tooltip("Normalized dead zone (0.0 - 0.5). Input below this magnitude returns Vector2.zero.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float deadZone = 0.1f;

    [Tooltip("Smooth return duration when joystick is released.")]
    [SerializeField] private float resetDuration = 0.12f;

    // ─── Public Properties ──────────────────────────────────────
    /// <summary>Current normalized movement direction (magnitude clamped to [0, 1]).</summary>
    public Vector2 Direction { get; private set; } = Vector2.zero;
    public float Magnitude => Direction.magnitude;
    public bool IsDragging => activePointerId != -1;

    // ─── Internal State ─────────────────────────────────────────
    private int activePointerId = -1;
    private Canvas parentCanvas;
    private Camera canvasCamera;
    private Tween resetTween;

    private void Awake()
    {
        if (backgroundRect == null) backgroundRect = GetComponent<RectTransform>();
        if (handleRect == null && backgroundRect != null && backgroundRect.childCount > 0)
        {
            handleRect = backgroundRect.GetChild(0) as RectTransform;
        }

        parentCanvas = GetComponentInParent<Canvas>();
        UpdateCanvasCamera();
    }

    private void OnEnable()
    {
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
        if (activePointerId != -1) return; // Already tracking a finger

        activePointerId = eventData.pointerId;

        if (resetTween != null && resetTween.IsActive())
        {
            resetTween.Kill();
            resetTween = null;
        }

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
        ReleaseJoystick();
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

            Vector2 rawVector = clampedPoint / radius;
            float rawMag = rawVector.magnitude;

            if (rawMag <= deadZone)
            {
                Direction = Vector2.zero;
            }
            else
            {
                float remappedMag = Mathf.Clamp01((rawMag - deadZone) / (1f - deadZone));
                Direction = rawVector.normalized * remappedMag;
            }

            if (inputReader != null)
            {
                inputReader.SetMoveInput(Direction);
            }
        }
    }

    private void ReleaseJoystick()
    {
        activePointerId = -1;
        Direction = Vector2.zero;

        if (inputReader != null)
        {
            inputReader.SetMoveInput(Vector2.zero);
        }

        if (handleRect != null)
        {
            if (resetTween != null && resetTween.IsActive())
            {
                resetTween.Kill();
            }

            if (resetDuration > 0f)
            {
                resetTween = handleRect.DOAnchorPos(Vector2.zero, resetDuration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);
            }
            else
            {
                handleRect.anchoredPosition = Vector2.zero;
            }
        }
    }

    /// <summary>
    /// Forces complete reset of joystick state, input, and tweens.
    /// Safe to call on death, pause, or scene change.
    /// </summary>
    public void ResetJoystick()
    {
        activePointerId = -1;
        Direction = Vector2.zero;

        if (resetTween != null && resetTween.IsActive())
        {
            resetTween.Kill();
            resetTween = null;
        }

        if (handleRect != null)
        {
            handleRect.anchoredPosition = Vector2.zero;
        }

        if (inputReader != null)
        {
            inputReader.SetMoveInput(Vector2.zero);
        }
    }
}
