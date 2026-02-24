using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopPopupController : MonoBehaviour
{
    // ===================================================================
    #region Data Types
    // ===================================================================

    [System.Serializable]
    public class ShopOfferUI
    {
        public GameObject root;
        public TextMeshProUGUI amountText;
        public Image offerImage;
        public Button buyButton;
        public TextMeshProUGUI priceText;
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Inspector Fields
    // ===================================================================

    [Header("=== Header ===")]
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("=== Offers ===")]
    [SerializeField] private List<ShopOfferUI> offers;

    [Header("=== Shop Type ===")]
    [SerializeField] private bool isSoftCurrencyShop = true;

    // ===================================================================
    #endregion

    // ===================================================================
    #region Private State
    // ===================================================================

    private List<GameDataManager.ShopOffer> offerDataList;

    // ===================================================================
    #endregion

    // ===================================================================
    #region Unity Lifecycle
    // ===================================================================

    private void Start()
    {
        closeButton?.onClick.AddListener(UIManager.Instance.HideShopPopup);
    }

    private void OnEnable()
    {
        RefreshOffers();
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Public API
    // ===================================================================

    public void OpenAsSoftCurrencyShop()
    {
        isSoftCurrencyShop = true;
        RefreshOffers();
    }

    public void OpenAsHardCurrencyShop()
    {
        isSoftCurrencyShop = false;
        RefreshOffers();
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Refresh
    // ===================================================================

    private void RefreshOffers()
    {
        if (GameDataManager.Instance == null) return;

        offerDataList = isSoftCurrencyShop
            ? GameDataManager.Instance.SoftCurrencyOffers
            : GameDataManager.Instance.HardCurrencyOffers;

        for (int i = 0; i < offers.Count; i++)
        {
            bool hasData = offerDataList != null && i < offerDataList.Count;

            if (offers[i].root != null)
                offers[i].root.SetActive(hasData);

            if (!hasData) continue;

            GameDataManager.ShopOffer data = offerDataList[i];
            int index = i; // capture

            if (offers[i].amountText != null) offers[i].amountText.text = $"+{data.amount}";
            if (offers[i].offerImage != null) offers[i].offerImage.sprite = data.icon;
            if (offers[i].priceText != null) offers[i].priceText.text = data.priceLabel;

            offers[i].buyButton?.onClick.RemoveAllListeners();
            offers[i].buyButton?.onClick.AddListener(() => OnBuyPressed(offerDataList[index]));
        }
    }

    private void OnBuyPressed(GameDataManager.ShopOffer offer)
    {
        Debug.Log($"[Shop] Buying: {offer.productId}");
        IAPManager.Instance?.BuyProduct(offer.productId);
    }

    // ===================================================================
    #endregion
}