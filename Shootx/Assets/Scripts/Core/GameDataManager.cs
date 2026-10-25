using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameDataManager : MonoBehaviour
{
    public static GameDataManager Instance { get; private set; }

    // ===================================================================
    #region Events
    // ===================================================================

    public static event Action OnCurrencyChanged;
    public static event Action OnLevelChanged;
    public static event Action OnMissionsUpdated;
    public static event Action OnEquipmentChanged;

    // ===================================================================
    #endregion

    // ===================================================================
    #region Data Types
    // ===================================================================

    [Serializable]
    public class GunData
    {
        public int id;
        public string weaponName;
        public int damage;
        public int magazine;
        public bool isBurst;
        public Sprite icon;
        public Sprite singleIcon;
        public Sprite burstIcon;
        public List<Sprite> skins;
        public GunsPanelController.GunCategory category;
    }

    [Serializable]
    public class OutfitData
    {
        public int id;
        public string outfitName;
        public Sprite icon;
        public OutfitsPanelController.OutfitCategory category;
    }

    [Serializable]
    public class DailyMission
    {
        public string missionName;
        public Sprite rewardSprite;
        public int rewardValue;
        public bool isHardCurrency;
        public int currentAmount;
        public int requiredAmount;
    }

    [Serializable]
    public class ShopOffer
    {
        public string productId;
        public Sprite icon;
        public int amount;
        public string priceLabel;
        public bool isHardCurrency;
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Player Data
    // ===================================================================

    [Header("=== Currency ===")]
    [SerializeField] private int softCurrency = 500;
    [SerializeField] private int hardCurrency = 50;

    [Header("=== Level ===")]
    [SerializeField] private int currentLevel = 1;

    [Header("=== Equipped Items ===")]
    [SerializeField] private int equippedGunId = 0;
    [SerializeField] private int equippedOutfitId = 0;

    [Header("=== IAP & Ownership ===")]
    [SerializeField] private bool adsRemoved = false;
    [SerializeField] private bool multiplayerUnlocked = false;
    [SerializeField] private int saveSchemaVersion = 2;
    private HashSet<int> ownedGuns = new HashSet<int>();
    private HashSet<int> ownedOutfits = new HashSet<int>();
    private HashSet<string> processedTransactions = new HashSet<string>();
    private HashSet<int> claimedLevelRewards = new HashSet<int>();

    [Header("=== Zone Sprites ===")]
    [SerializeField] private List<Sprite> zoneSprites;

    [Header("=== Guns Data ===")]
    [SerializeField] private List<GunData> allGuns;

    [Header("=== Outfits Data ===")]
    [SerializeField] private List<OutfitData> allOutfits;

    [Header("=== Daily Missions ===")]
    [SerializeField] private List<DailyMission> dailyMissions;
    [SerializeField] private string dailyMissionsResetTime; // ISO string

    [Header("=== Shop Offers ===")]
    [SerializeField] private List<ShopOffer> softCurrencyOffers;
    [SerializeField] private List<ShopOffer> hardCurrencyOffers;

    // ===================================================================
    #endregion

    // ===================================================================
    #region Properties
    // ===================================================================

    public int SoftCurrency => softCurrency;
    public int HardCurrency => hardCurrency;
    public int CurrentLevel => currentLevel;
    public int EquippedGunId => equippedGunId;
    public int EquippedOutfitId => equippedOutfitId;
    public bool AdsRemoved => adsRemoved;
    public bool MultiplayerUnlocked => multiplayerUnlocked;

    public List<ShopOffer> SoftCurrencyOffers => softCurrencyOffers;
    public List<ShopOffer> HardCurrencyOffers => hardCurrencyOffers;

    public float DailyMissionsProgress
    {
        get
        {
            if (dailyMissions == null || dailyMissions.Count == 0) return 0f;
            int done = 0;
            foreach (var m in dailyMissions)
                if (m.currentAmount >= m.requiredAmount) done++;
            return (float)done / dailyMissions.Count;
        }
    }

    public int DailyMissionsDone
    {
        get
        {
            int done = 0;
            foreach (var m in dailyMissions)
                if (m.currentAmount >= m.requiredAmount) done++;
            return done;
        }
    }

    public int DailyMissionsTotal => dailyMissions?.Count ?? 0;

    // ===================================================================
    #endregion

    // ===================================================================
    #region Unity Lifecycle
    // ===================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        //DontDestroyOnLoad(gameObject);
        LoadFromPlayerPrefs();
    }

    private void Start()
    {

        SceneManager.activeSceneChanged += OnactiveSceneChanged;
    }

    private void OnactiveSceneChanged(Scene preScene, Scene CurrentScene)
    {
        if (CurrentScene.buildIndex == 3)
        {
            Destroy(gameObject);
        }
    }
    // ===================================================================
    #endregion

    // ===================================================================
    #region Currency
    // ===================================================================

    public void AddSoftCurrency(int amount)
    {
        softCurrency += amount;
        SaveData();
        OnCurrencyChanged?.Invoke();
    }

    public bool SpendSoftCurrency(int amount)
    {
        if (softCurrency < amount) return false;
        softCurrency -= amount;
        SaveData();
        OnCurrencyChanged?.Invoke();
        return true;
    }

    public void AddHardCurrency(int amount)
    {
        hardCurrency += amount;
        SaveData();
        OnCurrencyChanged?.Invoke();
    }

    public bool SpendHardCurrency(int amount)
    {
        if (hardCurrency < amount) return false;
        hardCurrency -= amount;
        SaveData();
        OnCurrencyChanged?.Invoke();
        return true;
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Level
    // ===================================================================

    public void SetLevel(int level)
    {
        currentLevel = level;
        PlayerPrefs.SetInt("currentLevel", currentLevel);
        OnLevelChanged?.Invoke();
    }

    public void AdvanceLevel()
    {
        SetLevel(currentLevel + 1);
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Equip
    // ===================================================================

    public void EquipGun(int id)
    {
        equippedGunId = id;
        PlayerPrefs.SetInt("equippedGun", id);
        SaveData();
        OnEquipmentChanged?.Invoke();
    }

    public void EquipOutfit(int id)
    {
        equippedOutfitId = id;
        PlayerPrefs.SetInt("equippedOutfit", id);
        SaveData();
        OnEquipmentChanged?.Invoke();
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Getters
    // ===================================================================

    public Sprite GetZoneSprite(int zoneIndex)
    {
        if (zoneSprites == null || zoneIndex < 0 || zoneIndex >= zoneSprites.Count) return null;
        return zoneSprites[zoneIndex];
    }

    public GunData GetGunData(int id)
        => allGuns?.Find(g => g.id == id);

    public List<GunData> GetGunsForCategory(GunsPanelController.GunCategory cat)
        => allGuns?.FindAll(g => g.category == cat) ?? new List<GunData>();

    public List<OutfitData> GetOutfitsForCategory(OutfitsPanelController.OutfitCategory cat)
        => allOutfits?.FindAll(o => o.category == cat) ?? new List<OutfitData>();

    public List<DailyMission> GetDailyMissions()
        => dailyMissions ?? new List<DailyMission>();

    public TimeSpan GetDailyMissionsTimeRemaining()
    {
        if (string.IsNullOrEmpty(dailyMissionsResetTime))
            return TimeSpan.FromHours(24);

        if (DateTime.TryParse(dailyMissionsResetTime, out DateTime resetTime))
        {
            TimeSpan remaining = resetTime - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        return TimeSpan.Zero;
    }

    public void UpdateMissionProgress(int missionIndex, int newAmount)
    {
        if (dailyMissions == null || missionIndex >= dailyMissions.Count) return;
        dailyMissions[missionIndex].currentAmount = newAmount;
        OnMissionsUpdated?.Invoke();
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region IAP Unlocks & Ownership
    // ===================================================================

    public void RemoveAds()
    {
        adsRemoved = true;
        SaveData();

        if (GameAdManager.Instance != null)
        {
            if (GameAdManager.Instance.IsBannerVisible())
                GameAdManager.Instance.HideBanner();

            GameAdManager.Instance.StopAutoInterstitial();
        }

        // Purchasing remove_ads grants BOTH ad removal and permanent online access
        if (Shootx.Store.MultiplayerAccessManager.Instance != null)
        {
            Shootx.Store.MultiplayerAccessManager.Instance.GrantPermanentAccess();
        }
    }

    public void UnlockMultiplayer()
    {
        multiplayerUnlocked = true;
        SaveData();

        if (Shootx.Store.MultiplayerAccessManager.Instance != null)
        {
            Shootx.Store.MultiplayerAccessManager.Instance.GrantPermanentAccess();
        }
    }

    public void AddOwnedGun(int id)
    {
        if (ownedGuns.Add(id))
            SaveData();
    }

    public void AddOwnedOutfit(int id)
    {
        if (ownedOutfits.Add(id))
            SaveData();
    }

    public bool IsGunOwned(int id)
    {
        // Gun 0 is the starting free pistol, always owned
        if (id == 0) return true;
        return ownedGuns.Contains(id);
    }

    public bool IsOutfitOwned(int id)
    {
        // Outfit 0 is the starting free outfit, always owned
        if (id == 0) return true;
        return ownedOutfits.Contains(id);
    }

    public bool TryPurchaseItem(Shootx.Store.StoreItemDefinition item)
    {
        if (item == null)
        {
            Debug.LogError("[GameDataManager] Purchase failed: item is null.");
            return false;
        }

        if (item.itemType == Shootx.Store.StoreItemType.Weapon && IsGunOwned(item.numericId))
        {
            Debug.LogWarning($"[GameDataManager] Weapon {item.displayName} is already owned.");
            return false;
        }

        if (item.itemType == Shootx.Store.StoreItemType.Outfit && IsOutfitOwned(item.numericId))
        {
            Debug.LogWarning($"[GameDataManager] Outfit {item.displayName} is already owned.");
            return false;
        }

        if (item.purchaseCurrency == Shootx.Store.CurrencyType.SoftCurrency)
        {
            if (softCurrency < item.softCurrencyPrice)
            {
                Debug.LogWarning($"[GameDataManager] Insufficient Soft Currency: have {softCurrency}, need {item.softCurrencyPrice}");
                return false;
            }

            if (!SpendSoftCurrency(item.softCurrencyPrice))
                return false;
        }
        else
        {
            if (hardCurrency < item.hardCurrencyPrice)
            {
                Debug.LogWarning($"[GameDataManager] Insufficient Hard Currency: have {hardCurrency}, need {item.hardCurrencyPrice}");
                return false;
            }

            if (!SpendHardCurrency(item.hardCurrencyPrice))
                return false;
        }

        if (item.itemType == Shootx.Store.StoreItemType.Weapon)
        {
            AddOwnedGun(item.numericId);
        }
        else
        {
            AddOwnedOutfit(item.numericId);
        }

        SaveData();
        Debug.Log($"[GameDataManager] Successfully purchased {item.displayName}!");
        return true;
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Level Completion Rewards
    // ===================================================================

    public int CalculateLevelReward(int level)
    {
        // 500 levels in the game. Expected earnings between 50 and 150 soft currency per level.
        // Formula: 50 + (level * 100 / 500). Average is 100.
        int bonus = Mathf.RoundToInt((Mathf.Clamp(level, 1, 500) - 1) * (100f / 499f));
        return Mathf.Clamp(50 + bonus, 50, 150);
    }

    public int ClaimLevelReward(int level, int defaultReward = 0)
    {
        if (claimedLevelRewards.Contains(level))
        {
            Debug.Log($"[GameDataManager] Level {level} reward has already been claimed. Preventing duplicate grant.");
            return 0;
        }

        int reward = defaultReward > 0 ? defaultReward : CalculateLevelReward(level);
        claimedLevelRewards.Add(level);
        AddSoftCurrency(reward);
        SaveData();

        Debug.Log($"[GameDataManager] Granted {reward} Soft Currency for level {level}.");
        return reward;
    }

    public bool IsTransactionProcessed(string transactionId)
    {
        return processedTransactions.Contains(transactionId);
    }

    public void RecordTransaction(string transactionId)
    {
        if (processedTransactions.Add(transactionId))
            SaveData();
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Save / Load
    // ===================================================================

    private void SaveData()
    {
        PlayerPrefs.SetInt("softCurrency", softCurrency);
        PlayerPrefs.SetInt("hardCurrency", hardCurrency);
        PlayerPrefs.SetInt("adsRemoved", adsRemoved ? 1 : 0);
        PlayerPrefs.SetInt("multiplayerUnlocked", multiplayerUnlocked ? 1 : 0);
        PlayerPrefs.SetInt("saveSchemaVersion", saveSchemaVersion);
        PlayerPrefs.SetString("ownedGuns", string.Join(",", ownedGuns));
        PlayerPrefs.SetString("ownedOutfits", string.Join(",", ownedOutfits));
        PlayerPrefs.SetString("processedTransactions", string.Join(",", processedTransactions));
        PlayerPrefs.SetString("claimedLevelRewards", string.Join(",", claimedLevelRewards));
        PlayerPrefs.Save();
    }

    private void LoadFromPlayerPrefs()
    {
        softCurrency = PlayerPrefs.GetInt("softCurrency", softCurrency);
        hardCurrency = PlayerPrefs.GetInt("hardCurrency", hardCurrency);
        currentLevel = PlayerPrefs.GetInt("currentLevel", 1);
        equippedGunId = PlayerPrefs.GetInt("equippedGun", 0);
        equippedOutfitId = PlayerPrefs.GetInt("equippedOutfit", 0);
        adsRemoved = PlayerPrefs.GetInt("adsRemoved", 0) == 1;
        multiplayerUnlocked = PlayerPrefs.GetInt("multiplayerUnlocked", 0) == 1;
        saveSchemaVersion = PlayerPrefs.GetInt("saveSchemaVersion", saveSchemaVersion);

        LoadIntHashSet("ownedGuns", ownedGuns);
        LoadIntHashSet("ownedOutfits", ownedOutfits);
        LoadStringHashSet("processedTransactions", processedTransactions);
        LoadIntHashSet("claimedLevelRewards", claimedLevelRewards);

        // Ensure default items are always owned
        ownedGuns.Add(0);
        ownedOutfits.Add(0);
    }

    private void LoadIntHashSet(string key, HashSet<int> set)
    {
        set.Clear();
        string data = PlayerPrefs.GetString(key, "");
        if (!string.IsNullOrEmpty(data))
        {
            string[] parts = data.Split(',');
            foreach (var p in parts)
            {
                if (int.TryParse(p, out int val))
                    set.Add(val);
            }
        }
    }

    private void LoadStringHashSet(string key, HashSet<string> set)
    {
        set.Clear();
        string data = PlayerPrefs.GetString(key, "");
        if (!string.IsNullOrEmpty(data))
        {
            string[] parts = data.Split(',');
            foreach (var p in parts)
            {
                if (!string.IsNullOrEmpty(p))
                    set.Add(p);
            }
        }
    }

    // ===================================================================
    #endregion
}