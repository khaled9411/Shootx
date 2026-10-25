using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class SettingsPopupController : MonoBehaviour
{
    [Header("=== Switches ===")]
    [SerializeField] private Toggle hapticToggle;
    [SerializeField] private Toggle soundsToggle;
    [SerializeField] private Toggle musicToggle;

    [Header("=== Buttons ===")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button noAdsButton;
    [SerializeField] private TextMeshProUGUI noAdsText;
    [SerializeField] private Button supportButton;
    [SerializeField] private Button restorePurchasesButton;

    // PlayerPrefs keys
    private const string HAPTIC_KEY = "settings_haptic";
    private const string SOUNDS_KEY = "settings_sounds";
    private const string MUSIC_KEY = "settings_music";

    private const string REMOVE_ADS_PRODUCT_ID = "remove_ads";

    private void Awake()
    {
        closeButton?.onClick.AddListener(UIManager.Instance.HideSettingsPopup);
        noAdsButton?.onClick.AddListener(OnNoAdsPressed);
        supportButton?.onClick.AddListener(OnSupportPressed);
        restorePurchasesButton?.onClick.AddListener(OnRestorePressed);

        hapticToggle?.onValueChanged.AddListener(OnHapticChanged);
        soundsToggle?.onValueChanged.AddListener(OnSoundsChanged);
        musicToggle?.onValueChanged.AddListener(OnMusicChanged);

        if (noAdsText == null && noAdsButton != null)
            noAdsText = noAdsButton.GetComponentInChildren<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        LoadSettings();
        RefreshNoAdsUI();

        IAPManager.OnPurchaseSuccess += HandlePurchaseSuccess;
        IAPManager.OnInitializedEvent += RefreshNoAdsUI;
    }

    private void OnDisable()
    {
        IAPManager.OnPurchaseSuccess -= HandlePurchaseSuccess;
        IAPManager.OnInitializedEvent -= RefreshNoAdsUI;
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
    #region Remove Ads & IAP
    // ===================================================================

    private void RefreshNoAdsUI()
    {
        if (noAdsButton == null) return;

        bool isOwned = GameDataManager.Instance != null && GameDataManager.Instance.AdsRemoved;

        if (isOwned)
        {
            noAdsButton.interactable = false;
            if (noAdsText != null)
                noAdsText.text = "<color=#00FF88>ADS REMOVED</color>";
        }
        else
        {
            noAdsButton.interactable = true;
            string localizedPrice = IAPManager.Instance != null ? IAPManager.Instance.GetLocalizedPrice(REMOVE_ADS_PRODUCT_ID) : "";

            if (noAdsText != null)
            {
                noAdsText.text = !string.IsNullOrEmpty(localizedPrice)
                    ? $"REMOVE ADS ({localizedPrice})"
                    : "REMOVE ADS";
            }
        }
    }

    private void OnNoAdsPressed()
    {
        if (GameDataManager.Instance != null && GameDataManager.Instance.AdsRemoved)
        {
            Debug.Log("[Settings] Ads already removed.");
            return;
        }

        Debug.Log($"[Settings] Purchasing: {REMOVE_ADS_PRODUCT_ID}");
        noAdsButton?.transform.DOPunchScale(Vector3.one * 0.1f, 0.2f);
        IAPManager.Instance?.BuyProduct(REMOVE_ADS_PRODUCT_ID);
    }

    private void HandlePurchaseSuccess(string productId)
    {
        if (productId.Equals(REMOVE_ADS_PRODUCT_ID, System.StringComparison.OrdinalIgnoreCase))
        {
            RefreshNoAdsUI();
            noAdsButton?.transform.DOPunchScale(Vector3.one * 0.15f, 0.3f);
        }
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
        restorePurchasesButton?.transform.DOPunchScale(Vector3.one * 0.1f, 0.2f);
        IAPManager.Instance?.RestorePurchases();
    }

    // ===================================================================
    #endregion
}