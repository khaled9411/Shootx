using Shootx.Store;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.SceneManagement;

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
    private bool _isStoreConnected = false;
    private bool _areProductsFetched = false;

    public bool IsInitialized => storeController != null && _isStoreConnected && _areProductsFetched;

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
        SceneManager.activeSceneChanged += OnactiveSceneChanged;
    }

    private void OnactiveSceneChanged(Scene preScene, Scene CurrentScene)
    {
        if (CurrentScene.buildIndex == 3)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
        SceneManager.activeSceneChanged -= OnactiveSceneChanged;
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void UnsubscribeEvents()
    {
        if (storeController == null)
            return;

        storeController.OnStoreConnected -= OnStoreConnected;
        storeController.OnStoreDisconnected -= OnStoreDisconnected;
        storeController.OnProductsFetched -= OnProductsFetched;
        storeController.OnProductsFetchFailed -= OnProductsFetchFailed;
        storeController.OnPurchasesFetched -= OnPurchasesFetched;
        storeController.OnPurchasesFetchFailed -= OnPurchasesFetchFailed;
        storeController.OnPurchasePending -= OnPurchasePending;
        storeController.OnPurchaseConfirmed -= OnPurchaseConfirmed;
        storeController.OnPurchaseFailed -= OnPurchaseFailed;
        storeController.OnPurchaseDeferred -= OnPurchaseDeferred;
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

            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            Debug.Log("[IAP] Getting Store Controller...");

            storeController = UnityIAPServices.StoreController();

            // ----------------------------------------------------
            // 1. Subscribe to ALL required events BEFORE Connect
            // ----------------------------------------------------
            UnsubscribeEvents();

            storeController.OnStoreConnected += OnStoreConnected;
            storeController.OnStoreDisconnected += OnStoreDisconnected;
            storeController.OnProductsFetched += OnProductsFetched;
            storeController.OnProductsFetchFailed += OnProductsFetchFailed;
            storeController.OnPurchasesFetched += OnPurchasesFetched;
            storeController.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
            storeController.OnPurchasePending += OnPurchasePending;
            storeController.OnPurchaseConfirmed += OnPurchaseConfirmed;
            storeController.OnPurchaseFailed += OnPurchaseFailed;
            storeController.OnPurchaseDeferred += OnPurchaseDeferred;

            // ----------------------------------------------------
            // 2. Connect to the Store
            // ----------------------------------------------------
            Debug.Log("[IAP] Connecting to Store...");
            await storeController.Connect();
            Debug.Log("[IAP] Store Connect initiated. Awaiting OnStoreConnected...");
        }
        catch (Exception e)
        {
            Debug.LogError($"[IAP] Initialization Failed:\n{e}");
        }
    }

    // ============================================================
    // STORE LIFECYCLE EVENTS
    // ============================================================

    private void OnStoreConnected()
    {
        Debug.Log("[IAP] Store successfully connected!");
        _isStoreConnected = true;

        FetchCatalogProducts();
    }

    private void OnStoreDisconnected(StoreConnectionFailureDescription failure)
    {
        _isStoreConnected = false;
        _areProductsFetched = false;
        Debug.LogError($"[IAP] Store disconnected: {failure.Message}");
    }

    private void FetchCatalogProducts()
    {
        if (storeController == null) return;

        // Auto-load product definitions from Resources if inspector list is empty
        if (products == null || products.Count == 0)
        {
            var loaded = Resources.LoadAll<IAPProductDefinition>("IAPProducts");
            if (loaded != null && loaded.Length > 0)
            {
                products = new List<IAPProductDefinition>(loaded);
            }
        }

        var productDefs = new List<ProductDefinition>();
        var registeredIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Add inspector products first
        if (products != null)
        {
            foreach (var p in products)
            {
                if (p != null && !string.IsNullOrEmpty(p.productId) && registeredIds.Add(p.productId))
                {
                    productDefs.Add(new ProductDefinition(p.productId, p.productType));
                }
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

        Debug.Log($"[IAP] Fetching {productDefs.Count} Products from store...");
        storeController.FetchProducts(productDefs);
    }

    private void OnProductsFetched(List<Product> fetchedProducts)
    {
        int count = fetchedProducts != null ? fetchedProducts.Count : 0;
        Debug.Log($"[IAP] Products successfully fetched: {count} products available.");
        _areProductsFetched = true;

        OnInitializedEvent?.Invoke();

        // After products are fetched, fetch existing purchases to restore entitlements
        Debug.Log("[IAP] Fetching previous purchases...");
        storeController.FetchPurchases();
    }

    private void OnProductsFetchFailed(ProductFetchFailed failure)
    {
        Debug.LogError($"[IAP] Products fetch failed: {failure.FailureReason}");
    }

    // ============================================================
    // BUY PRODUCT
    // ============================================================

    public async void BuyProduct(string productId)
    {
        if (string.IsNullOrEmpty(productId))
        {
            Debug.LogError("[IAP] Purchase failed: Product ID is empty.");
            return;
        }

        if (!IsInitialized)
        {
            Debug.Log($"[IAP] Store not fully initialized yet. Waiting for store connection to buy '{productId}'...");

            float timeout = 5f;
            float elapsed = 0f;
            while (!IsInitialized && elapsed < timeout)
            {
                await Task.Delay(250);
                elapsed += 0.25f;
            }

            if (!IsInitialized)
            {
                Debug.LogError("[IAP] Purchase failed: IAP is not initialized after waiting.");
                OnPurchaseFailedEvent?.Invoke(productId, "The store is not yet initialized; please try again in a moment.");
                return;
            }
        }

        Product product = GetProduct(productId);
        if (product == null)
        {
            Debug.LogWarning($"[IAP] Product definition for '{productId}' not cached yet. Attempting direct purchase by ID...");
            storeController.PurchaseProduct(productId);
            return;
        }

        Debug.Log($"[IAP] Initiating purchase for: {productId}");
        storeController.PurchaseProduct(product);
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

        if (Application.platform == RuntimePlatform.IPhonePlayer ||
            Application.platform == RuntimePlatform.OSXPlayer)
        {
            storeController.RestoreTransactions((success, error) =>
            {
                if (success)
                {
                    Debug.Log("[IAP] Apple transactions restore completed successfully.");
                }
                else
                {
                    Debug.LogError($"[IAP] Apple transactions restore failed: {error}");
                }
            });
        }
        else
        {
            storeController.FetchPurchases();
        }
    }

    // ============================================================
    // PRODUCT QUERIES & LOCALIZED PRICES
    // ============================================================

    public Product GetProduct(string productId)
    {
        if (storeController == null)
            return null;

        var allProducts = storeController.GetProducts();
        if (allProducts == null) return null;

        return allProducts.FirstOrDefault(p => p != null && p.definition != null &&
            p.definition.id.Equals(productId, StringComparison.OrdinalIgnoreCase));
    }

    public string GetLocalizedPrice(string productId)
    {
        var product = GetProduct(productId);
        if (product != null && product.metadata != null && !string.IsNullOrEmpty(product.metadata.localizedPriceString))
        {
            return product.metadata.localizedPriceString;
        }

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

            ProcessPurchasedProduct(product, order.Info?.TransactionID);
        }

        // Authoritatively confirm the pending order after rewards are granted and saved
        storeController.ConfirmPurchase(order);
        Debug.Log("[IAP] Purchase confirmed successfully.");
    }

    private void OnPurchaseConfirmed(Order order)
    {
        switch (order)
        {
            case ConfirmedOrder confirmedOrder:
                Debug.Log($"[IAP] Order confirmed: {confirmedOrder.Info?.TransactionID}");
                break;
            case FailedOrder failedOrder:
                Debug.LogError($"[IAP] Order confirmation failed: {failedOrder.FailureReason} - {failedOrder.Details}");
                break;
        }
    }

    private void OnPurchaseFailed(FailedOrder failedOrder)
    {
        string reason = failedOrder != null ? $"{failedOrder.FailureReason} - {failedOrder.Details}" : "Unknown purchase error";
        Debug.LogError($"[IAP] Purchase Failed: {reason}");
        OnPurchaseFailedEvent?.Invoke("", reason);
    }

    private void OnPurchaseDeferred(DeferredOrder deferredOrder)
    {
        Debug.Log("[IAP] Purchase is deferred (pending parent or bank approval).");
    }

    // ============================================================
    // PROCESS PURCHASE & DEDUPLICATION
    // ============================================================

    private void ProcessPurchasedProduct(Product product, string orderId)
    {
        if (product == null || product.definition == null) return;

        string productId = product.definition.id;

        // Transaction Deduplication
#pragma warning disable CS0618
        string txId = !string.IsNullOrEmpty(orderId) ? orderId : (!string.IsNullOrEmpty(product.transactionID) ? product.transactionID : product.receipt);
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

    private void OnPurchasesFetchFailed(PurchasesFetchFailureDescription failure)
    {
        Debug.LogError($"[IAP] Purchases fetch failed: {failure.Message} ({failure.FailureReason})");
    }

    // ============================================================
    // PRODUCT DATABASE
    // ============================================================

    private IAPProductDefinition GetProductDefinition(string productId)
    {
        return products.Find(p => p != null && p.productId.Equals(productId, StringComparison.OrdinalIgnoreCase));
    }
}
