using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI controller for the MultiplayerMenu scene.
/// Manages all panels: main menu, lobby browser, join-by-code, create lobby, loading, errors.
/// All DOTween animations follow the WinLosePauseUI style.
/// </summary>
public class MultiplayerMenuUI : MonoBehaviour
{
    // ─── Main Menu Panel ────────────────────────────────────────
    [Header("Main Menu Panel")]
    [SerializeField] private CanvasGroup mainMenuPanel;
    [SerializeField] private RectTransform mainMenuRect;
    [SerializeField] private Button quickMatchButton;
    [SerializeField] private Button browseLobbiesButton;
    [SerializeField] private Button createPublicButton;
    [SerializeField] private Button createPrivateButton;
    [SerializeField] private Button joinByCodeButton;
    [SerializeField] private Button backButton;
    [SerializeField] private TextMeshProUGUI playerNameText;

    // ─── Lobby Browser Panel ────────────────────────────────────
    [Header("Lobby Browser Panel")]
    [SerializeField] private CanvasGroup browserPanel;
    [SerializeField] private RectTransform browserPanelRect;
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button browserBackButton;
    [SerializeField] private Transform lobbyListContent;
    [SerializeField] private GameObject lobbyEntryPrefab;
    [SerializeField] private TextMeshProUGUI emptyListText;
    [SerializeField] private GameObject browserLoadingIndicator;

    // ─── Join By Code Panel ─────────────────────────────────────
    [Header("Join By Code Panel")]
    [SerializeField] private CanvasGroup joinCodePanel;
    [SerializeField] private RectTransform joinCodePanelRect;
    [SerializeField] private TMP_InputField codeInput;
    [SerializeField] private Button joinCodeConfirmButton;
    [SerializeField] private Button joinCodeBackButton;

    // ─── Create Lobby Panel ─────────────────────────────────────
    [Header("Create Lobby Panel")]
    [SerializeField] private CanvasGroup createPanel;
    [SerializeField] private RectTransform createPanelRect;
    [SerializeField] private TMP_InputField lobbyNameInput;
    [SerializeField] private Button createConfirmButton;
    [SerializeField] private Button createBackButton;
    [SerializeField] private TextMeshProUGUI createTitleText;

    // ─── Loading Overlay ────────────────────────────────────────
    [Header("Loading")]
    [SerializeField] private CanvasGroup loadingPanel;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private RectTransform loadingSpinner;

    // ─── Error / Notification ───────────────────────────────────
    [Header("Notification")]
    [SerializeField] private CanvasGroup notificationPanel;
    [SerializeField] private RectTransform notificationRect;
    [SerializeField] private TextMeshProUGUI notificationText;
    [SerializeField] private Button notificationDismissButton;

    // ─── Animation Settings ─────────────────────────────────────
    [Header("Animation")]
    [SerializeField] private float panelFadeDuration = 0.3f;
    [SerializeField] private float panelScaleDuration = 0.4f;
    [SerializeField] private float buttonDelay = 0.06f;

    // ─── Internal ───────────────────────────────────────────────
    private bool _isCreatingPrivate;
    private bool _isBusy;
    private Sequence _spinnerSeq;
    private Sequence _notifSeq;
    private readonly List<GameObject> _spawnedEntries = new List<GameObject>();

    // ═══════════════════════════════════════════════════════════
    // Unity Lifecycle
    // ═══════════════════════════════════════════════════════════

    private void Start()
    {
        // Wire button events
        if (quickMatchButton) quickMatchButton.onClick.AddListener(OnQuickMatch);
        if (browseLobbiesButton) browseLobbiesButton.onClick.AddListener(OnBrowseLobbies);
        if (createPublicButton) createPublicButton.onClick.AddListener(OnCreatePublic);
        if (createPrivateButton) createPrivateButton.onClick.AddListener(OnCreatePrivate);
        if (joinByCodeButton) joinByCodeButton.onClick.AddListener(OnJoinByCode);
        if (backButton) backButton.onClick.AddListener(OnBack);
        if (refreshButton) refreshButton.onClick.AddListener(OnRefreshLobbies);
        if (browserBackButton) browserBackButton.onClick.AddListener(OnBrowserBack);
        if (joinCodeConfirmButton) joinCodeConfirmButton.onClick.AddListener(OnJoinCodeConfirm);
        if (joinCodeBackButton) joinCodeBackButton.onClick.AddListener(OnJoinCodeBack);
        if (createConfirmButton) createConfirmButton.onClick.AddListener(OnCreateConfirm);
        if (createBackButton) createBackButton.onClick.AddListener(OnCreateBack);
        if (notificationDismissButton) notificationDismissButton.onClick.AddListener(DismissNotification);

        // Register UI Button audio
        Shootx.Audio.UIButtonAudio.RegisterHierarchy(transform);

        // Subscribe to session errors
        if (MultiplayerSessionManager.Instance)
        {
            MultiplayerSessionManager.Instance.OnError += ShowNotification;
        }

        // Set player name
        if (playerNameText && MultiplayerSessionManager.Instance)
        {
            playerNameText.text = MultiplayerSessionManager.Instance.GetPlayerName();
        }

        // Initialize panel states
        HideAllPanels();
        ShowMainMenu();
    }

