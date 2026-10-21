using System;
using DG.Tweening;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI component for a single lobby entry row in the lobby browser.
/// Instantiated by <see cref="MultiplayerMenuUI.RefreshLobbyList"/>.
/// </summary>
public class LobbyEntryUI : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private TextMeshProUGUI lobbyNameText;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Join")]
    [SerializeField] private Button joinButton;
    [SerializeField] private TextMeshProUGUI joinButtonText;

    // ─── Internal ───────────────────────────────────────────────
    private string _lobbyId;
    private Action<string> _onJoinCallback;
    private bool _isJoining;

    /// <summary>
    /// Initialize this entry with lobby data and a join callback.
    /// Called once after instantiation by the lobby browser.
    /// </summary>
    /// <param name="lobby">The lobby to display.</param>
    /// <param name="onJoin">Callback invoked with the lobby ID when the join button is pressed.</param>
    public void Setup(Lobby lobby, Action<string> onJoin)
    {
        _lobbyId = lobby.Id;
        _onJoinCallback = onJoin;
        _isJoining = false;

        // Lobby name
        if (lobbyNameText)
            lobbyNameText.text = string.IsNullOrWhiteSpace(lobby.Name) ? "Unnamed Lobby" : lobby.Name;

        // Player count (e.g. "2/4")
        if (playerCountText)
            playerCountText.text = $"{lobby.Players.Count}/{lobby.MaxPlayers}";

        // Status
        if (statusText)
        {
            int available = lobby.MaxPlayers - lobby.Players.Count;
            if (lobby.IsLocked)
                statusText.text = "In Game";
            else if (available <= 0)
                statusText.text = "Full";
            else
                statusText.text = "Open";
        }

        // Join button
        if (joinButton)
        {
            bool canJoin = !lobby.IsLocked && lobby.Players.Count < lobby.MaxPlayers;
            joinButton.interactable = canJoin;
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(OnJoinPressed);

            if (joinButtonText)
                joinButtonText.text = canJoin ? "Join" : "Full";
        }
    }

    private void OnJoinPressed()
    {
        if (_isJoining || string.IsNullOrEmpty(_lobbyId)) return;
        _isJoining = true;

        // Button press animation
        if (joinButton)
        {
            var rt = joinButton.GetComponent<RectTransform>();
            if (rt)
            {
                rt.DOKill();
                Sequence seq = DOTween.Sequence();
                seq.Append(rt.DOScale(0.9f, 0.06f));
                seq.Append(rt.DOScale(1f, 0.12f).SetEase(Ease.OutBack));
            }

            joinButton.interactable = false;
            if (joinButtonText) joinButtonText.text = "Joining...";
        }

        _onJoinCallback?.Invoke(_lobbyId);
    }

    private void OnDestroy()
    {
        DOTween.Kill(this);
        if (joinButton)
        {
            var rt = joinButton.GetComponent<RectTransform>();
            if (rt) rt.DOKill();
        }
    }
}
