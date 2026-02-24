using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class MissionsPopupController : MonoBehaviour
{
    // ===================================================================
    #region Data Types
    // ===================================================================

    [System.Serializable]
    public class MissionItemUI
    {
        public GameObject root;
        public TextMeshProUGUI missionNameText;
        public Image rewardIcon;
        public TextMeshProUGUI rewardValueText;
        public Slider progressBar;
        public TextMeshProUGUI progressText;        // "5/8"
        public Image completedCheck;
        public CanvasGroup dimOverlay;
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Inspector Fields
    // ===================================================================

    [Header("=== Header ===")]
    [SerializeField] private Button closeButton;

    [Header("=== Timer ===")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private Image timerIcon;

    [Header("=== Mission Items (exactly 3) ===")]
    [SerializeField] private List<MissionItemUI> missionItems;

    // ===================================================================
    #endregion

    // ===================================================================
    #region Private State
    // ===================================================================

    private Coroutine timerCoroutine;

    // ===================================================================
    #endregion

    // ===================================================================
    #region Unity Lifecycle
    // ===================================================================

    private void Start()
    {
        closeButton?.onClick.AddListener(UIManager.Instance.HideMissionsPopup);
    }

    private void OnEnable()
    {
        RefreshMissions();
        StartTimer();
    }

    private void OnDisable()
    {
        if (timerCoroutine != null) StopCoroutine(timerCoroutine);
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Timer
    // ===================================================================

    private void StartTimer()
    {
        if (timerCoroutine != null) StopCoroutine(timerCoroutine);
        timerCoroutine = StartCoroutine(TimerRoutine());
    }

    private IEnumerator TimerRoutine()
    {
        while (true)
        {
            TimeSpan remaining = GameDataManager.Instance != null
                ? GameDataManager.Instance.GetDailyMissionsTimeRemaining()
                : TimeSpan.FromHours(24);

            if (timerText != null)
                timerText.text = $"{remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";

            yield return new WaitForSeconds(1f);
        }
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Missions Refresh
    // ===================================================================

    public void RefreshMissions()
    {
        if (GameDataManager.Instance == null) return;

        List<GameDataManager.DailyMission> missions = GameDataManager.Instance.GetDailyMissions();

        for (int i = 0; i < missionItems.Count && i < missions.Count; i++)
        {
            SetupMissionItem(missionItems[i], missions[i]);
        }
    }

    private void SetupMissionItem(MissionItemUI ui, GameDataManager.DailyMission data)
    {
        if (ui == null || ui.root == null) return;

        if (ui.missionNameText != null) ui.missionNameText.text = data.missionName;

        if (ui.rewardIcon != null) ui.rewardIcon.sprite = data.rewardSprite;
        if (ui.rewardValueText != null) ui.rewardValueText.text = $"+{data.rewardValue}";

        float progress = data.requiredAmount > 0
            ? (float)data.currentAmount / data.requiredAmount
            : 1f;

        if (ui.progressBar != null) ui.progressBar.DOValue(progress, 0.5f).SetEase(Ease.OutCubic);
        if (ui.progressText != null) ui.progressText.text = $"{data.currentAmount}/{data.requiredAmount}";

        bool completed = data.currentAmount >= data.requiredAmount;

        if (ui.completedCheck != null) ui.completedCheck.gameObject.SetActive(completed);
        if (ui.progressBar != null) ui.progressBar.gameObject.SetActive(!completed);
        if (ui.progressText != null) ui.progressText.gameObject.SetActive(!completed);

        if (ui.dimOverlay != null)
            ui.dimOverlay.DOFade(completed ? 0.45f : 0f, 0.3f);
    }

    // ===================================================================
    #endregion
}