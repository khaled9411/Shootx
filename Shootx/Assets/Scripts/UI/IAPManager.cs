using UnityEngine;

public class IAPManager : MonoBehaviour
{
    public static IAPManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void BuyProduct(string productId)
    {
        Debug.Log($"[IAP] Purchasing: {productId}");
    }

    public void BuyNoAds()
    {
        Debug.Log("[IAP] Buying No Ads");
    }

    public void RestorePurchases()
    {
        Debug.Log("[IAP] Restoring Purchases");
    }
}