    private void OnDestroy()
    {
        _spinnerSeq?.Kill();
        _notifSeq?.Kill();

        if (MultiplayerSessionManager.Instance)
        {
            MultiplayerSessionManager.Instance.OnError -= ShowNotification;
        }

        DOTween.Kill(this);
    }

    // ═══════════════════════════════════════════════════════════
    // Panel Management
    // ═══════════════════════════════════════════════════════════

    private void HideAllPanels()
    {
        HidePanelInstant(mainMenuPanel);
        HidePanelInstant(browserPanel);
        HidePanelInstant(joinCodePanel);
        HidePanelInstant(createPanel);
        HidePanelInstant(loadingPanel);
        HidePanelInstant(notificationPanel);
    }

    private void HidePanelInstant(CanvasGroup panel)
    {
        if (panel == null) return;
        panel.alpha = 0f;
        panel.interactable = false;
        panel.blocksRaycasts = false;
    }

    private void ShowPanel(CanvasGroup panel, RectTransform rect)
    {
        if (panel == null) return;
        panel.alpha = 0f;
        panel.interactable = false;
        panel.blocksRaycasts = false;

        if (rect) rect.localScale = Vector3.one * 0.85f;

        Sequence seq = DOTween.Sequence();
        seq.Append(panel.DOFade(1f, panelFadeDuration));
        if (rect) seq.Join(rect.DOScale(Vector3.one, panelScaleDuration).SetEase(Ease.OutBack));
        seq.OnComplete(() =>
        {
            panel.interactable = true;
            panel.blocksRaycasts = true;
        });
    }

    private void HidePanel(CanvasGroup panel, RectTransform rect, System.Action onComplete = null)
    {
        if (panel == null) { onComplete?.Invoke(); return; }
        panel.interactable = false;
        panel.blocksRaycasts = false;

        Sequence seq = DOTween.Sequence();
        if (rect) seq.Append(rect.DOScale(Vector3.one * 0.85f, panelFadeDuration * 0.7f).SetEase(Ease.InBack));
        seq.Join(panel.DOFade(0f, panelFadeDuration));
        seq.OnComplete(() => onComplete?.Invoke());
    }

    // ═══════════════════════════════════════════════════════════
    // Main Menu
    // ═══════════════════════════════════════════════════════════

    private void ShowMainMenu()
    {
        ShowPanel(mainMenuPanel, mainMenuRect);

        // Stagger button animations
        var buttons = new RectTransform[]
        {
            quickMatchButton?.GetComponent<RectTransform>(),
            browseLobbiesButton?.GetComponent<RectTransform>(),
            createPublicButton?.GetComponent<RectTransform>(),
            createPrivateButton?.GetComponent<RectTransform>(),
            joinByCodeButton?.GetComponent<RectTransform>()
        };

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            buttons[i].localScale = Vector3.zero;
            float delay = 0.15f + i * buttonDelay;
            buttons[i].DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack).SetDelay(delay);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Loading
    // ═══════════════════════════════════════════════════════════

    private void ShowLoading(string message)
    {
        if (loadingText) loadingText.text = message;
        if (loadingPanel)
        {
            loadingPanel.alpha = 0f;
            loadingPanel.blocksRaycasts = true;
            loadingPanel.DOFade(1f, 0.2f);
        }
        StartSpinner();
    }

    private void HideLoading()
    {
        if (loadingPanel)
        {
            loadingPanel.DOFade(0f, 0.2f).OnComplete(() =>
            {
                loadingPanel.blocksRaycasts = false;
            });
        }
        StopSpinner();
    }

    private void StartSpinner()
    {
        if (loadingSpinner == null) return;
        _spinnerSeq?.Kill();
        _spinnerSeq = DOTween.Sequence();
        _spinnerSeq.Append(loadingSpinner.DORotate(new Vector3(0, 0, -360), 1f, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear));
        _spinnerSeq.SetLoops(-1);
    }

