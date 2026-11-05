using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Controls subtle, polished DOTween animations for the Solo Mode Main Menu.
/// Fully coordinated with UIManager appearance and disappearance transitions:
/// - Entrance: Seamlessly augments UIManager's staggered CanvasGroup fade with subtle, non-intrusive transform reveals.
/// - Exit: Stops idle animations without jarring pops or layout snaps during fade-out.
/// - Idle: Gentle, organic breathing on logo, play button visual, and selected bottom buttons.
/// - Panels & Popups: Automatically stops when submenus or popups are opened to prevent background motion.
/// Preserves exact layout positions, original scales, and button responsiveness at all times.
/// </summary>
public class SoloMainMenuAnimationController : MonoBehaviour
{
    // ===================================================================
    #region Inspector Fields - General Toggles
    // ===================================================================

    [Header("=== Master Toggles ===")]
    [Tooltip("Enable or disable the one-time entrance sequence when opening the menu.")]
    [SerializeField] private bool enableEntranceAnimations = true;

    [Tooltip("Enable or disable subtle continuous idle breathing animations.")]
    [SerializeField] private bool enableIdleAnimations = true;

    // ===================================================================
    #endregion

    // ===================================================================
    #region Inspector Fields - Title / Logo
    // ===================================================================

    [Header("=== Title / Game Logo ===")]
    [Tooltip("Main title / logo RectTransform to animate.")]
    [SerializeField] private RectTransform gameLogo;

    [Tooltip("Enable vertical floating bobbing for the game logo.")]
    [SerializeField] private bool enableLogoBob = true;

    [Tooltip("Vertical floating distance in UI pixels (suggested: 4-6 pixels).")]
    [SerializeField] private float logoBobDistance = 4.5f;

    [Tooltip("Total cycle duration in seconds for one complete up-and-down float (suggested: 2.5-4s).")]
    [SerializeField] private float logoBobCycleDuration = 3.2f;

    [Tooltip("Easing curve for logo floating bobbing.")]
    [SerializeField] private Ease logoBobEase = Ease.InOutSine;

    // ===================================================================
    #endregion

    // ===================================================================
    #region Inspector Fields - Play Button & Tap To Play
    // ===================================================================

    [Header("=== Play Button / Tap To Play ===")]
    [Tooltip("CanvasGroup for the tap to play area.")]
    [SerializeField] private CanvasGroup tapToPlayGroup;

    [Tooltip("Visual child RectTransform of the play prompt (TapToPlayText) to breathe without affecting hit area.")]
    [SerializeField] private RectTransform tapToPlayVisual;

    [Tooltip("Interactive button component for Tap to Play.")]
    [SerializeField] private Button playButton;

    [Tooltip("Enable scale breathing idle animation for the Play prompt.")]
    [SerializeField] private bool enablePlayPulse = true;

    [Tooltip("Breathing scale peak multiplier (suggested: 1.025 = 2.5% increase).")]
    [SerializeField] private float playPulseScale = 1.025f;

    [Tooltip("Total cycle duration in seconds for one full breathing cycle (suggested: 1.5-2.2s).")]
    [SerializeField] private float playPulseCycleDuration = 1.8f;

    [Tooltip("Easing curve for play breathing pulse.")]
    [SerializeField] private Ease playPulseEase = Ease.InOutSine;

    // ===================================================================
    #endregion

    // ===================================================================
    #region Inspector Fields - Secondary Buttons
    // ===================================================================

