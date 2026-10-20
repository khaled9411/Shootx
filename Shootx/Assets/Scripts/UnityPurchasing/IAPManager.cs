using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Purchasing;
using Unity.Services.Core;

public class IAPManager : MonoBehaviour
{
    public static IAPManager Instance { get; private set; }

    [Header("=== Product Database ===")]
    [Tooltip("Add all IAP Product Definitions here")]
    [SerializeField]
    private List<IAPProductDefinition> products = new List<IAPProductDefinition>();

    private StoreController storeController;

    public bool IsInitialized => storeController != null;

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

            foreach (var p in products)
            {
                if (p != null && !string.IsNullOrEmpty(p.productId))
                {
                    productDefs.Add(
                        new ProductDefinition(
                            p.productId,
                            p.productType
                        )
                    );
                }
            }

            // ----------------------------------------------------
            // Fetch products
            // ----------------------------------------------------

            Debug.Log("[IAP] Fetching Products...");

            storeController.FetchProducts(productDefs);

            Debug.Log("[IAP] Products fetch requested.");

            // ----------------------------------------------------
            // Restore purchases
            // ----------------------------------------------------

            Debug.Log("[IAP] Fetching previous purchases...");

            storeController.FetchPurchases();

            Debug.Log("[IAP] Initialization successful.");
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"[IAP] Initialization Failed:\n{e}"
            );
        }
    }

    // ============================================================
    // BUY PRODUCT
    // ============================================================

    public void BuyProduct(string productId)
    {
        if (!IsInitialized)
        {
            Debug.LogError(
                "[IAP] Purchase failed: IAP is not initialized."
            );

            return;
        }

        if (string.IsNullOrEmpty(productId))
        {
            Debug.LogError(
                "[IAP] Purchase failed: Product ID is empty."
            );

            return;
        }

        Debug.Log(
            $"[IAP] Initiating purchase for: {productId}"
        );

        storeController.PurchaseProduct(productId);
    }

    // ============================================================
    // RESTORE PURCHASES
    // ============================================================

    public void RestorePurchases()
    {
        if (!IsInitialized)
        {
            Debug.LogError(
                "[IAP] Restore failed: IAP is not initialized."
            );

            return;
        }

        Debug.Log("[IAP] Restoring Purchases...");

        storeController.FetchPurchases();
    }

    // ============================================================
    // PURCHASE PENDING
    // ============================================================

    private void OnPurchasePending(PendingOrder order)
    {
        if (order == null)
        {
            Debug.LogError(
                "[IAP] Pending order is null."
            );

            return;
        }

        Debug.Log(
            "[IAP] Purchase pending."
        );

        /*
         * IMPORTANT:
         *
         * In IAP 5, PendingOrder does not have:
         *
         * order.ProductId
         * order.TransactionId
         *
         * Instead, products are obtained from:
         *
         * order.CartOrdered.Items()
         *
         * and the product ID is obtained from the Product.
         */

        var cart = order.CartOrdered;

        if (cart == null)
        {
            Debug.LogError(
                "[IAP] Pending order has no cart."
            );

            return;
        }

        foreach (var item in cart.Items())
        {
            Product product = item.Product;

            if (product == null)
                continue;

            string productId = product.definition.id;

            Debug.Log(
                $"[IAP] Processing purchase: {productId}"
            );

            ProcessPurchasedProduct(productId);
        }

        // Confirm the pending order after granting rewards.
        storeController.ConfirmPurchase(order);

        Debug.Log(
            "[IAP] Purchase confirmed."
        );
    }

    // ============================================================
    // PROCESS PURCHASE
    // ============================================================

    private void ProcessPurchasedProduct(string productId)
    {
        if (string.IsNullOrEmpty(productId))
        {
            Debug.LogError(
                "[IAP] Product ID is empty."
            );

            return;
        }

        IAPProductDefinition def =
            GetProductDefinition(productId);

        if (def == null)
        {
            Debug.LogError(
                $"[IAP] Unknown Product ID: {productId}"
            );

            return;
        }

        Debug.Log(
            $"[IAP] Granting rewards for: {productId}"
        );

        GrantRewards(def);
    }

    // ============================================================
    // PURCHASES FETCHED / RESTORE
    // ============================================================

    private void OnPurchasesFetched(Orders orders)
    {
        Debug.Log(
            "[IAP] Purchases fetched."
        );

        /*
         * IMPORTANT:
         *
         * Orders is NOT directly enumerable.
         *
         * Therefore:
         *
         * foreach (var order in orders)
         *
         * is invalid.
         *
         * We access the completed orders through:
         *
         * orders.ConfirmedOrders
         */

        if (orders == null)
        {
            Debug.LogWarning(
                "[IAP] Orders is null."
            );

            return;
        }

        foreach (var order in orders.ConfirmedOrders)
        {
            if (order == null)
                continue;

            foreach (var item in order.CartOrdered.Items())
            {
                Product product = item.Product;

                if (product == null)
                    continue;

                string productId =
                    product.definition.id;

                IAPProductDefinition def =
                    GetProductDefinition(productId);

                if (def == null)
                    continue;

                /*
                 * Only restore Non-Consumables.
                 *
                 * Consumables such as coins should NOT be
                 * granted again during restore.
                 */

                if (def.productType == ProductType.NonConsumable)
                {
                    Debug.Log(
                        $"[IAP] Restoring Non-Consumable: {productId}"
                    );

                    GrantRewards(def);
                }
            }
        }

        Debug.Log(
            "[IAP] Purchases restored successfully."
        );
    }

    // ============================================================
    // PURCHASE FAILED
    // ============================================================

    private void OnPurchaseFailed(FailedOrder failedOrder)
    {
        if (failedOrder == null)
        {
            Debug.LogError(
                "[IAP] Purchase failed: Unknown order."
            );

            return;
        }

        Debug.LogError(
            $"[IAP] Purchase Failed: {failedOrder}"
        );
    }

    // ============================================================
    // GRANT REWARDS
    // ============================================================

    private void GrantRewards(IAPProductDefinition def)
    {
        if (def == null)
            return;

        if (GameDataManager.Instance == null)
        {
            Debug.LogError(
                "[IAP] GameDataManager.Instance is null."
            );

            return;
        }

        // --------------------------------------------------------
        // Hard Currency
        // --------------------------------------------------------

        if (def.hardCoinsAmount > 0)
        {
            GameDataManager.Instance.AddHardCurrency(
                def.hardCoinsAmount
            );

            Debug.Log(
                $"[IAP] Granted {def.hardCoinsAmount} Hard Coins."
            );
        }

        // --------------------------------------------------------
        // Remove Ads
        // --------------------------------------------------------

        if (def.removesAds)
        {
            GameDataManager.Instance.RemoveAds();

            Debug.Log(
                "[IAP] Ads removed."
            );
        }

        // --------------------------------------------------------
        // Multiplayer
        // --------------------------------------------------------

        if (def.unlocksMultiplayer)
        {
            if (!GameDataManager.Instance.MultiplayerUnlocked)
            {
                GameDataManager.Instance.UnlockMultiplayer();

                Debug.Log(
                    "[IAP] Multiplayer unlocked."
                );
            }
            else
            {
                Debug.Log(
                    "[IAP] Multiplayer already unlocked."
                );
            }
        }

        // --------------------------------------------------------
        // Guns
        // --------------------------------------------------------

        if (def.unlockGunIds != null)
        {
            foreach (var gunId in def.unlockGunIds)
            {
                if (string.IsNullOrEmpty(gunId.ToString()))
                    continue;

                GameDataManager.Instance.AddOwnedGun(gunId);

                Debug.Log(
                    $"[IAP] Unlocked Gun ID: {gunId}"
                );
            }
        }

        // --------------------------------------------------------
        // Outfits
        // --------------------------------------------------------

        if (def.unlockOutfitIds != null)
        {
            foreach (var outfitId in def.unlockOutfitIds)
            {
                if (string.IsNullOrEmpty(outfitId.ToString()))
                    continue;

                GameDataManager.Instance.AddOwnedOutfit(
                    outfitId
                );

                Debug.Log(
                    $"[IAP] Unlocked Outfit ID: {outfitId}"
                );
            }
        }
    }

    // ============================================================
    // PRODUCT DATABASE
    // ============================================================

    private IAPProductDefinition GetProductDefinition(
        string productId
    )
    {
        return products.Find(
            p =>
                p != null &&
                p.productId == productId
        );
    }
}