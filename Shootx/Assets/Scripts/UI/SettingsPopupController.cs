using UnityEngine;
using UnityEngine.UI;

public class SettingsPopupController : MonoBehaviour
{
    [Header("=== Switches ===")]
    [SerializeField] private Toggle hapticToggle;
    [SerializeField] private Toggle soundsToggle;
    [SerializeField] private Toggle musicToggle;

    [Header("=== Buttons ===")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button noAdsButton;
    [SerializeField] private Button supportButton;
    [SerializeField] private Button restorePurchasesButton;

    // PlayerPrefs keys
    private const string HAPTIC_KEY = "settings_haptic";
    private const string SOUNDS_KEY = "settings_sounds";
    private const string MUSIC_KEY = "settings_music";

    private void Awake()
    {
        closeButton?.onClick.AddListener(UIManager.Instance.HideSettingsPopup);
        noAdsButton?.onClick.AddListener(OnNoAdsPressed);
        supportButton?.onClick.AddListener(OnSupportPressed);
        restorePurchasesButton?.onClick.AddListener(OnRestorePressed);

        hapticToggle?.onValueChanged.AddListener(OnHapticChanged);
        soundsToggle?.onValueChanged.AddListener(OnSoundsChanged);
        musicToggle?.onValueChanged.AddListener(OnMusicChanged);
    }

    private void OnEnable()
    {
        LoadSettings();
    }

    // ===================================================================
    #region Load / Save
    // ===================================================================

    private void LoadSettings()
    {
        if (hapticToggle != null) hapticToggle.isOn = PlayerPrefs.GetInt(HAPTIC_KEY, 1) == 1;
        if (soundsToggle != null) soundsToggle.isOn = PlayerPrefs.GetInt(SOUNDS_KEY, 1) == 1;
        if (musicToggle != null) musicToggle.isOn = PlayerPrefs.GetInt(MUSIC_KEY, 1) == 1;
    }

    private void OnHapticChanged(bool value)
    {
        PlayerPrefs.SetInt(HAPTIC_KEY, value ? 1 : 0);
        AudioManager.Instance?.SetHaptic(value);
    }

    private void OnSoundsChanged(bool value)
    {
        PlayerPrefs.SetInt(SOUNDS_KEY, value ? 1 : 0);
        AudioManager.Instance?.SetSounds(value);
    }

    private void OnMusicChanged(bool value)
    {
        PlayerPrefs.SetInt(MUSIC_KEY, value ? 1 : 0);
        AudioManager.Instance?.SetMusic(value);
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Button Handlers
    // ===================================================================

    private void OnNoAdsPressed()
    {
        Debug.Log("[Settings] No Ads pressed");
        IAPManager.Instance?.BuyNoAds();
    }

    private void OnSupportPressed()
    {
        string email = "support@shootx.com";
        string subject = UnityEngine.Networking.UnityWebRequest.EscapeURL("Game Support");
        Application.OpenURL($"mailto:{email}?subject={subject}");
    }

    private void OnRestorePressed()
    {
        Debug.Log("[Settings] Restore Purchases pressed");
        IAPManager.Instance?.RestorePurchases();
    }

    // ===================================================================
    #endregion
}