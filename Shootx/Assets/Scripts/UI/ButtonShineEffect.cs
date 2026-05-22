using UnityEngine;
using DG.Tweening;

public class ButtonShineEffect : MonoBehaviour
{
    [Header("Shine Elements")]
    public RectTransform shineImage;

    [Header("Position Settings")]
    public Vector3 startPoint;
    public Vector3 endPoint;

    [Header("Timing Settings")]
    public float shineDuration = 0.5f;
    public float delayTime = 2f;

    void Start()
    {
        StartShineEffect();
    }

    void StartShineEffect()
    {
        shineImage.anchoredPosition = startPoint;
        Sequence shineSequence = DOTween.Sequence();
        shineSequence.SetUpdate(true);
        shineSequence.AppendCallback(() => shineImage.anchoredPosition = startPoint);
        shineSequence.Append(shineImage.DOAnchorPos(endPoint, shineDuration).SetEase(Ease.Linear));
        shineSequence.AppendInterval(delayTime);
        shineSequence.SetLoops(-1, LoopType.Restart );
    }
}