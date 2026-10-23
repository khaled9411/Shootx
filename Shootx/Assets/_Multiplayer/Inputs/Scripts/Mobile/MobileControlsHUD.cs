using UnityEngine;

/// <summary>
/// Master HUD controller for mobile touch controls.
/// Manages platform visibility, Editor override, and lifecycle resets on death/respawn.
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

    private PlayerHealth localPlayerHealth;

    private void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        EvaluatePlatformVisibility();
    }

    private void Start()
    {
        EvaluatePlatformVisibility();
        TryFindLocalPlayer();
    }

    private void OnEnable()
    {
        EvaluatePlatformVisibility();
    }

    private void OnDisable()
    {
        ResetAllControls();
        UnsubscribeFromPlayer();
    }

    private void OnDestroy()
    {
        ResetAllControls();
        UnsubscribeFromPlayer();
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
