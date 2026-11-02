using Shootx.Store;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Purchasing;

public enum IAPInitializationState
{
    NotInitialized,
    Initializing,
    StoreConnected,
    ProductsFetched,
    Ready,
    Failed,
    Unavailable
}

public class IAPManager : MonoBehaviour
{
    public static IAPManager Instance { get; private set; }

    public static event Action<string> OnPurchaseSuccess;
    public static event Action<string, string> OnPurchaseFailedEvent;
    public static event Action OnInitializedEvent;
    public static event Action<IAPInitializationState> OnStateChangedEvent;

    [Header("=== Product Database ===")]
    [Tooltip("Add all IAP Product Definitions here (or auto-loaded from Resources)")]
    [SerializeField]
    private List<IAPProductDefinition> products = new List<IAPProductDefinition>();

    // Centralized State Machine
    public IAPInitializationState CurrentState { get; private set; } = IAPInitializationState.NotInitialized;
    public bool IsInitialized => CurrentState == IAPInitializationState.Ready;
    public bool AreProductsFetched => _areProductsFetched;
    public string LastInitializationError { get; private set; } = string.Empty;

    private StoreController _storeController;
    private bool _isStoreConnected = false;
    private bool _areProductsFetched = false;
    private bool _isPurchaseInProgress = false;

    private TaskCompletionSource<bool> _initializationTcs;
    private TaskCompletionSource<bool> _productFetchTcs;
    private readonly Dictionary<string, bool> _productAvailability = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

    // The official 12 products required by specification
    public static readonly (string id, ProductType type)[] OfficialProducts = new (string, ProductType)[]
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
        // Begin initialization early on application bootstrap
        await EnsureInitializedAsync();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void SetState(IAPInitializationState newState, string errorMsg = "")
    {
        CurrentState = newState;
        if (!string.IsNullOrEmpty(errorMsg))
        {
            LastInitializationError = errorMsg;
        }

        Debug.Log($"[IAP] State transition: {newState}" + (!string.IsNullOrEmpty(errorMsg) ? $" | Error: {errorMsg}" : ""));
        OnStateChangedEvent?.Invoke(newState);
    }

    private void UnsubscribeEvents()
    {
        if (_storeController == null)
            return;

        _storeController.OnStoreConnected -= OnStoreConnected;
        _storeController.OnStoreDisconnected -= OnStoreDisconnected;
        _storeController.OnProductsFetched -= OnProductsFetched;
        _storeController.OnProductsFetchFailed -= OnProductsFetchFailed;
        _storeController.OnPurchasesFetched -= OnPurchasesFetched;
        _storeController.OnPurchasesFetchFailed -= OnPurchasesFetchFailed;
        _storeController.OnPurchasePending -= OnPurchasePending;
        _storeController.OnPurchaseConfirmed -= OnPurchaseConfirmed;
        _storeController.OnPurchaseFailed -= OnPurchaseFailed;
        _storeController.OnPurchaseDeferred -= OnPurchaseDeferred;
    }

    private void SubscribeEvents()
    {
        if (_storeController == null)
            return;

        _storeController.OnStoreConnected += OnStoreConnected;
        _storeController.OnStoreDisconnected += OnStoreDisconnected;
        _storeController.OnProductsFetched += OnProductsFetched;
        _storeController.OnProductsFetchFailed += OnProductsFetchFailed;
        _storeController.OnPurchasesFetched += OnPurchasesFetched;
        _storeController.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
        _storeController.OnPurchasePending += OnPurchasePending;
        _storeController.OnPurchaseConfirmed += OnPurchaseConfirmed;
        _storeController.OnPurchaseFailed += OnPurchaseFailed;
        _storeController.OnPurchaseDeferred += OnPurchaseDeferred;
    }

    // ===================================================================
    #region Centralized Initialization Pipeline
    // ===================================================================

