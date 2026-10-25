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
    }

    [System.Serializable]
    public class BundlePreviewUI
    {
        public GameObject root;
        public TextMeshProUGUI bundleTitleText;
        public TextMeshProUGUI bundleDescriptionText;
        public Transform itemsContainer;
        public GameObject bundleItemPrefab;
        public Button buyBundleButton;
        public TextMeshProUGUI bundlePriceText;
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Inspector Fields
    // ===================================================================

    [Header("=== Header & Navigation ===")]
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Button restorePurchasesButton;

    [Header("=== Currency Indicators ===")]
    [SerializeField] private TextMeshProUGUI softCurrencyText;
    [SerializeField] private TextMeshProUGUI hardCurrencyText;

    [Header("=== Tabs ===")]
    [SerializeField] private Button coinsTabButton;
    [SerializeField] private Button bundlesTabButton;
    [SerializeField] private Button accessTabButton;
    [SerializeField] private GameObject coinsPanel;
    [SerializeField] private GameObject bundlesPanel;
    [SerializeField] private GameObject accessPanel;

    [Header("=== Offers List ===")]
    [SerializeField] private List<ShopOfferUI> offers = new List<ShopOfferUI>();

    [Header("=== Bundle Details Preview ===")]
    [SerializeField] private BundlePreviewUI bundlePreview;

    [Header("=== Status & Feedback ===")]
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private GameObject loadingOverlay;

    // ===================================================================
    #endregion

    // ===================================================================
    #region Private State
    // ===================================================================

    private string _selectedBundleId = "package_01";
    private readonly List<GameObject> _spawnedBundleItemIcons = new List<GameObject>();
    private readonly List<Button> _bundleSelectButtons = new List<Button>();
    private bool _isBuildingFallback = false;

    private static readonly string[] BundleIds =
    {
        "package_01", "package_02", "package_03",
        "package_04", "package_05", "package_06"
    };

    private static readonly (string pid, string name, string amount, string defaultPrice)[] CoinPackDefs =
    {
        ("coins_200", "200 COINS", "+200 HARD COINS", "$0.99"),
        ("coins_500", "500 COINS", "+500 HARD COINS", "$1.99"),
        ("coins_1200", "1,200 COINS", "+1,200 HARD COINS", "$3.99"),
        ("coins_2500", "2,500 COINS", "+2,500 HARD COINS", "$7.99")
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

        if (coinsPanel == null || offers == null || offers.Count == 0)
        {
            BuildDefaultShopUI();
        }
    }

    private void Start()
    {
        closeButton?.onClick.RemoveAllListeners();
        closeButton?.onClick.AddListener(UIManager.Instance.HideShopPopup);

        restorePurchasesButton?.onClick.RemoveAllListeners();
        restorePurchasesButton?.onClick.AddListener(OnRestorePurchasesClicked);

        coinsTabButton?.onClick.RemoveAllListeners();
        coinsTabButton?.onClick.AddListener(() => SwitchTab(0));

        bundlesTabButton?.onClick.RemoveAllListeners();
        bundlesTabButton?.onClick.AddListener(() => SwitchTab(1));

        accessTabButton?.onClick.RemoveAllListeners();
        accessTabButton?.onClick.AddListener(() => SwitchTab(2));

        IAPManager.OnPurchaseSuccess += HandlePurchaseSuccess;
        IAPManager.OnPurchaseFailedEvent += HandlePurchaseFailed;
        IAPManager.OnInitializedEvent += RefreshPrices;
    }

    private void OnDestroy()
    {
        IAPManager.OnPurchaseSuccess -= HandlePurchaseSuccess;
        IAPManager.OnPurchaseFailedEvent -= HandlePurchaseFailed;
        IAPManager.OnInitializedEvent -= RefreshPrices;
    }

    private void OnEnable()
    {
        RefreshUI();
        RefreshPrices();
        SwitchTab(0);
        ClearFeedback();
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Tabs & Navigation
    // ===================================================================

    public void SwitchTab(int tabIndex)
    {
        if (coinsPanel) coinsPanel.SetActive(tabIndex == 0);
        if (bundlesPanel) bundlesPanel.SetActive(tabIndex == 1);
        if (accessPanel) accessPanel.SetActive(tabIndex == 2);

        if (titleText != null)
        {
            switch (tabIndex)
            {
                case 0: titleText.text = "COINS SHOP"; break;
                case 1: titleText.text = "SPECIAL BUNDLES"; break;
                case 2: titleText.text = "PASSES & ACCESS"; break;
            }
        }

        HighlightTabButton(coinsTabButton, tabIndex == 0);
        HighlightTabButton(bundlesTabButton, tabIndex == 1);
        HighlightTabButton(accessTabButton, tabIndex == 2);

        if (tabIndex == 1)
        {
            SelectBundle(_selectedBundleId);
        }
    }

    private void HighlightTabButton(Button btn, bool active)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = active ? new Color(0.18f, 0.55f, 0.95f, 1f) : new Color(0.2f, 0.23f, 0.3f, 0.9f);
        }
        btn.transform.DOScale(active ? 1.05f : 1f, 0.2f);
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region UI Refresh
    // ===================================================================

    public void RefreshUI()
    {
        if (GameDataManager.Instance != null)
        {
            if (softCurrencyText != null)
                softCurrencyText.text = GameDataManager.Instance.SoftCurrency.ToString("N0");

            if (hardCurrencyText != null)
                hardCurrencyText.text = GameDataManager.Instance.HardCurrency.ToString("N0");
        }

        RefreshOffers();

        if (bundlesPanel != null && bundlesPanel.activeSelf)
        {
            SelectBundle(_selectedBundleId);
        }
    }

    public void RefreshPrices()
    {
        if (IAPManager.Instance == null || !IAPManager.Instance.IsInitialized)
            return;

        for (int i = 0; i < offers.Count; i++)
        {
            if (offers[i] == null || string.IsNullOrEmpty(offers[i].productId))
                continue;

            string localized = IAPManager.Instance.GetLocalizedPrice(offers[i].productId);
            if (!string.IsNullOrEmpty(localized) && offers[i].priceText != null)
            {
                offers[i].priceText.text = localized;
            }
        }

        if (bundlePreview != null && bundlePreview.bundlePriceText != null && !string.IsNullOrEmpty(_selectedBundleId))
        {
            string bPrice = IAPManager.Instance.GetLocalizedPrice(_selectedBundleId);
            if (!string.IsNullOrEmpty(bPrice))
                bundlePreview.bundlePriceText.text = bPrice;
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

            if (ui.offerImage != null && ui.offerImage.sprite == null)
            {
                ui.offerImage.color = new Color(1f, 1f, 1f, 0.35f);
            }

            // Update permanent pass states
            if (pid == "online_access" && Shootx.Store.MultiplayerAccessManager.Instance != null)
            {
                if (Shootx.Store.MultiplayerAccessManager.Instance.GetAccessState() == Shootx.Store.MultiplayerAccessState.PermanentAccess)
                {
                    if (ui.priceText != null) ui.priceText.text = "OWNED";
                    if (ui.buyButton != null) ui.buyButton.interactable = false;
                }
            }
            else if (pid == "remove_ads" && GameDataManager.Instance != null && GameDataManager.Instance.AdsRemoved)
            {
                if (ui.priceText != null) ui.priceText.text = "OWNED";
                if (ui.buyButton != null) ui.buyButton.interactable = false;
            }
        }
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Bundle Preview
    // ===================================================================

    public void SelectBundle(string bundleProductId)
    {
        _selectedBundleId = bundleProductId;

        var db = StoreCatalogDatabase.Instance;
        if (db == null || bundlePreview == null) return;

        BundleDefinition bundle = db.GetBundleByProductId(bundleProductId);
        if (bundle == null) return;

        if (bundlePreview.bundleTitleText) bundlePreview.bundleTitleText.text = bundle.bundleName;
        if (bundlePreview.bundleDescriptionText) bundlePreview.bundleDescriptionText.text = bundle.description;

        string price = IAPManager.Instance != null ? IAPManager.Instance.GetLocalizedPrice(bundleProductId) : "";
        if (bundlePreview.bundlePriceText)
        {
            bundlePreview.bundlePriceText.text = !string.IsNullOrEmpty(price) ? price : "BUY";
        }

        bool allOwned = bundle.AreAllItemsOwned();
        if (bundlePreview.buyBundleButton != null)
        {
            bundlePreview.buyBundleButton.interactable = !allOwned;
            if (allOwned && bundlePreview.bundlePriceText != null)
            {
                bundlePreview.bundlePriceText.text = "OWNED";
            }
        }

        bundlePreview.buyBundleButton?.onClick.RemoveAllListeners();
        bundlePreview.buyBundleButton?.onClick.AddListener(() => OnBuyClicked(bundleProductId));

        // Clear previous item icons
        foreach (var go in _spawnedBundleItemIcons)
        {
            if (go) Destroy(go);
        }
        _spawnedBundleItemIcons.Clear();

        // Populate included items
        if (bundlePreview.itemsContainer != null)
        {
            foreach (var item in bundle.includedItems)
            {
                if (item == null) continue;

                GameObject iconObj;
                if (bundlePreview.bundleItemPrefab != null)
                {
                    iconObj = Instantiate(bundlePreview.bundleItemPrefab, bundlePreview.itemsContainer);
                }
                else
                {
                    iconObj = CreateDefaultBundleItemCard(bundlePreview.itemsContainer, item);
                }
                _spawnedBundleItemIcons.Add(iconObj);

                var txt = iconObj.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null)
                {
                    txt.textWrappingMode = TextWrappingModes.NoWrap;
                    bool owned = item.IsOwned();
                    txt.text = owned ? $"{item.displayName}\n<color=#00FF88>[OWNED]</color>" : $"{item.displayName}\n<color=#FFD700>[NEW]</color>";
                }
            }
        }
    }

    private GameObject CreateDefaultBundleItemCard(Transform parent, StoreItemDefinition item)
    {
        var go = new GameObject("BundleItem_" + item.id, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(100f, 90f);

        var img = go.GetComponent<Image>();
        img.color = new Color(0.18f, 0.22f, 0.3f, 0.95f);

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(4f, 4f);
        textRect.offsetMax = new Vector2(-4f, -4f);

        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = item.displayName;
        tmp.fontSize = 11;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return go;
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Purchase Handling
    // ===================================================================

    public void OnBuyClicked(string productId)
    {
        if (string.IsNullOrEmpty(productId)) return;

        ShowLoading(true);
        SetFeedback("Processing purchase with store...");

        if (IAPManager.Instance == null)
        {
            ShowLoading(false);
            SetFeedback("Store unavailable.", isError: true);
            return;
        }

        IAPManager.Instance.BuyProduct(productId);
    }

    private void HandlePurchaseSuccess(string productId)
    {
        ShowLoading(false);
        SetFeedback("<color=#00FF88>Purchase Successful!</color>");
        RefreshUI();

        if (MultiplayerAccessGateModal.Instance != null)
            MultiplayerAccessGateModal.Instance.RefreshUI();
    }

    private void HandlePurchaseFailed(string productId, string reason)
    {
        ShowLoading(false);
        SetFeedback($"Purchase failed: {reason}", isError: true);
    }

    private void OnRestorePurchasesClicked()
    {
        ShowLoading(true);
        SetFeedback("Restoring previous purchases...");

        if (IAPManager.Instance != null)
        {
            IAPManager.Instance.RestorePurchases();
        }

        DOVirtual.DelayedCall(3f, () =>
        {
            ShowLoading(false);
            SetFeedback("Purchases restored.");
            RefreshUI();
        });
    }

    private void ShowLoading(bool show)
    {
        if (loadingOverlay) loadingOverlay.SetActive(show);
    }

    private void SetFeedback(string msg, bool isError = false)
    {
        if (feedbackText != null)
        {
            feedbackText.text = isError ? $"<color=#FF4444>{msg}</color>" : msg;
            feedbackText.DOKill();
            feedbackText.transform.DOPunchScale(Vector3.one * 0.08f, 0.25f);
        }
    }

    private void ClearFeedback()
    {
        if (feedbackText != null) feedbackText.text = "";
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Dynamic UI Builder
    // ===================================================================

    private void BuildDefaultShopUI()
    {
        if (_isBuildingFallback) return;
        _isBuildingFallback = true;

        // Locate PopupCard in hierarchy
        Transform cardTransform = transform.Find("PopupCard");
        if (cardTransform == null) cardTransform = transform;

        // 2. Tab Bar
        var tabBar = new GameObject("TabBar", typeof(RectTransform));
        tabBar.transform.SetParent(cardTransform, false);
        var tabRect = tabBar.GetComponent<RectTransform>();
        tabRect.anchorMin = new Vector2(0.5f, 1f);
        tabRect.anchorMax = new Vector2(0.5f, 1f);
        tabRect.sizeDelta = new Vector2(440f, 42f);
        tabRect.anchoredPosition = new Vector2(0f, -180f);

        coinsTabButton = CreateButton(tabBar.transform, "CoinsTab", "COIN PACKS", new Vector2(140f, 38f), new Vector2(-145f, 0f), new Color(0.18f, 0.55f, 0.95f)).GetComponent<Button>();
        bundlesTabButton = CreateButton(tabBar.transform, "BundlesTab", "BUNDLES", new Vector2(140f, 38f), new Vector2(0f, 0f), new Color(0.2f, 0.23f, 0.3f)).GetComponent<Button>();
        accessTabButton = CreateButton(tabBar.transform, "AccessTab", "ONLINE PASSES", new Vector2(140f, 38f), new Vector2(145f, 0f), new Color(0.2f, 0.23f, 0.3f)).GetComponent<Button>();

        // 3. Panels Container
        var panelsRoot = new GameObject("PanelsContainer", typeof(RectTransform));
        panelsRoot.transform.SetParent(cardTransform, false);
        var panRect = panelsRoot.GetComponent<RectTransform>();
        panRect.anchorMin = new Vector2(0.05f, 0.12f);
        panRect.anchorMax = new Vector2(0.95f, 0.82f);
        panRect.offsetMin = Vector2.zero;
        panRect.offsetMax = Vector2.zero;

        // Panel 0: Coins Panel
        coinsPanel = new GameObject("CoinsPanel", typeof(RectTransform));
        coinsPanel.transform.SetParent(panelsRoot.transform, false);
        StretchRect(coinsPanel.GetComponent<RectTransform>());

        offers.Clear();
        float[] coinX = { -280f, -90f, 100f, 290f };
        for (int i = 0; i < CoinPackDefs.Length; i++)
        {
            var def = CoinPackDefs[i];
            var card = CreateCoinCard(coinsPanel.transform, def.pid, def.name, def.amount, def.defaultPrice, new Vector2(coinX[i], 0f));
            offers.Add(card);
        }

        // Panel 1: Bundles Panel
        bundlesPanel = new GameObject("BundlesPanel", typeof(RectTransform));
        bundlesPanel.transform.SetParent(panelsRoot.transform, false);
        StretchRect(bundlesPanel.GetComponent<RectTransform>());
        bundlesPanel.SetActive(false);

        // Bundle selector buttons top of bundles panel
        var bSelector = new GameObject("BundleSelector", typeof(RectTransform));
        bSelector.transform.SetParent(bundlesPanel.transform, false);
        var bSelRect = bSelector.GetComponent<RectTransform>();
        bSelRect.anchorMin = new Vector2(0f, 0.72f);
        bSelRect.anchorMax = new Vector2(1f, 1f);

        bSelRect.SetInsetAndSizeFromParentEdge(
            RectTransform.Edge.Top, 60f, bSelRect.rect.height
        );

        string[] bundleShortNames = { "STARTER", "ADVANCED", "ELITE", "HEROES", "LEGEND", "WEAPONS" };
        float bStartPos = -175f;
        float bStep = 70f;
        for (int i = 0; i < BundleIds.Length; i++)
        {
            string bId = BundleIds[i];
            var bBtn = CreateButton(bSelector.transform, "Btn_" + bId, bundleShortNames[i], new Vector2(66f, 36f), new Vector2(bStartPos + i * bStep, 0f), new Color(0.18f, 0.22f, 0.32f)).GetComponent<Button>();
            bBtn.GetComponentInChildren<TMP_Text>().textWrappingMode = TextWrappingModes.NoWrap;
            bBtn.onClick.AddListener(() => SelectBundle(bId));
            _bundleSelectButtons.Add(bBtn);
        }

        // Bundle Preview Box
        bundlePreview = new BundlePreviewUI();
        var bpBox = new GameObject("BundlePreviewBox", typeof(RectTransform), typeof(Image));
        bpBox.transform.SetParent(bundlesPanel.transform, false);
        var bpRect = bpBox.GetComponent<RectTransform>();
        bpRect.anchorMin = new Vector2(0f, 0f);
        bpRect.anchorMax = new Vector2(1f, 0.7f);
        bpRect.offsetMin = Vector2.zero;
        bpRect.offsetMax = Vector2.zero;
        bpBox.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.22f, 0.95f);
        bundlePreview.root = bpBox;

        bundlePreview.bundleTitleText = CreateTMP(bpBox.transform, "Title", "BUNDLE TITLE", 18, true, TextAlignmentOptions.Center, new Vector2(380f, 28f), new Vector2(0f, 85f), new Color(1f, 0.85f, 0.2f));
        bundlePreview.bundleDescriptionText = CreateTMP(bpBox.transform, "Desc", "Included items bundle description", 13, false, TextAlignmentOptions.Center, new Vector2(380f, 26f), new Vector2(0f, 58f), Color.white);

        // Included items container
        var icRoot = new GameObject("ItemsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        icRoot.transform.SetParent(bpBox.transform, false);
        var icRect = icRoot.GetComponent<RectTransform>();
        icRect.sizeDelta = new Vector2(380f, 85f);
        icRect.anchoredPosition = new Vector2(0f, -2f);
        var hlg = icRoot.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 120;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        bundlePreview.itemsContainer = icRoot.transform;

        // Buy Bundle Button
        var bBuyGo = CreateButton(bpBox.transform, "BuyBundleBtn", "BUY BUNDLE ($4.99)", new Vector2(260f, 44f), new Vector2(0f, -72f), new Color(0.06f, 0.75f, 0.35f));
        bundlePreview.buyBundleButton = bBuyGo.GetComponent<Button>();
        bundlePreview.bundlePriceText = bBuyGo.GetComponentInChildren<TextMeshProUGUI>();

        // Panel 2: Passes Panel
        accessPanel = new GameObject("AccessPanel", typeof(RectTransform));
        accessPanel.transform.SetParent(panelsRoot.transform, false);
        StretchRect(accessPanel.GetComponent<RectTransform>());
        accessPanel.SetActive(false);

        // Pass 1: Online Access
        var pass1 = CreatePassCard(accessPanel.transform, "online_access", "ONLINE ACCESS PASS", "Permanent multiplayer access to all lobbies and modes.", "$1.99", new Vector2(-105f, 0f), new Color(0.15f, 0.5f, 0.9f));
        offers.Add(pass1);

        // Pass 2: Remove Ads
        var pass2 = CreatePassCard(accessPanel.transform, "remove_ads", "REMOVE ADS + ACCESS", "Permanently removes all forced ads AND unlocks permanent Multiplayer access.", "$3.99", new Vector2(105f, 0f), new Color(0.9f, 0.6f, 0.1f));
        offers.Add(pass2);

        // 4. Footer Bar
        var footer = new GameObject("FooterBar", typeof(RectTransform));
        footer.transform.SetParent(cardTransform, false);
        var footRect = footer.GetComponent<RectTransform>();
        footRect.anchorMin = new Vector2(0.5f, 0f);
        footRect.anchorMax = new Vector2(0.5f, 0f);
        footRect.sizeDelta = new Vector2(440f, 38f);
        footRect.anchoredPosition = new Vector2(0f, 22f);

        restorePurchasesButton = CreateButton(footer.transform, "RestoreBtn", "RESTORE PURCHASES", new Vector2(180f, 32f), new Vector2(-325f, 45f), new Color(0.25f, 0.28f, 0.38f)).GetComponent<Button>();
        feedbackText = CreateTMP(footer.transform, "Feedback", "", 13, false, TextAlignmentOptions.Center, new Vector2(220f, 30f), new Vector2(100f, 0f), Color.white);
    }

    private ShopOfferUI CreateCoinCard(Transform parent, string pid, string name, string amount, string price, Vector2 pos)
    {
        var card = new GameObject("Card_" + pid, typeof(RectTransform), typeof(Image));
        card.transform.SetParent(parent, false);
        var rect = card.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(180f, 200f);
        rect.anchoredPosition = pos;
        card.GetComponent<Image>().color = new Color(0.14f, 0.17f, 0.24f, 0.98f);

        var title = CreateTMP(card.transform, "Title", name, 13, true, TextAlignmentOptions.Center, new Vector2(90f, 25f), new Vector2(0f, 68f), new Color(0.2f, 0.9f, 1f));
        var amt = CreateTMP(card.transform, "Amount", amount, 11, false, TextAlignmentOptions.Center, new Vector2(90f, 40f), new Vector2(0f, 25f), Color.white);

        var btnGo = CreateButton(card.transform, "BuyBtn", price, new Vector2(88f, 36f), new Vector2(0f, -60f), new Color(0.06f, 0.75f, 0.35f));
        var btn = btnGo.GetComponent<Button>();
        var priceTxt = btnGo.GetComponentInChildren<TextMeshProUGUI>();

        return new ShopOfferUI
        {
            root = card,
            titleText = title,
            amountText = amt,
            buyButton = btn,
            priceText = priceTxt,
            productId = pid
        };
    }

    private ShopOfferUI CreatePassCard(Transform parent, string pid, string titleStr, string descStr, string price, Vector2 pos, Color accentColor)
    {
        var card = new GameObject("Card_" + pid, typeof(RectTransform), typeof(Image));
        card.transform.SetParent(parent, false);
        var rect = card.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(195f, 210f);
        rect.anchoredPosition = pos;
        card.GetComponent<Image>().color = new Color(0.13f, 0.16f, 0.23f, 0.98f);

        var title = CreateTMP(card.transform, "Title", titleStr, 14, true, TextAlignmentOptions.Center, new Vector2(180f, 30f), new Vector2(0f, 78f), accentColor);
        var desc = CreateTMP(card.transform, "Desc", descStr, 12, false, TextAlignmentOptions.Center, new Vector2(180f, 85f), new Vector2(0f, 15f), Color.white);

        var btnGo = CreateButton(card.transform, "BuyBtn", price, new Vector2(165f, 40f), new Vector2(0f, -70f), new Color(0.06f, 0.75f, 0.35f));
        var btn = btnGo.GetComponent<Button>();
        var priceTxt = btnGo.GetComponentInChildren<TextMeshProUGUI>();

        return new ShopOfferUI
        {
            root = card,
            titleText = title,
            amountText = desc,
            buyButton = btn,
            priceText = priceTxt,
            productId = pid
        };
    }

    private GameObject CreateButton(Transform parent, string name, string label, Vector2 size, Vector2 pos, Color btnColor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;

        var img = go.GetComponent<Image>();
        img.color = btnColor;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 13;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return go;
    }

    private TextMeshProUGUI CreateTMP(Transform parent, string name, string content, float fontSize, bool isBold, TextAlignmentOptions align, Vector2 size, Vector2 pos, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = fontSize;
        if (isBold) tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = align;
        tmp.color = color;

        return tmp;
    }

    private void StretchRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // ===================================================================
    #endregion
}