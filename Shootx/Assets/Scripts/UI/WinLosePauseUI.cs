using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class WinLosePauseUI : MonoBehaviour
{
    public static WinLosePauseUI Instance { get; private set; }

    #region Inspector Fields

    [Header("WIN SCREEN")]
    [SerializeField] private CanvasGroup winPanel;
    [SerializeField] private Image winHeader;
    [SerializeField] private TextMeshProUGUI winTitleText;
    [SerializeField] private TextMeshProUGUI rewardLabelText;
    [SerializeField] private TextMeshProUGUI moneyAmountText;
    [SerializeField] private GameObject moneyRewardGroup;
    [SerializeField] private Button doubleMoneyButton;
    [SerializeField] private Button nextButton;

    [Header("LOSE SCREEN")]
    [SerializeField] private CanvasGroup losePanel;
    [SerializeField] private Image loseHeader;
    [SerializeField] private TextMeshProUGUI loseTitleText;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button skipLevelButton;

    [Header("PAUSE MENU")]
    [SerializeField] private CanvasGroup pausePanel;
    [SerializeField] private Image pauseHeader;
    [SerializeField] private TextMeshProUGUI pauseTitleText;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button retryPauseButton;
    [SerializeField] private Button skipLevelPauseButton;

    [Header("ANIMATION SETTINGS")]
    [SerializeField] private float bgFadeDuration = 0.35f;
    [SerializeField] private float elementDelay = 0.07f;
    [SerializeField] private float popInDuration = 0.45f;
    [SerializeField] private float punchStrength = 0.2f;
    [SerializeField] private float moneyCountDuration = 1.0f;
    [SerializeField] private Ease elementEaseIn = Ease.OutBack;
    [SerializeField] private Ease elementEaseOut = Ease.InBack;

    // EVENTS
    public static event Action OnNextLevel;
    public static event Action OnDoubleMoney;
    public static event Action OnRetry;
    public static event Action OnSkipLevel;
    public static event Action OnResume;

    #endregion

    #region Private State

    private int _currentMoney = 0;
    private bool _isPaused = false;
    private Tween _moneyCountTween;

    // Win
    private RectTransform _winHeaderRT;
    private RectTransform _winTitleRT;
    private RectTransform _rewardGroupRT;
    private RectTransform _doubleButtonRT;
    private RectTransform _nextButtonRT;

    // Lose
    private RectTransform _loseHeaderRT;
    private RectTransform _loseTitleRT;
    private RectTransform _retryButtonRT;
    private RectTransform _skipLevelButtonRT;

    // Pause
    private RectTransform _pauseHeaderRT;
    private RectTransform _pauseTitleRT;
    private RectTransform _resumeButtonRT;
    private RectTransform _retryPauseRT;
    private RectTransform _skipLevelPauseRT;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        CacheRectTransforms();
        InitPanels();
        BindButtons();
    }

    #endregion

    #region Initialization

    private void CacheRectTransforms()
    {
        if (winHeader) _winHeaderRT = winHeader.GetComponent<RectTransform>();
        if (winTitleText) _winTitleRT = winTitleText.GetComponent<RectTransform>();
        if (moneyRewardGroup) _rewardGroupRT = moneyRewardGroup.GetComponent<RectTransform>();
        if (doubleMoneyButton) _doubleButtonRT = doubleMoneyButton.GetComponent<RectTransform>();
        if (nextButton) _nextButtonRT = nextButton.GetComponent<RectTransform>();

        if (loseHeader) _loseHeaderRT = loseHeader.GetComponent<RectTransform>();
        if (loseTitleText) _loseTitleRT = loseTitleText.GetComponent<RectTransform>();
        if (retryButton) _retryButtonRT = retryButton.GetComponent<RectTransform>();
        if (skipLevelButton) _skipLevelButtonRT = skipLevelButton.GetComponent<RectTransform>();

        if (pauseHeader) _pauseHeaderRT = pauseHeader.GetComponent<RectTransform>();
        if (pauseTitleText) _pauseTitleRT = pauseTitleText.GetComponent<RectTransform>();
        if (resumeButton) _resumeButtonRT = resumeButton.GetComponent<RectTransform>();
        if (retryPauseButton) _retryPauseRT = retryPauseButton.GetComponent<RectTransform>();
        if (skipLevelPauseButton) _skipLevelPauseRT = skipLevelPauseButton.GetComponent<RectTransform>();
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
        if (retryButton) retryButton.onClick.AddListener(OnRetryClicked);
        if (skipLevelButton) skipLevelButton.onClick.AddListener(OnSkipLevelClicked);

        // Pause
        if (pauseButton) pauseButton.onClick.AddListener(TogglePause);
        if (resumeButton) resumeButton.onClick.AddListener(OnResumeClicked);
        if (retryPauseButton) retryPauseButton.onClick.AddListener(OnRetryClicked);
        if (skipLevelPauseButton) skipLevelPauseButton.onClick.AddListener(OnSkipLevelClicked);

        OnSkipLevel += FindFirstObjectByType<LevelLoader>().OnSkipLevel;
    }

    private void OnDestroy()
    {
        OnSkipLevel -= FindFirstObjectByType<LevelLoader>().OnSkipLevel;
    }

    #endregion

    // =================================================================
    #region WIN SCREEN

    public void ShowWin(int earnedMoney)
    {
        _currentMoney = earnedMoney;
        GameDataManager.Instance.AddSoftCurrency(earnedMoney);

        HideInstant(winPanel);
        winPanel.alpha = 0f;

        Sequence seq = DOTween.Sequence().SetUpdate(true);

        if (_winHeaderRT)
        {
            Vector2 origPos = _winHeaderRT.anchoredPosition;
            _winHeaderRT.anchoredPosition = origPos + Vector2.up * 200f;
            Color c = winHeader.color; c.a = 0f; winHeader.color = c;

            seq.Append(winHeader.DOFade(1f, bgFadeDuration).SetUpdate(true));
            seq.Join(_winHeaderRT
                .DOAnchorPos(origPos, popInDuration * 0.8f)
                .SetEase(Ease.OutBounce).SetUpdate(true));
        }

        seq.AppendCallback(() =>
        {
            winPanel.alpha = 1f;
            winPanel.interactable = true;
            winPanel.blocksRaycasts = true;
        });

        if (_winTitleRT)
        {
            _winTitleRT.localScale = Vector3.zero;
            winTitleText.alpha = 0f;
            seq.AppendInterval(elementDelay);
            seq.Append(winTitleText.DOFade(1f, 0.2f).SetUpdate(true));
            seq.Join(_winTitleRT
                .DOScale(1.15f, popInDuration * 0.5f)
                .SetEase(Ease.OutCubic).SetUpdate(true));
            seq.Append(_winTitleRT.DOScale(1f, 0.15f).SetUpdate(true));
            seq.Append(_winTitleRT
                .DOPunchScale(Vector3.one * punchStrength, 0.5f, 8, 0.6f)
                .SetUpdate(true));
        }

        if (_rewardGroupRT)
        {
            Vector2 origPos = _rewardGroupRT.anchoredPosition;
            _rewardGroupRT.anchoredPosition = origPos + Vector2.up * 60f;
            _rewardGroupRT.localScale = Vector3.zero;

            if (moneyAmountText)
            {
                moneyAmountText.text = "0";
                moneyAmountText.alpha = 0f;
            }

            seq.AppendInterval(elementDelay);
            seq.Append(_rewardGroupRT
                .DOScale(1f, popInDuration * 0.75f)
                .SetEase(elementEaseIn).SetUpdate(true));
            seq.Join(_rewardGroupRT
                .DOAnchorPos(origPos, popInDuration * 0.75f)
                .SetEase(elementEaseIn).SetUpdate(true));

            if (moneyAmountText)
                seq.Join(moneyAmountText.DOFade(1f, 0.25f).SetUpdate(true));

            seq.AppendCallback(() => StartMoneyCount(0, _currentMoney));
        }

        seq.AppendInterval(moneyCountDuration * 0.6f);
        if (_doubleButtonRT)
        {
            _doubleButtonRT.localScale = Vector3.zero;
            seq.Append(_doubleButtonRT
                .DOScale(1f, popInDuration)
                .SetEase(elementEaseIn).SetUpdate(true));
            seq.Append(_doubleButtonRT
                .DOPunchScale(Vector3.one * (punchStrength + 0.1f), 0.5f, 8, 0.5f)
                .SetUpdate(true));
        }

        seq.AppendInterval(elementDelay);
        if (_nextButtonRT)
        {
            Vector2 origPos = _nextButtonRT.anchoredPosition;
            _nextButtonRT.anchoredPosition = origPos + Vector2.down * 70f;
            _nextButtonRT.localScale = Vector3.zero;

            seq.Append(_nextButtonRT
                .DOScale(1f, popInDuration * 0.9f)
                .SetEase(elementEaseIn).SetUpdate(true));
            seq.Join(_nextButtonRT
                .DOAnchorPos(origPos, popInDuration * 0.9f)
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
            x => { current = x; if (moneyAmountText) moneyAmountText.text = "+" + x.ToString("N0"); },
            to,
            moneyCountDuration
        ).SetEase(Ease.OutExpo).SetUpdate(true);
    }

    private void HideWin(Action onComplete = null)
    {
        _moneyCountTween?.Kill();
        winPanel.interactable = false;
        winPanel.blocksRaycasts = false;

        Sequence seq = DOTween.Sequence().SetUpdate(true);
        seq.Append(winPanel.DOFade(0f, 0.25f).SetUpdate(true));
        seq.OnComplete(() => { HideInstant(winPanel); onComplete?.Invoke(); });
        seq.Play();
    }

    #endregion

    // =================================================================
    #region LOSE SCREEN

    public void ShowLose()
    {
        HideInstant(losePanel);
        losePanel.alpha = 0f;

        Sequence seq = DOTween.Sequence().SetUpdate(true);

        if (_loseHeaderRT)
        {
            Vector2 origPos = _loseHeaderRT.anchoredPosition;
            _loseHeaderRT.anchoredPosition = origPos + Vector2.up * 200f;
            Color c = loseHeader.color; c.a = 0f; loseHeader.color = c;

            seq.Append(loseHeader.DOFade(1f, bgFadeDuration).SetUpdate(true));
            seq.Join(_loseHeaderRT
                .DOAnchorPos(origPos, popInDuration * 0.7f)
                .SetEase(Ease.OutBounce).SetUpdate(true));
        }

        seq.AppendCallback(() =>
        {
            losePanel.alpha = 1f;
            losePanel.interactable = true;
            losePanel.blocksRaycasts = true;
        });

        if (_loseTitleRT)
        {
            _loseTitleRT.localScale = Vector3.zero;
            loseTitleText.alpha = 0f;

            seq.AppendInterval(elementDelay);
            seq.Append(loseTitleText.DOFade(1f, 0.2f).SetUpdate(true));
            seq.Join(_loseTitleRT
                .DOScale(1.2f, popInDuration * 0.5f)
                .SetEase(Ease.OutCubic).SetUpdate(true));
            seq.Append(_loseTitleRT.DOScale(1f, 0.12f).SetUpdate(true));
            seq.Append(_loseTitleRT
                .DOShakePosition(0.55f, strength: 12f, vibrato: 14, randomness: 90,
                    snapping: false, fadeOut: true).SetUpdate(true));
        }

        seq.AppendInterval(elementDelay);
        if (_retryButtonRT)
        {
            Vector2 origPos = _retryButtonRT.anchoredPosition;
            _retryButtonRT.anchoredPosition = origPos + Vector2.down * 80f;
            _retryButtonRT.localScale = Vector3.zero;

            seq.Append(_retryButtonRT
                .DOScale(1f, popInDuration).SetEase(elementEaseIn).SetUpdate(true));
            seq.Join(_retryButtonRT
                .DOAnchorPos(origPos, popInDuration).SetEase(elementEaseIn).SetUpdate(true));
            seq.Append(_retryButtonRT
                .DOPunchScale(Vector3.one * punchStrength, 0.4f, 6, 0.5f).SetUpdate(true));
        }

        seq.AppendInterval(elementDelay);
        if (_skipLevelButtonRT)
        {
            Vector2 origPos = _skipLevelButtonRT.anchoredPosition;
            _skipLevelButtonRT.anchoredPosition = origPos + Vector2.down * 80f;
            _skipLevelButtonRT.localScale = Vector3.zero;

            seq.Append(_skipLevelButtonRT
                .DOScale(1f, popInDuration).SetEase(elementEaseIn).SetUpdate(true));
            seq.Join(_skipLevelButtonRT
                .DOAnchorPos(origPos, popInDuration).SetEase(elementEaseIn).SetUpdate(true));
        }

        seq.Play();
    }

    private void HideLose(Action onComplete = null)
    {
        losePanel.interactable = false;
        losePanel.blocksRaycasts = false;

        Sequence seq = DOTween.Sequence().SetUpdate(true);
        seq.Append(losePanel.DOFade(0f, 0.25f).SetUpdate(true));
        seq.OnComplete(() => { HideInstant(losePanel); onComplete?.Invoke(); });
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

        HideInstant(pausePanel);
        pausePanel.alpha = 0f;

        Sequence seq = DOTween.Sequence().SetUpdate(true);

        if (_pauseHeaderRT)
        {
            Vector2 origPos = _pauseHeaderRT.anchoredPosition;
            _pauseHeaderRT.anchoredPosition = origPos + Vector2.up * 150f;
            Color c = pauseHeader.color; c.a = 0f; pauseHeader.color = c;

            seq.Append(pauseHeader.DOFade(1f, bgFadeDuration).SetUpdate(true));
            seq.Join(_pauseHeaderRT
                .DOAnchorPos(origPos, popInDuration * 0.7f)
                .SetEase(Ease.OutBack).SetUpdate(true));
        }

        seq.AppendCallback(() =>
        {
            pausePanel.alpha = 1f;
            pausePanel.interactable = true;
            pausePanel.blocksRaycasts = true;
        });

        RectTransform[] buttons = { _resumeButtonRT, _retryPauseRT, _skipLevelPauseRT };
        foreach (var btn in buttons)
        {
            if (btn == null) continue;
            btn.localScale = Vector3.zero;
            seq.AppendInterval(elementDelay);
            seq.Append(btn
                .DOScale(1f, popInDuration * 0.75f)
                .SetEase(elementEaseIn).SetUpdate(true));
        }

        seq.Play();
    }

    public void ResumeGame()
    {
        _isPaused = false;

        Sequence seq = DOTween.Sequence().SetUpdate(true);

        RectTransform[] buttons = { _skipLevelPauseRT, _retryPauseRT, _resumeButtonRT };
        foreach (var btn in buttons)
        {
            if (btn == null) continue;
            seq.Append(btn
                .DOScale(0f, popInDuration * 0.4f)
                .SetEase(elementEaseOut).SetUpdate(true));
            seq.AppendInterval(elementDelay * 0.5f);
        }

        if (_pauseHeaderRT)
        {
            seq.Append(pauseHeader.DOFade(0f, 0.2f).SetUpdate(true));
            seq.Join(_pauseHeaderRT
                .DOAnchorPosY(_pauseHeaderRT.anchoredPosition.y + 120f, 0.3f)
                .SetEase(Ease.InBack).SetUpdate(true));
        }

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
            HideWin(() => OnNextLevel?.Invoke()));
    }

    // ?? Double Money: ??? ?????? ??????? ????? ??????????????????????
    private void OnDoubleClicked()
    {
        // ????? ???? ?????? ???? ????? ???????
        if (doubleMoneyButton) doubleMoneyButton.interactable = false;

        AnimateButtonPress(_doubleButtonRT, () =>
        {
            GameAdManager.Instance.ShowRewardedAd(watched =>
            {
                if (watched)
                {
                    // ?????? ???? ??????? ? ????? ????????
                    ConfirmDoubleMoney();
                    OnDoubleMoney?.Invoke();
                }
                else
                {
                    // ?? ????? ? ????? ????? ????
                    Debug.Log("[WinLosePauseUI] ?? ????? ???????? ?? ??????.");
                    if (doubleMoneyButton) doubleMoneyButton.interactable = true;
                }
            });
        });
    }

    public void ConfirmDoubleMoney()
    {
        int doubled = _currentMoney * 2;
        GameDataManager.Instance.AddSoftCurrency(_currentMoney); // ????? ????? ???
        StartMoneyCount(_currentMoney, doubled);
        _currentMoney = doubled;

        // ????? ?? ???????? ??? ?????????
        if (_doubleButtonRT)
            _doubleButtonRT.DOScale(0f, 0.3f).SetEase(Ease.InBack).SetUpdate(true);
    }

    private void OnRetryClicked()
    {
        if (_isPaused)
        {
            AnimateButtonPress(_retryPauseRT, () =>
            {
                ResumeGame();
                OnRetry?.Invoke();
            });
        }
        else
        {
            AnimateButtonPress(_retryButtonRT, () =>
                HideLose(() => OnRetry?.Invoke()));
        }
    }

    // ?? Skip Level: ??? ?????? ??????? ????? (Lose + Pause) ?????????
    private void OnSkipLevelClicked()
    {
        if (_isPaused)
        {
            // Skip ?? ????? ????
            if (skipLevelPauseButton) skipLevelPauseButton.interactable = false;

            AnimateButtonPress(_skipLevelPauseRT, () =>
            {
                GameAdManager.Instance.ShowRewardedAd(watched =>
                {
                    if (watched)
                    {
                        ResumeGame();
                        OnSkipLevel?.Invoke();
                    }
                    else
                    {
                        Debug.Log("[WinLosePauseUI] ?? ????? ???????? ?? Skip.");
                        if (skipLevelPauseButton) skipLevelPauseButton.interactable = true;
                    }
                });
            });
        }
        else
        {
            if (skipLevelButton) skipLevelButton.interactable = false;

            AnimateButtonPress(_skipLevelButtonRT, () =>
            {
                GameAdManager.Instance.ShowRewardedAd(watched =>
                {
                    if (watched)
                    {
                        HideLose(() => OnSkipLevel?.Invoke());
                    }
                    else
                    {
                        Debug.Log("[WinLosePauseUI] ?? ????? ???????? ?? Skip.");
                        if (skipLevelButton) skipLevelButton.interactable = true;
                    }
                });
            });
        }
    }

    private void OnResumeClicked()
    {
        AnimateButtonPress(_resumeButtonRT, () =>
        {
            ResumeGame();
            OnResume?.Invoke();
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
        seq.Append(rt.DOScale(0.88f, 0.07f).SetUpdate(true));
        seq.Append(rt.DOScale(1f, 0.14f).SetEase(Ease.OutBack).SetUpdate(true));
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