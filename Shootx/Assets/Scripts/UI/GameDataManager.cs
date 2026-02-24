using System;
using System.Collections.Generic;
using UnityEngine;

public class GameDataManager : MonoBehaviour
{
    public static GameDataManager Instance { get; private set; }

    // ===================================================================
    #region Events
    // ===================================================================

    public static event Action OnCurrencyChanged;
    public static event Action OnLevelChanged;
    public static event Action OnMissionsUpdated;

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
        DontDestroyOnLoad(gameObject);
        LoadFromPlayerPrefs();
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Currency
    // ===================================================================

    public void AddSoftCurrency(int amount)
    {
        softCurrency += amount;
        SaveCurrency();
        OnCurrencyChanged?.Invoke();
    }

    public bool SpendSoftCurrency(int amount)
    {
        if (softCurrency < amount) return false;
        softCurrency -= amount;
        SaveCurrency();
        OnCurrencyChanged?.Invoke();
        return true;
    }

    public void AddHardCurrency(int amount)
    {
        hardCurrency += amount;
        SaveCurrency();
        OnCurrencyChanged?.Invoke();
    }

    public bool SpendHardCurrency(int amount)
    {
        if (hardCurrency < amount) return false;
        hardCurrency -= amount;
        SaveCurrency();
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
    }

    public void EquipOutfit(int id)
    {
        equippedOutfitId = id;
        PlayerPrefs.SetInt("equippedOutfit", id);
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
    #region Save / Load
    // ===================================================================

    private void SaveCurrency()
    {
        PlayerPrefs.SetInt("softCurrency", softCurrency);
        PlayerPrefs.SetInt("hardCurrency", hardCurrency);
    }

    private void LoadFromPlayerPrefs()
    {
        softCurrency = PlayerPrefs.GetInt("softCurrency", softCurrency);
        hardCurrency = PlayerPrefs.GetInt("hardCurrency", hardCurrency);
        currentLevel = PlayerPrefs.GetInt("currentLevel", 1);
        equippedGunId = PlayerPrefs.GetInt("equippedGun", 0);
        equippedOutfitId = PlayerPrefs.GetInt("equippedOutfit", 0);
    }

    // ===================================================================
    #endregion
}