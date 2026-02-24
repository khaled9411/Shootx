using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class LevelBarItem : MonoBehaviour
{
    [Header("=== Bar Visual ===")]
    [SerializeField] private Image barImage;
    [SerializeField] private RectTransform barRect;

    [Header("=== Special Level Extras ===")]
    [SerializeField] private GameObject specialIconRoot;
    [SerializeField] private Image specialIcon;
    [SerializeField] private Image completedIcon;

    [Header("=== Special Icons (assign in Inspector) ===")]
    [SerializeField] private Sprite bossSprite;
    [SerializeField] private Sprite bonusSprite;
    [SerializeField] private Sprite completedSprite;

    // ===================================================================
    #region Public Setup Method
    // ===================================================================


    public void SetupBar(
        ZoneProgressController.SpecialLevelType type,
        float height,
        ZoneProgressController.LevelBarState state,
        Color lockedColor,
        Color completedColor,
        Color currentColor1,
        Color currentColor2,
        float pulseSpeed,
        float pulseScaleMax,
        List<Tween> tweenList)
    {
        //if (barRect != null)
            barRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        bool isSpecial = type != ZoneProgressController.SpecialLevelType.Normal;
        if (specialIconRoot != null)
            specialIconRoot.SetActive(isSpecial);

        if (isSpecial && specialIcon != null)
        {
            bool done = state == ZoneProgressController.LevelBarState.Completed;

            if (done && completedIcon != null)
            {
                specialIcon.gameObject.SetActive(false);
                completedIcon.gameObject.SetActive(true);
                completedIcon.sprite = completedSprite;
            }
            else
            {
                if (completedIcon != null) completedIcon.gameObject.SetActive(false);
                specialIcon.gameObject.SetActive(true);
                specialIcon.sprite = type == ZoneProgressController.SpecialLevelType.Boss
                    ? bossSprite
                    : bonusSprite;
            }
        }

        ResetScale();
        KillMyTweens();

        switch (state)
        {
            case ZoneProgressController.LevelBarState.Locked:
                if (barImage != null) barImage.color = lockedColor;
                break;

            case ZoneProgressController.LevelBarState.Completed:
                if (barImage != null) barImage.color = completedColor;
                PlayCompletedFlash(completedColor);
                break;

            case ZoneProgressController.LevelBarState.Current:
                StartPulse(currentColor1, currentColor2, pulseSpeed, pulseScaleMax, tweenList);
                break;
        }
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Animations
    // ===================================================================

    private void StartPulse(Color c1, Color c2, float speed, float scaleMax, List<Tween> tweenList)
    {
        if (barImage == null) return;

        barImage.color = c1;

        Tween colorTween = barImage
            .DOColor(c2, speed)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);

        Tween scaleTween = transform
            .DOScale(scaleMax, speed * 0.9f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);

        tweenList.Add(colorTween);
        tweenList.Add(scaleTween);
    }

    private void PlayCompletedFlash(Color finalColor)
    {
        if (barImage == null) return;

        barImage.color = Color.white;
        barImage.DOColor(finalColor, 0.5f).SetEase(Ease.OutCubic);
    }

    public void ResetScale()
    {
        transform.localScale = Vector3.one;
    }

    private void KillMyTweens()
    {
        DOTween.Kill(barImage);
        DOTween.Kill(transform);
    }

    // ===================================================================
    #endregion
}