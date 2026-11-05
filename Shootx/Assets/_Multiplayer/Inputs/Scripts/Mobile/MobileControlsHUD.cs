using UnityEngine;

/// <summary>
/// Master HUD controller for mobile touch controls.
/// Manages platform visibility, Editor override, Dominant Hand layout mirroring (Right/Left),
/// and lifecycle resets on death/respawn.
/// </summary>
public class MobileControlsHUD : MonoBehaviour
{
    public enum ControlPlatformMode
    {
        Auto,
        ForceMobileInEditor,
        AlwaysMobile,
        AlwaysDesktop
    }

    [Header("Platform & Testing Settings")]
    [Tooltip("Control mode policy. Auto enables mobile controls on Android/iOS and hides on desktop.")]
    [SerializeField] private ControlPlatformMode platformMode = ControlPlatformMode.Auto;

#pragma warning disable 0414
    [Tooltip("If enabled, forces mobile controls to appear in the Unity Editor for quick testing.")]
    [SerializeField] private bool forceMobileInEditor = true;
#pragma warning restore 0414

    [Header("Input Dependency")]
    [SerializeField] private InputReader inputReader;

    [Header("Controls References")]
    [SerializeField] private FixedJoystick movementJoystick;
    [SerializeField] private AimFireJoystick aimFireJoystick;
    [SerializeField] private MobileMeleeButton meleeButton;
    [SerializeField] private MobileDashButton dashButton;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Layout & RectTransform References")]
    [SerializeField] private RectTransform movementJoystickRect;
    [SerializeField] private RectTransform combatControlsRect;
    [SerializeField] private RectTransform aimFireJoystickRect;
    [SerializeField] private RectTransform meleeButtonRect;
    [SerializeField] private RectTransform dashButtonRect;

    // ─── Public Properties ──────────────────────────────────────
    public DominantHand CurrentDominantHand { get; private set; } = DominantHand.RightHanded;

    private PlayerHealth localPlayerHealth;

