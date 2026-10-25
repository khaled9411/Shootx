using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Purchasing;
using Unity.Services.Core;
using Shootx.Store;

public class IAPManager : MonoBehaviour
{
    public static IAPManager Instance { get; private set; }

    public static event Action<string> OnPurchaseSuccess;
    public static event Action<string, string> OnPurchaseFailedEvent;
    public static event Action OnInitializedEvent;

    [Header("=== Product Database ===")]
    [Tooltip("Add all IAP Product Definitions here")]
    [SerializeField]
    private List<IAPProductDefinition> products = new List<IAPProductDefinition>();

    private StoreController storeController;

    public bool IsInitialized => storeController != null;

    // The official 12 products required by specification
    private static readonly (string id, ProductType type)[] DefaultProducts = new (string, ProductType)[]
    {
        ("package_01", ProductType.NonConsumable),
        ("package_02", ProductType.NonConsumable),
        ("package_03", ProductType.NonConsumable),
        ("package_04", ProductType.NonConsumable),
        ("package_05", ProductType.NonConsumable),
        ("package_06", ProductType.NonConsumable),
        ("online_access", ProductType.NonConsumable),
        ("remove_ads", ProductType.NonConsumable),
        ("coins_200", ProductType.Consumable),
        ("coins_500", ProductType.Consumable),
        ("coins_1200", ProductType.Consumable),
        ("coins_2500", ProductType.Consumable)
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
        await InitializePurchasingAsync();
    }

    private void OnDestroy()
    {
        if (storeController == null)
            return;

        storeController.OnPurchasePending -= OnPurchasePending;
        storeController.OnPurchaseFailed -= OnPurchaseFailed;
        storeController.OnPurchasesFetched -= OnPurchasesFetched;
    }

    // ============================================================
    // INITIALIZATION
    // ============================================================

    public async Task InitializePurchasingAsync()
    {
        if (IsInitialized)
            return;

        try
        {
            Debug.Log("[IAP] Initializing Unity Services...");

            await UnityServices.InitializeAsync();

            Debug.Log("[IAP] Getting Store Controller...");

            storeController = UnityIAPServices.StoreController();

            // ----------------------------------------------------
            // Register events
            // ----------------------------------------------------

            storeController.OnPurchasePending += OnPurchasePending;
            storeController.OnPurchaseFailed += OnPurchaseFailed;
            storeController.OnPurchasesFetched += OnPurchasesFetched;

            // ----------------------------------------------------
            // Connect
            // ----------------------------------------------------

            Debug.Log("[IAP] Connecting to Store...");

            await storeController.Connect();

            // ----------------------------------------------------
            // Build product definitions
            // ----------------------------------------------------

            var productDefs = new List<ProductDefinition>();
            var registeredIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Add inspector products first
            foreach (var p in products)
            {
                if (p != null && !string.IsNullOrEmpty(p.productId) && registeredIds.Add(p.productId))
                {
                    productDefs.Add(new ProductDefinition(p.productId, p.productType));
                }
            }

            // Ensure all 12 official products are present
            foreach (var def in DefaultProducts)
            {
                if (registeredIds.Add(def.id))
                {
                    productDefs.Add(new ProductDefinition(def.id, def.type));
                }
            }

            // ----------------------------------------------------
            // Fetch products
            // ----------------------------------------------------

            Debug.Log($"[IAP] Fetching {productDefs.Count} Products...");

            storeController.FetchProducts(productDefs);

            Debug.Log("[IAP] Products fetch requested.");

            // ----------------------------------------------------
            // Restore purchases
            // ----------------------------------------------------

            Debug.Log("[IAP] Fetching previous purchases...");

            storeController.FetchPurchases();

            Debug.Log("[IAP] Initialization successful.");
            OnInitializedEvent?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[IAP] Initialization Failed:\n{e}");
        }
    }

    // ============================================================
    // BUY PRODUCT
    // ============================================================

