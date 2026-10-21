using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI controller for the MultiplayerLobby (waiting room) scene.
/// Displays lobby info, player slots, readiness, host controls, and lobby code.
/// All animations follow the DOTween patterns established in MultiplayerMenuUI.
/// </summary>
public class MultiplayerLobbyUI : MonoBehaviour
{
    // ─── Header ─────────────────────────────────────────────────
    [Header("Lobby Header")]
    [SerializeField] private CanvasGroup headerPanel;
    [SerializeField] private RectTransform headerRect;
    [SerializeField] private TextMeshProUGUI lobbyNameText;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private TextMeshProUGUI hostIndicatorText;

    // ─── Lobby Code ─────────────────────────────────────────────
    [Header("Lobby Code")]
    [SerializeField] private CanvasGroup codePanel;
    [SerializeField] private RectTransform codePanelRect;
    [SerializeField] private TextMeshProUGUI lobbyCodeText;
    [SerializeField] private Button copyCodeButton;
    [SerializeField] private TextMeshProUGUI copyButtonText;

    // ─── Player Slots ───────────────────────────────────────────
    [Header("Player Slots")]
    [SerializeField] private PlayerSlotUI[] playerSlots = new PlayerSlotUI[4];

    // ─── Controls Panel ─────────────────────────────────────────
    [Header("Controls")]
    [SerializeField] private CanvasGroup controlsPanel;
    [SerializeField] private RectTransform controlsRect;
    [SerializeField] private Button readyButton;
    [SerializeField] private TextMeshProUGUI readyButtonText;
    [SerializeField] private Button startMatchButton;
    [SerializeField] private TextMeshProUGUI startMatchButtonText;
    [SerializeField] private Button leaveButton;

    // ─── Status / Loading ───────────────────────────────────────
    [Header("Status")]
    [SerializeField] private CanvasGroup statusPanel;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private RectTransform loadingSpinner;

    // ─── Notification ───────────────────────────────────────────
    [Header("Notification")]
    [SerializeField] private CanvasGroup notificationPanel;
    [SerializeField] private RectTransform notificationRect;
    [SerializeField] private TextMeshProUGUI notificationText;
    [SerializeField] private Button notificationDismissButton;

    // ─── Animation Settings ─────────────────────────────────────
    [Header("Animation")]
    [SerializeField] private float panelFadeDuration = 0.3f;
    [SerializeField] private float panelScaleDuration = 0.4f;
    [SerializeField] private float slotStaggerDelay = 0.08f;

    // ─── Internal ───────────────────────────────────────────────
    private bool _isReady;
    private bool _isBusy;
    private bool _isStartingMatch;
    private Sequence _spinnerSeq;
    private Sequence _notifSeq;
    private string _previousCopyText;

    // ═══════════════════════════════════════════════════════════
    // Unity Lifecycle
    // ═══════════════════════════════════════════════════════════

    private void Start()
    {
        // Wire buttons
        if (readyButton) readyButton.onClick.AddListener(OnReadyToggle);
        if (startMatchButton) startMatchButton.onClick.AddListener(OnStartMatch);
        if (leaveButton) leaveButton.onClick.AddListener(OnLeave);
        if (copyCodeButton) copyCodeButton.onClick.AddListener(OnCopyCode);
        if (notificationDismissButton) notificationDismissButton.onClick.AddListener(DismissNotification);

        // Subscribe to session events
        var mgr = MultiplayerSessionManager.Instance;
        if (mgr)
        {
            mgr.OnLobbyUpdated += OnLobbyUpdated;
            mgr.OnError += ShowNotification;
            mgr.OnMatchStarting += OnMatchStarting;
            mgr.OnSessionEnded += OnSessionEnded;
        }

        // Initial panel state
        HideAllPanels();
        InitializeLobbyView();
    }

