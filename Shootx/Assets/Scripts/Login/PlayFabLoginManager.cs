using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PlayFab;
using PlayFab.ClientModels;
using DG.Tweening;
using UnityEngine.SceneManagement;

public class PlayFabLoginManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject usernamePanel;
    [SerializeField] private CanvasGroup usernamePanelGroup;
    [SerializeField] private RectTransform usernamePanelRect;
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private Button confirmButton;

    [Header("Username Settings")]
    [SerializeField] private int minLength = 3;
    [SerializeField] private int maxLength = 20;

    private bool isLoggedIn;

    private readonly string[] bannedWords =
    {
        "admin", "moderator", "mod", "owner",
        "system", "support", "playfab", "null", "test",
        "fuck", "shit", "bitch", "cunt", "asshole", "bastard",
        "dick", "pussy", "cock", "whore", "slut", "motherfucker",
        "faggot", "retard", "nigga", "nigger", "twat", "wanker",
        "prick", "douchebag", "jackass", "dumbass", "shithead",
        "fuckface", "dipshit", "bullshit", "goddamn", "damn", "ass",
        "god", "jesus", "christ", "satan", "devil", "hell", "bitchass",
        "crap",
    };

    private void OnEnable()
    {
        usernamePanel.SetActive(false);
        usernamePanelGroup.alpha = 0f;
        usernamePanelRect.localScale = Vector3.zero;

        feedbackText.text = "Connecting...";
        feedbackText.color = Color.white;
        confirmButton.interactable = false;

        Login();
    }

    private void Login()
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            Debug.Log("No internet connection. Loading offline mode...");
            LoadMainScene();
            return;
        }

        string customId = GetOrCreateDeviceId();

        var request = new LoginWithCustomIDRequest
        {
            CustomId = customId,
            CreateAccount = true
        };

        PlayFabClientAPI.LoginWithCustomID(request, OnLoginSuccess, OnLoginFailed);
    }

    private string GetOrCreateDeviceId()
    {
        string deviceId = SystemInfo.deviceUniqueIdentifier;

        if (string.IsNullOrEmpty(deviceId) || deviceId == SystemInfo.unsupportedIdentifier)
        {
            if (PlayerPrefs.HasKey("CUSTOM_ID"))
            {
                deviceId = PlayerPrefs.GetString("CUSTOM_ID");
            }
            else
            {
                deviceId = Guid.NewGuid().ToString();
                PlayerPrefs.SetString("CUSTOM_ID", deviceId);
                PlayerPrefs.Save();
            }
        }
        return deviceId;
    }

    private void OnLoginSuccess(LoginResult result)
    {
        Debug.Log("PlayFab ID: " + result.PlayFabId);
        isLoggedIn = true;
        confirmButton.interactable = true;
        feedbackText.text = "";

        PlayFabClientAPI.GetAccountInfo(new GetAccountInfoRequest(), accountResult =>
        {
            string displayName = accountResult.AccountInfo?.TitleInfo?.DisplayName;

            if (string.IsNullOrWhiteSpace(displayName))
            {
                ShowUsernamePanel();
            }
            else
            {
                Debug.Log("Username: " + displayName);
                LoadMainScene();
            }
        }, OnPlayFabError);
    }

    private void OnLoginFailed(PlayFabError error)
    {
        isLoggedIn = false;
        ShowFeedback("Login failed. Loading offline mode...", isError: true);
        Debug.LogWarning("PlayFab Login Failed: " + error.ErrorMessage);
        LoadMainScene();
    }

    private void OnPlayFabError(PlayFabError error)
    {
        ShowFeedback("Login failed. Loading offline mode...", isError: true);
        Debug.LogWarning("PlayFab Error: " + error.ErrorMessage);
        LoadMainScene();
    }

    public void ConfirmUsername()
    {
        confirmButton.transform.DOPunchScale(Vector3.one * 0.1f, 0.2f, 10, 1);

        if (!isLoggedIn)
        {
            ShowFeedback("Please wait...", isError: true);
            return;
        }

        string username = usernameInput.text.Trim();

        if (!ValidateUsername(username))
            return;

        confirmButton.interactable = false;
        ShowFeedback("Saving...", isError: false);

        var request = new UpdateUserTitleDisplayNameRequest { DisplayName = username };

        PlayFabClientAPI.UpdateUserTitleDisplayName(request, result =>
        {
            ShowFeedback("Success!", isError: false);
            PlayerPrefs.SetInt("HasRegistered", 1);
            PlayerPrefs.Save();
            HideUsernamePanel();
            Debug.Log("Display Name Saved: " + result.DisplayName);
            DOVirtual.DelayedCall(0.4f, LoadMainScene);
        },
        error =>
        {
            confirmButton.interactable = true;
            ShowFeedback(error.ErrorMessage, isError: true);
        });
    }

    private bool ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            ShowFeedback("Username cannot be empty", isError: true);
            return false;
        }

        if (username.Length < minLength || username.Length > maxLength)
        {
            ShowFeedback($"Must be {minLength}-{maxLength} characters", isError: true);
            return false;
        }

        foreach (string word in bannedWords)
        {
            if (username.ToLower().Contains(word.ToLower()))
            {
                ShowFeedback("Username not allowed", isError: true);
                return false;
            }
        }

        if (!Regex.IsMatch(username, @"^[a-zA-Z0-9_]+$"))
        {
            ShowFeedback("Only letters, numbers, and underscore", isError: true);
            return false;
        }

        return true;
    }

    private void LoadMainScene()
    {
        SceneManager.LoadScene("Main");
    }

    #region Animations & UI Handling

    private void ShowUsernamePanel()
    {
        usernamePanel.SetActive(true);
        feedbackText.text = "Choose a username";
        feedbackText.color = Color.white;

        usernamePanelGroup.DOFade(1f, 0.3f);
        usernamePanelRect.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);
    }

    private void HideUsernamePanel()
    {
        usernamePanelGroup.DOFade(0f, 0.3f);
        usernamePanelRect.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack).OnComplete(() =>
        {
            usernamePanel.SetActive(false);
        });
    }

    private void ShowFeedback(string message, bool isError)
    {
        feedbackText.text = message;
        feedbackText.color = isError ? Color.red : Color.green;

        if (isError)
        {
            feedbackText.transform.DOComplete();
            feedbackText.transform.DOShakePosition(0.4f, strength: new Vector3(10f, 0, 0), vibrato: 10);
        }
    }

    #endregion
}