    public void BuyProduct(string productId)
    {
        if (!IsInitialized)
        {
            Debug.LogError("[IAP] Purchase failed: IAP is not initialized.");
            OnPurchaseFailedEvent?.Invoke(productId, "Store not initialized. Please try again in a moment.");
            return;
        }

        if (string.IsNullOrEmpty(productId))
        {
            Debug.LogError("[IAP] Purchase failed: Product ID is empty.");
            return;
        }

        Debug.Log($"[IAP] Initiating purchase for: {productId}");
        storeController.PurchaseProduct(productId);
    }

    // ============================================================
    // RESTORE PURCHASES
    // ============================================================

    public void RestorePurchases()
    {
        if (!IsInitialized)
        {
            Debug.LogError("[IAP] Restore failed: IAP is not initialized.");
            return;
        }

        Debug.Log("[IAP] Restoring Purchases...");
        storeController.FetchPurchases();
    }

    // ============================================================
    // PRODUCT QUERIES & LOCALIZED PRICES
    // ============================================================

    public Product GetProduct(string productId)
    {
        if (!IsInitialized || storeController == null)
            return null;

        var products = storeController.GetProducts();
        if (products == null) return null;

        return products.FirstOrDefault(p => p != null && p.definition != null &&
            p.definition.id.Equals(productId, StringComparison.OrdinalIgnoreCase));
    }

    public string GetLocalizedPrice(string productId)
    {
        var product = GetProduct(productId);
        if (product != null && product.metadata != null && !string.IsNullOrEmpty(product.metadata.localizedPriceString))
        {
            return product.metadata.localizedPriceString;
        }

        // Return empty or fallback indicator if store metadata is not loaded yet
        return "";
    }

    // ============================================================
    // PURCHASE PENDING & CONFIRMATION
    // ============================================================

    private void OnPurchasePending(PendingOrder order)
    {
        if (order == null)
        {
            Debug.LogError("[IAP] Pending order is null.");
            return;
        }

        var cart = order.CartOrdered;
        if (cart == null)
        {
            Debug.LogError("[IAP] Pending order has no cart.");
            return;
        }

        foreach (var item in cart.Items())
        {
            Product product = item.Product;
            if (product == null) continue;

            string productId = product.definition.id;
            Debug.Log($"[IAP] Processing purchased product: {productId}");

            ProcessPurchasedProduct(product);
        }

        // Authoritatively confirm the pending order after rewards are granted and saved
        storeController.ConfirmPurchase(order);
        Debug.Log("[IAP] Purchase confirmed successfully.");
    }

    // ============================================================
    // PROCESS PURCHASE & DEDUPLICATION
    // ============================================================

    private void ProcessPurchasedProduct(Product product)
    {
        if (product == null || product.definition == null) return;

        string productId = product.definition.id;

        // Transaction Deduplication
#pragma warning disable CS0618
        string txId = !string.IsNullOrEmpty(product.transactionID) ? product.transactionID : product.receipt;
#pragma warning restore CS0618

        if (!string.IsNullOrEmpty(txId) && GameDataManager.Instance != null && GameDataManager.Instance.IsTransactionProcessed(txId))
        {
            Debug.LogWarning($"[IAP] Transaction {txId} for product {productId} already processed. Skipping duplicate grant.");
            return;
        }

        if (!string.IsNullOrEmpty(txId) && GameDataManager.Instance != null)
        {
            GameDataManager.Instance.RecordTransaction(txId);
        }

        GrantRewards(productId);
        OnPurchaseSuccess?.Invoke(productId);
    }

    // ============================================================
    // GRANT REWARDS
    // ============================================================