    private void OnDestroy()
    {
        _spinnerSeq?.Kill();
        _notifSeq?.Kill();
        DOTween.Kill(this);

        var mgr = MultiplayerSessionManager.Instance;
        if (mgr)
        {
            mgr.OnLobbyUpdated -= OnLobbyUpdated;
            mgr.OnError -= ShowNotification;
            mgr.OnMatchStarting -= OnMatchStarting;
            mgr.OnSessionEnded -= OnSessionEnded;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Initialization
    // ═══════════════════════════════════════════════════════════

    private void InitializeLobbyView()
    {
        var mgr = MultiplayerSessionManager.Instance;
        if (mgr == null || mgr.CurrentLobby == null)
        {
            ShowNotification("No active lobby. Returning to menu...");
            Invoke(nameof(ReturnToMenu), 2f);
            return;
        }

        // Animate panels in
        AnimateEntrance();

        // Configure host vs client controls
        ConfigureControls(mgr.IsHost);

        // Set lobby code
        UpdateLobbyCode(mgr.CurrentLobby);

        // Initial lobby update
        UpdateLobbyDisplay(mgr.CurrentLobby);
    }

    private void ConfigureControls(bool isHost)
    {
        // Start Match is host-only
        if (startMatchButton)
        {
            startMatchButton.gameObject.SetActive(isHost);
            startMatchButton.interactable = false; // Disabled until conditions met
            if (startMatchButtonText) startMatchButtonText.text = "Start Match";
        }

        // Ready button is client-only (host doesn't need to ready up)
        if (readyButton)
        {
            readyButton.gameObject.SetActive(!isHost);
            if (readyButtonText) readyButtonText.text = "Ready";
        }

        _isReady = false;

        // Host indicator
        if (hostIndicatorText)
        {
            hostIndicatorText.text = isHost ? "You are the Host" : "Waiting Room";
            hostIndicatorText.gameObject.SetActive(true);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Panel Management
    // ═══════════════════════════════════════════════════════════

    private void HideAllPanels()
    {
        HidePanelInstant(headerPanel);
        HidePanelInstant(codePanel);
        HidePanelInstant(controlsPanel);
        HidePanelInstant(statusPanel);
        HidePanelInstant(notificationPanel);
    }

    private void HidePanelInstant(CanvasGroup panel)
    {
        if (panel == null) return;
        panel.alpha = 0f;
        panel.interactable = false;
        panel.blocksRaycasts = false;
    }

    private void ShowPanel(CanvasGroup panel, RectTransform rect = null, float delay = 0f)
    {
        if (panel == null) return;
        panel.alpha = 0f;
        panel.interactable = false;
        panel.blocksRaycasts = false;
        if (rect) rect.localScale = Vector3.one * 0.85f;

        Sequence seq = DOTween.Sequence();
        if (delay > 0f) seq.AppendInterval(delay);
        seq.Append(panel.DOFade(1f, panelFadeDuration));
        if (rect) seq.Join(rect.DOScale(Vector3.one, panelScaleDuration).SetEase(Ease.OutBack));
        seq.OnComplete(() =>
        {
            panel.interactable = true;
            panel.blocksRaycasts = true;
        });
    }

    private void AnimateEntrance()
    {
        // Header first
        ShowPanel(headerPanel, headerRect, 0f);

        // Code panel
        ShowPanel(codePanel, codePanelRect, 0.1f);

        // Player slots — staggered
        for (int i = 0; i < playerSlots.Length; i++)
        {
            if (playerSlots[i] == null) continue;
            var slotRect = playerSlots[i].GetComponent<RectTransform>();
            if (slotRect)
            {
                slotRect.localScale = Vector3.zero;
                float slotDelay = 0.2f + i * slotStaggerDelay;
                slotRect.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack).SetDelay(slotDelay);
            }
        }

        // Controls panel last
        ShowPanel(controlsPanel, controlsRect, 0.5f);
    }

    // ═══════════════════════════════════════════════════════════
    // Lobby Update Handler
    // ═══════════════════════════════════════════════════════════

    private void OnLobbyUpdated(Lobby lobby)
    {
        if (lobby == null) return;
        UpdateLobbyDisplay(lobby);
    }

    private void UpdateLobbyDisplay(Lobby lobby)
    {
        // Header
        if (lobbyNameText)
            lobbyNameText.text = string.IsNullOrWhiteSpace(lobby.Name) ? "Lobby" : lobby.Name;

        if (playerCountText)
            playerCountText.text = $"{lobby.Players.Count}/{lobby.MaxPlayers}";

        // Update player slots
        UpdatePlayerSlots(lobby);

        // Update start button state (host only)
        UpdateStartButton();
    }

    private void UpdatePlayerSlots(Lobby lobby)
    {
        string hostId = lobby.HostId;

        for (int i = 0; i < playerSlots.Length; i++)
        {
            if (playerSlots[i] == null) continue;

            if (i < lobby.Players.Count)
            {
                var player = lobby.Players[i];
                string playerName = "Unknown";
                bool isReady = false;

                if (player.Data != null)
                {
                    if (player.Data.TryGetValue("name", out var nameData))
                        playerName = nameData.Value;

                    if (player.Data.TryGetValue("ready", out var readyData))
                        isReady = readyData.Value == "1";
                }

                bool isHost = player.Id == hostId;

                // Only animate if this is a different player or state change
                bool wasOccupied = playerSlots[i].IsOccupied;
                bool wasReady = playerSlots[i].IsReady;
                bool samePlayer = playerSlots[i].PlayerId == player.Id;

                if (!wasOccupied || !samePlayer)
                {
                    // New player joined this slot
                    playerSlots[i].SetOccupied(player.Id, playerName, isHost, isReady, animate: true);
                }
                else if (wasReady != isReady)
                {
                    // Same player, ready state changed
                    playerSlots[i].SetReady(isReady);
                }
                else
                {
                    // Same player, same state — update quietly
                    playerSlots[i].SetOccupied(player.Id, playerName, isHost, isReady, animate: false);
                }
            }
            else
            {
                // Empty slot
                if (playerSlots[i].IsOccupied)
                {
                    playerSlots[i].AnimateLeave();
                }
                else
                {
                    playerSlots[i].SetEmpty(animate: false);
                }
            }
        }
    }

    private void UpdateLobbyCode(Lobby lobby)
    {
        if (lobbyCodeText == null) return;

        string code = lobby?.LobbyCode;
        if (!string.IsNullOrEmpty(code))
        {
            lobbyCodeText.text = code;
        }
        else
        {
            lobbyCodeText.text = "—";
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Start Button Logic
    // ═══════════════════════════════════════════════════════════

    private void UpdateStartButton()
    {
        var mgr = MultiplayerSessionManager.Instance;
        if (mgr == null || startMatchButton == null) return;

        if (!mgr.IsHost)
        {
            startMatchButton.gameObject.SetActive(false);
            return;
        }

        bool canStart = mgr.CanStartMatch();
        bool wasInteractable = startMatchButton.interactable;
        startMatchButton.interactable = canStart && !_isStartingMatch;

        // Subtle pulse when start becomes available
        if (canStart && !wasInteractable && !_isStartingMatch)
        {
            var rt = startMatchButton.GetComponent<RectTransform>();
            if (rt)
            {
                rt.DOKill();
                rt.DOPunchScale(Vector3.one * 0.08f, 0.4f, 6, 0.5f);
            }
        }

        // Update text
        if (startMatchButtonText)
        {
            if (_isStartingMatch)
                startMatchButtonText.text = "Starting...";
            else if (canStart)
                startMatchButtonText.text = "Start Match";
            else
            {
                int count = mgr.GetPlayerCount();
                if (count < MultiplayerSessionManager.MinPlayersToStart)
                    startMatchButtonText.text = $"Need {MultiplayerSessionManager.MinPlayersToStart}+ Players";
                else
                    startMatchButtonText.text = "Waiting for Ready...";
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Button Handlers
    // ═══════════════════════════════════════════════════════════

    private async void OnReadyToggle()
    {
        if (_isBusy) return;
        _isBusy = true;

        AnimatePress(readyButton);

        _isReady = !_isReady;

        // Optimistic UI update
        if (readyButtonText)
            readyButtonText.text = _isReady ? "Not Ready" : "Ready";

        var mgr = MultiplayerSessionManager.Instance;
        if (mgr != null)
        {
            bool success = await mgr.SetPlayerReadyAsync(_isReady);
            if (!success)
            {
                // Revert on failure
                _isReady = !_isReady;
                if (readyButtonText)
                    readyButtonText.text = _isReady ? "Not Ready" : "Ready";
            }
        }

        _isBusy = false;
    }

    private async void OnStartMatch()
    {
        if (_isBusy || _isStartingMatch) return;

        var mgr = MultiplayerSessionManager.Instance;
        if (mgr == null || !mgr.CanStartMatch()) return;

        _isBusy = true;
        _isStartingMatch = true;

        AnimatePress(startMatchButton);

        // Show starting state
        if (startMatchButton) startMatchButton.interactable = false;
        if (startMatchButtonText) startMatchButtonText.text = "Starting...";
        ShowStatus("Starting match...");

        bool success = await mgr.StartMatchAsync();

        if (!success)
        {
            _isStartingMatch = false;
            _isBusy = false;
            HideStatus();
            UpdateStartButton();
        }
        // If success, scene will transition via NetworkSceneManager
    }

    private void OnLeave()
    {
        if (_isBusy) return;
        _isBusy = true;

        AnimatePress(leaveButton);
        ShowStatus("Leaving lobby...");

        var mgr = MultiplayerSessionManager.Instance;
        if (mgr != null)
        {
            mgr.LeaveSession();
        }
        else
        {
            ReturnToMenu();
        }
    }

    private void OnCopyCode()
    {
        AnimatePress(copyCodeButton);

        if (lobbyCodeText == null || string.IsNullOrEmpty(lobbyCodeText.text)) return;

        string code = lobbyCodeText.text;
        if (code == "—") return;

        // Copy to clipboard
        GUIUtility.systemCopyBuffer = code;

        // Visual feedback
        if (copyButtonText)
        {
            if (string.IsNullOrEmpty(_previousCopyText))
                _previousCopyText = copyButtonText.text;

            copyButtonText.text = "Copied!";

            // Revert after delay
            DOVirtual.DelayedCall(1.5f, () =>
            {
                if (copyButtonText && !string.IsNullOrEmpty(_previousCopyText))
                    copyButtonText.text = _previousCopyText;
            });
        }

        // Scale punch on the code text
        if (lobbyCodeText)
        {
            var rt = lobbyCodeText.GetComponent<RectTransform>();
            if (rt)
            {
                rt.DOKill();
                rt.DOPunchScale(Vector3.one * 0.1f, 0.3f, 5, 0.5f);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Session Event Handlers
    // ═══════════════════════════════════════════════════════════

    private void OnMatchStarting()
    {
        ShowStatus("Match is starting...");

        // Disable all controls
        if (readyButton) readyButton.interactable = false;
        if (startMatchButton) startMatchButton.interactable = false;
        if (leaveButton) leaveButton.interactable = false;
        if (copyCodeButton) copyCodeButton.interactable = false;
    }

    private void OnSessionEnded()
    {
        // Session manager handles cleanup and scene transition
        Debug.Log("[LobbyUI] Session ended.");
    }

    private void ReturnToMenu()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            MultiplayerSessionManager.MultiplayerMenuScene);
    }

    // ═══════════════════════════════════════════════════════════
    // Status / Loading
    // ═══════════════════════════════════════════════════════════

    private void ShowStatus(string message)
    {
        if (statusText) statusText.text = message;
        if (statusPanel)
        {
            statusPanel.alpha = 0f;
            statusPanel.blocksRaycasts = true;
            statusPanel.DOFade(1f, 0.2f);
        }
        StartSpinner();
    }

    private void HideStatus()
    {
        if (statusPanel)
        {
            statusPanel.DOFade(0f, 0.2f).OnComplete(() =>
            {
                statusPanel.blocksRaycasts = false;
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
        if (notificationRect)
            _notifSeq.Join(notificationRect.DOScale(1f, 0.3f).SetEase(Ease.OutBack));
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