    /// <summary>
    /// Thread-safe and race-condition free initialization manager.
    /// Re-entrant: Multiple components can call this simultaneously and will await the same operation.
    /// </summary>
    public async Task<bool> EnsureInitializedAsync(bool forceRetry = false)
    {
        if (IsInitialized && !forceRetry)
        {
            return true;
        }

        if (CurrentState == IAPInitializationState.Initializing)
        {
            Debug.Log("[IAP] Initialization already in progress. Awaiting existing task...");
            if (_initializationTcs != null)
            {
                return await _initializationTcs.Task;
            }
        }

        _initializationTcs = new TaskCompletionSource<bool>();
        SetState(IAPInitializationState.Initializing);

        try
        {
            Debug.Log($"[IAP] Starting IAP Initialization on {Application.platform} (Unity IAP v5.4.3)...");

            // 1. Optional Unity Services Core initialization
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    Debug.Log("[IAP] Initializing Unity Services Core...");
                    await UnityServices.InitializeAsync();
                    Debug.Log("[IAP] Unity Services Core initialized successfully.");
                }
            }
            catch (Exception ugsEx)
            {
                Debug.LogWarning($"[IAP] Unity Services Core optional initialization warning: {ugsEx.Message}. Proceeding with native store connection.");
            }

            // 2. Instantiate and wire StoreController
            Debug.Log("[IAP] Getting StoreController from UnityIAPServices...");
            _storeController = UnityIAPServices.StoreController();

            UnsubscribeEvents();
            SubscribeEvents();

            // 3. Connect to Store
            Debug.Log("[IAP] Connecting to platform store (Google Play / Apple StoreKit)...");
            var connectTask = _storeController.Connect();

            // Wait for store connection task
            await connectTask;

            // Wait until _isStoreConnected is confirmed or timeout after 10s
            float connectTimer = 0f;
            while (!_isStoreConnected && connectTimer < 10f)
            {
                await Task.Delay(200);
                connectTimer += 0.2f;
            }

            if (!_isStoreConnected)
            {
                string errMsg = "Failed to establish store connection within timeout.";
                Debug.LogError($"[IAP] {errMsg}");
                SetState(IAPInitializationState.Failed, errMsg);
                _initializationTcs.TrySetResult(false);
                return false;
            }

            // 4. Fetch Products Catalog
            Debug.Log("[IAP] Store connected. Fetching products catalog...");
            bool productsFetched = await FetchCatalogProductsAsync(15f);

            if (!productsFetched && _productAvailability.Count(p => p.Value) == 0)
            {
                string errMsg = "Products could not be fetched from the store.";
                Debug.LogError($"[IAP] {errMsg}");
                SetState(IAPInitializationState.Failed, errMsg);
                _initializationTcs.TrySetResult(false);
                return false;
            }

            SetState(IAPInitializationState.Ready);
            OnInitializedEvent?.Invoke();

