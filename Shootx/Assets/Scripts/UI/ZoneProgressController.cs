using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class ZoneProgressController : MonoBehaviour
{
    // ===================================================================
    #region Data Types
    // ===================================================================

    public enum LevelBarState
    {
        Locked,
        Completed,
        Current
    }

    public enum SpecialLevelType
    {
        Normal,
        Boss,
        Bonus
    }

    [System.Serializable]
    public class LevelBarData
    {
        public SpecialLevelType type = SpecialLevelType.Normal;
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Inspector Fields
    // ===================================================================

    [Header("=== Zone Images ===")]
    [SerializeField] private Image currentZoneImage;
    [SerializeField] private Image nextZoneImage;

    [Header("=== Level Bars (10 bars) ===")]
    [SerializeField] private List<LevelBarItem> levelBars;

    [Header("=== Bar Colors ===")]
    [SerializeField] private Color lockedColor = new Color(0.45f, 0.45f, 0.45f);
    [SerializeField] private Color completedColor = new Color(0.28f, 0.85f, 0.28f);
    [SerializeField] private Color currentColor1 = Color.white;
    [SerializeField] private Color currentColor2 = new Color(1f, 0.9f, 0.1f);

    [Header("=== Bar Sizes ===")]
    [SerializeField] private float normalBarHeight = 48f;
    [SerializeField] private float specialBarHeight = 64f;

    [Header("=== Current Pulse ===")]
    [SerializeField] private float pulseSpeed = 1.1f;
    [SerializeField] private float pulseScaleMax = 1.12f;

    [Header("=== Layout Config (Populated from GameDataManager) ===")]
    [SerializeField] private List<LevelBarData> levelLayout = new List<LevelBarData>(10);

    // ===================================================================
    #endregion

    // ===================================================================
    #region Private State
    // ===================================================================

    private int currentLevelInZone = 0;
    private List<Tween> activeTweens = new List<Tween>();

    // ===================================================================
    #endregion

    // ===================================================================
    #region Unity Lifecycle
    // ===================================================================

    private void Start()
    {
        GameDataManager.OnLevelChanged += OnLevelChanged;
        Refresh();
    }

    private void OnEnable()
    {
        GameDataManager.OnLevelChanged += OnLevelChanged;
        Refresh();
    }

    private void OnDisable()
    {
        GameDataManager.OnLevelChanged -= OnLevelChanged;
        KillAllTweens();
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Public API
    // ===================================================================

    public void Refresh()
    {
        if (GameDataManager.Instance == null) return;

        int totalLevel = GameDataManager.Instance.CurrentLevel;
        int zoneIndex = (totalLevel - 1) / 10;
        currentLevelInZone = (totalLevel - 1) % 10;

        UpdateZoneImages(zoneIndex);
        UpdateAllBars();
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Zone Transition
    // ===================================================================

    public void PlayZoneTransition(Sprite newNextZoneSprite)
    {
        Sequence seq = DOTween.Sequence();

        seq.Append(currentZoneImage.DOFade(0f, 0.3f));

        seq.AppendCallback(() =>
        {
            currentZoneImage.sprite = nextZoneImage.sprite;
            currentZoneImage.DOFade(1f, 0.3f);
            nextZoneImage.DOFade(0f, 0.2f).OnComplete(() =>
            {
                nextZoneImage.sprite = newNextZoneSprite;
                nextZoneImage.DOFade(1f, 0.3f);
            });
        });

        seq.AppendInterval(0.35f);
        seq.AppendCallback(() => ResetBarsForNewZone());
        seq.Play();
    }

    private void ResetBarsForNewZone()
    {
        currentLevelInZone = 0;
        UpdateAllBars();
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Bar Updates
    // ===================================================================

    private void UpdateZoneImages(int zoneIndex)
    {
        if (GameDataManager.Instance == null) return;

        Sprite currentSprite = GameDataManager.Instance.GetZoneSprite(zoneIndex);
        Sprite nextSprite = GameDataManager.Instance.GetZoneSprite(zoneIndex + 1);

        if (currentZoneImage != null && currentSprite != null)
            currentZoneImage.sprite = currentSprite;
        if (nextZoneImage != null && nextSprite != null)
            nextZoneImage.sprite = nextSprite;
    }

    private void UpdateAllBars()
    {
        KillAllTweens();

        for (int i = 0; i < levelBars.Count && i < 10; i++)
        {
            LevelBarItem bar = levelBars[i];
            if (bar == null) continue;

            SpecialLevelType type = (i < levelLayout.Count)
                ? levelLayout[i].type
                : SpecialLevelType.Normal;

            bool isSpecial = type != SpecialLevelType.Normal;
            float barHeight = isSpecial ? specialBarHeight : normalBarHeight;

            LevelBarState state;
            if (i < currentLevelInZone) state = LevelBarState.Completed;
            else if (i == currentLevelInZone) state = LevelBarState.Current;
            else state = LevelBarState.Locked;

            bar.SetupBar(type, barHeight, state,
                lockedColor, completedColor, currentColor1, currentColor2,
                pulseSpeed, pulseScaleMax, activeTweens);

            Debug.Log($"Updated bar {i + 1}: Type={type}, State={state}");
        }
    }

    private void OnLevelChanged()
    {
        if (GameDataManager.Instance == null) return;

        int totalLevel = GameDataManager.Instance.CurrentLevel;
        int newLevelInZone = (totalLevel - 1) % 10;
        bool crossedZoneBorder = newLevelInZone == 0 && totalLevel > 1;

        if (crossedZoneBorder)
        {
            int zoneIndex = (totalLevel - 1) / 10;
            Sprite newSprite = GameDataManager.Instance.GetZoneSprite(zoneIndex + 1);
            PlayZoneTransition(newSprite);
        }
        else
        {
            currentLevelInZone = newLevelInZone;
            UpdateAllBars();
        }
    }

    private void KillAllTweens()
    {
        foreach (var t in activeTweens) t?.Kill();
        activeTweens.Clear();

        foreach (var bar in levelBars)
            if (bar != null) bar.ResetScale();
    }

    // ===================================================================
    #endregion
}