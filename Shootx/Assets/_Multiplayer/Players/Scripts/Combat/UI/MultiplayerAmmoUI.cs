using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// UI component that displays ammo count and reload state.
/// Attach to a Canvas and link it to the local player's PlayerShooting.
/// Designed to work on both PC and mobile.
/// </summary>
public class MultiplayerAmmoUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Text element showing current ammo (e.g. '12 / 12').")]
    [SerializeField] private TextMeshProUGUI ammoText;

    [Tooltip("Optional reload progress bar.")]
    [SerializeField] private Slider reloadBar;

    [Tooltip("Optional reload button for mobile. Hook this to PlayerShooting.RequestReload().")]
    [SerializeField] private Button reloadButton;

    [Tooltip("Text/Image shown while reloading.")]
    [SerializeField] private GameObject reloadIndicator;

    private PlayerShooting playerShooting;

    /// <summary>
    /// Called to bind this UI to a specific player's shooting component.
    /// Typically called after the local player spawns.
    /// </summary>
    public void Initialize(PlayerShooting shooting)
    {
        // Unsubscribe from previous if any
        if (playerShooting != null)
        {
            playerShooting.OnAmmoChanged -= UpdateAmmoDisplay;
            playerShooting.OnReloadStateChanged -= UpdateReloadState;
        }

        playerShooting = shooting;

        if (playerShooting != null)
        {
            playerShooting.OnAmmoChanged += UpdateAmmoDisplay;
            playerShooting.OnReloadStateChanged += UpdateReloadState;
        }

        // Setup reload button for mobile
        if (reloadButton != null && playerShooting != null)
        {
            reloadButton.onClick.RemoveAllListeners();
            reloadButton.onClick.AddListener(() => playerShooting.RequestReload());
        }

        // Hide reload indicator initially
        if (reloadIndicator != null)
        {
            reloadIndicator.SetActive(false);
        }

        if (reloadBar != null)
        {
            reloadBar.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (playerShooting != null)
        {
            playerShooting.OnAmmoChanged -= UpdateAmmoDisplay;
            playerShooting.OnReloadStateChanged -= UpdateReloadState;
        }
    }

    private void UpdateAmmoDisplay(int current, int max)
    {
        if (ammoText != null)
        {
            ammoText.text = $"{current} / {max}";
        }
    }

    private void UpdateReloadState(bool reloading)
    {
        if (reloadIndicator != null)
        {
            reloadIndicator.SetActive(reloading);
        }

        if (reloadBar != null)
        {
            reloadBar.gameObject.SetActive(reloading);
        }

        if (reloadButton != null)
        {
            reloadButton.interactable = !reloading;
        }
    }
}
