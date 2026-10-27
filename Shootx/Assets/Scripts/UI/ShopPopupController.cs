using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Shootx.Store;

public class ShopPopupController : MonoBehaviour
{
    public static ShopPopupController Instance { get; private set; }

    // ===================================================================
    #region Data Types
    // ===================================================================

    [System.Serializable]
    public class ShopOfferUI
    {
        public GameObject root;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI amountText;
        public Image offerImage;
        public Button buyButton;
        public TextMeshProUGUI priceText;
        public string productId;
        public GameObject ownedBadge;
    }

    [System.Serializable]
    public class BundlePreviewUI
    {
        public GameObject root;
        public TextMeshProUGUI bundleTitleText;
        public TextMeshProUGUI bundleDescriptionText;
        public TextMeshProUGUI bundleBadgeText;
        public Transform itemsContainer;
        public GameObject bundleItemPrefab;
        public Button buyBundleButton;
        public TextMeshProUGUI bundlePriceText;
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Inspector Fields - Controls & Containers
    // ===================================================================

    [Header("=== Card & Canvas Group ===")]
    [SerializeField] private RectTransform cardRect;
    [SerializeField] private CanvasGroup popupCanvasGroup;

    [Header("=== Header & Navigation ===")]
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Button restorePurchasesButton;

    [Header("=== Currency Display ===")]
    [SerializeField] private TextMeshProUGUI softCurrencyText;
    [SerializeField] private TextMeshProUGUI hardCurrencyText;

    [Header("=== Tabs ===")]
    [SerializeField] private Button bundlesTabButton;
    [SerializeField] private Button coinsTabButton;
    [SerializeField] private Button accessTabButton;
    [SerializeField] private GameObject bundlesPanel;
    [SerializeField] private GameObject coinsPanel;
    [SerializeField] private GameObject accessPanel;

    [Header("=== Offers List ===")]
    [SerializeField] private List<ShopOfferUI> offers = new List<ShopOfferUI>();

    [Header("=== Bundle Details Preview ===")]
    [SerializeField] private BundlePreviewUI bundlePreview;

    [Header("=== Status & Feedback ===")]
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private GameObject loadingOverlay;
    [SerializeField] private TextMeshProUGUI loadingText;

    // Visual assets preserved for Inspector serialization compatibility
    [Header("=== Visual Style Assets (Leaderboard Style) ===")]
    [SerializeField] private TMP_FontAsset titleFont;
    [SerializeField] private TMP_FontAsset bodyFont;
    [SerializeField] private Sprite windowFrameSprite;
    [SerializeField] private Sprite cardNormalSprite;
    [SerializeField] private Sprite cardGoldSprite;
    [SerializeField] private Sprite cardSilverSprite;
    [SerializeField] private Sprite cardPlayerSprite;
    [SerializeField] private Sprite buttonGreenSprite;
    [SerializeField] private Sprite buttonBlueSprite;
    [SerializeField] private Sprite closeIconSprite;
    [SerializeField] private Sprite goldCoinSprite;
    [SerializeField] private Sprite silverCoinSprite;
    [SerializeField] private Sprite checkIconSprite;
    [SerializeField] private Sprite onlineIconSprite;

    // ===================================================================
    #endregion

    // ===================================================================
    #region Private State
    // ===================================================================

    private string _selectedBundleId = "package_01";
    private readonly List<GameObject> _spawnedBundleItemIcons = new List<GameObject>();
    private readonly List<Button> _bundleSelectButtons = new List<Button>();
    private GameObject _itemTemplate;
    private bool _isPurchasing = false;
    private Tween _feedbackTween;

    private static readonly (string pid, string name, string tag)[] BundleMetadata =
    {
        ("package_01", "STARTER PACK", "STARTER"),
        ("package_02", "ADVANCED PACK", "POPULAR"),
        ("package_03", "ELITE PACK", "ELITE"),
        ("package_04", "CHARACTER PACK", "CHARACTERS"),
        ("package_05", "LEGENDARY PACK", "LEGENDARY"),
        ("package_06", "WEAPONS PACK", "ARSENAL")
    };

    // ===================================================================
    #endregion

    // ===================================================================
    #region Unity Lifecycle
    // ===================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        AutoBindExistingReferences();
        SetupItemTemplate();
        SetupBundleRibbonButtons();
    }

