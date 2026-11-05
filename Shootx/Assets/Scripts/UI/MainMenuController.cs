using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class MainMenuController : MonoBehaviour
{
    // ===================================================================
    #region Inspector Fields
    // ===================================================================

    [Header("=== Animation Controller ===")]
    [SerializeField] private SoloMainMenuAnimationController animationController;

    [Header("=== Currency Bar ===")]
    [SerializeField] private TextMeshProUGUI softCurrencyText;
    [SerializeField] private TextMeshProUGUI hardCurrencyText;
    [SerializeField] private Button softCurrencyButton;
    [SerializeField] private Button hardCurrencyButton;

    [Header("=== Level Display ===")]
    [SerializeField] private TextMeshProUGUI levelText;

    [Header("=== Game Logo ===")]
    [SerializeField] private RectTransform gameLogo;

    [Header("=== Tap To Play ===")]
    [SerializeField] private CanvasGroup tapToPlayGroup;
    [SerializeField] private TextMeshProUGUI tapToPlayText;

    [Header("=== Settings Button ===")]
    [SerializeField] private Button settingsButton;

    [Header("=== Bottom Buttons ===")]
    [SerializeField] private Button gunsButton;
    [SerializeField] private Button outfitsButton;
    [SerializeField] private Button missionsButton;
    [SerializeField] private Button onlineButton;

    [Header("=== Missions Button Progress ===")]
    [SerializeField] private Slider missionsProgressBar;
    [SerializeField] private TextMeshProUGUI missionsProgressText;  // "3/5"

    [Header("=== Legacy Logo Animation (Fallback) ===")]
    [SerializeField] private float logoBobAmplitude = 5f;
    [SerializeField] private float logoBobDuration = 1.6f;

    [Header("=== Legacy TapToPlay Pulse (Fallback) ===")]
    [SerializeField] private float tapPulseScale = 1.025f;
    [SerializeField] private float tapPulseDuration = 0.9f;

    // ===================================================================
    #endregion

    // ===================================================================
    #region Public Properties for Integration
    // ===================================================================

    public RectTransform GameLogo => gameLogo;
    public CanvasGroup TapToPlayGroup => tapToPlayGroup;
    public TextMeshProUGUI TapToPlayText => tapToPlayText;
    public Button SettingsButton => settingsButton;
    public Button GunsButton => gunsButton;
    public Button OutfitsButton => outfitsButton;
    public Button MissionsButton => missionsButton;
    public Button OnlineButton => onlineButton;
    public SoloMainMenuAnimationController AnimationController => animationController;

    // ===================================================================
    #endregion

    // ===================================================================
    #region Private State
    // ===================================================================

    private Vector3 logoOriginalPos;
    private bool logoPosCached = false;
    private Tween logoBobTween;
    private Tween tapPulseTween;

    // ===================================================================
    #endregion

    // ===================================================================
    #region Unity Lifecycle
    // ===================================================================

    private void Awake()
    {
        EnsureAnimationController();
    }

    private void Start()
    {
        EnsureAnimationController();
        AssignButtonListeners();

        if (gameLogo != null)
        {
            logoOriginalPos = gameLogo.anchoredPosition;
            logoPosCached = true;
        }

        if (animationController != null)
        {
            animationController.CacheOriginalTransforms();
        }

        RefreshUI();
    }

    private void OnEnable()
    {
        GameDataManager.OnCurrencyChanged += RefreshCurrency;
        GameDataManager.OnLevelChanged += RefreshLevel;
        UIManager.OnTapToPlay += OnTapToPlayStarted;
    }

    private void OnDisable()
    {
        GameDataManager.OnCurrencyChanged -= RefreshCurrency;
        GameDataManager.OnLevelChanged -= RefreshLevel;
        UIManager.OnTapToPlay -= OnTapToPlayStarted;

        StopIdleAnimations();
    }

    private void EnsureAnimationController()
    {
        if (animationController == null)
            animationController = GetComponent<SoloMainMenuAnimationController>();
        if (animationController == null)
            animationController = gameObject.AddComponent<SoloMainMenuAnimationController>();
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Button Setup
    // ===================================================================

    private void AssignButtonListeners()
    {
        settingsButton?.onClick.AddListener(UIManager.Instance.ShowSettingsPopup);
        gunsButton?.onClick.AddListener(UIManager.Instance.ShowGunsPanel);
        outfitsButton?.onClick.AddListener(UIManager.Instance.ShowOutfitsPanel);
        missionsButton?.onClick.AddListener(UIManager.Instance.ShowMissionsPopup);
        onlineButton?.onClick.AddListener(OnOnlinePressed);

        softCurrencyButton?.onClick.AddListener(() => UIManager.Instance.ShowShopPopup());
        hardCurrencyButton?.onClick.AddListener(() => UIManager.Instance.ShowShopPopup());
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region UI Refresh
    // ===================================================================

    public void RefreshUI()
    {
        RefreshCurrency();
        RefreshLevel();
        RefreshMissionsProgress();
    }

    private void RefreshCurrency()
    {
        int soft = GameDataManager.Instance != null ? GameDataManager.Instance.SoftCurrency : 0;
        int hard = GameDataManager.Instance != null ? GameDataManager.Instance.HardCurrency : 0;

        if (softCurrencyText != null)
            softCurrencyText.text = FormatNumber(soft);

        if (hardCurrencyText != null)
            hardCurrencyText.text = FormatNumber(hard);
    }

    private void RefreshLevel()
    {
        int level = GameDataManager.Instance != null ? GameDataManager.Instance.CurrentLevel : 1;
        if (levelText != null)
            levelText.text = $"LEVEL {level}";
    }

    public void RefreshMissionsProgress()
    {
        if (GameDataManager.Instance == null) return;

        float progress = GameDataManager.Instance.DailyMissionsProgress; // 0..1
        int done = GameDataManager.Instance.DailyMissionsDone;
        int total = GameDataManager.Instance.DailyMissionsTotal;

        if (missionsProgressBar != null)
            missionsProgressBar.DOValue(progress, 0.4f).SetEase(Ease.OutCubic).SetUpdate(true);

        if (missionsProgressText != null)
            missionsProgressText.text = $"{done}/{total}";
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Idle & Entrance Animations Control
    // ===================================================================

    public void RestartIdleAnimations()
    {
        EnsureAnimationController();
        if (animationController != null)
        {
            animationController.StartIdleAnimations();
        }
        else
        {
            StartFallbackLogoAnimation();
            StartFallbackTapToPlayPulse();
        }
    }

    public void StopIdleAnimations()
    {
        if (animationController != null)
        {
            animationController.StopAllAnimations(restoreTransforms: false);
        }
        else
        {
            StopFallbackIdleAnimations();
        }
    }

    private void OnTapToPlayStarted()
    {
        StopIdleAnimations();
    }

    private void StartFallbackLogoAnimation()
    {
        if (gameLogo == null) return;

        if (logoBobTween != null && logoBobTween.IsActive())
            logoBobTween.Kill();

        if (logoPosCached)
            gameLogo.anchoredPosition = logoOriginalPos;

        logoBobTween = gameLogo
            .DOAnchorPosY(logoOriginalPos.y + logoBobAmplitude, logoBobDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    private void StartFallbackTapToPlayPulse()
    {
        if (tapToPlayText == null) return;

        if (tapPulseTween != null && tapPulseTween.IsActive())
            tapPulseTween.Kill();

        tapToPlayText.transform.localScale = Vector3.one;

        tapPulseTween = tapToPlayText.transform
            .DOScale(tapPulseScale, tapPulseDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    private void StopFallbackIdleAnimations()
    {
        if (logoBobTween != null && logoBobTween.IsActive())
        {
            logoBobTween.Kill();
            logoBobTween = null;
        }

        if (tapPulseTween != null && tapPulseTween.IsActive())
        {
            tapPulseTween.Kill();
            tapPulseTween = null;
        }

        if (gameLogo != null)
        {
            gameLogo.DOKill();
            if (logoPosCached) gameLogo.anchoredPosition = logoOriginalPos;
        }

        if (tapToPlayText != null)
        {
            tapToPlayText.transform.DOKill();
            tapToPlayText.transform.localScale = Vector3.one;
        }
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Button Handlers
    // ===================================================================

    private void OnOnlinePressed()
    {
        var accessMgr = Shootx.Store.MultiplayerAccessManager.Instance;
        if (accessMgr != null && accessMgr.HasAccess())
        {
            Debug.Log("[MainMenu] Player has valid multiplayer access. Transitioning to MultiplayerMenu scene...");
            UnityEngine.SceneManagement.SceneManager.LoadScene("MultiplayerMenu");
        }
        else
        {
            Debug.Log("[MainMenu] Player does not have multiplayer access. Showing access gate modal...");
            if (Shootx.Store.MultiplayerAccessGateModal.Instance != null)
            {
                Shootx.Store.MultiplayerAccessGateModal.Instance.Show();
            }
            else
            {
                Debug.LogWarning("[MainMenu] MultiplayerAccessGateModal instance not found. Opening shop as fallback.");
                UIManager.Instance?.ShowShopPopup();
            }
        }
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Helpers
    // ===================================================================

    private string FormatNumber(int number)
    {
        if (number >= 1_000_000) return $"{number / 1_000_000f:0.#}M";
        if (number >= 1_000) return $"{number / 1_000f:0.#}K";
        return number.ToString();
    }

    // ===================================================================
    #endregion
}
