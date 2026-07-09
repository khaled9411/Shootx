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

    [Header("SHINE")]
    [SerializeField] private Image shineImage;
    [SerializeField] private float shineRotateSpeed = 60f;

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
    [SerializeField] private float bgFadeDuration = 0.25f;
    [SerializeField] private float elementDelay = 0.04f;
    [SerializeField] private float popInDuration = 0.40f;
    [SerializeField] private float punchStrength = 0.25f;
    [SerializeField] private float moneyCountDuration = 1.2f;
    [SerializeField] private Ease elementEaseIn = Ease.OutBack;
    [SerializeField] private Ease elementEaseOut = Ease.InBack;

    // ?? EVENTS ??????????????????????????????????????????????????????????????
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
    private Tween _shineTween;
    private LevelLoader _levelLoader;

    // Win
    private RectTransform _winHeaderRT;
    private RectTransform _winTitleRT;
    private RectTransform _rewardGroupRT;
    private RectTransform _doubleButtonRT;
    private RectTransform _nextButtonRT;
    private RectTransform _shineRT;

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
        if (shineImage) _shineRT = shineImage.GetComponent<RectTransform>();

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

        if (shineImage) shineImage.color = new Color(1f, 1f, 1f, 0f);
    }

    private void BindButtons()
    {
        if (nextButton) nextButton.onClick.AddListener(OnNextClicked);
        if (doubleMoneyButton) doubleMoneyButton.onClick.AddListener(OnDoubleClicked);
        if (retryButton) retryButton.onClick.AddListener(OnRetryClicked);
        if (skipLevelButton) skipLevelButton.onClick.AddListener(OnSkipLevelClicked);
        if (pauseButton) pauseButton.onClick.AddListener(TogglePause);
        if (resumeButton) resumeButton.onClick.AddListener(OnResumeClicked);
        if (retryPauseButton) retryPauseButton.onClick.AddListener(OnRetryClicked);
        if (skipLevelPauseButton) skipLevelPauseButton.onClick.AddListener(OnSkipLevelClicked);

        _levelLoader = FindFirstObjectByType<LevelLoader>();
        if (_levelLoader != null) OnSkipLevel += _levelLoader.OnSkipLevel;
        else Debug.LogWarning("[WinLosePauseUI] LevelLoader not found in scene!");
    }

    private void OnDestroy()
    {
        _shineTween?.Kill();
        if (_levelLoader != null) OnSkipLevel -= _levelLoader.OnSkipLevel;
    }

    #endregion

    #region WIN SCREEN

    public void ShowWin(int earnedMoney)
    {
        _currentMoney = earnedMoney;
        GameDataManager.Instance.AddSoftCurrency(earnedMoney);

        HideInstant(winPanel);
        winPanel.alpha = 0f;

        if (_winHeaderRT)
        {
            _winHeaderRT.localScale = Vector3.one * 1.4f;
            _winHeaderRT.anchoredPosition += Vector2.up * 300f;
            Color c = winHeader.color; c.a = 0f; winHeader.color = c;
        }

        if (_winTitleRT)
        {
            _winTitleRT.localScale = Vector3.one * 2.2f;
            winTitleText.alpha = 0f;
        }

        if (_rewardGroupRT)
        {
            _rewardGroupRT.localScale = Vector3.zero;
            if (moneyAmountText) { moneyAmountText.text = "0"; moneyAmountText.alpha = 0f; }
        }

        if (_doubleButtonRT) _doubleButtonRT.localScale = Vector3.zero;

        if (_nextButtonRT)
        {
            _nextButtonRT.localScale = Vector3.zero;
            _nextButtonRT.anchoredPosition += Vector2.down * 120f;
        }

        if (_shineRT)
        {
            _shineRT.localScale = Vector3.zero;
            _shineRT.localRotation = Quaternion.identity;
            Color sc = shineImage.color; sc.a = 0f; shineImage.color = sc;
        }


        Sequence seq = DOTween.Sequence().SetUpdate(true);

        seq.AppendCallback(() =>
        {
            winPanel.alpha = 1f;
            winPanel.interactable = true;
            winPanel.blocksRaycasts = true;
        });

        if (_winHeaderRT)
        {
            Vector2 targetPos = _winHeaderRT.anchoredPosition - Vector2.up * 300f;
            seq.Append(
                winHeader.DOFade(1f, bgFadeDuration).SetUpdate(true)
            );
            seq.Join(
                _winHeaderRT.DOAnchorPos(targetPos, popInDuration * 0.7f)
                            .SetEase(Ease.OutBounce).SetUpdate(true)
            );
            seq.Join(
                _winHeaderRT.DOScale(1f, popInDuration * 0.6f)
                            .SetEase(Ease.OutBack).SetUpdate(true)
            );
        }

        if (_winTitleRT)
        {
            seq.Append(
                winTitleText.DOFade(1f, 0.15f).SetUpdate(true)
            );
            seq.Join(
                _winTitleRT.DOScale(1f, popInDuration * 0.55f)
                           .SetEase(Ease.OutBack, overshoot: 1.8f).SetUpdate(true)
            );

            seq.AppendCallback(() =>
                _winTitleRT.DOPunchScale(
                    Vector3.one * (punchStrength + 0.15f),
                    0.55f, 9, 0.5f
                ).SetUpdate(true)
            );
        }

        seq.AppendInterval(elementDelay * 2f);

        if (_rewardGroupRT)
        {
            seq.Append(
                _rewardGroupRT.DOScale(1.15f, popInDuration * 0.5f)
                              .SetEase(Ease.OutExpo).SetUpdate(true)
            );
            seq.Join(
                _rewardGroupRT.DOScale(1f, 0.18f).SetDelay(popInDuration * 0.5f).SetUpdate(true)
            );

            if (moneyAmountText)
                seq.Join(moneyAmountText.DOFade(1f, 0.2f).SetUpdate(true));

            if (_shineRT)
            {
                seq.Join(
                    shineImage.DOFade(1f, 0.3f).SetUpdate(true)
                );
                seq.Join(
                    _shineRT.DOScale(1.2f, popInDuration * 0.6f)
                            .SetEase(Ease.OutBack).SetUpdate(true)
                );
                seq.AppendCallback(() =>
                {
                    _shineRT.DOScale(1f, 0.15f).SetUpdate(true);
                    StartShineRotation();
                });
            }

            seq.AppendCallback(() => StartMoneyCount(0, _currentMoney));
        }

        if (_doubleButtonRT)
        {
            seq.Join(
                _doubleButtonRT.DOScale(1.2f, popInDuration * 0.55f)
                               .SetEase(Ease.OutBack, overshoot: 2f).SetUpdate(true)
            );
            seq.AppendCallback(() =>
                _doubleButtonRT.DOScale(1f, 0.15f).SetUpdate(true)
            );

            seq.AppendCallback(() =>
                _doubleButtonRT
                    .DOPunchScale(Vector3.one * (punchStrength + 0.1f), 0.5f, 7, 0.45f)
                    .SetUpdate(true)
            );
        }

        if (_nextButtonRT)
        {
            Vector2 targetPos = _nextButtonRT.anchoredPosition + Vector2.down * 120f;

            Vector2 finalPos = _nextButtonRT.anchoredPosition - Vector2.down * 120f;

            seq.Join(
                _nextButtonRT.DOScale(1.1f, popInDuration * 0.6f)
                             .SetEase(Ease.OutBack).SetUpdate(true)
            );
            seq.Join(
                _nextButtonRT.DOAnchorPos(finalPos, popInDuration * 0.6f)
                             .SetEase(Ease.OutBack).SetUpdate(true)
            );
            seq.AppendCallback(() =>
                _nextButtonRT.DOScale(1f, 0.15f).SetUpdate(true)
            );
        }

        seq.Play();
    }


    private void StartShineRotation()
    {
        _shineTween?.Kill();
        if (_shineRT == null) return;

        _shineTween = _shineRT
            .DORotate(new Vector3(0f, 0f, -360f), 360f / shineRotateSpeed, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart)
            .SetUpdate(true);
    }

    private void StopShineRotation()
    {
        _shineTween?.Kill();
        _shineTween = null;
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
        StopShineRotation();

        winPanel.interactable = false;
        winPanel.blocksRaycasts = false;

        Sequence seq = DOTween.Sequence().SetUpdate(true);
        seq.Append(winPanel.DOFade(0f, 0.25f).SetUpdate(true));
        seq.OnComplete(() => { HideInstant(winPanel); onComplete?.Invoke(); });
        seq.Play();
    }

    #endregion

    #region LOSE SCREEN

    public void ShowLose()
    {
        HideInstant(losePanel);
        losePanel.alpha = 0f;

        if (_loseHeaderRT)
        {
            _loseHeaderRT.localScale = Vector3.one * 1.4f;
            _loseHeaderRT.anchoredPosition += Vector2.up * 300f;
            Color c = loseHeader.color; c.a = 0f; loseHeader.color = c;
        }

        if (_loseTitleRT)
        {
            _loseTitleRT.localScale = Vector3.one * 2.2f;
            loseTitleText.alpha = 0f;
        }

        if (_retryButtonRT)
        {
            _retryButtonRT.localScale = Vector3.zero;
            _retryButtonRT.anchoredPosition += Vector2.down * 120f;
        }

        if (_skipLevelButtonRT)
        {
            _skipLevelButtonRT.localScale = Vector3.zero;
            _skipLevelButtonRT.anchoredPosition += Vector2.down * 120f;
        }

        Sequence seq = DOTween.Sequence().SetUpdate(true);

        seq.AppendCallback(() =>
        {
            losePanel.alpha = 1f;
            losePanel.interactable = true;
            losePanel.blocksRaycasts = true;
        });

        if (_loseHeaderRT)
        {
            Vector2 targetPos = _loseHeaderRT.anchoredPosition - Vector2.up * 300f;
            seq.Append(loseHeader.DOFade(1f, bgFadeDuration).SetUpdate(true));
            seq.Join(_loseHeaderRT.DOAnchorPos(targetPos, popInDuration * 0.7f).SetEase(Ease.OutBounce).SetUpdate(true));
            seq.Join(_loseHeaderRT.DOScale(1f, popInDuration * 0.6f).SetEase(Ease.OutBack).SetUpdate(true));
        }

        if (_loseTitleRT)
        {
            seq.Append(loseTitleText.DOFade(1f, 0.15f).SetUpdate(true));
            seq.Join(_loseTitleRT.DOScale(1f, popInDuration * 0.55f).SetEase(Ease.OutBack, overshoot: 1.8f).SetUpdate(true));
            seq.AppendCallback(() => _loseTitleRT.DOPunchScale(Vector3.one * (punchStrength + 0.15f), 0.55f, 9, 0.5f).SetUpdate(true));
        }

        seq.AppendInterval(elementDelay * 2f);

        RectTransform[] loseButtons = { _retryButtonRT, _skipLevelButtonRT };
        foreach (var btn in loseButtons)
        {
            if (btn == null) continue;
            Vector2 finalPos = btn.anchoredPosition - Vector2.down * 120f;

            seq.Join(btn.DOScale(1.1f, popInDuration * 0.6f).SetEase(Ease.OutBack).SetUpdate(true));
            seq.Join(btn.DOAnchorPos(finalPos, popInDuration * 0.6f).SetEase(Ease.OutBack).SetUpdate(true));
            seq.AppendCallback(() => btn.DOScale(1f, 0.15f).SetUpdate(true));
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

        if (_pauseHeaderRT)
        {
            _pauseHeaderRT.localScale = Vector3.one * 1.4f;
            _pauseHeaderRT.anchoredPosition += Vector2.up * 300f;
            Color c = pauseHeader.color; c.a = 0f; pauseHeader.color = c;
        }

        if (_pauseTitleRT)
        {
            _pauseTitleRT.localScale = Vector3.one * 2.2f;
            pauseTitleText.alpha = 0f;
        }

        RectTransform[] buttons = { _resumeButtonRT, _retryPauseRT, _skipLevelPauseRT };
        foreach (var btn in buttons)
        {
            if (btn == null) continue;
            btn.localScale = Vector3.zero;
            btn.anchoredPosition += Vector2.down * 120f;
        }

        Sequence seq = DOTween.Sequence().SetUpdate(true);

        seq.AppendCallback(() =>
        {
            pausePanel.alpha = 1f;
            pausePanel.interactable = true;
            pausePanel.blocksRaycasts = true;
        });

        if (_pauseHeaderRT)
        {
            Vector2 targetPos = _pauseHeaderRT.anchoredPosition - Vector2.up * 300f;
            seq.Append(pauseHeader.DOFade(1f, bgFadeDuration).SetUpdate(true));
            seq.Join(_pauseHeaderRT.DOAnchorPos(targetPos, popInDuration * 0.7f).SetEase(Ease.OutBounce).SetUpdate(true));
            seq.Join(_pauseHeaderRT.DOScale(1f, popInDuration * 0.6f).SetEase(Ease.OutBack).SetUpdate(true));
        }

        if (_pauseTitleRT)
        {
            seq.Append(pauseTitleText.DOFade(1f, 0.15f).SetUpdate(true));
            seq.Join(_pauseTitleRT.DOScale(1f, popInDuration * 0.55f).SetEase(Ease.OutBack, overshoot: 1.8f).SetUpdate(true));
            seq.AppendCallback(() => _pauseTitleRT.DOPunchScale(Vector3.one * (punchStrength + 0.15f), 0.55f, 9, 0.5f).SetUpdate(true));
        }

        seq.AppendInterval(elementDelay * 2f);

        foreach (var btn in buttons)
        {
            if (btn == null) continue;
            Vector2 finalPos = btn.anchoredPosition - Vector2.down * 120f;

            seq.Join(btn.DOScale(1.1f, popInDuration * 0.6f).SetEase(Ease.OutBack).SetUpdate(true));
            seq.Join(btn.DOAnchorPos(finalPos, popInDuration * 0.6f).SetEase(Ease.OutBack).SetUpdate(true));
            seq.AppendCallback(() => btn.DOScale(1f, 0.15f).SetUpdate(true));
            seq.AppendInterval(elementDelay);
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

    #region Button Callbacks

    private void OnNextClicked()
    {
        AnimateButtonPress(_nextButtonRT, () =>
            HideWin(() => OnNextLevel?.Invoke()));
    }

    private void OnDoubleClicked()
    {
        if (doubleMoneyButton) doubleMoneyButton.interactable = false;

        AnimateButtonPress(_doubleButtonRT, () =>
        {
            GameAdManager.Instance.ShowRewardedAd(watched =>
            {
                if (watched)
                {
                    ConfirmDoubleMoney();
                    OnDoubleMoney?.Invoke();
                }
                else
                {
                    Debug.Log("[WinLosePauseUI] Ad not completed – no double.");
                    if (doubleMoneyButton) doubleMoneyButton.interactable = true;
                }
            });
        });
    }

    public void ConfirmDoubleMoney()
    {
        int doubled = _currentMoney * 2;
        GameDataManager.Instance.AddSoftCurrency(_currentMoney);
        StartMoneyCount(_currentMoney, doubled);
        _currentMoney = doubled;

        if (_doubleButtonRT)
            _doubleButtonRT.DOScale(0f, 0.3f).SetEase(Ease.InBack).SetUpdate(true);
    }

    private void OnRetryClicked()
    {
        if (_isPaused)
            AnimateButtonPress(_retryPauseRT, () => { ResumeGame(); OnRetry?.Invoke(); });
        else
            AnimateButtonPress(_retryButtonRT, () => HideLose(() => OnRetry?.Invoke()));
    }

    private void OnSkipLevelClicked()
    {
        if (_isPaused)
        {
            if (skipLevelPauseButton) skipLevelPauseButton.interactable = false;
            AnimateButtonPress(_skipLevelPauseRT, () =>
            {
                GameAdManager.Instance.ShowRewardedAd(watched =>
                {
                    if (watched) { ResumeGame(); OnSkipLevel?.Invoke(); }
                    else
                    {
                        Debug.Log("[WinLosePauseUI] Ad not completed – no skip.");
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
                    if (watched) HideLose(() => OnSkipLevel?.Invoke());
                    else
                    {
                        Debug.Log("[WinLosePauseUI] Ad not completed – no skip.");
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