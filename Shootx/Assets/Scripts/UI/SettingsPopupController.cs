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

    [Header("=== Dominant Hand ===")]
    [SerializeField] private Button rightHandButton;
    [SerializeField] private Button leftHandButton;
    [SerializeField] private Image rightHandBackground;
    [SerializeField] private Image leftHandBackground;
    [SerializeField] private TextMeshProUGUI rightHandText;
    [SerializeField] private TextMeshProUGUI leftHandText;
    [SerializeField] private Toggle dominantHandToggle;

    [Header("=== Dominant Hand Visual Colors ===")]
    [SerializeField] private Color dominantHandActiveBgColor = new Color(0.12f, 0.75f, 0.95f, 0.95f);
    [SerializeField] private Color dominantHandInactiveBgColor = new Color(0.08f, 0.12f, 0.18f, 0.75f);
    [SerializeField] private Color dominantHandActiveTextColor = Color.white;
    [SerializeField] private Color dominantHandInactiveTextColor = new Color(0.65f, 0.75f, 0.85f, 0.65f);

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
    public const string DOMINANT_HAND_KEY = DominantHandSettings.PrefKey;

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

        rightHandButton?.onClick.AddListener(() => SetDominantHand(DominantHand.RightHanded));
        leftHandButton?.onClick.AddListener(() => SetDominantHand(DominantHand.LeftHanded));

        if (dominantHandToggle != null)
        {
            dominantHandToggle.onValueChanged.AddListener(OnDominantHandToggleChanged);
        }

        EnsureDominantHandBindings();

        if (noAdsText == null && noAdsButton != null)
            noAdsText = noAdsButton.GetComponentInChildren<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        EnsureDominantHandBindings();
        LoadSettings();
        RefreshDominantHandUI();
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

        RefreshDominantHandUI();
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
    #region Dominant Hand Management
    // ===================================================================

    public void SetDominantHand(DominantHand hand)
    {
        DominantHandSettings.CurrentHand = hand;
        RefreshDominantHandUI();

        if (hand == DominantHand.RightHanded)
            rightHandButton?.transform.DOPunchScale(Vector3.one * 0.1f, 0.2f);
        else
            leftHandButton?.transform.DOPunchScale(Vector3.one * 0.1f, 0.2f);
    }

    private void OnDominantHandToggleChanged(bool isLeft)
    {
        DominantHand hand = isLeft ? DominantHand.LeftHanded : DominantHand.RightHanded;
        if (DominantHandSettings.CurrentHand != hand)
        {
            SetDominantHand(hand);
        }
    }

    public void RefreshDominantHandUI()
    {
        DominantHand current = DominantHandSettings.CurrentHand;

        if (dominantHandToggle != null)
        {
            dominantHandToggle.SetIsOnWithoutNotify(current == DominantHand.LeftHanded);
        }

        bool isRight = (current == DominantHand.RightHanded);

        if (rightHandBackground != null)
            rightHandBackground.color = isRight ? dominantHandActiveBgColor : dominantHandInactiveBgColor;

        if (leftHandBackground != null)
            leftHandBackground.color = !isRight ? dominantHandActiveBgColor : dominantHandInactiveBgColor;

        if (rightHandText != null)
            rightHandText.color = isRight ? dominantHandActiveTextColor : dominantHandInactiveTextColor;

        if (leftHandText != null)
            leftHandText.color = !isRight ? dominantHandActiveTextColor : dominantHandInactiveTextColor;
    }

    private void EnsureDominantHandBindings()
    {
        if (rightHandButton != null && leftHandButton != null) return;

        Transform row = transform.Find("DominantHandRow");
        if (row == null)
        {
            row = FindDeepChild(transform, "DominantHandRow");
        }

        if (row != null)
        {
            if (rightHandButton == null)
            {
                var rBtn = row.Find("SegmentedContainer/RightHandButton") ?? FindDeepChild(row, "RightHandButton");
                if (rBtn != null)
                {
                    rightHandButton = rBtn.GetComponent<Button>();
                    rightHandButton?.onClick.RemoveAllListeners();
                    rightHandButton?.onClick.AddListener(() => SetDominantHand(DominantHand.RightHanded));
                }
            }

            if (leftHandButton == null)
            {
                var lBtn = row.Find("SegmentedContainer/LeftHandButton") ?? FindDeepChild(row, "LeftHandButton");
                if (lBtn != null)
                {
                    leftHandButton = lBtn.GetComponent<Button>();
                    leftHandButton?.onClick.RemoveAllListeners();
                    leftHandButton?.onClick.AddListener(() => SetDominantHand(DominantHand.LeftHanded));
                }
            }

            if (rightHandBackground == null && rightHandButton != null)
                rightHandBackground = rightHandButton.GetComponent<Image>();

            if (leftHandBackground == null && leftHandButton != null)
                leftHandBackground = leftHandButton.GetComponent<Image>();

            if (rightHandText == null && rightHandButton != null)
                rightHandText = rightHandButton.GetComponentInChildren<TextMeshProUGUI>();

            if (leftHandText == null && leftHandButton != null)
                leftHandText = leftHandButton.GetComponentInChildren<TextMeshProUGUI>();

            if (dominantHandToggle == null)
            {
                dominantHandToggle = row.GetComponentInChildren<Toggle>();
                if (dominantHandToggle != null)
                {
                    dominantHandToggle.onValueChanged.RemoveAllListeners();
                    dominantHandToggle.onValueChanged.AddListener(OnDominantHandToggleChanged);
                }
            }
        }
    }

    private Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Equals(name, System.StringComparison.OrdinalIgnoreCase)) return child;
            Transform found = FindDeepChild(child, name);
            if (found != null) return found;
        }
        return null;
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