    private void StopSpinner()
    {
        _spinnerSeq?.Kill();
        _spinnerSeq = null;
    }

    // ═══════════════════════════════════════════════════════════
    // Notifications
    // ═══════════════════════════════════════════════════════════

    public void ShowNotification(string message)
    {
        if (notificationPanel == null) return;

        Shootx.Audio.AudioManager.Instance?.PlaySound2D(Shootx.Audio.SoundId.UI_ActionFailed);

        _notifSeq?.Kill();
        if (notificationText) notificationText.text = message;

        notificationPanel.alpha = 0f;
        notificationPanel.blocksRaycasts = true;
        notificationPanel.interactable = true;
        if (notificationRect)
        {
            notificationRect.localScale = Vector3.one * 0.8f;
        }

        _notifSeq = DOTween.Sequence();
        _notifSeq.Append(notificationPanel.DOFade(1f, 0.25f));
        if (notificationRect) _notifSeq.Join(notificationRect.DOScale(1f, 0.3f).SetEase(Ease.OutBack));
    }

    private void DismissNotification()
    {
        if (notificationPanel == null) return;
        _notifSeq?.Kill();
        notificationPanel.interactable = false;
        notificationPanel.DOFade(0f, 0.2f).OnComplete(() =>
        {
            notificationPanel.blocksRaycasts = false;
        });
    }

    // ═══════════════════════════════════════════════════════════
    // Button Handlers
    // ═══════════════════════════════════════════════════════════

