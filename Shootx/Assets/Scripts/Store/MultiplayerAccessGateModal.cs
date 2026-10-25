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
                    if (_instance == null)
                    {
                        var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
                        if (canvas != null)
                        {
                            var go = new GameObject("MultiplayerAccessGateModal");
                            go.transform.SetParent(canvas.transform, false);
                            _instance = go.AddComponent<MultiplayerAccessGateModal>();
                        }
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("=== Canvas Group & Roots ===")]
        [SerializeField] private CanvasGroup modalGroup;
        [SerializeField] private RectTransform modalBox;

        [Header("=== UI Texts ===")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI timerText;

        [Header("=== Action Buttons ===")]
        [SerializeField] private Button buyOnlineAccessButton;
        [SerializeField] private TextMeshProUGUI buyOnlinePriceText;

        [SerializeField] private Button buyRemoveAdsButton;
        [SerializeField] private TextMeshProUGUI buyRemoveAdsPriceText;

        [SerializeField] private Button watchAdButton;
        [SerializeField] private Button enterMultiplayerButton;
        [SerializeField] private Button closeButton;

        [Header("=== Feedback Message ===")]
        [SerializeField] private TextMeshProUGUI feedbackText;

        [Header("=== Animation Settings ===")]
        [SerializeField] private float animDuration = 0.3f;

        private Sequence _animSeq;
        private float _timerTick = 0f;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            if (modalGroup == null) modalGroup = GetComponent<CanvasGroup>();
            if (modalGroup == null) modalGroup = gameObject.AddComponent<CanvasGroup>();
            if (modalBox == null) modalBox = GetComponent<RectTransform>();

            if (buyOnlineAccessButton == null || watchAdButton == null)
            {
                BuildFallbackUI();
            }

            HideInstant();
        }

        private void Start()
        {
            WireButtons();
            MultiplayerAccessManager.OnAccessChanged += RefreshUI;
        }

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
        }

        private void OnDestroy()
        {
            MultiplayerAccessManager.OnAccessChanged -= RefreshUI;
            _animSeq?.Kill();
        }

        private void Update()
        {
            if (modalGroup != null && modalGroup.alpha > 0.05f)
            {
                _timerTick += Time.unscaledDeltaTime;
                if (_timerTick >= 1f)
                {
                    _timerTick = 0f;
                    UpdateTimerDisplay();
                }
            }
        }

        public void Show()
        {
            RefreshUI();
            ClearFeedback();

            if (modalGroup == null) return;

            _animSeq?.Kill();
            modalGroup.blocksRaycasts = true;
            modalGroup.interactable = true;

            if (modalBox != null)
            {
                modalBox.localScale = Vector3.one * 0.75f;
            }

            _animSeq = DOTween.Sequence().SetUpdate(true);
            _animSeq.Append(modalGroup.DOFade(1f, animDuration * 0.6f));
            if (modalBox != null)
            {
                _animSeq.Join(modalBox.DOScale(Vector3.one, animDuration).SetEase(Ease.OutBack));
            }
        }

        public void Hide()
        {
            if (modalGroup == null) return;

            modalGroup.blocksRaycasts = false;
            modalGroup.interactable = false;

            _animSeq?.Kill();
            _animSeq = DOTween.Sequence().SetUpdate(true);
            if (modalBox != null)
            {
                _animSeq.Append(modalBox.DOScale(Vector3.one * 0.8f, animDuration * 0.7f).SetEase(Ease.InBack));
            }
            _animSeq.Join(modalGroup.DOFade(0f, animDuration * 0.7f));
            _animSeq.OnComplete(HideInstant);
        }

        private void HideInstant()
        {
            if (modalGroup != null)
            {
                modalGroup.alpha = 0f;
                modalGroup.blocksRaycasts = false;
                modalGroup.interactable = false;
            }
            if (modalBox != null)
            {
                modalBox.localScale = Vector3.zero;
            }
        }

        public void RefreshUI()
        {
            var accessMgr = MultiplayerAccessManager.Instance;
            if (accessMgr == null) return;

            MultiplayerAccessState state = accessMgr.GetAccessState();

            if (state == MultiplayerAccessState.PermanentAccess)
            {
                if (statusText) statusText.text = "<color=#00FF88>Permanent Online Access Active</color>";
                if (timerText) timerText.text = "Unlimited Access";
                if (enterMultiplayerButton) enterMultiplayerButton.gameObject.SetActive(true);
                if (watchAdButton) watchAdButton.gameObject.SetActive(false);
            }
            else if (state == MultiplayerAccessState.TemporaryAccess)
            {
                if (statusText) statusText.text = "<color=#FFCC00>Temporary Access Active</color>";
                UpdateTimerDisplay();
                if (enterMultiplayerButton) enterMultiplayerButton.gameObject.SetActive(true);
                if (watchAdButton) watchAdButton.gameObject.SetActive(true);
            }
            else
            {
                if (statusText) statusText.text = "<color=#FF4444>No Online Access</color>";
                if (timerText) timerText.text = "Watch an ad for 24h or purchase access";
                if (enterMultiplayerButton) enterMultiplayerButton.gameObject.SetActive(false);
                if (watchAdButton) watchAdButton.gameObject.SetActive(true);
            }

            // Update IAP price labels if IAP is initialized
            if (IAPManager.Instance != null && IAPManager.Instance.IsInitialized)
            {
                string onlinePrice = IAPManager.Instance.GetLocalizedPrice("online_access");
                if (buyOnlinePriceText && !string.IsNullOrEmpty(onlinePrice))
                    buyOnlinePriceText.text = onlinePrice;

                string removeAdsPrice = IAPManager.Instance.GetLocalizedPrice("remove_ads");
                if (buyRemoveAdsPriceText && !string.IsNullOrEmpty(removeAdsPrice))
                    buyRemoveAdsPriceText.text = removeAdsPrice;
            }
        }

        private void UpdateTimerDisplay()
        {
            var accessMgr = MultiplayerAccessManager.Instance;
            if (accessMgr == null) return;

            MultiplayerAccessState state = accessMgr.GetAccessState();
            if (state == MultiplayerAccessState.TemporaryAccess)
            {
                if (timerText)
                    timerText.text = $"Expires in: {accessMgr.GetFormattedRemainingTime()}";
            }
        }

        private void OnBuyOnlineAccessClicked()
        {
            SetFeedback("Connecting to store...");
            if (IAPManager.Instance == null)
            {
                SetFeedback("Store unavailable.", isError: true);
                return;
            }
            IAPManager.Instance.BuyProduct("online_access");
        }

        private void OnBuyRemoveAdsClicked()
        {
            SetFeedback("Connecting to store...");
            if (IAPManager.Instance == null)
            {
                SetFeedback("Store unavailable.", isError: true);
                return;
            }
            IAPManager.Instance.BuyProduct("remove_ads");
        }

        private void OnWatchAdClicked()
        {
            SetFeedback("Loading Rewarded Video...");
            if (MultiplayerAccessManager.Instance == null)
            {
                SetFeedback("Access manager unavailable.", isError: true);
                return;
            }

            MultiplayerAccessManager.Instance.RequestRewardedAdAccess((success) =>
            {
                if (success)
                {
                    SetFeedback("<color=#00FF88>Success! 24 Hours Granted.</color>");
                    RefreshUI();
                }
                else
                {
                    SetFeedback("Ad not completed. Access was not granted.", isError: true);
                }
            });
        }

        private void OnEnterMultiplayerClicked()
        {
            var accessMgr = MultiplayerAccessManager.Instance;
            if (accessMgr != null && accessMgr.HasAccess())
            {
                Hide();
                Debug.Log("[MultiplayerAccess] Transitioning to MultiplayerMenu scene...");
                SceneManager.LoadScene("MultiplayerMenu");
            }
            else
            {
                SetFeedback("Access required to enter multiplayer.", isError: true);
                RefreshUI();
            }
        }

        public void SetFeedback(string msg, bool isError = false)
        {
            if (feedbackText != null)
            {
                feedbackText.text = isError ? $"<color=#FF4444>{msg}</color>" : msg;
                feedbackText.DOKill();
                feedbackText.transform.DOPunchScale(Vector3.one * 0.1f, 0.25f);
            }
        }

        public void ClearFeedback()
        {
            if (feedbackText != null)
                feedbackText.text = "";
        }

        #region Fallback UI Construction
        private void BuildFallbackUI()
        {
            // Ensure RectTransform on root
            var rootRect = GetComponent<RectTransform>();
            if (rootRect == null) rootRect = gameObject.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // Fullscreen backdrop
            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(transform, false);
            var bgRect = backdrop.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            backdrop.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.08f, 0.85f);

            // Modal Box
            var boxGo = new GameObject("ModalBox", typeof(RectTransform), typeof(Image));
            boxGo.transform.SetParent(transform, false);
            modalBox = boxGo.GetComponent<RectTransform>();
            modalBox.sizeDelta = new Vector2(460f, 560f);
            modalBox.anchoredPosition = Vector2.zero;
            boxGo.GetComponent<Image>().color = new Color(0.11f, 0.13f, 0.18f, 0.98f);

            // Close button top right
            var closeGo = CreateButton(boxGo.transform, "CloseBtn", "X", new Vector2(40f, 40f), new Vector2(200f, 250f), new Color(0.3f, 0.35f, 0.45f));
            closeButton = closeGo.GetComponent<Button>();

            // Title
            titleText = CreateTMP(boxGo.transform, "Title", "ONLINE ACCESS GATE", 24, true, TextAlignmentOptions.Center, new Vector2(400f, 40f), new Vector2(0f, 230f), new Color(0f, 0.9f, 1f));

            // Status Text
            statusText = CreateTMP(boxGo.transform, "Status", "Checking access...", 18, true, TextAlignmentOptions.Center, new Vector2(420f, 35f), new Vector2(0f, 185f), Color.white);

            // Timer Text
            timerText = CreateTMP(boxGo.transform, "Timer", "Expires in: --:--:--", 15, false, TextAlignmentOptions.Center, new Vector2(420f, 30f), new Vector2(0f, 150f), new Color(0.8f, 0.85f, 0.9f));

            // Action 1: Watch Ad (+24h)
            var adGo = CreateButton(boxGo.transform, "WatchAdBtn", "WATCH AD (+24H PASS)", new Vector2(380f, 54f), new Vector2(0f, 90f), new Color(0.06f, 0.65f, 0.35f));
            watchAdButton = adGo.GetComponent<Button>();

            // Action 2: Permanent Access
            var permGo = CreateButton(boxGo.transform, "BuyOnlineBtn", "ONLINE ACCESS PASS", new Vector2(380f, 54f), new Vector2(0f, 25f), new Color(0.12f, 0.45f, 0.85f));
            buyOnlineAccessButton = permGo.GetComponent<Button>();
            buyOnlinePriceText = CreateTMP(permGo.transform, "Price", "$1.99", 14, false, TextAlignmentOptions.Right, new Vector2(100f, 30f), new Vector2(130f, 0f), Color.yellow);

            // Action 3: Remove Ads + Access
            var noAdsGo = CreateButton(boxGo.transform, "BuyRemoveAdsBtn", "REMOVE ADS + ACCESS", new Vector2(380f, 54f), new Vector2(0f, -40f), new Color(0.85f, 0.55f, 0.1f));
            buyRemoveAdsButton = noAdsGo.GetComponent<Button>();
            buyRemoveAdsPriceText = CreateTMP(noAdsGo.transform, "Price", "$3.99", 14, false, TextAlignmentOptions.Right, new Vector2(100f, 30f), new Vector2(130f, 0f), Color.yellow);

            // Action 4: Enter Multiplayer (Active when access is valid)
            var enterGo = CreateButton(boxGo.transform, "EnterMultiplayerBtn", "ENTER MULTIPLAYER ->", new Vector2(380f, 52f), new Vector2(0f, -110f), new Color(0f, 0.75f, 0.9f));
            enterMultiplayerButton = enterGo.GetComponent<Button>();

            // Feedback Text
            feedbackText = CreateTMP(boxGo.transform, "Feedback", "", 14, false, TextAlignmentOptions.Center, new Vector2(400f, 35f), new Vector2(0f, -170f), Color.white);

            WireButtons();
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
            tmp.fontSize = 15;
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
        #endregion
    }
}