    private void Start()
    {
        WireButtonListeners();

        IAPManager.OnPurchaseSuccess += HandlePurchaseSuccess;
        IAPManager.OnPurchaseFailedEvent += HandlePurchaseFailed;
        IAPManager.OnInitializedEvent += RefreshPrices;

        GameDataManager.OnCurrencyChanged += RefreshCurrency;
    }

    private void OnDestroy()
    {
        IAPManager.OnPurchaseSuccess -= HandlePurchaseSuccess;
        IAPManager.OnPurchaseFailedEvent -= HandlePurchaseFailed;
        IAPManager.OnInitializedEvent -= RefreshPrices;

        GameDataManager.OnCurrencyChanged -= RefreshCurrency;

        _feedbackTween?.Kill();
        transform.DOKill(true);
    }

    private void OnEnable()
    {
        // Purely update values without overriding user layout
        RefreshCurrency();
        RefreshUI();
        RefreshPrices();
        SwitchTab(0);
        ClearFeedback();
        ShowLoading(false);
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Non-Destructive Auto-Binding & Template Setup
    // ===================================================================

    /// <summary>
    /// Non-destructively finds existing scene elements if unassigned in Inspector,
    /// without creating or destroying any GameObjects or modifying any transforms.
    /// </summary>
    private void AutoBindExistingReferences()
    {
        if (cardRect == null)
        {
            var found = transform.Find("PopupCard");
            if (found != null) cardRect = found.GetComponent<RectTransform>();
        }

        if (popupCanvasGroup == null)
            popupCanvasGroup = GetComponent<CanvasGroup>();

        if (cardRect != null)
        {
            if (closeButton == null)
            {
                var btnT = cardRect.Find("Header/CloseButton") ?? cardRect.Find("CloseButton");
                if (btnT != null) closeButton = btnT.GetComponent<Button>();
            }

            if (restorePurchasesButton == null)
            {
                var btnT = cardRect.Find("FooterBar/RestoreBtn") ?? cardRect.Find("RestoreBtn");
                if (btnT != null) restorePurchasesButton = btnT.GetComponent<Button>();
            }

            if (softCurrencyText == null)
            {
                var txtT = cardRect.Find("Header/CurrencyRibbon/SoftBadge/Val");
                if (txtT != null) softCurrencyText = txtT.GetComponent<TextMeshProUGUI>();
            }

            if (hardCurrencyText == null)
            {
                var txtT = cardRect.Find("Header/CurrencyRibbon/HardBadge/Val");
                if (txtT != null) hardCurrencyText = txtT.GetComponent<TextMeshProUGUI>();
            }

            if (bundlesTabButton == null)
            {
                var tabT = cardRect.Find("TabBar/BundlesTab");
                if (tabT != null) bundlesTabButton = tabT.GetComponent<Button>();
            }

            if (coinsTabButton == null)
            {
                var tabT = cardRect.Find("TabBar/CoinsTab");
                if (tabT != null) coinsTabButton = tabT.GetComponent<Button>();
            }

            if (accessTabButton == null)
            {
                var tabT = cardRect.Find("TabBar/AccessTab");
                if (tabT != null) accessTabButton = tabT.GetComponent<Button>();
            }

            if (bundlesPanel == null)
            {
                var pT = cardRect.Find("BundlesPanel");
                if (pT != null) bundlesPanel = pT.gameObject;
            }

            if (coinsPanel == null)
            {
                var pT = cardRect.Find("CoinsPanel");
                if (pT != null) coinsPanel = pT.gameObject;
            }

            if (accessPanel == null)
            {
                var pT = cardRect.Find("AccessPanel");
                if (pT != null) accessPanel = pT.gameObject;
            }

            if (loadingOverlay == null)
            {
                var ldT = cardRect.Find("LoadingOverlay");
                if (ldT != null) loadingOverlay = ldT.gameObject;
            }

            if (loadingText == null && loadingOverlay != null)
            {
                loadingText = loadingOverlay.GetComponentInChildren<TextMeshProUGUI>();
            }

            if (feedbackText == null)
            {
                var fbT = cardRect.Find("FooterBar/Feedback") ?? cardRect.Find("Feedback");
                if (fbT != null) feedbackText = fbT.GetComponent<TextMeshProUGUI>();
            }
        }
    }

    /// <summary>
    /// Uses the user's existing item card inside itemsContainer (or a prefab) as a template.
    /// This ensures any spawned cards 100% inherit the user's manual styling, fonts, and materials.
    /// </summary>
    private void SetupItemTemplate()
    {
        if (_itemTemplate != null) return;

        if (bundlePreview?.bundleItemPrefab != null)
        {
            _itemTemplate = bundlePreview.bundleItemPrefab;
        }
        else if (bundlePreview?.itemsContainer != null && bundlePreview.itemsContainer.childCount > 0)
        {
            // Use the first user-created item as the template
            _itemTemplate = bundlePreview.itemsContainer.GetChild(0).gameObject;
            _itemTemplate.SetActive(false); // Hide the template item
        }
    }

    /// <summary>
    /// Wires or creates bundle selection buttons in BundleRibbon by using the user's
    /// existing buttons as prefab templates.
    /// </summary>
    private void SetupBundleRibbonButtons()
    {
        Transform ribbon = bundlesPanel != null ? bundlesPanel.transform.Find("BundleRibbon") : null;
        if (ribbon == null) return;

        _bundleSelectButtons.Clear();

        // Check for existing template button in ribbon
        GameObject buttonTemplate = ribbon.childCount > 0 ? ribbon.GetChild(0).gameObject : null;

        for (int i = 0; i < BundleMetadata.Length; i++)
        {
            string pid = BundleMetadata[i].pid;
            Transform btnT = ribbon.Find("Btn_" + pid);

            // If missing, clone from the user's existing button template
            if (btnT == null && buttonTemplate != null)
            {
                GameObject clone = Instantiate(buttonTemplate, ribbon);
                clone.name = "Btn_" + pid;
                clone.SetActive(true);
                btnT = clone.transform;

                var label = btnT.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = BundleMetadata[i].tag;
            }

            if (btnT != null)
            {
                Button btn = btnT.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    string bundleId = pid;
                    btn.onClick.AddListener(() => SelectBundle(bundleId));
                    _bundleSelectButtons.Add(btn);
                }
            }
        }
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Button Wireup & Navigation
    // ===================================================================

    private void WireButtonListeners()
    {
        closeButton?.onClick.RemoveAllListeners();
        closeButton?.onClick.AddListener(OnCloseClicked);

        restorePurchasesButton?.onClick.RemoveAllListeners();
        restorePurchasesButton?.onClick.AddListener(OnRestorePurchasesClicked);

        bundlesTabButton?.onClick.RemoveAllListeners();
        bundlesTabButton?.onClick.AddListener(() => SwitchTab(0));

        coinsTabButton?.onClick.RemoveAllListeners();
        coinsTabButton?.onClick.AddListener(() => SwitchTab(1));

        accessTabButton?.onClick.RemoveAllListeners();
        accessTabButton?.onClick.AddListener(() => SwitchTab(2));
    }

    private void OnCloseClicked()
    {
        closeButton?.transform.DOPunchScale(new Vector3(-0.08f, -0.08f, 0f), 0.15f);
        UIManager.Instance?.HideShopPopup();
    }

    public void SwitchTab(int tabIndex)
    {
        if (bundlesPanel != null) bundlesPanel.SetActive(tabIndex == 0);
        if (coinsPanel != null) coinsPanel.SetActive(tabIndex == 1);
        if (accessPanel != null) accessPanel.SetActive(tabIndex == 2);

        if (tabIndex == 0)
        {
            SelectBundle(_selectedBundleId);
        }
    }

    /// <summary>
    /// Preserved for API compatibility; does NOT alter user layout in code.
    /// Layout is controlled directly by the Unity Editor Inspector and CanvasScaler.
    /// </summary>
    public void ApplyResponsiveLayout()
    {
        // Intentionally no-op to protect user's manual design
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region UI Values Refresh (Text, Price, Owned Status)
    // ===================================================================

    public void RefreshUI()
    {
        RefreshCurrency();
        RefreshOffers();

        if (bundlesPanel != null && bundlesPanel.activeSelf)
        {
            SelectBundle(_selectedBundleId);
        }
    }

    private void RefreshCurrency()
    {
        if (GameDataManager.Instance != null)
        {
            if (softCurrencyText != null)
                softCurrencyText.text = GameDataManager.Instance.SoftCurrency.ToString("N0");

            if (hardCurrencyText != null)
                hardCurrencyText.text = GameDataManager.Instance.HardCurrency.ToString("N0");
        }
    }

    public void RefreshPrices()
    {
        if (IAPManager.Instance == null || !IAPManager.Instance.IsInitialized)
            return;

        // Refresh coin and pass offer prices
        for (int i = 0; i < offers.Count; i++)
        {
            ShopOfferUI ui = offers[i];
            if (ui == null || string.IsNullOrEmpty(ui.productId)) continue;

            string localized = IAPManager.Instance.GetLocalizedPrice(ui.productId);
            if (!string.IsNullOrEmpty(localized) && ui.priceText != null)
            {
                if (ui.priceText.text != "OWNED")
                {
                    ui.priceText.text = localized;
                }
            }
        }

        // Refresh bundle preview price
        if (bundlePreview != null && bundlePreview.bundlePriceText != null && !string.IsNullOrEmpty(_selectedBundleId))
        {
            var db = StoreCatalogDatabase.Instance;
            BundleDefinition bundle = db != null ? db.GetBundleByProductId(_selectedBundleId) : null;
            if (bundle != null && bundle.AreAllItemsOwned())
            {
                bundlePreview.bundlePriceText.text = "ALL ITEMS OWNED";
            }
            else
            {
                string bPrice = IAPManager.Instance.GetLocalizedPrice(_selectedBundleId);
                if (!string.IsNullOrEmpty(bPrice))
                    bundlePreview.bundlePriceText.text = bPrice;
            }
        }
    }

    private void RefreshOffers()
    {
        for (int i = 0; i < offers.Count; i++)
        {
            ShopOfferUI ui = offers[i];
            if (ui == null || ui.root == null) continue;

            string pid = ui.productId;
            if (string.IsNullOrEmpty(pid)) continue;

            ui.buyButton?.onClick.RemoveAllListeners();
            ui.buyButton?.onClick.AddListener(() => OnBuyClicked(pid));

            // Check permanent pass entitlement states
            if (pid == "online_access" && MultiplayerAccessManager.Instance != null)
            {
                bool owned = MultiplayerAccessManager.Instance.GetAccessState() == MultiplayerAccessState.PermanentAccess;
                ApplyCardOwnedState(ui, owned);
            }
            else if (pid == "remove_ads" && GameDataManager.Instance != null)
            {
                bool owned = GameDataManager.Instance.AdsRemoved;
                ApplyCardOwnedState(ui, owned);
            }
        }
    }

    private void ApplyCardOwnedState(ShopOfferUI ui, bool isOwned)
    {
        if (isOwned)
        {
            if (ui.priceText != null) ui.priceText.text = "OWNED";
            if (ui.buyButton != null) ui.buyButton.interactable = false;
            if (ui.ownedBadge != null) ui.ownedBadge.SetActive(true);
        }
        else
        {
            if (ui.buyButton != null) ui.buyButton.interactable = true;
            if (ui.ownedBadge != null) ui.ownedBadge.SetActive(false);
            if (IAPManager.Instance != null && IAPManager.Instance.IsInitialized && ui.priceText != null)
            {
                string price = IAPManager.Instance.GetLocalizedPrice(ui.productId);
                if (!string.IsNullOrEmpty(price)) ui.priceText.text = price;
            }
        }
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Bundle Showcase & Prefab/Template-Based Items
    // ===================================================================

    public void SelectBundle(string bundleProductId)
    {
        _selectedBundleId = bundleProductId;

        var db = StoreCatalogDatabase.Instance;
        if (db == null || bundlePreview == null) return;

        BundleDefinition bundle = db.GetBundleByProductId(bundleProductId);
        if (bundle == null) return;

        // Update bundle texts
        if (bundlePreview.bundleTitleText != null)
            bundlePreview.bundleTitleText.text = bundle.bundleName.ToUpperInvariant();

        if (bundlePreview.bundleDescriptionText != null)
            bundlePreview.bundleDescriptionText.text = bundle.description;

        if (bundlePreview.bundleBadgeText != null)
        {
            string tag = "SPECIAL PACK";
            foreach (var meta in BundleMetadata)
            {
                if (meta.pid == bundleProductId) { tag = meta.tag; break; }
            }
            bundlePreview.bundleBadgeText.text = $"[{tag}]";
        }

        bool allOwned = bundle.AreAllItemsOwned();

        if (bundlePreview.bundlePriceText != null)
        {
            if (allOwned)
            {
                bundlePreview.bundlePriceText.text = "ALL ITEMS OWNED";
            }
            else
            {
                string price = IAPManager.Instance != null ? IAPManager.Instance.GetLocalizedPrice(bundleProductId) : "";
                bundlePreview.bundlePriceText.text = !string.IsNullOrEmpty(price) ? $"BUY ({price})" : "BUY BUNDLE";
            }
        }

        if (bundlePreview.buyBundleButton != null)
        {
            bundlePreview.buyBundleButton.interactable = !allOwned && !_isPurchasing;
            bundlePreview.buyBundleButton.onClick.RemoveAllListeners();
            bundlePreview.buyBundleButton.onClick.AddListener(() => OnBuyClicked(bundleProductId));
        }

        // Repopulate included items cards using the user's template/prefab
        foreach (var go in _spawnedBundleItemIcons)
        {
            if (go != null && go != _itemTemplate)
                Destroy(go);
        }
        _spawnedBundleItemIcons.Clear();

        if (_itemTemplate != null && bundlePreview.itemsContainer != null)
        {
            foreach (var item in bundle.includedItems)
            {
                if (item == null) continue;

                // Instantiate directly from the user's prefab/template
                GameObject card = Instantiate(_itemTemplate, bundlePreview.itemsContainer);
                card.name = "Item_" + item.id;
                card.SetActive(true);
                _spawnedBundleItemIcons.Add(card);

                // Update ONLY the text values
                var nameTmp = card.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
                if (nameTmp == null) nameTmp = card.GetComponentInChildren<TextMeshProUGUI>();
                if (nameTmp != null) nameTmp.text = item.displayName;

                var typeBadgeTmp = card.transform.Find("TypeBadge")?.GetComponent<TextMeshProUGUI>();
                if (typeBadgeTmp != null)
                {
                    typeBadgeTmp.text = item.itemType == StoreItemType.Weapon ? "WEAPON" : "OUTFIT";
                }

                var statusTmp = card.transform.Find("Status")?.GetComponent<TextMeshProUGUI>();
                if (statusTmp != null)
                {
                    statusTmp.text = item.IsOwned() ? "<color=#00FF88>[OWNED]</color>" : "<color=#FFD700>[NEW]</color>";
                }
            }
        }
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Purchase Flow & IAP Integration
    // ===================================================================

    public void OnBuyClicked(string productId)
    {
        if (string.IsNullOrEmpty(productId) || _isPurchasing) return;

        _isPurchasing = true;
        ShowLoading(true, "Connecting to store...");
        SetFeedback("Processing purchase with store...");

        if (IAPManager.Instance == null)
        {
            _isPurchasing = false;
            ShowLoading(false);
            SetFeedback("Store unavailable. Please check internet connection.", isError: true);
            return;
        }

        IAPManager.Instance.BuyProduct(productId);
    }

    private void HandlePurchaseSuccess(string productId)
    {
        _isPurchasing = false;
        ShowLoading(false);
        SetFeedback("<color=#00FF88>Purchase Successful! Items Delivered.</color>");
        RefreshUI();

        if (MultiplayerAccessGateModal.Instance != null)
            MultiplayerAccessGateModal.Instance.RefreshUI();
    }

    private void HandlePurchaseFailed(string productId, string reason)
    {
        _isPurchasing = false;
        ShowLoading(false);
        SetFeedback($"Purchase not completed: {reason}", isError: true);
        RefreshUI();
    }

    private void OnRestorePurchasesClicked()
    {
        restorePurchasesButton?.transform.DOPunchScale(new Vector3(-0.08f, -0.08f, 0f), 0.15f);

        ShowLoading(true, "Restoring previous purchases...");
        SetFeedback("Contacting store for previous purchases...");

        if (IAPManager.Instance != null)
        {
            IAPManager.Instance.RestorePurchases();
        }

        DOVirtual.DelayedCall(3f, () =>
        {
            ShowLoading(false);
            SetFeedback("<color=#00FF88>Purchase restoration completed.</color>");
            RefreshUI();
        });
    }

    private void ShowLoading(bool show, string message = "Processing...")
    {
        if (loadingOverlay != null)
        {
            loadingOverlay.SetActive(show);
        }
        if (loadingText != null)
        {
            loadingText.text = message;
        }
    }

    private void SetFeedback(string msg, bool isError = false)
    {
        if (feedbackText != null)
        {
            feedbackText.text = isError ? $"<color=#FF4444>{msg}</color>" : msg;
            _feedbackTween?.Kill();
            feedbackText.transform.localScale = Vector3.one;
            _feedbackTween = feedbackText.transform.DOPunchScale(Vector3.one * 0.08f, 0.25f).SetUpdate(true);
        }
    }

    private void ClearFeedback()
    {
        if (feedbackText != null) feedbackText.text = "";
    }

    // ===================================================================
    #endregion
}