    public void GrantRewards(string productId)
    {
        if (string.IsNullOrEmpty(productId)) return;

        Debug.Log($"[IAP] Granting rewards for Product ID: {productId}");

        // 1. Check Consumable Coin Packs
        switch (productId.ToLowerInvariant())
        {
            case "coins_200":
                GameDataManager.Instance?.AddHardCurrency(200);
                Debug.Log("[IAP] Granted 200 Hard Currency.");
                return;

            case "coins_500":
                GameDataManager.Instance?.AddHardCurrency(500);
                Debug.Log("[IAP] Granted 500 Hard Currency.");
                return;

            case "coins_1200":
                GameDataManager.Instance?.AddHardCurrency(1200);
                Debug.Log("[IAP] Granted 1,200 Hard Currency.");
                return;

            case "coins_2500":
                GameDataManager.Instance?.AddHardCurrency(2500);
                Debug.Log("[IAP] Granted 2,500 Hard Currency.");
                return;

            case "online_access":
                MultiplayerAccessManager.Instance?.GrantPermanentAccess();
                GameDataManager.Instance?.UnlockMultiplayer();
                Debug.Log("[IAP] Granted Permanent Online Access.");
                return;

            case "remove_ads":
                GameDataManager.Instance?.RemoveAds();
                MultiplayerAccessManager.Instance?.GrantPermanentAccess();
                Debug.Log("[IAP] Granted Remove Ads AND Permanent Online Access.");
                return;
        }

        // 2. Check Bundles (package_01 through package_06)
        if (productId.StartsWith("package_", StringComparison.OrdinalIgnoreCase))
        {
            var bundle = StoreCatalogDatabase.Instance != null
                ? StoreCatalogDatabase.Instance.GetBundleByProductId(productId)
                : null;

            if (bundle != null)
            {
                bundle.GrantBundleRewards();
                Debug.Log($"[IAP] Granted Bundle {productId} via StoreCatalogDatabase ({bundle.bundleName}).");
                return;
            }

            // Fallback to IAPProductDefinition if configured
            var def = GetProductDefinition(productId);
            if (def != null)
            {
                GrantDefinitionFallback(def);
                return;
            }

            Debug.LogWarning($"[IAP] Bundle definition for {productId} not found in database.");
        }
    }

    private void GrantDefinitionFallback(IAPProductDefinition def)
    {
        if (def == null || GameDataManager.Instance == null) return;

        if (def.hardCoinsAmount > 0)
            GameDataManager.Instance.AddHardCurrency(def.hardCoinsAmount);

        if (def.removesAds)
            GameDataManager.Instance.RemoveAds();

        if (def.unlocksMultiplayer)
            MultiplayerAccessManager.Instance?.GrantPermanentAccess();

        if (def.unlockGunIds != null)
        {
            foreach (var gunId in def.unlockGunIds)
                GameDataManager.Instance.AddOwnedGun(gunId);
        }

        if (def.unlockOutfitIds != null)
        {
            foreach (var outfitId in def.unlockOutfitIds)
                GameDataManager.Instance.AddOwnedOutfit(outfitId);
        }
    }

    // ============================================================
    // PURCHASES FETCHED / RESTORE
    // ============================================================

    private void OnPurchasesFetched(Orders orders)
    {
        Debug.Log("[IAP] Purchases fetched.");

        if (orders == null || orders.ConfirmedOrders == null)
        {
            Debug.LogWarning("[IAP] Orders is null.");
            return;
        }

        foreach (var order in orders.ConfirmedOrders)
        {
            if (order == null || order.CartOrdered == null)
                continue;

            foreach (var item in order.CartOrdered.Items())
            {
                Product product = item.Product;
                if (product == null || product.definition == null) continue;

                string productId = product.definition.id;

                // Only restore Non-Consumable products
                if (product.definition.type == ProductType.NonConsumable)
                {
                    Debug.Log($"[IAP] Restoring Non-Consumable: {productId}");
                    GrantRewards(productId);
                }
            }
        }

        Debug.Log("[IAP] Purchases restored successfully.");
    }

    // ============================================================
    // PURCHASE FAILED
    // ============================================================

    private void OnPurchaseFailed(FailedOrder failedOrder)
    {
        string reason = failedOrder != null ? failedOrder.ToString() : "Unknown purchase error";
        Debug.LogError($"[IAP] Purchase Failed: {reason}");
        OnPurchaseFailedEvent?.Invoke("", reason);
    }

    // ============================================================
    // PRODUCT DATABASE
    // ============================================================

    private IAPProductDefinition GetProductDefinition(string productId)
    {
        return products.Find(p => p != null && p.productId.Equals(productId, StringComparison.OrdinalIgnoreCase));
    }
}