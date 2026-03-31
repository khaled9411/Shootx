using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class WinLosePauseUI : MonoBehaviour
{
    public static WinLosePauseUI Instance { get; private set; }

    // =================================================================
    #region Inspector Fields

    [Header("WIN SCREEN")]
    [SerializeField] private CanvasGroup winPanel;
    [SerializeField] private Image winBackground;
    [SerializeField] private TextMeshProUGUI winTitleText;
    [SerializeField] private TextMeshProUGUI moneyAmountText;
    [SerializeField] private GameObject moneyIcon;
    [SerializeField] private Button doubleMoneyButton;
    [SerializeField] private Button nextButton;

    [Header("LOSE SCREEN")]
    [SerializeField] private CanvasGroup losePanel;
    [SerializeField] private Image loseBackground;
    [SerializeField] private TextMeshProUGUI loseTitleText;
    [SerializeField] private Button homeButton;

    [Header("PAUSE MENU")]
    [SerializeField] private CanvasGroup pausePanel;
    [SerializeField] private Image pauseBackground;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button skipButton;

    [Header("ANIMATION SETTINGS")]
    [SerializeField] private float bgFadeDuration = 0.4f;
    [SerializeField] private float elementDelay = 0.08f;
    [SerializeField] private float popInDuration = 0.5f;
    [SerializeField] private float punchStrength = 0.25f;
    [SerializeField] private float moneyCountDuration = 1.2f;
    [SerializeField] private Ease elementEaseIn = Ease.OutBack;
    [SerializeField] private Ease elementEaseOut = Ease.InBack;

    //EVENTS
    public static event Action OnNextLevel;
    public static event Action OnDoubleMoney;
    public static event Action OnHome;
    public static event Action OnRestart;
    public static event Action OnContinue;
    public static event Action OnSkip;

    #endregion


    private int _currentMoney = 0;
    private bool _isPaused = false;
    private Tween _moneyCountTween;

    private RectTransform _winTitleRT;
    private RectTransform _moneyRT;
    private RectTransform _doubleButtonRT;
    private RectTransform _nextButtonRT;

    private RectTransform _loseTitleRT;
    private RectTransform _homeButtonRT;

    private RectTransform _pauseBgRT;
    private RectTransform _restartRT;
    private RectTransform _continueRT;
    private RectTransform _skipRT;


    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        CacheRectTransforms();
        InitPanels();
        BindButtons();
    }


    // =================================================================
    #region Initialization

    private void CacheRectTransforms()
    {
        // Win
        if (winTitleText) _winTitleRT = winTitleText.GetComponent<RectTransform>();
        if (moneyAmountText) _moneyRT = moneyAmountText.GetComponent<RectTransform>();
        if (doubleMoneyButton) _doubleButtonRT = doubleMoneyButton.GetComponent<RectTransform>();
        if (nextButton) _nextButtonRT = nextButton.GetComponent<RectTransform>();

        // Lose
        if (loseTitleText) _loseTitleRT = loseTitleText.GetComponent<RectTransform>();
        if (homeButton) _homeButtonRT = homeButton.GetComponent<RectTransform>();

        // Pause
        if (restartButton) _restartRT = restartButton.GetComponent<RectTransform>();
        if (continueButton) _continueRT = continueButton.GetComponent<RectTransform>();
        if (skipButton) _skipRT = skipButton.GetComponent<RectTransform>();
    }

    private void InitPanels()
    {
        HideInstant(winPanel);
        HideInstant(losePanel);
        HideInstant(pausePanel);
    }

    private void BindButtons()
    {
        // Win
        if (nextButton) nextButton.onClick.AddListener(OnNextClicked);
        if (doubleMoneyButton) doubleMoneyButton.onClick.AddListener(OnDoubleClicked);

        // Lose
        if (homeButton) homeButton.onClick.AddListener(OnHomeClicked);

        // Pause
        if (pauseButton) pauseButton.onClick.AddListener(TogglePause);
        if (restartButton) restartButton.onClick.AddListener(OnRestartClicked);
        if (continueButton) continueButton.onClick.AddListener(OnContinueClicked);
        if (skipButton) skipButton.onClick.AddListener(OnSkipClicked);
    }

    #endregion

    // =================================================================
    #region WIN SCREEN

    public void ShowWin(int earnedMoney)
    {
        _currentMoney = earnedMoney;
        winPanel.interactable = false;
        winPanel.blocksRaycasts = false;

        Sequence seq = DOTween.Sequence().SetUpdate(true);

        if (winBackground)
        {
            Color c = winBackground.color; c.a = 0f;
            winBackground.color = c;
            seq.Append(winBackground.DOFade(1f, bgFadeDuration).SetUpdate(true));
        }

        seq.AppendCallback(() =>
        {
            winPanel.alpha = 1f;
            winPanel.interactable = true;
            winPanel.blocksRaycasts = true;
        });

        if (_winTitleRT)
        {
            _winTitleRT.anchoredPosition += Vector2.up * 80f;
            winTitleText.alpha = 0f;
            seq.Append(winTitleText.DOFade(1f, 0.35f).SetUpdate(true));
            seq.Join(_winTitleRT.DOAnchorPosY(
                _winTitleRT.anchoredPosition.y - 80f, popInDuration)
                .SetEase(elementEaseIn).SetUpdate(true));
        }

        seq.AppendInterval(elementDelay);
        if (moneyIcon)
        {
            moneyIcon.transform.localScale = Vector3.zero;
            seq.Append(moneyIcon.transform
                .DOScale(1f, popInDuration * 0.85f)
                .SetEase(elementEaseIn).SetUpdate(true));
        }

        if (_moneyRT)
        {
            moneyAmountText.alpha = 0f;
            moneyAmountText.text = "0";
            _moneyRT.localScale = Vector3.zero;
            seq.Join(moneyAmountText.DOFade(1f, 0.3f).SetUpdate(true));
            seq.Join(_moneyRT
                .DOScale(1f, popInDuration * 0.8f)
                .SetEase(elementEaseIn).SetUpdate(true));

            seq.AppendCallback(() => StartMoneyCount(0, _currentMoney));
        }

        seq.AppendInterval(moneyCountDuration * 0.5f);
        if (_doubleButtonRT)
        {
            _doubleButtonRT.localScale = Vector3.zero;
            seq.Append(_doubleButtonRT
                .DOScale(1f, popInDuration)
                .SetEase(elementEaseIn).SetUpdate(true));
            seq.Append(_doubleButtonRT
                .DOPunchScale(Vector3.one * punchStrength, 0.4f, 6, 0.5f)
                .SetUpdate(true));
        }

        seq.AppendInterval(elementDelay);
        if (_nextButtonRT)
        {
            _nextButtonRT.anchoredPosition += Vector2.down * 60f;
            nextButton.GetComponent<CanvasGroup>()?.DOFade(0f, 0f);
            _nextButtonRT.localScale = Vector3.zero;
            seq.Append(_nextButtonRT
                .DOScale(1f, popInDuration * 0.9f)
                .SetEase(elementEaseIn).SetUpdate(true));
        }

        seq.Play();
    }

    private void StartMoneyCount(int from, int to)
    {
        _moneyCountTween?.Kill();
        int current = from;
        _moneyCountTween = DOTween.To(
            () => current,
            x => { current = x; moneyAmountText.text = x.ToString("N0"); },
            to,
            moneyCountDuration
        ).SetEase(Ease.OutExpo).SetUpdate(true);
    }

    private void HideWin(Action onComplete = null)
    {
        _moneyCountTween?.Kill();
        Sequence seq = DOTween.Sequence().SetUpdate(true);
        seq.Append(winPanel.DOFade(0f, 0.3f).SetUpdate(true));
        seq.OnComplete(() =>
        {
            HideInstant(winPanel);
            onComplete?.Invoke();
        });
        seq.Play();
    }

    #endregion

    // =================================================================
    #region LOSE SCREEN

    public void ShowLose()
    {
        losePanel.interactable = false;
        losePanel.blocksRaycasts = false;

        Sequence seq = DOTween.Sequence().SetUpdate(true);

        if (loseBackground)
        {
            Color c = loseBackground.color; c.a = 0f;
            loseBackground.color = c;
            seq.Append(loseBackground.DOFade(1f, bgFadeDuration).SetUpdate(true));
        }

        seq.AppendCallback(() =>
        {
            losePanel.alpha = 1f;
            losePanel.interactable = true;
            losePanel.blocksRaycasts = true;
        });

        if (_loseTitleRT)
        {
            loseTitleText.alpha = 0f;
            _loseTitleRT.localScale = Vector3.zero;

            seq.Append(loseTitleText.DOFade(1f, 0.3f).SetUpdate(true));
            seq.Join(_loseTitleRT
                .DOScale(1.2f, popInDuration * 0.6f)
                .SetEase(Ease.OutCubic).SetUpdate(true));
            seq.Append(_loseTitleRT
                .DOScale(1f, 0.2f).SetUpdate(true));

            seq.Append(_loseTitleRT
                .DOShakePosition(0.4f, strength: 8f, vibrato: 10, randomness: 90, snapping: false, fadeOut: true)
                .SetUpdate(true));
        }

        seq.AppendInterval(elementDelay);
        if (_homeButtonRT)
        {
            _homeButtonRT.anchoredPosition += Vector2.down * 60f;
            _homeButtonRT.localScale = Vector3.zero;
            seq.Append(_homeButtonRT
                .DOScale(1f, popInDuration)
                .SetEase(elementEaseIn).SetUpdate(true));
            seq.Join(_homeButtonRT
                .DOAnchorPosY(_homeButtonRT.anchoredPosition.y + 60f, popInDuration)
                .SetEase(elementEaseIn).SetUpdate(true));
        }

        seq.Play();
    }

    private void HideLose(Action onComplete = null)
    {
        Sequence seq = DOTween.Sequence().SetUpdate(true);
        seq.Append(losePanel.DOFade(0f, 0.3f).SetUpdate(true));
        seq.OnComplete(() =>
        {
            HideInstant(losePanel);
            onComplete?.Invoke();
        });
        seq.Play();
    }

    #endregion

    // =================================================================
    #region PAUSE MENU

    public void TogglePause()
    {
        if (_isPaused) ResumeGame();
        else PauseGame();
    }

    public void PauseGame()
    {
        _isPaused = true;
        Time.timeScale = 0f;

        pausePanel.interactable = false;
        pausePanel.blocksRaycasts = false;

        Sequence seq = DOTween.Sequence().SetUpdate(true);

        if (pauseBackground)
        {
            Color c = pauseBackground.color; c.a = 0f;
            pauseBackground.color = c;
            seq.Append(pauseBackground.DOFade(0.85f, bgFadeDuration).SetUpdate(true));
        }

        seq.AppendCallback(() =>
        {
            pausePanel.alpha = 1f;
            pausePanel.interactable = true;
            pausePanel.blocksRaycasts = true;
        });

        RectTransform[] buttons = { _continueRT, _restartRT, _skipRT };
        foreach (var btn in buttons)
        {
            if (btn == null) continue;
            btn.localScale = Vector3.zero;
            seq.AppendInterval(elementDelay);
            seq.Append(btn
                .DOScale(1f, popInDuration * 0.8f)
                .SetEase(elementEaseIn).SetUpdate(true));
        }

        seq.Play();
    }

    public void ResumeGame()
    {
        _isPaused = false;

        Sequence seq = DOTween.Sequence().SetUpdate(true);

        RectTransform[] buttons = { _skipRT, _restartRT, _continueRT };
        foreach (var btn in buttons)
        {
            if (btn == null) continue;
            seq.Append(btn
                .DOScale(0f, popInDuration * 0.5f)
                .SetEase(elementEaseOut).SetUpdate(true));
            seq.AppendInterval(elementDelay * 0.5f);
        }

        if (pauseBackground)
            seq.Append(pauseBackground.DOFade(0f, 0.25f).SetUpdate(true));

        seq.OnComplete(() =>
        {
            HideInstant(pausePanel);
            Time.timeScale = 1f;
        });

        seq.Play();
    }

    #endregion

    // =================================================================
    #region Button Callbacks

    private void OnNextClicked()
    {
        AnimateButtonPress(_nextButtonRT, () =>
        {
            HideWin(() => OnNextLevel?.Invoke());
        });
    }

    private void OnDoubleClicked()
    {
        AnimateButtonPress(_doubleButtonRT, () =>
        {
            OnDoubleMoney?.Invoke();
            //WinLosePauseUI.Instance.ConfirmDoubleMoney()
        });
    }

    public void ConfirmDoubleMoney()
    {
        int doubled = _currentMoney * 2;
        StartMoneyCount(_currentMoney, doubled);
        _currentMoney = doubled;

        if (_doubleButtonRT)
            _doubleButtonRT.DOScale(0f, 0.3f).SetUpdate(true);
    }

    private void OnHomeClicked()
    {
        AnimateButtonPress(_homeButtonRT, () =>
        {
            HideLose(() => OnHome?.Invoke());
        });
    }

    private void OnRestartClicked()
    {
        AnimateButtonPress(_restartRT, () =>
        {
            ResumeGame();
            OnRestart?.Invoke();
        });
    }

    private void OnContinueClicked()
    {
        AnimateButtonPress(_continueRT, () =>
        {
            ResumeGame();
            OnContinue?.Invoke();
        });
    }

    private void OnSkipClicked()
    {
        AnimateButtonPress(_skipRT, () =>
        {
            ResumeGame();
            OnSkip?.Invoke();
        });
    }

    #endregion

    // =================================================================
    #region Helpers

    private void AnimateButtonPress(RectTransform rt, Action onComplete = null)
    {
        if (rt == null) { onComplete?.Invoke(); return; }

        rt.DOKill();
        Sequence seq = DOTween.Sequence().SetUpdate(true);
        seq.Append(rt.DOScale(0.85f, 0.08f).SetUpdate(true));
        seq.Append(rt.DOScale(1f, 0.12f).SetEase(Ease.OutBack).SetUpdate(true));
        seq.OnComplete(() => onComplete?.Invoke());
        seq.Play();
    }

    private void HideInstant(CanvasGroup cg)
    {
        if (cg == null) return;
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    #endregion
}