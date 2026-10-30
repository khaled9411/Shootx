using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    // ===================================================================
    #region Inspector Fields - Canvas Groups & Panels
    // ===================================================================

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

    [Header("=== Menu Background ===")]
    [Tooltip("Background CanvasGroup (contains Fill, Shine, PlayerPreview). Kept visible across all menus, hidden only when gameplay starts.")]
    [SerializeField] private CanvasGroup menuBackground;
    [SerializeField] private GameObject menuBackgroundObject;

    [Header("=== Transition Settings ===")]
    [SerializeField] private float panelFadeDuration = 0.25f;
    [SerializeField] private float popupScaleDuration = 0.25f;
    [SerializeField] private Ease popupEaseIn = Ease.OutBack;
    [SerializeField] private Ease popupEaseOut = Ease.InBack;

    [Header("=== Main Menu Animation Settings ===")]
    [Tooltip("Duration for main menu elements entrance animation")]
    [SerializeField] private float menuFadeDuration = 0.22f;
    [Tooltip("Stagger interval between main menu elements appearing")]
    [SerializeField] private float elementStaggerDelay = 0.04f;
    [Tooltip("Exit duration when player taps to play")]
    [SerializeField] private float tapToPlayExitDuration = 0.22f;

    // ===================================================================
    #endregion

    // ===================================================================
    #region Private State & Events
    // ===================================================================

    private CanvasGroup currentPanel;
    private readonly Stack<CanvasGroup> panelHistory = new Stack<CanvasGroup>();

    public static event Action OnTapToPlay;
    public static event Action OnEnterGame;

    private bool gameStarted = false;

    // Sequences for clean management & instant cancellation
    private Sequence mainMenuSequence;
    private Sequence tapToPlaySequence;

    // ===================================================================
    #endregion

    // ===================================================================
    #region Unity Lifecycle
    // ===================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        EnsureMenuBackground();
        ShowMenuBackground(instant: true);
        InitializeAllPanels();
    }

    private void Start()
    {
        ShowMainMenu(instant: true);

        if (playButton != null)
        {
            playButton.onClick.RemoveListener(TriggerTapToPlay);
            playButton.onClick.AddListener(TriggerTapToPlay);
        }
    }

    private void OnDestroy()
    {
        KillMainMenuTweens();
        if (tapToPlaySequence != null && tapToPlaySequence.IsActive())
        {
            tapToPlaySequence.Kill();
            tapToPlaySequence = null;
        }
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Menu Background Management
    // ===================================================================

    /// <summary>
    /// Locates the Background GameObject and ensures it has a CanvasGroup for smooth alpha control.
    /// Works automatically even if not wired in the inspector.
    /// </summary>
    public void EnsureMenuBackground()
    {
        if (menuBackground != null) return;

        if (menuBackgroundObject != null)
        {
            menuBackground = menuBackgroundObject.GetComponent<CanvasGroup>();
            if (menuBackground == null)
                menuBackground = menuBackgroundObject.AddComponent<CanvasGroup>();
            return;
        }

        // Try to find the child named "Background" on this Canvas
        Transform bg = transform.Find("Background");
        if (bg == null)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name.Equals("Background", StringComparison.OrdinalIgnoreCase))
                {
                    bg = child;
                    break;
                }
            }
        }

        if (bg != null)
        {
            menuBackground = bg.GetComponent<CanvasGroup>();
            if (menuBackground == null)
                menuBackground = bg.gameObject.AddComponent<CanvasGroup>();
        }
    }

    /// <summary>
    /// Guarantees the background (and its Fill, Shine, and PlayerPreview children)
    /// remains active and completely solid across all menus and transitions.
    /// </summary>
    public void ShowMenuBackground(bool instant = true)
    {
        EnsureMenuBackground();
        if (menuBackground == null) return;

        menuBackground.DOKill();
        menuBackground.gameObject.SetActive(true);

        if (instant)
        {
            menuBackground.alpha = 1f;
        }
        else
        {
            menuBackground.DOFade(1f, panelFadeDuration).SetEase(Ease.OutQuad);
        }

        menuBackground.interactable = true;
        menuBackground.blocksRaycasts = true;
    }

    #if UNITY_EDITOR
    [ContextMenu("Setup Menu Background")]
    private void ContextMenuSetupMenuBackground()
    {
        EnsureMenuBackground();
        if (menuBackground != null)
        {
            Debug.Log($"[UIManager] Successfully linked Menu Background: {menuBackground.gameObject.name}");
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
    #endif

    // ===================================================================
    #endregion

    // ===================================================================
    #region Initialization
    // ===================================================================

    private void InitializeAllPanels()
    {
        SetCanvasGroupState(mainMenuPanel, false, instant: true);
        SetCanvasGroupState(gunsPanel, false, instant: true);
        SetCanvasGroupState(outfitsPanel, false, instant: true);
        SetCanvasGroupState(gamepanel, false, instant: true);
        SetCanvasGroupState(settingsPopup, false, instant: true);
        SetCanvasGroupState(missionsPopup, false, instant: true);
        SetCanvasGroupState(shopPopup, false, instant: true);

        if (settingsPopup != null)
        {
            settingsPopup.transform.localScale = Vector3.zero;
            settingsPopup.gameObject.SetActive(false);
        }
        if (missionsPopup != null)
        {
            missionsPopup.transform.localScale = Vector3.zero;
            missionsPopup.gameObject.SetActive(false);
        }
        if (shopPopup != null)
        {
            shopPopup.transform.localScale = Vector3.zero;
            shopPopup.gameObject.SetActive(false);
        }

        if (gunsPanel != null) gunsPanel.gameObject.SetActive(false);
        if (outfitsPanel != null) outfitsPanel.gameObject.SetActive(false);
    }

    private void KillMainMenuTweens()
    {
        if (mainMenuSequence != null && mainMenuSequence.IsActive())
        {
            mainMenuSequence.Kill();
            mainMenuSequence = null;
        }

        if (topBarGroup != null) topBarGroup.DOKill();
        if (progressGroup != null) progressGroup.DOKill();
        if (tapToPlayGroup != null) tapToPlayGroup.DOKill();
        if (bottomButtonsGroup != null) bottomButtonsGroup.DOKill();
        if (desapperGroup != null) desapperGroup.DOKill();
        if (mainMenuPanel != null) mainMenuPanel.DOKill();
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Main Menu
    // ===================================================================

    public void ShowMainMenu(bool instant = false)
    {
        gameStarted = false;

        // Background is always visible and solid in menus
        ShowMenuBackground(instant: true);

        if (mainMenuPanel != null)
            mainMenuPanel.gameObject.SetActive(true);

        if (playButton != null)
            playButton.interactable = true;

        GetComponent<MainMenuController>()?.RestartIdleAnimations();

        if (instant)
        {
            KillMainMenuTweens();
            ResetMainMenuElementsInstant();
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

    private void ResetMainMenuElementsInstant()
    {
        if (topBarGroup != null)
        {
            topBarGroup.alpha = 1f;
            topBarGroup.interactable = true;
            topBarGroup.blocksRaycasts = true;
        }

        if (progressGroup != null)
        {
            progressGroup.alpha = 1f;
        }

        if (tapToPlayGroup != null)
        {
            tapToPlayGroup.alpha = 1f;
        }

        if (bottomButtonsGroup != null)
        {
            bottomButtonsGroup.alpha = 1f;
            bottomButtonsGroup.interactable = true;
            bottomButtonsGroup.blocksRaycasts = true;
        }

        if (desapperGroup != null)
        {
            desapperGroup.alpha = 1f;
            desapperGroup.interactable = true;
            desapperGroup.blocksRaycasts = true;
        }

        if (mainMenuPanel != null)
        {
            mainMenuPanel.alpha = 1f;
            mainMenuPanel.interactable = true;
            mainMenuPanel.blocksRaycasts = true;
        }
    }

    /// <summary>
    /// Snappy, modern, staggered entrance animation for main menu elements (~0.3s total)
    /// without mutating any Transform positions, preserving exact layout placement.
    /// </summary>
    private void ShowMainMenuAnimated()
    {
        KillMainMenuTweens();

        ShowMenuBackground(instant: true);

        if (mainMenuPanel != null)
            mainMenuPanel.gameObject.SetActive(true);

        SetCanvasGroupState(mainMenuPanel, true, instant: true);

        if (playButton != null)
            playButton.interactable = true;

        if (desapperGroup != null)
        {
            desapperGroup.alpha = 1f;
            desapperGroup.interactable = true;
            desapperGroup.blocksRaycasts = true;
        }

        // Set initial alphas
        if (topBarGroup != null) topBarGroup.alpha = 0f;
        if (progressGroup != null) progressGroup.alpha = 0f;
        if (tapToPlayGroup != null) tapToPlayGroup.alpha = 0f;
        if (bottomButtonsGroup != null) bottomButtonsGroup.alpha = 0f;

        mainMenuSequence = DOTween.Sequence();
        float t = 0f;

        // Top bar fades in
        if (topBarGroup != null)
        {
            mainMenuSequence.Insert(t, topBarGroup.DOFade(1f, menuFadeDuration).SetEase(Ease.OutQuad));
        }

        t += elementStaggerDelay;

        // Progress bar fades in
        if (progressGroup != null)
        {
            mainMenuSequence.Insert(t, progressGroup.DOFade(1f, menuFadeDuration).SetEase(Ease.OutQuad));
        }

        t += elementStaggerDelay;

        // Tap to play fades in
        if (tapToPlayGroup != null)
        {
            mainMenuSequence.Insert(t, tapToPlayGroup.DOFade(1f, menuFadeDuration).SetEase(Ease.OutQuad));
        }

        t += elementStaggerDelay;

        // Bottom buttons fade in
        if (bottomButtonsGroup != null)
        {
            mainMenuSequence.Insert(t, bottomButtonsGroup.DOFade(1f, menuFadeDuration).SetEase(Ease.OutQuad));
        }

        mainMenuSequence.OnComplete(() =>
        {
            ResetMainMenuElementsInstant();
        });

        mainMenuSequence.Play();
    }

    #endregion

    // ===================================================================
    #region Tap To Play (Gameplay Transition)
    // ===================================================================

    private void TriggerTapToPlay()
    {
        if (gameStarted) return;
        gameStarted = true;

        // 1. Immediately KILL any running Main Menu entrance animation
        KillMainMenuTweens();

        // 2. Immediately disable raycasts so no more clicks can be captured
        if (playButton != null)
            playButton.interactable = false;

        if (mainMenuPanel != null)
            mainMenuPanel.blocksRaycasts = false;

        if (topBarGroup != null)
            topBarGroup.blocksRaycasts = false;

        if (progressGroup != null)
            progressGroup.blocksRaycasts = false;

        if (tapToPlayGroup != null)
            tapToPlayGroup.blocksRaycasts = false;

        if (bottomButtonsGroup != null)
            bottomButtonsGroup.blocksRaycasts = false;

        if (desapperGroup != null)
            desapperGroup.blocksRaycasts = false;

        if (menuBackground != null)
            menuBackground.blocksRaycasts = false;

        // Fire camera transition and audio immediately
        OnTapToPlay?.Invoke();

        // 3. Clean, fast exit animation for all menu elements
        if (tapToPlaySequence != null && tapToPlaySequence.IsActive())
            tapToPlaySequence.Kill();

        tapToPlaySequence = DOTween.Sequence();
        float exitDur = tapToPlayExitDuration;

        if (tapToPlayGroup != null)
            tapToPlaySequence.Join(tapToPlayGroup.DOFade(0f, exitDur).SetEase(Ease.InQuad));

        if (progressGroup != null)
            tapToPlaySequence.Join(progressGroup.DOFade(0f, exitDur).SetEase(Ease.InQuad));

        if (desapperGroup != null)
            tapToPlaySequence.Join(desapperGroup.DOFade(0f, exitDur).SetEase(Ease.InQuad));

        if (topBarGroup != null)
            tapToPlaySequence.Join(topBarGroup.DOFade(0f, exitDur).SetEase(Ease.InQuad));

        if (bottomButtonsGroup != null)
            tapToPlaySequence.Join(bottomButtonsGroup.DOFade(0f, exitDur).SetEase(Ease.InQuad));

        // FADE OUT BACKGROUND ONLY HERE WHEN GAMEPLAY ACTUALLY STARTS!
        if (menuBackground != null)
        {
            tapToPlaySequence.Join(menuBackground.DOFade(0f, exitDur + 0.05f).SetEase(Ease.InOutQuad));
        }

        // Show Gameplay UI
        if (gamepanel != null)
        {
            gamepanel.gameObject.SetActive(true);
            gamepanel.alpha = 0f;
            SetCanvasGroupState(gamepanel, true, instant: true);
            tapToPlaySequence.Join(gamepanel.DOFade(1f, exitDur + 0.08f).SetEase(Ease.OutCubic));
        }

        tapToPlaySequence.OnComplete(() =>
        {
            // Fully disable and hide main menu and background so nothing can ever leak into gameplay
            if (mainMenuPanel != null)
            {
                mainMenuPanel.alpha = 0f;
                SetCanvasGroupState(mainMenuPanel, false, instant: true);
                mainMenuPanel.gameObject.SetActive(false);
            }

            if (menuBackground != null)
            {
                menuBackground.alpha = 0f;
                menuBackground.gameObject.SetActive(false);
            }

            OnEnterGame?.Invoke();
        });

        tapToPlaySequence.Play();
    }

    #endregion

    // ===================================================================
    #region Game Result Screens
    // ===================================================================

    public void ShowWinScreen(int earnedMoney = 50)
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

    // ===================================================================
    #region Guns Panel
    // ===================================================================

    public void ShowGunsPanel()
    {
        ShowMenuBackground(instant: true);

        HideCurrentPanel(() =>
        {
            panelHistory.Push(currentPanel);
            currentPanel = gunsPanel;
            ShowPanelAnimated(gunsPanel);
        });
    }

    public void HideGunsPanel() => BackToPreviousPanel();

    #endregion

    // ===================================================================
    #region Outfits Panel
    // ===================================================================

    public void ShowOutfitsPanel()
    {
        ShowMenuBackground(instant: true);

        HideCurrentPanel(() =>
        {
            panelHistory.Push(currentPanel);
            currentPanel = outfitsPanel;
            ShowPanelAnimated(outfitsPanel);
        });
    }

    public void HideOutfitsPanel() => BackToPreviousPanel();

    #endregion

    // ===================================================================
    #region Popups (Settings, Missions, Shop)
    // ===================================================================

    public void ShowSettingsPopup() => ShowPopup(settingsPopup);
    public void HideSettingsPopup() => HidePopup(settingsPopup);

    public void ShowMissionsPopup() => ShowPopup(missionsPopup);
    public void HideMissionsPopup() => HidePopup(missionsPopup);

    public void ShowShopPopup() => ShowPopup(shopPopup);
    public void HideShopPopup() => HidePopup(shopPopup);

    #endregion

    // ===================================================================
    #region Core Transition Helpers
    // ===================================================================

    private void ShowPanelAnimated(CanvasGroup panel)
    {
        if (panel == null) return;

        ShowMenuBackground(instant: true);

        panel.gameObject.SetActive(true);
        SetCanvasGroupState(panel, true, instant: true);
        panel.alpha = 0f;
        panel.DOKill();

        Sequence seq = DOTween.Sequence();
        seq.Append(panel.DOFade(1f, panelFadeDuration).SetEase(Ease.OutCubic));
        seq.Play();
    }

    private void HideCurrentPanel(Action onComplete = null)
    {
        if (currentPanel == null) { onComplete?.Invoke(); return; }

        CanvasGroup panelToHide = currentPanel;
        panelToHide.DOKill();

        Sequence seq = DOTween.Sequence();
        seq.Append(panelToHide.DOFade(0f, panelFadeDuration * 0.7f).SetEase(Ease.InCubic));
        seq.OnComplete(() =>
        {
            SetCanvasGroupState(panelToHide, false, instant: true);
            panelToHide.gameObject.SetActive(false);
            onComplete?.Invoke();
        });
        seq.Play();
    }

    private void BackToPreviousPanel()
    {
        CanvasGroup previous = panelHistory.Count > 0 ? panelHistory.Pop() : mainMenuPanel;

        ShowMenuBackground(instant: true);

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

        ShowMenuBackground(instant: true);

        popup.gameObject.SetActive(true);
        SetCanvasGroupState(popup, true, instant: true);
        popup.transform.DOKill();
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

        popup.transform.DOKill();
        Sequence seq = DOTween.Sequence();
        seq.Append(popup.transform.DOScale(Vector3.zero, popupScaleDuration * 0.85f).SetEase(popupEaseOut));
        seq.Join(popup.DOFade(0f, popupScaleDuration * 0.85f));
        seq.OnComplete(() =>
        {
            SetCanvasGroupState(popup, false, instant: true);
            popup.gameObject.SetActive(false);
        });
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

    // ===================================================================
    #region Public State Queries
    // ===================================================================

    public bool isgameactive()
    {
        return gameStarted;
    }

    // ===================================================================
    #endregion
}
