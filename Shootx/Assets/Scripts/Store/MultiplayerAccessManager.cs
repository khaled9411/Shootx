using System;
using System.Globalization;
using UnityEngine;

namespace Shootx.Store
{
    public class MultiplayerAccessManager : MonoBehaviour
    {
        public static MultiplayerAccessManager Instance { get; private set; }

        private const string PermanentAccessPrefKey = "onlineAccessPermanent";
        private const string TempAccessExpiryPrefKey = "multiplayer_temp_access_expiry_utc";

        public static event Action OnAccessChanged;

        [Header("=== Runtime Status (Debug) ===")]
        [SerializeField] private bool hasPermanentAccess = false;
        [SerializeField] private string temporaryExpiryString = "";

        private DateTime? _temporaryExpiryUtc = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadEntitlements();
        }

        private void Start()
        {
            // Sync with GameDataManager if it was already marked unlocked
            if (GameDataManager.Instance != null && GameDataManager.Instance.MultiplayerUnlocked)
            {
                if (!hasPermanentAccess)
                {
                    hasPermanentAccess = true;
                    SaveEntitlements();
                }
            }
        }

        public MultiplayerAccessState GetAccessState()
        {
            if (hasPermanentAccess)
                return MultiplayerAccessState.PermanentAccess;

            if (_temporaryExpiryUtc.HasValue && DateTime.UtcNow < _temporaryExpiryUtc.Value)
                return MultiplayerAccessState.TemporaryAccess;

            return MultiplayerAccessState.NoAccess;
        }

        public bool HasAccess()
        {
            return GetAccessState() != MultiplayerAccessState.NoAccess;
        }

        public TimeSpan GetRemainingTemporaryTime()
        {
            if (!_temporaryExpiryUtc.HasValue) return TimeSpan.Zero;
            TimeSpan diff = _temporaryExpiryUtc.Value - DateTime.UtcNow;
            return diff > TimeSpan.Zero ? diff : TimeSpan.Zero;
        }

        public string GetFormattedRemainingTime()
        {
            TimeSpan rem = GetRemainingTemporaryTime();
            if (rem <= TimeSpan.Zero) return "Expired";
            if (rem.TotalHours >= 1)
                return $"{(int)rem.TotalHours}h {rem.Minutes}m";
            return $"{rem.Minutes}m {rem.Seconds}s";
        }

        public void GrantPermanentAccess()
        {
            hasPermanentAccess = true;
            SaveEntitlements();

            if (GameDataManager.Instance != null && !GameDataManager.Instance.MultiplayerUnlocked)
            {
                GameDataManager.Instance.UnlockMultiplayer();
            }

            Debug.Log("[MultiplayerAccess] Permanent Online Access granted.");
            OnAccessChanged?.Invoke();
        }

        public void GrantTemporaryAccess(int hours = 24)
        {
            DateTime now = DateTime.UtcNow;
            DateTime baseTime = (_temporaryExpiryUtc.HasValue && _temporaryExpiryUtc.Value > now)
                ? _temporaryExpiryUtc.Value
                : now;

            _temporaryExpiryUtc = baseTime.AddHours(hours);
            temporaryExpiryString = _temporaryExpiryUtc.Value.ToString("o", CultureInfo.InvariantCulture);

            SaveEntitlements();

            Debug.Log($"[MultiplayerAccess] Temporary Online Access granted for {hours}h until {_temporaryExpiryUtc.Value:u}");
            OnAccessChanged?.Invoke();
        }

        public void RequestRewardedAdAccess(Action<bool> onResult)
        {
            if (GameAdManager.Instance == null)
            {
                Debug.LogError("[MultiplayerAccess] GameAdManager instance is null.");
                onResult?.Invoke(false);
                return;
            }

            Debug.Log("[MultiplayerAccess] Requesting Rewarded Ad for 24h temporary multiplayer access...");

            GameAdManager.Instance.ShowRewardedAd((watched) =>
            {
                if (watched)
                {
                    GrantTemporaryAccess(24);
                    onResult?.Invoke(true);
                }
                else
                {
                    Debug.LogWarning("[MultiplayerAccess] Rewarded Ad was closed or failed without completion. Access NOT granted.");
                    onResult?.Invoke(false);
                }
            });
        }

        private void SaveEntitlements()
        {
            PlayerPrefs.SetInt(PermanentAccessPrefKey, hasPermanentAccess ? 1 : 0);
            if (_temporaryExpiryUtc.HasValue)
            {
                PlayerPrefs.SetString(TempAccessExpiryPrefKey, _temporaryExpiryUtc.Value.ToString("o", CultureInfo.InvariantCulture));
            }
            else
            {
                PlayerPrefs.DeleteKey(TempAccessExpiryPrefKey);
            }
            PlayerPrefs.Save();
        }

        private void LoadEntitlements()
        {
            hasPermanentAccess = PlayerPrefs.GetInt(PermanentAccessPrefKey, 0) == 1;

            string savedExpiry = PlayerPrefs.GetString(TempAccessExpiryPrefKey, "");
            temporaryExpiryString = savedExpiry;

            if (!string.IsNullOrEmpty(savedExpiry))
            {
                if (DateTime.TryParse(savedExpiry, null, DateTimeStyles.RoundtripKind, out DateTime parsed))
                {
                    _temporaryExpiryUtc = parsed;
                }
                else
                {
                    _temporaryExpiryUtc = null;
                }
            }
            else
            {
                _temporaryExpiryUtc = null;
            }
        }
    }
}
