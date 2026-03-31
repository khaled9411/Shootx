using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("=== Canvas Groups ===")]
    [SerializeField] private CanvasGroup mainMenuPanel;
    [SerializeField] private CanvasGroup gunsPanel;
    [SerializeField] private CanvasGroup outfitsPanel;
    [SerializeField] private CanvasGroup gamepanel;

    [Header("=== Popups ===")]
    [SerializeField] private CanvasGroup settingsPopup;
    [SerializeField] private CanvasGroup missionsPopup;
    [SerializeField] private CanvasGroup shopPopup;

    [Header("=== Main Menu Sub-Elements ===")]
    [SerializeField] private CanvasGroup topBarGroup;
    [SerializeField] private CanvasGroup tapToPlayGroup;
    [SerializeField] private CanvasGroup progressGroup;
    [SerializeField] private CanvasGroup bottomButtonsGroup;
    [SerializeField] private CanvasGroup desapperGroup;
    [SerializeField] private Button playButton;

    [Header("=== Transition Settings ===")]
    [SerializeField] private float panelFadeDuration = 0.35f;
    [SerializeField] private float popupScaleDuration = 0.3f;
    [SerializeField] private Ease popupEaseIn = Ease.OutBack;
    [SerializeField] private Ease popupEaseOut = Ease.InBack;
    [SerializeField] private float elementStaggerDelay = 0.06f;

    private CanvasGroup currentPanel;
    private Stack<CanvasGroup> panelHistory = new Stack<CanvasGroup>();

    public static event Action OnTapToPlay;
    public static event Action OnEnterGame;

    private bool gameStarted = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        InitializeAllPanels();
    }

    private void Start()
    {
        ShowMainMenu(instant: true);

        if (!gameStarted && currentPanel == mainMenuPanel)
        {
            if (playButton != null)
                playButton.onClick.AddListener(TriggerTapToPlay);
        }
    }

    // =================================================================
    #region Initialization
    // =================================================================

    private void InitializeAllPanels()
    {
        SetCanvasGroupState(mainMenuPanel, false, instant: true);
        SetCanvasGroupState(gunsPanel, false, instant: true);
        SetCanvasGroupState(outfitsPanel, false, instant: true);
        SetCanvasGroupState(settingsPopup, false, instant: true);
        SetCanvasGroupState(missionsPopup, false, instant: true);
        SetCanvasGroupState(shopPopup, false, instant: true);

        if (settingsPopup != null) settingsPopup.transform.localScale = Vector3.zero;
        if (missionsPopup != null) missionsPopup.transform.localScale = Vector3.zero;
        if (shopPopup != null) shopPopup.transform.localScale = Vector3.zero;
    }

    #endregion

    // =================================================================
    #region Main Menu
    // =================================================================

    public void ShowMainMenu(bool instant = false)
    {
        gameStarted = false;

        if (instant)
        {
            SetCanvasGroupState(mainMenuPanel, true, instant: true);
            currentPanel = mainMenuPanel;
            return;
        }

        HideCurrentPanel(() =>
        {
            currentPanel = mainMenuPanel;
            ShowMainMenuAnimated();
        });
    }

    private void ShowMainMenuAnimated()
    {
        SetCanvasGroupState(mainMenuPanel, true, instant: true);

        Sequence seq = DOTween.Sequence();

        if (topBarGroup != null)
        {
            topBarGroup.alpha = 0;
            topBarGroup.transform.localPosition += Vector3.up * 30f;
            seq.Append(topBarGroup.DOFade(1f, 0.4f));
            seq.Join(topBarGroup.transform.DOLocalMoveY(
                topBarGroup.transform.localPosition.y - 30f, 0.4f).SetEase(Ease.OutCubic));
        }

        if (progressGroup != null)
        {
            progressGroup.alpha = 0;
            seq.AppendInterval(elementStaggerDelay);
            seq.Append(progressGroup.DOFade(1f, 0.35f));
        }

        if (tapToPlayGroup != null)
        {
            tapToPlayGroup.alpha = 0;
            seq.AppendInterval(elementStaggerDelay);
            seq.Append(tapToPlayGroup.DOFade(1f, 0.4f));
        }

        if (bottomButtonsGroup != null)
        {
            bottomButtonsGroup.alpha = 0;
            bottomButtonsGroup.transform.localPosition += Vector3.down * 30f;
            seq.AppendInterval(elementStaggerDelay);
            seq.Append(bottomButtonsGroup.DOFade(1f, 0.35f));
            seq.Join(bottomButtonsGroup.transform.DOLocalMoveY(
                bottomButtonsGroup.transform.localPosition.y + 30f, 0.35f).SetEase(Ease.OutCubic));
        }

        seq.Play();
    }

    #endregion

    // =================================================================
    #region Tap To Play
    // =================================================================

    private void TriggerTapToPlay()
    {
        gameStarted = true;
        OnTapToPlay?.Invoke();

        Sequence seq = DOTween.Sequence();

        if (tapToPlayGroup != null)
            seq.Append(tapToPlayGroup.DOFade(0f, 0.3f));

        if (progressGroup != null)
            seq.Join(progressGroup.DOFade(0f, 0.3f));

        if (bottomButtonsGroup != null)
        {
            seq.Join(bottomButtonsGroup.DOFade(0f, 0.25f));
            seq.Join(bottomButtonsGroup.transform.DOLocalMoveY(
                bottomButtonsGroup.transform.localPosition.y - 40f, 0.3f).SetEase(Ease.InCubic));
        }

        if (desapperGroup != null)
            seq.Join(desapperGroup.DOFade(0f, 0.3f));

        if(gamepanel != null)
        {
            gamepanel.alpha = 0f;
            SetCanvasGroupState(gamepanel, true, instant: true);
            seq.Join(gamepanel.DOFade(1f, 0.4f).SetEase(Ease.OutCubic));
        }

        mainMenuPanel.blocksRaycasts = false;
        progressGroup.blocksRaycasts = false;
        topBarGroup.blocksRaycasts = false;
        bottomButtonsGroup.blocksRaycasts = false;
        tapToPlayGroup.blocksRaycasts = false;

        seq.OnComplete(() => OnEnterGame?.Invoke());
        seq.Play();
    }

    #endregion

    // =================================================================
    #region Game Result Screens
    // =================================================================

    public void ShowWinScreen(int earnedMoney = 0)
    {
        if (WinLosePauseUI.Instance != null)
            WinLosePauseUI.Instance.ShowWin(earnedMoney);
    }

    public void ShowLoseScreen()
    {
        if (WinLosePauseUI.Instance != null)
            WinLosePauseUI.Instance.ShowLose();
    }

    #endregion

    // =================================================================
    #region Guns Panel
    // =================================================================

    public void ShowGunsPanel()
    {
        HideCurrentPanel(() =>
        {
            panelHistory.Push(currentPanel);
            currentPanel = gunsPanel;
            ShowPanelAnimated(gunsPanel);
        });
    }

    public void HideGunsPanel() => BackToPreviousPanel();

    #endregion

    // =================================================================
    #region Outfits Panel
    // =================================================================

    public void ShowOutfitsPanel()
    {
        HideCurrentPanel(() =>
        {
            panelHistory.Push(currentPanel);
            currentPanel = outfitsPanel;
            ShowPanelAnimated(outfitsPanel);
        });
    }

    public void HideOutfitsPanel() => BackToPreviousPanel();

    #endregion

    // =================================================================
    #region Settings Popup
    // =================================================================

    public void ShowSettingsPopup() => ShowPopup(settingsPopup);
    public void HideSettingsPopup() => HidePopup(settingsPopup);

    #endregion

    // =================================================================
    #region Missions Popup
    // =================================================================

    public void ShowMissionsPopup() => ShowPopup(missionsPopup);
    public void HideMissionsPopup() => HidePopup(missionsPopup);

    #endregion

    // =================================================================
    #region Shop Popup
    // =================================================================

    public void ShowShopPopup() => ShowPopup(shopPopup);
    public void HideShopPopup() => HidePopup(shopPopup);

    #endregion

    // =================================================================
    #region Core Transition Helpers
    // =================================================================

    private void ShowPanelAnimated(CanvasGroup panel)
    {
        SetCanvasGroupState(panel, true, instant: true);
        panel.alpha = 0f;
        panel.transform.localPosition += Vector3.right * 60f;

        Sequence seq = DOTween.Sequence();
        seq.Append(panel.DOFade(1f, panelFadeDuration).SetEase(Ease.OutCubic));
        seq.Join(panel.transform.DOLocalMoveX(
            panel.transform.localPosition.x - 60f, panelFadeDuration).SetEase(Ease.OutCubic));
        seq.Play();
    }

    private void HideCurrentPanel(Action onComplete = null)
    {
        if (currentPanel == null) { onComplete?.Invoke(); return; }

        CanvasGroup panelToHide = currentPanel;
        Sequence seq = DOTween.Sequence();
        seq.Append(panelToHide.DOFade(0f, panelFadeDuration * 0.8f).SetEase(Ease.InCubic));
        seq.OnComplete(() =>
        {
            SetCanvasGroupState(panelToHide, false, instant: true);
            onComplete?.Invoke();
        });
        seq.Play();
    }

    private void BackToPreviousPanel()
    {
        CanvasGroup previous = panelHistory.Count > 0 ? panelHistory.Pop() : mainMenuPanel;

        HideCurrentPanel(() =>
        {
            currentPanel = previous;
            if (previous == mainMenuPanel)
                ShowMainMenuAnimated();
            else
                ShowPanelAnimated(previous);
        });
    }

    private void ShowPopup(CanvasGroup popup)
    {
        if (popup == null) return;

        SetCanvasGroupState(popup, true, instant: true);
        popup.transform.localScale = Vector3.zero;
        popup.alpha = 0f;

        Sequence seq = DOTween.Sequence();
        seq.Append(popup.DOFade(1f, popupScaleDuration * 0.5f));
        seq.Join(popup.transform.DOScale(Vector3.one, popupScaleDuration).SetEase(popupEaseIn));
        seq.Play();
    }

    private void HidePopup(CanvasGroup popup)
    {
        if (popup == null) return;

        Sequence seq = DOTween.Sequence();
        seq.Append(popup.transform.DOScale(Vector3.zero, popupScaleDuration * 0.85f).SetEase(popupEaseOut));
        seq.Join(popup.DOFade(0f, popupScaleDuration * 0.85f));
        seq.OnComplete(() => SetCanvasGroupState(popup, false, instant: true));
        seq.Play();
    }

    private void SetCanvasGroupState(CanvasGroup group, bool visible, bool instant = false)
    {
        if (group == null) return;
        if (instant)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
        else
        {
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }

    #endregion
}