            _initializationTcs.TrySetResult(true);
            return true;
        }
        catch (Exception ex)
        {
            string errMsg = $"Exception during IAP initialization: {ex.Message}";
            Debug.LogError($"[IAP] {errMsg}\n{ex}");
            SetState(IAPInitializationState.Failed, errMsg);
            _initializationTcs.TrySetResult(false);
            return false;
        }
    }

    public async Task InitializePurchasingAsync()
    {
        await EnsureInitializedAsync();
    }

    #endregion

    // ===================================================================
    #region Product Catalog & Fetching
    // ===================================================================

    private async Task<bool> FetchCatalogProductsAsync(float timeoutSeconds)
    {
        if (_storeController == null) return false;

        _productFetchTcs = new TaskCompletionSource<bool>();

        // 1. Load ScriptableObject Product Definitions from Resources
        if (products == null || products.Count == 0)
        {
            var loaded = Resources.LoadAll<IAPProductDefinition>("IAPProducts");
            if (loaded != null && loaded.Length > 0)
            {
                products = new List<IAPProductDefinition>(loaded);
                Debug.Log($"[IAP] Loaded {products.Count} IAPProductDefinition assets from Resources/IAPProducts.");
            }
        }

        // 2. Build Registration List
        var productDefs = new List<ProductDefinition>();
        var registeredIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Add inspector / resource definitions first
        if (products != null)
        {
            foreach (var p in products)
            {
                if (p != null && !string.IsNullOrEmpty(p.productId) && registeredIds.Add(p.productId))
                {
                    productDefs.Add(new ProductDefinition(p.productId, p.productId, p.productType, true));
                }
            }
        }

        // Ensure all 12 official products are present
        foreach (var def in OfficialProducts)
        {
            if (registeredIds.Add(def.id))
            {
                productDefs.Add(new ProductDefinition(def.id, def.id, def.type, true));
            }
        }

        Debug.Log($"[IAP] Requesting {productDefs.Count} products from store catalog...");
        _storeController.FetchProducts(productDefs);

        // Await fetch with safety timeout
        var delayTask = Task.Delay((int)(timeoutSeconds * 1000));
        var completedTask = await Task.WhenAny(_productFetchTcs.Task, delayTask);

        if (completedTask == _productFetchTcs.Task)
        {
            return await _productFetchTcs.Task;
        }
        else
        {
            Debug.LogWarning($"[IAP] FetchProducts timed out after {timeoutSeconds}s. Proceeding with currently cached products.");
            return _productAvailability.Count(p => p.Value) > 0;
        }
    }

    private void OnStoreConnected()
    {
        Debug.Log("[IAP] Store connection established successfully.");
        _isStoreConnected = true;
        SetState(IAPInitializationState.StoreConnected);
    }

    private void OnStoreDisconnected(StoreConnectionFailureDescription failure)
    {
        _isStoreConnected = false;
        _areProductsFetched = false;
        string err = failure != null ? failure.Message : "Store disconnected";
        Debug.LogError($"[IAP] Store disconnected: {err}");
        SetState(IAPInitializationState.Unavailable, err);
    }

    private void OnProductsFetched(List<Product> fetchedProducts)
    {
        int count = fetchedProducts != null ? fetchedProducts.Count : 0;
        Debug.Log($"[IAP] OnProductsFetched received: {count} products retrieved from store.");
        _areProductsFetched = true;

        if (fetchedProducts != null)
        {
            foreach (var product in fetchedProducts)
            {
                if (product != null && product.definition != null)
                {
                    string pid = product.definition.id;
                    _productAvailability[pid] = product.availableToPurchase;
                    Debug.Log($"[IAP] Product '{pid}': Available = {product.availableToPurchase}, Price = '{product.metadata?.localizedPriceString}', Type = {product.definition.type}");
                }
            }
        }

        SetState(IAPInitializationState.ProductsFetched);
        _productFetchTcs?.TrySetResult(true);

        // Fetch existing purchases to restore entitlements
        Debug.Log("[IAP] Fetching previous purchases to synchronize entitlements...");
        _storeController?.FetchPurchases();
    }

    private void OnProductsFetchFailed(ProductFetchFailed failure)
    {
        string reason = failure != null ? failure.FailureReason.ToString() : "Unknown failure";
        Debug.LogWarning($"[IAP] Products fetch warning/failed: {reason}");

        // If at least one product was previously cached/fetched, consider it partially available
        bool hasAnyAvailable = _productAvailability.Count(p => p.Value) > 0;
        _productFetchTcs?.TrySetResult(hasAnyAvailable);
    }

    #endregion

    // ===================================================================
    #region Purchase Processing Pipeline
    // ===================================================================

    /// <summary>
    /// Authoritative asynchronous purchase method with guaranteed validation and UI safety.
    /// </summary>
    public async Task<bool> BuyProductAsync(string productId)
    {
        if (string.IsNullOrEmpty(productId))
        {
            Debug.LogError("[IAP] Purchase failed: Product ID is empty.");
            OnPurchaseFailedEvent?.Invoke("", "Invalid product ID.");
            return false;
        }

        if (_isPurchaseInProgress)
        {
            Debug.LogWarning($"[IAP] Purchase already in progress. Ignoring duplicate request for '{productId}'.");
            return false;
        }

        _isPurchaseInProgress = true;

        try
        {
            Debug.Log($"[IAP] Purchase initiated for '{productId}'. Checking store readiness...");

            // 1. Ensure Store is initialized
            if (!IsInitialized)
            {
                Debug.Log("[IAP] Store not initialized yet. Attempting to ensure initialization...");
                bool initialized = await EnsureInitializedAsync();
                if (!initialized || !IsInitialized)
                {
                    string err = string.IsNullOrEmpty(LastInitializationError)
                        ? "Store is not yet initialized; please check your internet connection."
                        : $"Store initialization failed: {LastInitializationError}";
                    Debug.LogError($"[IAP] Purchase aborted: {err}");
                    OnPurchaseFailedEvent?.Invoke(productId, err);
                    _isPurchaseInProgress = false;
                    return false;
                }
            }

            // 2. Check Product Existence and Availability
            Product product = GetProduct(productId);
            if (product == null)
            {
                Debug.LogWarning($"[IAP] Product '{productId}' not found in cached catalog. Attempting direct lookup...");
                product = _storeController?.GetProductByCatalogListingId(productId);
            }

            if (product == null)
            {
                string err = $"Product '{productId}' is not registered or unavailable in the store.";
                Debug.LogError($"[IAP] Purchase failed: {err}");
                OnPurchaseFailedEvent?.Invoke(productId, err);
                _isPurchaseInProgress = false;
                return false;
            }

            if (!product.availableToPurchase)
            {
                string err = $"Product '{productId}' is currently unavailable for purchase in this store region.";
                Debug.LogWarning($"[IAP] Purchase failed: {err}");
                OnPurchaseFailedEvent?.Invoke(productId, err);
                _isPurchaseInProgress = false;
                return false;
            }

            // 3. Initiate native purchase
            Debug.Log($"[IAP] Launching platform purchase dialog for: {productId} ({product.metadata?.localizedPriceString})");
            _storeController.PurchaseProduct(product);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[IAP] Purchase exception for '{productId}': {ex.Message}\n{ex}");
            OnPurchaseFailedEvent?.Invoke(productId, ex.Message);
            _isPurchaseInProgress = false;
            return false;
        }
    }

    /// <summary>
    /// Legacy wrapper for backward compatibility with existing UI callers.
    /// </summary>
    public async void BuyProduct(string productId)
    {
        await BuyProductAsync(productId);
    }

    private void OnPurchasePending(PendingOrder order)
    {
        if (order == null)
        {
            Debug.LogError("[IAP] Pending order is null.");
            _isPurchaseInProgress = false;
            return;
        }

        var cart = order.CartOrdered;
        if (cart == null)
        {
            Debug.LogError("[IAP] Pending order has no cart.");
            _isPurchaseInProgress = false;
            return;
        }

        string orderTxId = order.Info?.TransactionID;
        Debug.Log($"[IAP] OnPurchasePending received for Transaction: '{orderTxId}'");

        foreach (var item in cart.Items())
        {
            Product product = item.Product;
            if (product == null || product.definition == null) continue;

            string productId = product.definition.id;
            Debug.Log($"[IAP] Processing pending cart item: {productId}");

            ProcessPurchasedProduct(product, orderTxId);
        }

        // Authoritatively confirm the pending order after rewards are granted and saved
        Debug.Log($"[IAP] Confirming purchase order: {orderTxId}...");
        _storeController.ConfirmPurchase(order);
        _isPurchaseInProgress = false;
    }

    private void OnPurchaseConfirmed(Order order)
    {
        _isPurchaseInProgress = false;
        switch (order)
        {
            case ConfirmedOrder confirmedOrder:
                Debug.Log($"[IAP] Purchase confirmed successfully. TransactionID: {confirmedOrder.Info?.TransactionID}");
                break;
            case FailedOrder failedOrder:
                string err = failedOrder != null ? $"{failedOrder.FailureReason} - {failedOrder.Details}" : "Unknown confirmation error";
                Debug.LogError($"[IAP] Purchase confirmation failed: {err}");
                break;
        }
    }

    private void OnPurchaseFailed(FailedOrder failedOrder)
    {
        _isPurchaseInProgress = false;
        string reason = failedOrder != null ? $"{failedOrder.FailureReason} - {failedOrder.Details}" : "Purchase failed or canceled";
        Debug.LogWarning($"[IAP] Purchase Failed: {reason}");
        OnPurchaseFailedEvent?.Invoke("", reason);
    }

    private void OnPurchaseDeferred(DeferredOrder deferredOrder)
    {
        _isPurchaseInProgress = false;
        Debug.Log("[IAP] Purchase is deferred (awaiting external or parental approval).");
    }

    #endregion

    // ===================================================================
    #region Transaction Deduplication & Reward Granting
    // ===================================================================

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
            Debug.LogWarning($"[IAP] Transaction '{txId}' for product '{productId}' was already processed. Skipping duplicate reward grant.");
            return;
        }

        if (!string.IsNullOrEmpty(txId) && GameDataManager.Instance != null)
        {
            GameDataManager.Instance.RecordTransaction(txId);
            Debug.Log($"[IAP] Recorded transaction '{txId}' in GameDataManager.");
        }

        GrantRewards(productId);
        OnPurchaseSuccess?.Invoke(productId);
    }

    public void GrantRewards(string productId)
    {
        if (string.IsNullOrEmpty(productId)) return;

        Debug.Log($"[IAP] Granting rewards for Product ID: '{productId}'");

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
                Debug.Log($"[IAP] Granted Bundle '{productId}' via StoreCatalogDatabase ({bundle.bundleName}).");
                return;
            }

            // Fallback to IAPProductDefinition if configured
            var def = GetProductDefinition(productId);
            if (def != null)
            {
                GrantDefinitionFallback(def);
                return;
            }

            Debug.LogWarning($"[IAP] Bundle definition for '{productId}' not found in database.");
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

    #endregion

    // ===================================================================
    #region Purchases Restoring
    // ===================================================================

    public async Task<bool> RestorePurchasesAsync()
    {
        if (!IsInitialized)
        {
            Debug.Log("[IAP] Restore requested before initialization. Initializing now...");
            bool initOk = await EnsureInitializedAsync();
            if (!initOk)
            {
                Debug.LogError("[IAP] Restore failed: Store could not be initialized.");
                return false;
            }
        }

        Debug.Log("[IAP] Restoring Purchases from store...");

        if (Application.platform == RuntimePlatform.IPhonePlayer ||
            Application.platform == RuntimePlatform.OSXPlayer)
        {
            _storeController.RestoreTransactions((success, error) =>
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
            _storeController.FetchPurchases();
        }

        return true;
    }

    public async void RestorePurchases()
    {
        await RestorePurchasesAsync();
    }

    private void OnPurchasesFetched(Orders orders)
    {
        Debug.Log("[IAP] Purchases fetched from store.");

        if (orders == null || orders.ConfirmedOrders == null)
        {
            Debug.Log("[IAP] No previous confirmed orders found.");
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

                // STRICT SAFETY: Only restore Non-Consumable products. NEVER restore consumable coins!
                if (product.definition.type == ProductType.NonConsumable)
                {
                    Debug.Log($"[IAP] Restoring Non-Consumable entitlement: {productId}");
                    GrantRewards(productId);
                }
                else
                {
                    Debug.Log($"[IAP] Skipping restoration of consumable product: {productId}");
                }
            }
        }

        Debug.Log("[IAP] Purchases restoration check completed.");
    }

    private void OnPurchasesFetchFailed(PurchasesFetchFailureDescription failure)
    {
        Debug.LogError($"[IAP] Purchases fetch failed: {failure?.Message} ({failure?.FailureReason})");
    }

    #endregion

    // ===================================================================
    #region Product Queries & Pricing
    // ===================================================================

    public bool IsProductAvailable(string productId)
    {
        if (string.IsNullOrEmpty(productId)) return false;

        if (_productAvailability.TryGetValue(productId, out bool available))
        {
            return available;
        }

        var product = GetProduct(productId);
        return product != null && product.availableToPurchase;
    }

    public Product GetProduct(string productId)
    {
        if (_storeController == null || string.IsNullOrEmpty(productId))
            return null;

        var allProducts = _storeController.GetProducts();
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

        return string.Empty;
    }

    private IAPProductDefinition GetProductDefinition(string productId)
    {
        return products.Find(p => p != null && p.productId.Equals(productId, StringComparison.OrdinalIgnoreCase));
    }

    #endregion
}