    [Header("=== Secondary Buttons ===")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button gunsButton;
    [SerializeField] private Button outfitsButton;
    [SerializeField] private Button missionsButton;
    [SerializeField] private Button onlineButton;

    [Tooltip("Enable gentle scale breathing for secondary action buttons.")]
    [SerializeField] private bool enableSecondaryButtonsIdle = true;

    [Tooltip("Subtle scale multiplier for secondary buttons (suggested: 1.012 to keep Play button visually dominant).")]
    [SerializeField] private float secondaryButtonScaleMultiplier = 1.012f;

    [Tooltip("Base cycle duration in seconds for secondary button idle breathing.")]
    [SerializeField] private float secondaryButtonBaseCycleDuration = 2.6f;

    [Tooltip("Easing curve for secondary button idle breathing.")]
    [SerializeField] private Ease secondaryButtonEase = Ease.InOutSine;

    // ===================================================================
    #endregion

    // ===================================================================
    #region Inspector Fields - Decorative Background Elements
    // ===================================================================

    [Header("=== Decorative Background Shine ===")]
    [Tooltip("RectTransform of the background ambient shine element.")]
    [SerializeField] private RectTransform shineTransform;

    [Tooltip("Image component of the background ambient shine element.")]
    [SerializeField] private Image shineImage;

    [Tooltip("Enable subtle breathing idle for the background shine.")]
    [SerializeField] private bool enableBackgroundShineIdle = true;

    [Tooltip("Scale breathing multiplier for the background shine.")]
    [SerializeField] private float shineScaleMultiplier = 1.025f;

    [Tooltip("Cycle duration in seconds for background shine breathing.")]
    [SerializeField] private float shineCycleDuration = 4f;

    [Tooltip("Easing curve for background shine breathing.")]
    [SerializeField] private Ease shineEase = Ease.InOutSine;

    // ===================================================================
    #endregion

    // ===================================================================
    #region Inspector Fields - Entrance Timing & Easing
    // ===================================================================

    [Header("=== Entrance Animation Settings ===")]
    [Tooltip("Duration of the logo entrance rise in seconds.")]
    [SerializeField] private float titleEntranceDuration = 0.38f;

    [Tooltip("Distance in UI pixels from which the title moves upward into normal position.")]
    [SerializeField] private float titleEntranceMoveDistance = 14f;

    [Tooltip("Easing curve for the title entrance.")]
    [SerializeField] private Ease titleEntranceEase = Ease.OutCubic;

    [Space(6)]
    [Tooltip("Duration of the Play button entrance animation in seconds.")]
    [SerializeField] private float playButtonEntranceDuration = 0.32f;

    [Tooltip("Starting scale fraction for Play button entrance scale-in.")]
    [SerializeField] private float playButtonEntranceStartScale = 0.94f;

    [Tooltip("Easing curve for Play button entrance.")]
    [SerializeField] private Ease playButtonEntranceEase = Ease.OutBack;

    [Space(6)]
    [Tooltip("Duration of each secondary button entrance in seconds.")]
    [SerializeField] private float secondaryButtonEntranceDuration = 0.28f;

    [Tooltip("Starting scale fraction for secondary button entrance scale-in.")]
    [SerializeField] private float secondaryButtonEntranceStartScale = 0.95f;

    [Tooltip("Stagger delay between consecutive secondary button appearances.")]
    [SerializeField] private float secondaryButtonStaggerDelay = 0.04f;



    // ===================================================================
    #endregion

    // ===================================================================
    #region Inspector Fields - Canvas Groups
    // ===================================================================

    [Header("=== Canvas Groups ===")]
    [SerializeField] private CanvasGroup mainMenuPanel;
    [SerializeField] private CanvasGroup topBarGroup;
    [SerializeField] private CanvasGroup progressGroup;
    [SerializeField] private CanvasGroup bottomButtonsGroup;

    // ===================================================================
    #endregion

    // ===================================================================
    #region Public Properties
    // ===================================================================

    public bool EnableEntranceAnimations { get => enableEntranceAnimations; set => enableEntranceAnimations = value; }
    public bool EnableIdleAnimations { get => enableIdleAnimations; set => enableIdleAnimations = value; }

    public RectTransform GameLogo => gameLogo;
    public CanvasGroup TapToPlayGroup => tapToPlayGroup;
    public RectTransform TapToPlayVisual => tapToPlayVisual;
    public Button PlayButton => playButton;

    public bool IsEntrancePlaying => entranceSequence != null && entranceSequence.IsActive() && entranceSequence.IsPlaying();
    public bool IsIdlePlaying => (logoBobTween != null && logoBobTween.IsActive() && logoBobTween.IsPlaying())
                              || (playPulseTween != null && playPulseTween.IsActive() && playPulseTween.IsPlaying());

    // ===================================================================
    #endregion

    // ===================================================================
    #region Cached Transform Data
    // ===================================================================

    private struct CachedTransformData
    {
        public RectTransform Rect;
        public Vector2 OriginalAnchoredPosition;
        public Vector3 OriginalLocalScale;
        public Quaternion OriginalLocalRotation;
    }

    private bool isTransformsCached = false;

    private CachedTransformData logoData;
    private CachedTransformData tapVisualData;
    private CachedTransformData shineData;
    private readonly List<CachedTransformData> bottomButtonsData = new List<CachedTransformData>();

    // ===================================================================
    #endregion

    // ===================================================================
    #region Active Tween Handles
    // ===================================================================

    private Sequence entranceSequence;
    private Tween logoBobTween;
    private Tween playPulseTween;
    private readonly List<Tween> secondaryButtonTweens = new List<Tween>();
    private Tween shineTween;

    // ===================================================================
    #endregion

    // ===================================================================
    #region Unity Lifecycle
    // ===================================================================

    private void Awake()
    {
        AutoResolveReferences();
        CacheOriginalTransforms();
    }

    private void OnDisable()
    {
        StopAllAnimations(restoreTransforms: true);
    }

    private void OnDestroy()
    {
        StopAllAnimations(restoreTransforms: false);
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Reference Resolution & Transform Caching
    // ===================================================================

    public void AutoResolveReferences()
    {
        var mainMenu = GetComponent<MainMenuController>();
        var uiManager = GetComponent<UIManager>();

        if (mainMenuPanel == null && uiManager != null) mainMenuPanel = uiManager.MainMenuPanel;
        if (gameLogo == null && mainMenu != null) gameLogo = mainMenu.GameLogo;
        if (tapToPlayGroup == null && mainMenu != null) tapToPlayGroup = mainMenu.TapToPlayGroup;
        if (tapToPlayGroup == null && uiManager != null) tapToPlayGroup = uiManager.TapToPlayGroup;

        if (tapToPlayVisual == null && mainMenu != null && mainMenu.TapToPlayText != null)
            tapToPlayVisual = mainMenu.TapToPlayText.rectTransform;

        if (playButton == null && uiManager != null) playButton = uiManager.PlayButton;

        if (settingsButton == null && mainMenu != null) settingsButton = mainMenu.SettingsButton;
        if (gunsButton == null && mainMenu != null) gunsButton = mainMenu.GunsButton;
        if (outfitsButton == null && mainMenu != null) outfitsButton = mainMenu.OutfitsButton;
        if (missionsButton == null && mainMenu != null) missionsButton = mainMenu.MissionsButton;
        if (onlineButton == null && mainMenu != null) onlineButton = mainMenu.OnlineButton;

        if (topBarGroup == null && uiManager != null) topBarGroup = uiManager.TopBarGroup;
        if (progressGroup == null && uiManager != null) progressGroup = uiManager.ProgressGroup;
        if (bottomButtonsGroup == null && uiManager != null) bottomButtonsGroup = uiManager.BottomButtonsGroup;

        if (shineTransform == null)
        {
            var shineGo = GameObject.Find("Shine");
            if (shineGo != null)
            {
                shineTransform = shineGo.GetComponent<RectTransform>();
                if (shineImage == null) shineImage = shineGo.GetComponent<Image>();
            }
        }
    }

    public void CacheOriginalTransforms()
    {
        if (isTransformsCached) return;

        if (gameLogo != null)
        {
            logoData = new CachedTransformData
            {
                Rect = gameLogo,
                OriginalAnchoredPosition = gameLogo.anchoredPosition,
                OriginalLocalScale = gameLogo.localScale,
                OriginalLocalRotation = gameLogo.localRotation
            };
        }

        if (tapToPlayVisual != null)
        {
            tapVisualData = new CachedTransformData
            {
                Rect = tapToPlayVisual,
                OriginalAnchoredPosition = tapToPlayVisual.anchoredPosition,
                OriginalLocalScale = tapToPlayVisual.localScale,
                OriginalLocalRotation = tapToPlayVisual.localRotation
            };
        }

        if (shineTransform != null)
        {
            shineData = new CachedTransformData
            {
                Rect = shineTransform,
                OriginalAnchoredPosition = shineTransform.anchoredPosition,
                OriginalLocalScale = shineTransform.localScale,
                OriginalLocalRotation = shineTransform.localRotation
            };
        }

        // Cache bottom action buttons (Guns, Outfits, Online)
        bottomButtonsData.Clear();
        Button[] actionButtons = { gunsButton, outfitsButton, onlineButton };
        foreach (var btn in actionButtons)
        {
            if (btn != null && btn.transform is RectTransform rt)
            {
                bottomButtonsData.Add(new CachedTransformData
                {
                    Rect = rt,
                    OriginalAnchoredPosition = rt.anchoredPosition,
                    OriginalLocalScale = rt.localScale,
                    OriginalLocalRotation = rt.localRotation
                });
            }
        }

        isTransformsCached = true;
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Coordinated Entrance Integration
    // ===================================================================

    /// <summary>
    /// Adds subtle upward rise to the logo coordinated into UIManager's entrance sequence.
    /// </summary>
    public void AddLogoEntrance(Sequence seq, float atTime, float duration)
    {
        if (!enableEntranceAnimations || gameLogo == null) return;
        CacheOriginalTransforms();

        gameLogo.DOKill();
        gameLogo.anchoredPosition = logoData.OriginalAnchoredPosition - new Vector2(0f, titleEntranceMoveDistance);
        seq.Insert(atTime, gameLogo.DOAnchorPosY(logoData.OriginalAnchoredPosition.y, Mathf.Max(0.2f, duration)).SetEase(titleEntranceEase));
    }

    /// <summary>
    /// Adds gentle scale-in to the play button visual coordinated into UIManager's entrance sequence.
    /// </summary>
    public void AddPlayButtonEntrance(Sequence seq, float atTime, float duration)
    {
        if (!enableEntranceAnimations || tapToPlayVisual == null) return;
        CacheOriginalTransforms();

        tapToPlayVisual.DOKill();
        tapToPlayVisual.localScale = tapVisualData.OriginalLocalScale * playButtonEntranceStartScale;
        seq.Insert(atTime, tapToPlayVisual.DOScale(tapVisualData.OriginalLocalScale, Mathf.Max(0.2f, duration)).SetEase(playButtonEntranceEase));
    }

    /// <summary>
    /// Adds subtle scale-in to bottom action buttons coordinated into UIManager's entrance sequence.
    /// </summary>
    public void AddSecondaryButtonsEntrance(Sequence seq, float atTime, float duration)
    {
        if (!enableEntranceAnimations || bottomButtonsData.Count == 0) return;
        CacheOriginalTransforms();

        for (int i = 0; i < bottomButtonsData.Count; i++)
        {
            var data = bottomButtonsData[i];
            if (data.Rect == null) continue;

            data.Rect.DOKill();
            data.Rect.localScale = data.OriginalLocalScale * secondaryButtonEntranceStartScale;
            float btnTime = atTime + (i * secondaryButtonStaggerDelay);
            seq.Insert(btnTime, data.Rect.DOScale(data.OriginalLocalScale, Mathf.Max(0.2f, duration)).SetEase(secondaryButtonEase));
        }
    }

    /// <summary>
    /// Fallback standalone entrance player if invoked independently.
    /// </summary>
    public void PlayEntranceAnimation(Action onComplete = null)
    {
        CacheOriginalTransforms();
        StopAllAnimations(restoreTransforms: true);

        if (!enableEntranceAnimations)
        {
            ResetToOriginalInstant();
            if (enableIdleAnimations) StartIdleAnimations();
            onComplete?.Invoke();
            return;
        }

        var uiManager = GetComponent<UIManager>();
        if (uiManager != null)
        {
            // Delegate directly to UIManager to preserve full panel and canvas lifecycle
            uiManager.ShowMainMenu(instant: false);
            onComplete?.Invoke();
        }
        else
        {
            if (enableIdleAnimations) StartIdleAnimations();
            onComplete?.Invoke();
        }
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Continuous Idle Animations
    // ===================================================================

    /// <summary>
    /// Starts subtle, non-distracting continuous idle animations:
    /// - Game Logo: gentle vertical floating (4-5 UI pixels, InOutSine)
    /// - Play Button: subtle scale breathing on visual child only (1.0 to 1.025, InOutSine)
    /// - Bottom action buttons (Guns, Outfits): subtle breathing (1.012, staggered)
    /// - Background Shine: slow ambient breathing
    /// Guards against running when the main menu panel is hidden or inactive.
    /// </summary>
    public void StartIdleAnimations()
    {
        CacheOriginalTransforms();
        StopIdleTweens(restoreTransforms: false);

        if (!enableIdleAnimations) return;

        // Guard: Do not run idle animations if main menu panel is inactive or hidden
        if (mainMenuPanel != null && (!mainMenuPanel.gameObject.activeInHierarchy || mainMenuPanel.alpha < 0.1f))
        {
            return;
        }

        // 1. Game Logo Gentle Vertical Floating
        if (enableLogoBob && gameLogo != null)
        {
            gameLogo.DOKill();
            gameLogo.anchoredPosition = logoData.OriginalAnchoredPosition;

            float halfCycle = Mathf.Max(0.1f, logoBobCycleDuration * 0.5f);
            float targetY = logoData.OriginalAnchoredPosition.y + logoBobDistance;

            logoBobTween = gameLogo
                .DOAnchorPosY(targetY, halfCycle)
                .SetEase(logoBobEase)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        // 2. Play Button Scale Breathing (Animates visual child to keep click hit area stable)
        if (enablePlayPulse && tapToPlayVisual != null)
        {
            tapToPlayVisual.DOKill();
            tapToPlayVisual.localScale = tapVisualData.OriginalLocalScale;

            float halfCycle = Mathf.Max(0.1f, playPulseCycleDuration * 0.5f);
            Vector3 targetScale = tapVisualData.OriginalLocalScale * playPulseScale;

            playPulseTween = tapToPlayVisual
                .DOScale(targetScale, halfCycle)
                .SetEase(playPulseEase)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        // 3. Bottom Action Buttons Gentle Breathing (Guns, Outfits)
        if (enableSecondaryButtonsIdle && bottomButtonsData.Count > 0)
        {
            float[] cycleOffsets = { 0.96f, 1.06f, 1.0f };

            for (int i = 0; i < bottomButtonsData.Count; i++)
            {
                var data = bottomButtonsData[i];
                if (data.Rect == null) continue;

                data.Rect.DOKill();
                data.Rect.localScale = data.OriginalLocalScale;

                float duration = Mathf.Max(0.2f, (secondaryButtonBaseCycleDuration * cycleOffsets[i % cycleOffsets.Length]) * 0.5f);
                float initialDelay = i * 0.28f; // Phase delay to prevent mechanical sync

                Vector3 targetScale = data.OriginalLocalScale * secondaryButtonScaleMultiplier;

                Tween btnTween = data.Rect
                    .DOScale(targetScale, duration)
                    .SetEase(secondaryButtonEase)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetDelay(initialDelay)
                    .SetUpdate(true);

                secondaryButtonTweens.Add(btnTween);
            }
        }

        // 4. Decorative Background Shine Breathing
        if (enableBackgroundShineIdle && shineTransform != null && shineTransform.gameObject.activeInHierarchy)
        {
            shineTransform.DOKill();
            shineTransform.localScale = shineData.OriginalLocalScale;

            float halfCycle = Mathf.Max(0.2f, shineCycleDuration * 0.5f);
            Vector3 targetScale = shineData.OriginalLocalScale * shineScaleMultiplier;

            shineTween = shineTransform
                .DOScale(targetScale, halfCycle)
                .SetEase(shineEase)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Cleanup & Transform Restoration
    // ===================================================================

    /// <summary>
    /// Safely kills running idle tweens without causing visual snaps during menu exit.
    /// </summary>
    public void StopIdleTweens(bool restoreTransforms = false)
    {
        if (logoBobTween != null && logoBobTween.IsActive())
        {
            logoBobTween.Kill();
            logoBobTween = null;
        }

        if (playPulseTween != null && playPulseTween.IsActive())
        {
            playPulseTween.Kill();
            playPulseTween = null;
        }

        foreach (var tween in secondaryButtonTweens)
        {
            if (tween != null && tween.IsActive())
            {
                tween.Kill();
            }
        }
        secondaryButtonTweens.Clear();

        if (shineTween != null && shineTween.IsActive())
        {
            shineTween.Kill();
            shineTween = null;
        }

        if (restoreTransforms && isTransformsCached)
        {
            RestoreExactOriginalTransforms();
        }
    }

    /// <summary>
    /// Stops all running animations safely.
    /// </summary>
    public void StopAllAnimations(bool restoreTransforms = false)
    {
        if (entranceSequence != null && entranceSequence.IsActive())
        {
            entranceSequence.Kill();
            entranceSequence = null;
        }

        StopIdleTweens(restoreTransforms);
    }

    /// <summary>
    /// Restores all cached positions, scales, and rotations immediately.
    /// </summary>
    public void ResetToOriginalInstant()
    {
        StopAllAnimations(restoreTransforms: true);
    }

    /// <summary>
    /// Sets all cached transforms back to their exact original layout values.
    /// Only restores position for elements that actually animate position (gameLogo).
    /// Preserves layout-governed positions (bottomButtons under HorizontalLayoutGroup).
    /// </summary>
    private void RestoreExactOriginalTransforms()
    {
        if (!isTransformsCached) return;

        // 1. Logo floats vertically, so restore its original anchoredPosition
        if (logoData.Rect != null)
        {
            logoData.Rect.DOKill();
            logoData.Rect.anchoredPosition = logoData.OriginalAnchoredPosition;
            logoData.Rect.localScale = logoData.OriginalLocalScale;
            logoData.Rect.localRotation = logoData.OriginalLocalRotation;
        }

        // 2. Tap to play visual only breathes scale, preserve its position untouched
        if (tapVisualData.Rect != null)
        {
            tapVisualData.Rect.DOKill();
            tapVisualData.Rect.localScale = tapVisualData.OriginalLocalScale;
            tapVisualData.Rect.localRotation = tapVisualData.OriginalLocalRotation;
        }

        // 3. Background shine only breathes scale, preserve its position untouched
        if (shineData.Rect != null)
        {
            shineData.Rect.DOKill();
            shineData.Rect.localScale = shineData.OriginalLocalScale;
            shineData.Rect.localRotation = shineData.OriginalLocalRotation;
        }

        // 4. Bottom action buttons are managed by HorizontalLayoutGroup.
        // NEVER overwrite anchoredPosition to avoid overriding Unity UI layout calculations!
        foreach (var data in bottomButtonsData)
        {
            if (data.Rect != null)
            {
                data.Rect.DOKill();
                data.Rect.localScale = data.OriginalLocalScale;
                data.Rect.localRotation = data.OriginalLocalRotation;
            }
        }

        // Force immediate rebuild of the bottom buttons layout group if present
        if (bottomButtonsGroup != null)
        {
            var rt = bottomButtonsGroup.GetComponent<RectTransform>();
            if (rt != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }
        }
    }

    // ===================================================================
    #endregion
}