    private void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        EnsureLayoutReferences();
        ApplyDominantHandLayout(DominantHandSettings.CurrentHand);
        EvaluatePlatformVisibility();
    }

    private void Start()
    {
        EvaluatePlatformVisibility();
        TryFindLocalPlayer();
    }

    private void OnEnable()
    {
        EnsureLayoutReferences();
        ApplyDominantHandLayout(DominantHandSettings.CurrentHand);
        DominantHandSettings.OnDominantHandChanged += HandleDominantHandChanged;
        EvaluatePlatformVisibility();
    }

    private void OnDisable()
    {
        DominantHandSettings.OnDominantHandChanged -= HandleDominantHandChanged;
        ResetAllControls();
        UnsubscribeFromPlayer();
    }

    private void OnDestroy()
    {
        DominantHandSettings.OnDominantHandChanged -= HandleDominantHandChanged;
        ResetAllControls();
        UnsubscribeFromPlayer();
    }

    private void HandleDominantHandChanged(DominantHand hand)
    {
        ApplyDominantHandLayout(hand);
    }

    /// <summary>
    /// Automatically resolves any missing RectTransform layout references from existing component fields.
    /// </summary>
    public void EnsureLayoutReferences()
    {
        if (movementJoystickRect == null && movementJoystick != null)
            movementJoystickRect = movementJoystick.GetComponent<RectTransform>();

        if (aimFireJoystickRect == null && aimFireJoystick != null)
            aimFireJoystickRect = aimFireJoystick.GetComponent<RectTransform>();

        if (meleeButtonRect == null && meleeButton != null)
            meleeButtonRect = meleeButton.GetComponent<RectTransform>();

        if (dashButtonRect == null && dashButton != null)
            dashButtonRect = dashButton.GetComponent<RectTransform>();

        if (combatControlsRect == null && aimFireJoystickRect != null)
            combatControlsRect = aimFireJoystickRect.parent as RectTransform;

        if (combatControlsRect == null)
        {
            Transform t = transform.Find("SafeAreaRoot/CombatControls") ?? transform.Find("CombatControls");
            if (t != null) combatControlsRect = t as RectTransform;
        }

        if (movementJoystickRect == null)
        {
            Transform t = transform.Find("SafeAreaRoot/MovementJoystick") ?? transform.Find("MovementJoystick");
            if (t != null) movementJoystickRect = t as RectTransform;
        }
    }

    /// <summary>
    /// Applies the selected dominant hand layout configuration.
    /// Right-handed (default): Movement Joystick on bottom-left, Combat Controls (Aim/Fire, Melee, Dash) on bottom-right.
    /// Left-handed (mirrored): Movement Joystick on bottom-right, Combat Controls (Aim/Fire, Melee, Dash) on bottom-left.
    /// </summary>
    public void ApplyDominantHandLayout(DominantHand hand)
    {
        CurrentDominantHand = hand;
        EnsureLayoutReferences();

        if (hand == DominantHand.LeftHanded)
        {
            // ─── LEFT-HANDED LAYOUT ──────────────────────────────────────────
            // Movement joystick moves to the right side
            if (movementJoystickRect != null)
            {
                movementJoystickRect.anchorMin = new Vector2(1f, 0f);
                movementJoystickRect.anchorMax = new Vector2(1f, 0f);
                movementJoystickRect.pivot = new Vector2(0.5f, 0.5f);
                movementJoystickRect.anchoredPosition = new Vector2(-180f, 180f);
                movementJoystickRect.sizeDelta = new Vector2(240f, 240f);
            }

            // Combat controls container moves to the left side
            if (combatControlsRect != null)
            {
                combatControlsRect.anchorMin = new Vector2(0f, 0f);
                combatControlsRect.anchorMax = new Vector2(0f, 0f);
                combatControlsRect.pivot = new Vector2(0f, 0f);
                combatControlsRect.anchoredPosition = Vector2.zero;
                combatControlsRect.sizeDelta = new Vector2(400f, 600f);
            }

            // Aim/Fire joystick on bottom-left within combat container
            if (aimFireJoystickRect != null)
            {
                aimFireJoystickRect.anchorMin = new Vector2(0f, 0f);
                aimFireJoystickRect.anchorMax = new Vector2(0f, 0f);
                aimFireJoystickRect.pivot = new Vector2(0.5f, 0.5f);
                aimFireJoystickRect.anchoredPosition = new Vector2(180f, 180f);
                aimFireJoystickRect.sizeDelta = new Vector2(240f, 240f);
            }

            // Melee button on left side directly above Aim/Fire joystick
            if (meleeButtonRect != null)
            {
                meleeButtonRect.anchorMin = new Vector2(0f, 0f);
                meleeButtonRect.anchorMax = new Vector2(0f, 0f);
                meleeButtonRect.pivot = new Vector2(0.5f, 0.5f);
                meleeButtonRect.anchoredPosition = new Vector2(180f, 375f);
                meleeButtonRect.sizeDelta = new Vector2(125f, 125f);
            }

            // Dash button beside Melee button towards screen center
            if (dashButtonRect != null)
            {
                dashButtonRect.anchorMin = new Vector2(0f, 0f);
                dashButtonRect.anchorMax = new Vector2(0f, 0f);
                dashButtonRect.pivot = new Vector2(0.5f, 0.5f);
                dashButtonRect.anchoredPosition = new Vector2(315f, 375f);
                dashButtonRect.sizeDelta = new Vector2(125f, 125f);
            }
        }
        else
        {
            // ─── RIGHT-HANDED LAYOUT (DEFAULT) ──────────────────────────────
            // Movement joystick on bottom-left
            if (movementJoystickRect != null)
            {
                movementJoystickRect.anchorMin = new Vector2(0f, 0f);
                movementJoystickRect.anchorMax = new Vector2(0f, 0f);
                movementJoystickRect.pivot = new Vector2(0.5f, 0.5f);
                movementJoystickRect.anchoredPosition = new Vector2(180f, 180f);
                movementJoystickRect.sizeDelta = new Vector2(240f, 240f);
            }

            // Combat controls container on bottom-right
            if (combatControlsRect != null)
            {
                combatControlsRect.anchorMin = new Vector2(1f, 0f);
                combatControlsRect.anchorMax = new Vector2(1f, 0f);
                combatControlsRect.pivot = new Vector2(1f, 0f);
                combatControlsRect.anchoredPosition = Vector2.zero;
                combatControlsRect.sizeDelta = new Vector2(400f, 600f);
            }

            // Aim/Fire joystick on bottom-right within combat container
            if (aimFireJoystickRect != null)
            {
                aimFireJoystickRect.anchorMin = new Vector2(1f, 0f);
                aimFireJoystickRect.anchorMax = new Vector2(1f, 0f);
                aimFireJoystickRect.pivot = new Vector2(0.5f, 0.5f);
                aimFireJoystickRect.anchoredPosition = new Vector2(-180f, 180f);
                aimFireJoystickRect.sizeDelta = new Vector2(240f, 240f);
            }

            // Melee button on right side directly above Aim/Fire joystick
            if (meleeButtonRect != null)
            {
                meleeButtonRect.anchorMin = new Vector2(1f, 0f);
                meleeButtonRect.anchorMax = new Vector2(1f, 0f);
                meleeButtonRect.pivot = new Vector2(0.5f, 0.5f);
                meleeButtonRect.anchoredPosition = new Vector2(-180f, 375f);
                meleeButtonRect.sizeDelta = new Vector2(125f, 125f);
            }

            // Dash button beside Melee button towards screen center
            if (dashButtonRect != null)
            {
                dashButtonRect.anchorMin = new Vector2(1f, 0f);
                dashButtonRect.anchorMax = new Vector2(1f, 0f);
                dashButtonRect.pivot = new Vector2(0.5f, 0.5f);
                dashButtonRect.anchoredPosition = new Vector2(-315f, 375f);
                dashButtonRect.sizeDelta = new Vector2(125f, 125f);
            }
        }
    }

    /// <summary>
    /// Evaluates whether the mobile UI should be visible based on platform and editor settings.
    /// </summary>
    public void EvaluatePlatformVisibility()
    {
        bool showControls = false;

        switch (platformMode)
        {
            case ControlPlatformMode.AlwaysMobile:
                showControls = true;
                break;

            case ControlPlatformMode.AlwaysDesktop:
                showControls = false;
                break;

            case ControlPlatformMode.ForceMobileInEditor:
                showControls = Application.isEditor || Application.isMobilePlatform;
                break;

            case ControlPlatformMode.Auto:
            default:
#if UNITY_ANDROID || UNITY_IOS
                showControls = true;
#elif UNITY_EDITOR
                showControls = forceMobileInEditor;
#else
                showControls = false;
#endif
                break;
        }

        SetHUDActive(showControls);
    }

    public void SetHUDActive(bool active)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = active ? 1f : 0f;
            canvasGroup.interactable = active;
            canvasGroup.blocksRaycasts = active;
        }
        else
        {
            gameObject.SetActive(active);
        }

        if (!active)
        {
            ResetAllControls();
        }
    }

    private void TryFindLocalPlayer()
    {
        if (localPlayerHealth != null) return;

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.IsOwner)
            {
                BindLocalPlayer(p);
                break;
            }
        }
    }

    public void BindLocalPlayer(PlayerHealth playerHealth)
    {
        UnsubscribeFromPlayer();

        if (playerHealth == null) return;

        localPlayerHealth = playerHealth;
        localPlayerHealth.OnDeath += HandlePlayerDeath;
        localPlayerHealth.OnRespawn += HandlePlayerRespawn;

        if (dashButton != null)
        {
            dashButton.BindPlayer(playerHealth.GetComponent<PlayerDash>());
        }
    }

    private void UnsubscribeFromPlayer()
    {
        if (localPlayerHealth != null)
        {
            localPlayerHealth.OnDeath -= HandlePlayerDeath;
            localPlayerHealth.OnRespawn -= HandlePlayerRespawn;
            localPlayerHealth = null;
        }
    }

    private void HandlePlayerDeath(ulong killerId)
    {
        ResetAllControls();
        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
        }
    }

    private void HandlePlayerRespawn()
    {
        ResetAllControls();
        if (canvasGroup != null)
        {
            canvasGroup.interactable = true;
        }
    }

    /// <summary>
    /// Resets all virtual joysticks, button states, and input reader values cleanly.
    /// </summary>
    public void ResetAllControls()
    {
        if (movementJoystick != null) movementJoystick.ResetJoystick();
        if (aimFireJoystick != null) aimFireJoystick.ResetJoystick();
        if (meleeButton != null) meleeButton.ResetButton();
        if (dashButton != null) dashButton.ResetButton();
        if (inputReader != null) inputReader.ResetMobileInput();
    }
}
