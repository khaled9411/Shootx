using UnityEngine;
using DG.Tweening;

public class MovementPoint : MonoBehaviour
{
    [Header("Point Settings")]
    [SerializeField] private int pointIndex = 0;

    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = Color.yellow;
    [SerializeField] private Color pressedColor = Color.green;
    [SerializeField] private Color lockedColor = Color.gray;

    [Header("Animation Settings")]
    [SerializeField] private float pulseScale = 1.2f;
    [SerializeField] private float pulseDuration = 0.5f;
    [SerializeField] private float clickScalePunch = 0.3f;

    private Renderer pointRenderer;
    private Vector3 originalScale;
    private Tween pulseTween;
    private bool isLocked = false;

    public int PointIndex => pointIndex;

    void Start()
    {
        pointRenderer = GetComponent<Renderer>();
        originalScale = transform.localScale;

        if (pointRenderer != null)
        {
            pointRenderer.material.color = normalColor;
        }

        StartPulseAnimation();
    }

    void StartPulseAnimation()
    {
        pulseTween = transform.DOScale(originalScale * pulseScale, pulseDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    public void SetLocked(bool locked)
    {
        isLocked = locked;

        if (pointRenderer != null)
        {
            if (locked)
            {
                pointRenderer.material.DOColor(lockedColor, 0.3f);
            }
            else
            {
                pointRenderer.material.DOColor(normalColor, 0.3f);
            }
        }
    }

    void OnMouseEnter()
    {
        if (pointRenderer != null && !isLocked)
        {
            pointRenderer.material.DOColor(hoverColor, 0.2f);
        }
    }

    void OnMouseExit()
    {
        if (pointRenderer != null && !isLocked)
        {
            pointRenderer.material.DOColor(normalColor, 0.2f);
        }
    }

    void OnMouseDown()
    {
        if (isLocked)
        {
            transform.DOShakePosition(0.3f, 0.1f, 20, 90, false, true);
            Debug.Log("This point is locked! You must go through the previous points first.");
            return;
        }

        if (pointRenderer != null)
        {
            pointRenderer.material.DOColor(pressedColor, 0.1f)
                .OnComplete(() => pointRenderer.material.DOColor(normalColor, 0.3f));
        }

        transform.DOPunchScale(Vector3.one * clickScalePunch, 0.3f, 5, 0.5f);

        PathMovementManager manager = FindFirstObjectByType<PathMovementManager>();
        if (manager != null)
        {
            manager.MoveToPoint(pointIndex);
        }
    }

    public void OnPlayerReached()
    {
        transform.DOPunchScale(Vector3.one * 0.5f, 0.4f, 8, 0.3f);
    }

    void OnDestroy()
    {
        pulseTween?.Kill();
    }
}