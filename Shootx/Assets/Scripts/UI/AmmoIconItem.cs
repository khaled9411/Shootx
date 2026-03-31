using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class AmmoIconItem : MonoBehaviour
{
    [Header("Icon Visual")]
    [SerializeField] private Image iconImage;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Colors")]
    [SerializeField] private Color activeColor = new Color(1f, 0.85f, 0.1f, 1f);
    [SerializeField] private Color consumedColor = new Color(0.2f, 0.2f, 0.2f, 0.4f);

    [Header("Animation Durations")]
    [SerializeField] private float consumeDuration = 0.35f;
    [SerializeField] private float returnDuration = 0.4f;

    private bool _isActive = true;
    private Sequence _currentSeq;

    private void Awake()
    {
        if (iconImage == null) iconImage = GetComponent<Image>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Init()
    {
        _isActive = true;
        iconImage.color = activeColor;
        canvasGroup.alpha = 1f;
        transform.localScale = Vector3.one;
    }

    public void PlayConsumeAnimation()
    {
        if (!_isActive) return;
        _isActive = false;

        _currentSeq?.Kill();
        _currentSeq = DOTween.Sequence();

        _currentSeq
            .Append(transform.DOScale(1.3f, consumeDuration * 0.25f).SetEase(Ease.OutBack))
            .Append(transform.DOScale(0f, consumeDuration * 0.75f).SetEase(Ease.InBack))
            .Join(canvasGroup.DOFade(0f, consumeDuration * 0.75f).SetEase(Ease.InQuad))
            .Join(iconImage.DOColor(consumedColor, consumeDuration * 0.4f))
            .OnComplete(() =>
            {
                iconImage.color = consumedColor;
                canvasGroup.alpha = 0.25f;
                transform.localScale = new Vector3(0.7f, 0.7f, 1f);
            });
    }

    public void PlayReturnAnimation()
    {
        if (_isActive) return;
        _isActive = true;

        _currentSeq?.Kill();
        _currentSeq = DOTween.Sequence();

        canvasGroup.alpha = 0f;
        transform.localScale = Vector3.zero;

        _currentSeq
            .Append(canvasGroup.DOFade(1f, returnDuration * 0.5f).SetEase(Ease.OutQuad))
            .Join(transform.DOScale(1.4f, returnDuration * 0.5f).SetEase(Ease.OutBack))
            .Join(iconImage.DOColor(activeColor, returnDuration * 0.5f))
            .Append(transform.DOScale(1f, returnDuration * 0.5f).SetEase(Ease.InOutElastic));
    }


    public void PlayIntroAnimation(float delay)
    {
        canvasGroup.alpha = 0f;
        transform.localScale = Vector3.zero;

        DOTween.Sequence()
            .SetDelay(delay)
            .Append(canvasGroup.DOFade(1f, 0.3f).SetEase(Ease.OutQuad))
            .Join(transform.DOScale(1.2f, 0.3f).SetEase(Ease.OutBack))
            .Append(transform.DOScale(1f, 0.15f).SetEase(Ease.InOutSine));
    }

    private void OnDestroy()
    {
        _currentSeq?.Kill();
    }
}