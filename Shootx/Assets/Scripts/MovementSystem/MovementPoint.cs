using UnityEngine;
using DG.Tweening;

public class MovementPoint : MonoBehaviour
{
    [Header("Point Settings")]
    [SerializeField] private int pointIndex = 0;

    public MovementPoint neighborUp;
    public MovementPoint neighborDown;
    public MovementPoint neighborLeft;
    public MovementPoint neighborRight;

    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color activeColor = Color.cyan;

    private Renderer pointRenderer;
    private Vector3 originalScale;
    private Tween pulseTween;

    public int PointIndex => pointIndex;

    void Start()
    {
        pointRenderer = GetComponent<Renderer>();
        originalScale = transform.localScale;
        if (pointRenderer != null)
            pointRenderer.material.color = normalColor;

        StartPulseAnimation();
    }

    void StartPulseAnimation()
    {
        pulseTween = transform.DOScale(originalScale * 1.2f, 0.5f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    public void OnPlayerReached()
    {
        transform.DOPunchScale(Vector3.one * 0.5f, 0.4f, 8, 0.3f);

        if (pointRenderer != null)
            pointRenderer.material.DOColor(activeColor, 0.3f);

        DirectionButtonsUI.Instance.ShowButtons(this);
        GameCameraController.Instance.OnPlayerReachedPoint(this);
    }

    public void OnPlayerLeft()
    {
        if (pointRenderer != null)
            pointRenderer.material.DOColor(normalColor, 0.3f);
    }

    void OnDestroy()
    {
        pulseTween?.Kill();
    }
}