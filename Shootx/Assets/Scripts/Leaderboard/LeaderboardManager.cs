using DG.Tweening;
using PlayFab;
using PlayFab.ClientModels;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance;

    [Header("UI Panels")]
    [SerializeField] private Button openLeaderboardButton;
    [SerializeField] private GameObject leaderboardPanel;
    [SerializeField] private GameObject offlinePanel;
    [SerializeField] private GameObject loadingText;

    [Header("Scroll View & Prefab")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Transform content;
    [SerializeField] private LeaderboardItemUI itemPrefab;

    [Header("Player Pinned Info")]
    [SerializeField] private LeaderboardItemUI playerPinnedItem;

    [Header("Sprites")]
    [SerializeField] private Sprite firstPlaceSprite;
    [SerializeField] private Sprite secondPlaceSprite;
    [SerializeField] private Sprite thirdPlaceSprite;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite playerHighlightSprite;

    private string playFabId;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        leaderboardPanel.SetActive(false);

        if (PlayerPrefs.GetInt("HasRegistered", 0) == 0)
        {
            openLeaderboardButton.gameObject.SetActive(false);
        }
        else
        {
            openLeaderboardButton.onClick.AddListener(OpenLeaderboard);
        }
    }

    public void OpenLeaderboard()
    {
        leaderboardPanel.SetActive(true);

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            ShowOfflineState();
            return;
        }

        offlinePanel.SetActive(false);
        loadingText.SetActive(true);

        if (PlayFabClientAPI.IsClientLoggedIn())
        {
            FetchLeaderboard();
        }
        else
        {
            SilentLogin();
        }
    }

    private void ShowOfflineState()
    {
        offlinePanel.SetActive(true);
        loadingText.SetActive(false);
        scrollRect.gameObject.SetActive(false);
        playerPinnedItem.gameObject.SetActive(false);
    }

    private void SilentLogin()
    {
        string deviceId = PlayerPrefs.GetString("CUSTOM_ID", SystemInfo.deviceUniqueIdentifier);

        var request = new LoginWithCustomIDRequest
        {
            CustomId = deviceId,
            CreateAccount = false
        };

        PlayFabClientAPI.LoginWithCustomID(request, result =>
        {
            playFabId = result.PlayFabId;
            FetchLeaderboard();
        }, error => ShowOfflineState());
    }

    private void FetchLeaderboard()
    {
        scrollRect.gameObject.SetActive(true);

        var request = new GetLeaderboardRequest
        {
            StatisticName = "LevelsCompleted",
            StartPosition = 0,
            MaxResultsCount = 100
        };

        PlayFabClientAPI.GetLeaderboard(request, OnLeaderboardReceived, OnError);

        var playerRankRequest = new GetLeaderboardAroundPlayerRequest
        {
            StatisticName = "LevelsCompleted",
            MaxResultsCount = 1
        };

        PlayFabClientAPI.GetLeaderboardAroundPlayer(playerRankRequest, OnPlayerRankReceived, OnError);
    }

    private void OnLeaderboardReceived(GetLeaderboardResult result)
    {
        loadingText.SetActive(false);

        foreach (Transform child in content)
        {
            Destroy(child.gameObject);
        }

        int playerIndex = -1;

        for (int i = 0; i < result.Leaderboard.Count; i++)
        {
            var item = result.Leaderboard[i];
            var spawnedItem = Instantiate(itemPrefab, content);

            spawnedItem.Setup(item.Position + 1, item.DisplayName, item.StatValue);

            if (item.Position == 0) ApplyRankStyle(spawnedItem, firstPlaceSprite, 1.15f);
            else if (item.Position == 1) ApplyRankStyle(spawnedItem, secondPlaceSprite, 1.1f);
            else if (item.Position == 2) ApplyRankStyle(spawnedItem, thirdPlaceSprite, 1.05f);
            else spawnedItem.SetImage(normalSprite);

            if (item.PlayFabId == playFabId || item.PlayFabId == PlayFabSettings.staticPlayer.PlayFabId)
            {
                playerIndex = i;
                spawnedItem.SetImage(playerHighlightSprite);

                spawnedItem.transform.DOScale(1.05f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
            }
        }

        if (playerIndex != -1)
        {
            DOVirtual.DelayedCall(0.1f, () => StartCoroutine(ScrollToPlayerCoroutine(playerIndex, result.Leaderboard.Count)));
        }
    }

    private void ApplyRankStyle(LeaderboardItemUI item, Sprite sprite, float scale)
    {
        item.SetImage(sprite);
        item.transform.localScale = Vector3.one * scale;
    }

    private IEnumerator ScrollToPlayerCoroutine(int index, int totalItems)
    {
        yield return new WaitForEndOfFrame();
        scrollRect.DOKill();
        if (totalItems <= 5 || index > 100)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            yield break;
        }

        float normalizedPosition = 1f - ((float)index / (totalItems - 1));
        normalizedPosition = Mathf.Clamp01(normalizedPosition);

        scrollRect.DOVerticalNormalizedPos(normalizedPosition, 1f).SetEase(Ease.OutCubic);
    }

    private void OnPlayerRankReceived(GetLeaderboardAroundPlayerResult result)
    {
        if (result.Leaderboard.Count > 0)
        {
            playerPinnedItem.gameObject.SetActive(true);
            var me = result.Leaderboard[0];
            playerPinnedItem.Setup(me.Position + 1, me.DisplayName, me.StatValue);
            playerPinnedItem.SetImage(playerHighlightSprite);
        }
    }

    private void OnError(PlayFabError error)
    {
        Debug.LogError(error.GenerateErrorReport());
        ShowOfflineState();
    }

    public void CloseLeaderboard()
    {
        leaderboardPanel.SetActive(false);
    }

    #region Update Level Function
    public void UpdatePlayerLevel(int newLevel)
    {
        if (PlayerPrefs.GetInt("HasRegistered", 0) == 0) return;
        if (Application.internetReachability == NetworkReachability.NotReachable) return;

        var request = new UpdatePlayerStatisticsRequest
        {
            Statistics = new List<StatisticUpdate>
            {
                new StatisticUpdate { StatisticName = "LevelsCompleted", Value = newLevel }
            }
        };

        if (PlayFabClientAPI.IsClientLoggedIn())
        {
            PlayFabClientAPI.UpdatePlayerStatistics(request,
                res => Debug.Log("Level Updated to PlayFab!"),
                err => Debug.LogError(err.GenerateErrorReport()));
        }
        else
        {
            SilentLoginAndUpdate(request);
        }
    }

    private void SilentLoginAndUpdate(UpdatePlayerStatisticsRequest updateRequest)
    {
        string deviceId = PlayerPrefs.GetString("CUSTOM_ID", SystemInfo.deviceUniqueIdentifier);
        PlayFabClientAPI.LoginWithCustomID(new LoginWithCustomIDRequest { CustomId = deviceId },
            res => PlayFabClientAPI.UpdatePlayerStatistics(updateRequest, null, null),
            null);
    }

    #endregion

    #region UI Testing (Mock Data)
    [ContextMenu("Test Leaderboard UI")]
    public void GenerateMockLeaderboard()
    {
        leaderboardPanel.SetActive(true);
        offlinePanel.SetActive(false);
        loadingText.SetActive(false);
        scrollRect.gameObject.SetActive(true);
        foreach (Transform child in content)
        {
            Destroy(child.gameObject);
        }

        int mockPlayerIndex = 45;
        int totalMockPlayers = 100;
        for (int i = 0; i < totalMockPlayers; i++)
        {
            var spawnedItem = Instantiate(itemPrefab, content);

            int rank = i + 1;
            string playerName = "Player " + rank;
            int level = 500 - (i * 2);

            spawnedItem.Setup(rank, playerName, level);

            if (i == 0) ApplyRankStyle(spawnedItem, firstPlaceSprite, 1.15f);
            else if (i == 1) ApplyRankStyle(spawnedItem, secondPlaceSprite, 1.1f);
            else if (i == 2) ApplyRankStyle(spawnedItem, thirdPlaceSprite, 1.05f);
            else spawnedItem.SetImage(normalSprite);
            if (i == mockPlayerIndex)
            {
                spawnedItem.SetImage(playerHighlightSprite);
                spawnedItem.transform.DOScale(1.05f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
            }
        }

        playerPinnedItem.gameObject.SetActive(true);
        playerPinnedItem.Setup(mockPlayerIndex + 1, "My_Test_Name", 500 - (mockPlayerIndex * 2));
        playerPinnedItem.SetImage(playerHighlightSprite);

        DOVirtual.DelayedCall(0.1f, () => StartCoroutine(ScrollToPlayerCoroutine(mockPlayerIndex, totalMockPlayers)));
    }

    #endregion
}