    private async void OnQuickMatch()
    {
        if (_isBusy) return;
        _isBusy = true;
        AnimatePress(quickMatchButton);

        ShowLoading("Finding a match...");
        HidePanel(mainMenuPanel, mainMenuRect);

        var mgr = MultiplayerSessionManager.Instance;
        if (mgr == null) { HideLoading(); _isBusy = false; return; }

        bool success = await mgr.QuickMatchAsync();
        HideLoading();
        _isBusy = false;

        if (success)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                MultiplayerSessionManager.MultiplayerLobbyScene);
        }
        else
        {
            ShowMainMenu();
        }
    }

    private void OnBrowseLobbies()
    {
        AnimatePress(browseLobbiesButton);
        HidePanel(mainMenuPanel, mainMenuRect, () =>
        {
            ShowPanel(browserPanel, browserPanelRect);
            RefreshLobbyList();
        });
    }

    private void OnCreatePublic()
    {
        _isCreatingPrivate = false;
        AnimatePress(createPublicButton);
        if (createTitleText) createTitleText.text = "Create Public Lobby";
        HidePanel(mainMenuPanel, mainMenuRect, () => ShowPanel(createPanel, createPanelRect));
    }

    private void OnCreatePrivate()
    {
        _isCreatingPrivate = true;
        AnimatePress(createPrivateButton);
        if (createTitleText) createTitleText.text = "Create Private Lobby";
        HidePanel(mainMenuPanel, mainMenuRect, () => ShowPanel(createPanel, createPanelRect));
    }

    private void OnJoinByCode()
    {
        AnimatePress(joinByCodeButton);
        HidePanel(mainMenuPanel, mainMenuRect, () => ShowPanel(joinCodePanel, joinCodePanelRect));
    }

    private void OnBack()
    {
        AnimatePress(backButton);
        Shootx.Audio.AudioManager.Instance?.PlaySound2D(Shootx.Audio.SoundId.UI_ButtonBack);

        // Clean up persistent objects before leaving multiplayer flow
        if (MultiplayerSessionManager.Instance)
            Destroy(MultiplayerSessionManager.Instance.gameObject);
        if (MultiplayerNetworkRoot.Instance)
            MultiplayerNetworkRoot.Instance.DestroyRoot();

        UnityEngine.SceneManagement.SceneManager.LoadScene("Main");
    }

    // ── Browser ─────────────────────────────────────────────────

    private void OnRefreshLobbies()
    {
        AnimatePress(refreshButton);
        RefreshLobbyList();
    }

    private void OnBrowserBack()
    {
        AnimatePress(browserBackButton);
        Shootx.Audio.AudioManager.Instance?.PlaySound2D(Shootx.Audio.SoundId.UI_ButtonBack);
        HidePanel(browserPanel, browserPanelRect, ShowMainMenu);
    }

    private async void RefreshLobbyList()
    {
        if (browserLoadingIndicator) browserLoadingIndicator.SetActive(true);
        if (emptyListText) emptyListText.gameObject.SetActive(false);

        // Clear old entries
        foreach (var go in _spawnedEntries)
        {
            if (go) Destroy(go);
        }
        _spawnedEntries.Clear();

        var mgr = MultiplayerSessionManager.Instance;
        if (mgr == null) return;

        List<Lobby> lobbies = await mgr.QueryPublicLobbiesAsync();

        if (browserLoadingIndicator) browserLoadingIndicator.SetActive(false);

        if (lobbies.Count == 0)
        {
            if (emptyListText)
            {
                emptyListText.text = "No lobbies available";
                emptyListText.gameObject.SetActive(true);
            }
            return;
        }

        for (int i = 0; i < lobbies.Count; i++)
        {
            var lobby = lobbies[i];
            if (lobbyEntryPrefab == null || lobbyListContent == null) continue;

            var entry = Instantiate(lobbyEntryPrefab, lobbyListContent);
            _spawnedEntries.Add(entry);

            var entryUI = entry.GetComponent<LobbyEntryUI>();
            if (entryUI)
            {
                entryUI.Setup(lobby, OnJoinFromBrowser);
            }

            // Staggered appear animation
            var rt = entry.GetComponent<RectTransform>();
            if (rt)
            {
                rt.localScale = Vector3.zero;
                rt.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack).SetDelay(i * 0.05f);
            }
        }
    }

    private async void OnJoinFromBrowser(string lobbyId)
    {
        if (_isBusy) return;
        _isBusy = true;
        ShowLoading("Joining lobby...");

        var mgr = MultiplayerSessionManager.Instance;
        if (mgr == null) { HideLoading(); _isBusy = false; return; }

        bool success = await mgr.JoinByIdAsync(lobbyId);
        HideLoading();
        _isBusy = false;

        if (success)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                MultiplayerSessionManager.MultiplayerLobbyScene);
        }
    }

    // ── Join By Code ────────────────────────────────────────────

    private async void OnJoinCodeConfirm()
    {
        if (_isBusy) return;
        _isBusy = true;
        AnimatePress(joinCodeConfirmButton);

        string code = codeInput != null ? codeInput.text.Trim() : "";
        if (string.IsNullOrWhiteSpace(code))
        {
            ShowNotification("Please enter a lobby code.");
            _isBusy = false;
            return;
        }

        ShowLoading("Joining lobby...");

        var mgr = MultiplayerSessionManager.Instance;
        if (mgr == null) { HideLoading(); _isBusy = false; return; }

        bool success = await mgr.JoinByCodeAsync(code);
        HideLoading();
        _isBusy = false;

        if (success)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                MultiplayerSessionManager.MultiplayerLobbyScene);
        }
    }

    private void OnJoinCodeBack()
    {
        AnimatePress(joinCodeBackButton);
        Shootx.Audio.AudioManager.Instance?.PlaySound2D(Shootx.Audio.SoundId.UI_ButtonBack);
        HidePanel(joinCodePanel, joinCodePanelRect, ShowMainMenu);
    }

    // ── Create Lobby ────────────────────────────────────────────

    private async void OnCreateConfirm()
    {
        if (_isBusy) return;
        _isBusy = true;
        AnimatePress(createConfirmButton);

        string lobbyName = lobbyNameInput != null ? lobbyNameInput.text.Trim() : "";
        ShowLoading(_isCreatingPrivate ? "Creating private lobby..." : "Creating public lobby...");

        var mgr = MultiplayerSessionManager.Instance;
        if (mgr == null) { HideLoading(); _isBusy = false; return; }

        bool success;
        if (_isCreatingPrivate)
            success = await mgr.CreatePrivateLobbyAsync(lobbyName);
        else
            success = await mgr.CreatePublicLobbyAsync(lobbyName);

        HideLoading();
        _isBusy = false;

        if (success)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                MultiplayerSessionManager.MultiplayerLobbyScene);
        }
    }

    private void OnCreateBack()
    {
        AnimatePress(createBackButton);
        Shootx.Audio.AudioManager.Instance?.PlaySound2D(Shootx.Audio.SoundId.UI_ButtonBack);
        HidePanel(createPanel, createPanelRect, ShowMainMenu);
    }

    // ═══════════════════════════════════════════════════════════
    // Animation Helpers
    // ═══════════════════════════════════════════════════════════

    private void AnimatePress(Button btn)
    {
        if (btn == null) return;
        var rt = btn.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.DOKill();
        Sequence seq = DOTween.Sequence();
        seq.Append(rt.DOScale(0.9f, 0.06f));
        seq.Append(rt.DOScale(1f, 0.12f).SetEase(Ease.OutBack));
    }
}
