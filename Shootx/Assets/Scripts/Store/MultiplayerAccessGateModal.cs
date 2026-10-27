using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;

namespace Shootx.Store
{
    public class MultiplayerAccessGateModal : MonoBehaviour
    {
        private static MultiplayerAccessGateModal _instance;
        public static MultiplayerAccessGateModal Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Object.FindFirstObjectByType<MultiplayerAccessGateModal>();
                }
                return _instance;
            }
            private set => _instance = value;
        }

        // ===================================================================
        #region Inspector Fields - UI Controls & Roots
        // ===================================================================

        [Header("=== Canvas Group & Roots ===")]
        [SerializeField] private CanvasGroup modalGroup;
        [SerializeField] private RectTransform modalBox;

        [Header("=== Header & Status Banner ===")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private Image statusBannerImage;
        [SerializeField] private Button closeButton;

        [Header("=== Action Option A: Online Access Pass ===")]
        [SerializeField] private GameObject onlineAccessCard;
        [SerializeField] private Button buyOnlineAccessButton;
        [SerializeField] private TextMeshProUGUI buyOnlinePriceText;
        [SerializeField] private GameObject onlineAccessOwnedBadge;

        [Header("=== Action Option B: Remove Ads + Access ===")]
        [SerializeField] private GameObject removeAdsCard;
        [SerializeField] private Button buyRemoveAdsButton;
        [SerializeField] private TextMeshProUGUI buyRemoveAdsPriceText;
        [SerializeField] private GameObject removeAdsOwnedBadge;

        [Header("=== Action Option C: Rewarded Video (24H Free) ===")]
        [SerializeField] private GameObject watchAdCard;
        [SerializeField] private Button watchAdButton;
        [SerializeField] private TextMeshProUGUI watchAdButtonText;

        [Header("=== Enter Multiplayer & Restore ===")]
        [SerializeField] private Button enterMultiplayerButton;
        [SerializeField] private Button restorePurchasesButton;

        [Header("=== Feedback Message & Loading ===")]
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
        [SerializeField] private Sprite buttonGreenSprite;
        [SerializeField] private Sprite buttonBlueSprite;
        [SerializeField] private Sprite closeIconSprite;
        [SerializeField] private Sprite videoIconSprite;
        [SerializeField] private Sprite onlineIconSprite;
        [SerializeField] private Sprite checkIconSprite;

        // ===================================================================
        #endregion

        // ===================================================================
        #region Runtime State
        // ===================================================================

        [Header("=== Animation Settings ===")]
        [SerializeField] private float animDuration = 0.25f;

        private Tween _feedbackTween;
        private float _timerTick = 0f;
        private bool _isAdLoading = false;

        // ===================================================================
        #endregion

        // ===================================================================
        #region Unity Lifecycle
        // ===================================================================

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            AutoBindExistingReferences();
            HideInstant();
        }

        private void Start()
        {
            WireButtons();

            MultiplayerAccessManager.OnAccessChanged += RefreshUI;
            IAPManager.OnPurchaseSuccess += HandlePurchaseSuccess;
            IAPManager.OnPurchaseFailedEvent += HandlePurchaseFailed;
            IAPManager.OnInitializedEvent += RefreshPrices;
        }

        private void OnDestroy()
        {
            MultiplayerAccessManager.OnAccessChanged -= RefreshUI;
            IAPManager.OnPurchaseSuccess -= HandlePurchaseSuccess;
            IAPManager.OnPurchaseFailedEvent -= HandlePurchaseFailed;
            IAPManager.OnInitializedEvent -= RefreshPrices;

            _feedbackTween?.Kill();
            modalGroup?.DOKill();
        }

        private void Update()
        {
            if (modalGroup == null || modalGroup.alpha < 0.1f) return;

            var accessMgr = MultiplayerAccessManager.Instance;
            if (accessMgr != null && accessMgr.GetAccessState() == MultiplayerAccessState.TemporaryAccess)
            {
                _timerTick += Time.unscaledDeltaTime;
                if (_timerTick >= 1f)
                {
                    _timerTick = 0f;
                    UpdateTimerDisplay();
                }
            }
        }

        // ===================================================================
        #endregion

        // ===================================================================
        #region Non-Destructive Auto-Binding
        // ===================================================================

        /// <summary>
        /// Non-destructively finds existing elements if unassigned, without creating or destroying
        /// any GameObjects or modifying any transforms.
        /// </summary>
        private void AutoBindExistingReferences()
        {
            if (modalGroup == null)
                modalGroup = GetComponent<CanvasGroup>();

            if (modalBox == null)
            {
                var found = transform.Find("ModalBox");
                if (found != null) modalBox = found.GetComponent<RectTransform>();
            }

            if (modalBox != null)
            {
                if (closeButton == null)
                {
                    var btnT = modalBox.Find("Header/CloseBtn") ?? modalBox.Find("CloseBtn");
                    if (btnT != null) closeButton = btnT.GetComponent<Button>();
                }

                if (titleText == null)
                {
                    var tT = modalBox.Find("Header/Title");
                    if (tT != null) titleText = tT.GetComponent<TextMeshProUGUI>();
                }

                if (statusText == null)
                {
                    var sT = modalBox.Find("StatusBanner/Status");
                    if (sT != null) statusText = sT.GetComponent<TextMeshProUGUI>();
                }

                if (timerText == null)
                {
                    var tmT = modalBox.Find("StatusBanner/Timer");
                    if (tmT != null) timerText = tmT.GetComponent<TextMeshProUGUI>();
                }

                if (statusBannerImage == null)
                {
                    var sbT = modalBox.Find("StatusBanner");
                    if (sbT != null) statusBannerImage = sbT.GetComponent<Image>();
                }

                if (enterMultiplayerButton == null)
                {
                    var btnT = modalBox.Find("FooterArea/EnterMultiplayerBtn");
                    if (btnT != null) enterMultiplayerButton = btnT.GetComponent<Button>();
                }

                if (restorePurchasesButton == null)
                {
                    var btnT = modalBox.Find("FooterArea/RestoreBtn");
                    if (btnT != null) restorePurchasesButton = btnT.GetComponent<Button>();
                }

                if (feedbackText == null)
                {
                    var fbT = modalBox.Find("FooterArea/Feedback");
                    if (fbT != null) feedbackText = fbT.GetComponent<TextMeshProUGUI>();
                }

                if (loadingOverlay == null)
                {
                    var ldT = modalBox.Find("LoadingOverlay");
                    if (ldT != null) loadingOverlay = ldT.gameObject;
                }

                if (loadingText == null && loadingOverlay != null)
                {
                    loadingText = loadingOverlay.GetComponentInChildren<TextMeshProUGUI>();
                }
            }
        }

        // ===================================================================
        #endregion

        // ===================================================================
        #region Wireup & Modal Transitions
        // ===================================================================

        private void WireButtons()
        {
            buyOnlineAccessButton?.onClick.RemoveAllListeners();
            buyOnlineAccessButton?.onClick.AddListener(OnBuyOnlineAccessClicked);

            buyRemoveAdsButton?.onClick.RemoveAllListeners();
            buyRemoveAdsButton?.onClick.AddListener(OnBuyRemoveAdsClicked);

            watchAdButton?.onClick.RemoveAllListeners();
            watchAdButton?.onClick.AddListener(OnWatchAdClicked);

            enterMultiplayerButton?.onClick.RemoveAllListeners();
            enterMultiplayerButton?.onClick.AddListener(OnEnterMultiplayerClicked);

            closeButton?.onClick.RemoveAllListeners();
            closeButton?.onClick.AddListener(Hide);

            restorePurchasesButton?.onClick.RemoveAllListeners();
            restorePurchasesButton?.onClick.AddListener(OnRestorePurchasesClicked);
        }

        /// <summary>
        /// Preserved for API compatibility; does NOT alter user layout in code.
        /// </summary>
        public void ApplyResponsiveLayout()
        {
            // Intentionally no-op to protect user's manual design
        }

        public void Show()
        {
            RefreshUI();
            RefreshPrices();
            ClearFeedback();
            ShowLoading(false);

            if (modalGroup != null)
            {
                modalGroup.blocksRaycasts = true;
                modalGroup.interactable = true;
                modalGroup.DOKill();
                modalGroup.alpha = 0f;
                modalGroup.DOFade(1f, animDuration).SetUpdate(true);
            }
            else
            {
                gameObject.SetActive(true);
            }
        }

        public void Hide()
        {
            closeButton?.transform.DOPunchScale(new Vector3(-0.08f, -0.08f, 0f), 0.15f);

            if (modalGroup != null)
            {
                modalGroup.blocksRaycasts = false;
                modalGroup.interactable = false;
                modalGroup.DOKill();
                modalGroup.DOFade(0f, animDuration).SetUpdate(true).OnComplete(HideInstant);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void HideInstant()
        {
            if (modalGroup != null)
            {
                modalGroup.alpha = 0f;
                modalGroup.blocksRaycasts = false;
                modalGroup.interactable = false;
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        // ===================================================================
        #endregion

        // ===================================================================
        #region UI Values Refresh (Text, Price, Entitlements)
        // ===================================================================

        public void RefreshUI()
        {
            var accessMgr = MultiplayerAccessManager.Instance;
            if (accessMgr == null) return;

            MultiplayerAccessState state = accessMgr.GetAccessState();

            // 1. Update Status Banner Values
            if (state == MultiplayerAccessState.PermanentAccess)
            {
                if (statusText) statusText.text = "<color=#00FF88>PERMANENT ACCESS ACTIVE</color>";
                if (timerText) timerText.text = "Unlimited Online Multiplayer Unlocked";

                ApplyOwnedState(buyOnlineAccessButton, buyOnlinePriceText, onlineAccessOwnedBadge, true);
                if (watchAdCard) watchAdCard.SetActive(false);
            }
            else if (state == MultiplayerAccessState.TemporaryAccess)
            {
                if (statusText) statusText.text = "<color=#FFD700>TEMPORARY ACCESS ACTIVE</color>";
                UpdateTimerDisplay();

                ApplyOwnedState(buyOnlineAccessButton, buyOnlinePriceText, onlineAccessOwnedBadge, false);
                if (watchAdCard) watchAdCard.SetActive(true);
                if (watchAdButtonText) watchAdButtonText.text = "EXTEND PASS (+24H)";
            }
            else
            {
                if (statusText) statusText.text = "<color=#00E5FF>MULTIPLAYER ACCESS REQUIRED</color>";
                if (timerText) timerText.text = "Watch an ad for 24h or unlock permanent access";

                ApplyOwnedState(buyOnlineAccessButton, buyOnlinePriceText, onlineAccessOwnedBadge, false);
                if (watchAdCard) watchAdCard.SetActive(true);
                if (watchAdButtonText) watchAdButtonText.text = "WATCH AD (+24H PASS)";
            }

            // Check Remove Ads state
            bool adsRemoved = GameDataManager.Instance != null && GameDataManager.Instance.AdsRemoved;
            ApplyOwnedState(buyRemoveAdsButton, buyRemoveAdsPriceText, removeAdsOwnedBadge, adsRemoved);

            // 2. Enter Multiplayer Button State
            bool hasAccess = accessMgr.HasAccess();
            if (enterMultiplayerButton != null)
            {
                enterMultiplayerButton.interactable = hasAccess;
            }

            RefreshPrices();
        }

        private void ApplyOwnedState(Button btn, TextMeshProUGUI priceTxt, GameObject badge, bool isOwned)
        {
            if (isOwned)
            {
                if (btn != null) btn.interactable = false;
                if (priceTxt != null) priceTxt.text = "OWNED";
                if (badge != null) badge.SetActive(true);
            }
            else
            {
                if (btn != null) btn.interactable = true;
                if (badge != null) badge.SetActive(false);
            }
        }

        public void RefreshPrices()
        {
            if (IAPManager.Instance == null || !IAPManager.Instance.IsInitialized)
                return;

            // Online Access price
            bool onlineOwned = MultiplayerAccessManager.Instance != null &&
                               MultiplayerAccessManager.Instance.GetAccessState() == MultiplayerAccessState.PermanentAccess;
            if (!onlineOwned && buyOnlinePriceText != null)
            {
                string price = IAPManager.Instance.GetLocalizedPrice("online_access");
                if (!string.IsNullOrEmpty(price)) buyOnlinePriceText.text = price;
            }

            // Remove Ads price
            bool adsOwned = GameDataManager.Instance != null && GameDataManager.Instance.AdsRemoved;
            if (!adsOwned && buyRemoveAdsPriceText != null)
            {
                string price = IAPManager.Instance.GetLocalizedPrice("remove_ads");
                if (!string.IsNullOrEmpty(price)) buyRemoveAdsPriceText.text = price;
            }
        }

        private void UpdateTimerDisplay()
        {
            if (timerText == null || MultiplayerAccessManager.Instance == null) return;
            string rem = MultiplayerAccessManager.Instance.GetFormattedRemainingTime();
            timerText.text = $"Expires in: {rem}";
        }

        // ===================================================================
        #endregion

        // ===================================================================
        #region Actions & Integration Handlers
        // ===================================================================

        public void OnBuyOnlineAccessClicked()
        {
            buyOnlineAccessButton?.transform.DOPunchScale(new Vector3(-0.06f, -0.06f, 0f), 0.15f);
            InitiatePurchase("online_access");
        }

        public void OnBuyRemoveAdsClicked()
        {
            buyRemoveAdsButton?.transform.DOPunchScale(new Vector3(-0.06f, -0.06f, 0f), 0.15f);
            InitiatePurchase("remove_ads");
        }

        private void InitiatePurchase(string productId)
        {
            ShowLoading(true, "Connecting to store...");
            SetFeedback("Initiating secure transaction...");

            if (IAPManager.Instance == null)
            {
                ShowLoading(false);
                SetFeedback("Store unavailable. Please check internet connection.", isError: true);
                return;
            }

            IAPManager.Instance.BuyProduct(productId);
        }

        public void OnWatchAdClicked()
        {
            if (_isAdLoading) return;

            watchAdButton?.transform.DOPunchScale(new Vector3(-0.06f, -0.06f, 0f), 0.15f);

            var accessMgr = MultiplayerAccessManager.Instance;
            if (accessMgr == null)
            {
                SetFeedback("Access manager unavailable.", isError: true);
                return;
            }

            _isAdLoading = true;
            ShowLoading(true, "Loading rewarded video ad...");
            SetFeedback("Preparing ad presentation...");

            accessMgr.RequestRewardedAdAccess(success =>
            {
                _isAdLoading = false;
                ShowLoading(false);
                if (success)
                {
                    SetFeedback("<color=#00FF88>Ad watched! 24 Hours Multiplayer Access Granted!</color>");
                    RefreshUI();
                }
                else
                {
                    SetFeedback("Ad was not completed or failed. Access not granted.", isError: true);
                }
            });
        }

        public void OnEnterMultiplayerClicked()
        {
            enterMultiplayerButton?.transform.DOPunchScale(new Vector3(-0.06f, -0.06f, 0f), 0.15f);

            if (MultiplayerAccessManager.Instance != null && MultiplayerAccessManager.Instance.HasAccess())
            {
                Hide();
                SceneManager.LoadScene("MultiplayerMenu");
            }
            else
            {
                SetFeedback("Multiplayer access required.", isError: true);
                RefreshUI();
            }
        }

        private void OnRestorePurchasesClicked()
        {
            restorePurchasesButton?.transform.DOPunchScale(new Vector3(-0.08f, -0.08f, 0f), 0.15f);

            ShowLoading(true, "Restoring purchases...");
            SetFeedback("Checking previous entitlements with store...");

            if (IAPManager.Instance != null)
            {
                IAPManager.Instance.RestorePurchases();
            }

            DOVirtual.DelayedCall(3f, () =>
            {
                ShowLoading(false);
                SetFeedback("<color=#00FF88>Restore completed.</color>");
                RefreshUI();
            });
        }

        private void HandlePurchaseSuccess(string productId)
        {
            ShowLoading(false);
            SetFeedback("<color=#00FF88>Purchase Successful! Access Updated.</color>");
            RefreshUI();
        }

        private void HandlePurchaseFailed(string productId, string reason)
        {
            ShowLoading(false);
            SetFeedback($"Purchase failed: {reason}", isError: true);
            RefreshUI();
        }

        private void ShowLoading(bool show, string message = "Loading...")
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
}
