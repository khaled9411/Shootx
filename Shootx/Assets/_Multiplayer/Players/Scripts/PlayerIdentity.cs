using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Synchronizes the player's display name across all clients using NGO NetworkVariable.
/// Reads the name from MultiplayerSessionManager or local PlayerPrefs on owner spawn.
/// </summary>
public class PlayerIdentity : NetworkBehaviour
{
    private readonly NetworkVariable<FixedString64Bytes> playerName = new NetworkVariable<FixedString64Bytes>(
        string.Empty,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>
    /// Event fired on all clients when this player's name changes or is initialized.
    /// </summary>
    public event Action<string> OnPlayerNameChanged;

    /// <summary>
    /// Current synchronized player name.
    /// </summary>
    public string PlayerName => playerName.Value.IsEmpty ? "Player" : playerName.Value.ToString();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        playerName.OnValueChanged += HandleNameChanged;

        if (IsOwner)
        {
            string localName = GetLocalPlayerName();
            SetPlayerNameServerRpc(localName);
        }

        // Fire initial event if already set
        if (!playerName.Value.IsEmpty)
        {
            OnPlayerNameChanged?.Invoke(playerName.Value.ToString());
        }
    }

    public override void OnNetworkDespawn()
    {
        playerName.OnValueChanged -= HandleNameChanged;
    }

    private void HandleNameChanged(FixedString64Bytes oldValue, FixedString64Bytes newValue)
    {
        OnPlayerNameChanged?.Invoke(newValue.ToString());
    }

    private string GetLocalPlayerName()
    {
        if (MultiplayerSessionManager.Instance != null)
        {
            string sessionName = MultiplayerSessionManager.Instance.GetPlayerName();
            if (!string.IsNullOrWhiteSpace(sessionName)) return sessionName;
        }

        string prefsName = PlayerPrefs.GetString("PlayerName", "");
        if (!string.IsNullOrWhiteSpace(prefsName)) return prefsName;

        return $"Player_{UnityEngine.Random.Range(1000, 9999)}";
    }

    [ServerRpc]
    private void SetPlayerNameServerRpc(FixedString64Bytes newName)
    {
        if (newName.IsEmpty)
        {
            newName = $"Player_{OwnerClientId}";
        }
        playerName.Value = newName;
    }
}
