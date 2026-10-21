using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central session manager for all multiplayer lobby operations.
/// Owns the lifecycle: create/join lobby, Relay allocation, heartbeat, cleanup.
/// Persists via DontDestroyOnLoad alongside the NetworkManager root.
/// </summary>
public class MultiplayerSessionManager : MonoBehaviour
{
    // ─── Singleton ──────────────────────────────────────────────
    public static MultiplayerSessionManager Instance { get; private set; }

    // ─── Constants ──────────────────────────────────────────────
    public const int MaxPlayers = 4;
    public const int MinPlayersToStart = 2;
    public const string RelayJoinCodeKey = "rjc";     // lobby data key
    public const string RelayProtocolKey = "rp";      // lobby data key for relay protocol
    public const string GameVersionKey = "gv";         // lobby data key
    public const string GameVersion = "1.0";
    public const string DefaultRelayProtocol = "udp";  // raw udp avoids DTLS handshake drops on ISP/firewall/DPI
    public const string GameplaySceneName = "MainMultiplayer";
    public const string MultiplayerMenuScene = "MultiplayerMenu";
    public const string MultiplayerLobbyScene = "MultiplayerLobby";

    private const float HeartbeatInterval = 15f;
    private const float LobbyPollInterval = 2f;

    // ─── Session State ──────────────────────────────────────────
    public enum SessionState
    {
        Idle,
        Creating,
        Joining,
        InLobby,
        StartingMatch,
        InGame,
        Leaving
    }

    public SessionState CurrentState { get; private set; } = SessionState.Idle;

    // ─── Lobby Data ─────────────────────────────────────────────
    public Lobby CurrentLobby { get; private set; }
    public bool IsHost { get; private set; }
    public string RelayJoinCode { get; private set; }
    public string LobbyCode => CurrentLobby?.LobbyCode;

    // ─── Events ─────────────────────────────────────────────────
    public event Action<Lobby> OnLobbyUpdated;
    public event Action<string> OnError;
    public event Action OnSessionEnded;
    public event Action OnMatchStarting;
    public event Action OnReturnToMenu;

    // ─── Internal ───────────────────────────────────────────────
    private Coroutine _heartbeatCoroutine;
    private Coroutine _lobbyPollCoroutine;
    private bool _isCleaningUp;
    private string _cachedPlayerName;

    // ═══════════════════════════════════════════════════════════
    // Unity Lifecycle
    // ═══════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
            CleanupSync();
        }
    }

    private void OnApplicationQuit()
    {
        CleanupSync();
    }

    // ═══════════════════════════════════════════════════════════
    // Player Name
    // ═══════════════════════════════════════════════════════════

    public string GetPlayerName()
    {
        if (string.IsNullOrWhiteSpace(_cachedPlayerName))
        {
            _cachedPlayerName = PlayerPrefs.GetString(PlayFabLoginManager.PlayerNameKey, "");
        }
        if (string.IsNullOrWhiteSpace(_cachedPlayerName))
        {
            _cachedPlayerName = $"Player_{UnityEngine.Random.Range(1000, 9999)}";
        }
        return _cachedPlayerName;
    }

    // ═══════════════════════════════════════════════════════════
    // Host: Create Public Lobby
    // ═══════════════════════════════════════════════════════════

    public async Task<bool> CreatePublicLobbyAsync(string lobbyName)
    {
        return await CreateLobbyInternalAsync(lobbyName, isPrivate: false);
    }

    // ═══════════════════════════════════════════════════════════
    // Host: Create Private Lobby
    // ═══════════════════════════════════════════════════════════

    public async Task<bool> CreatePrivateLobbyAsync(string lobbyName)
    {
        return await CreateLobbyInternalAsync(lobbyName, isPrivate: true);
    }

    // ═══════════════════════════════════════════════════════════
    // Client: Quick Match
    // ═══════════════════════════════════════════════════════════

    public async Task<bool> QuickMatchAsync()
    {
        if (CurrentState != SessionState.Idle)
        {
            RaiseError("Operation already in progress.");
            return false;
        }

        CurrentState = SessionState.Joining;

        try
        {
            if (!await MultiplayerServiceManager.EnsureInitializedAsync())
            {
                RaiseError("Failed to initialize multiplayer services.");
                CurrentState = SessionState.Idle;
                return false;
            }

            // Try to find and join an existing public lobby
            var queryOptions = new QueryLobbiesOptions
            {
                Count = 10,
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT),
                    new QueryFilter(QueryFilter.FieldOptions.IsLocked, "false", QueryFilter.OpOptions.EQ),
                },
                Order = new List<QueryOrder>
                {
                    new QueryOrder(false, QueryOrder.FieldOptions.AvailableSlots)
                }
            };

            var queryResponse = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);

            foreach (var lobby in queryResponse.Results)
            {
                // Filter by game version
                if (lobby.Data != null &&
                    lobby.Data.TryGetValue(GameVersionKey, out var versionData) &&
                    versionData.Value != GameVersion)
                    continue;

                try
                {
                    return await JoinLobbyByIdInternalAsync(lobby.Id);
                }
                catch (LobbyServiceException ex)
                {
                    Debug.LogWarning($"[MPSession] Failed to join lobby {lobby.Id}: {ex.Message}. Trying next...");
                    continue;
                }
            }

            // No lobby found — create one as host
            Debug.Log("[MPSession] QuickMatch: No suitable lobby found. Creating new one...");
            CurrentState = SessionState.Idle; // Reset so CreateLobby works
            return await CreatePublicLobbyAsync($"{GetPlayerName()}'s Game");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MPSession] QuickMatch failed: {ex.Message}");
            RaiseError($"Quick Match failed: {ex.Message}");
            await CleanupAsync();
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Client: Join by Lobby Code
    // ═══════════════════════════════════════════════════════════

    public async Task<bool> JoinByCodeAsync(string lobbyCode)
    {
        if (CurrentState != SessionState.Idle)
        {
            RaiseError("Operation already in progress.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(lobbyCode))
        {
            RaiseError("Please enter a lobby code.");
            return false;
        }

        CurrentState = SessionState.Joining;

        try
        {
            if (!await MultiplayerServiceManager.EnsureInitializedAsync())
            {
                RaiseError("Failed to initialize multiplayer services.");
                CurrentState = SessionState.Idle;
                return false;
            }

            string playerName = GetPlayerName();
            var joinOptions = new JoinLobbyByCodeOptions
            {
                Player = CreatePlayerData(playerName)
            };

            CurrentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, joinOptions);
            IsHost = false;

            Debug.Log($"[MPSession] Joined lobby by code: {CurrentLobby.Id}");

            // Get Relay join code from lobby data
            if (CurrentLobby.Data == null ||
                !CurrentLobby.Data.TryGetValue(RelayJoinCodeKey, out var relayData) ||
                string.IsNullOrEmpty(relayData.Value))
            {
                RaiseError("Lobby is missing relay connection data.");
                await CleanupAsync();
                return false;
            }

            RelayJoinCode = relayData.Value;

            string protocol = DefaultRelayProtocol;
            if (CurrentLobby.Data.TryGetValue(RelayProtocolKey, out var protoData) && !string.IsNullOrEmpty(protoData.Value))
            {
                protocol = protoData.Value;
            }

            // Join Relay
            var joinAllocation = await RelayService.Instance.JoinAllocationAsync(RelayJoinCode);
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            RelayServerData relayServerData = joinAllocation.ToRelayServerData(protocol);
            transport.SetRelayServerData(relayServerData);

            // Start NGO client
            NetworkManager.Singleton.StartClient();

            // Subscribe to network events
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.OnTransportFailure += OnNetworkTransportFailure;

            CurrentState = SessionState.InLobby;
            StartLobbyPolling();

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MPSession] Join by code failed: {ex.Message}");
            RaiseError($"Failed to join: {ex.Message}");
            await CleanupAsync();
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Client: Join by Lobby ID (from browser)
    // ═══════════════════════════════════════════════════════════

    public async Task<bool> JoinByIdAsync(string lobbyId)
    {
        if (CurrentState != SessionState.Idle)
        {
            RaiseError("Operation already in progress.");
            return false;
        }

        CurrentState = SessionState.Joining;

        try
        {
            if (!await MultiplayerServiceManager.EnsureInitializedAsync())
            {
                RaiseError("Failed to initialize multiplayer services.");
                CurrentState = SessionState.Idle;
                return false;
            }

            return await JoinLobbyByIdInternalAsync(lobbyId);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MPSession] Join by ID failed: {ex.Message}");
            RaiseError($"Failed to join: {ex.Message}");
            await CleanupAsync();
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Browse Public Lobbies
    // ═══════════════════════════════════════════════════════════

    public async Task<List<Lobby>> QueryPublicLobbiesAsync()
    {
        try
        {
            if (!await MultiplayerServiceManager.EnsureInitializedAsync())
                return new List<Lobby>();

            var queryOptions = new QueryLobbiesOptions
            {
                Count = 25,
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT),
                    new QueryFilter(QueryFilter.FieldOptions.IsLocked, "false", QueryFilter.OpOptions.EQ),
                },
                Order = new List<QueryOrder>
                {
                    new QueryOrder(false, QueryOrder.FieldOptions.Created)
                }
            };

            var response = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);

            // Filter by game version
            var filtered = new List<Lobby>();
            foreach (var lobby in response.Results)
            {
                if (lobby.Data != null &&
                    lobby.Data.TryGetValue(GameVersionKey, out var versionData) &&
                    versionData.Value != GameVersion)
                    continue;
                filtered.Add(lobby);
            }

            return filtered;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MPSession] Query lobbies failed: {ex.Message}");
            return new List<Lobby>();
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Player Readiness
    // ═══════════════════════════════════════════════════════════

    public async Task<bool> SetPlayerReadyAsync(bool isReady)
    {
        if (CurrentLobby == null) return false;

        try
        {
            string playerId = MultiplayerServiceManager.GetPlayerId();
            if (string.IsNullOrEmpty(playerId)) return false;

            var updateOptions = new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { "ready", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, isReady ? "1" : "0") }
                }
            };

            CurrentLobby = await LobbyService.Instance.UpdatePlayerAsync(
                CurrentLobby.Id, playerId, updateOptions);

            OnLobbyUpdated?.Invoke(CurrentLobby);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MPSession] Set ready failed: {ex.Message}");
            return false;
        }
    }

    public bool AreAllNonHostPlayersReady()
    {
        if (CurrentLobby == null) return false;

        string hostId = CurrentLobby.HostId;
        foreach (var player in CurrentLobby.Players)
        {
            if (player.Id == hostId) continue; // host doesn't need ready

            if (player.Data == null ||
                !player.Data.TryGetValue("ready", out var readyData) ||
                readyData.Value != "1")
                return false;
        }
        return true;
    }

    public int GetPlayerCount()
    {
        return CurrentLobby?.Players?.Count ?? 0;
    }

    public bool CanStartMatch()
    {
        if (!IsHost) return false;
        if (CurrentState != SessionState.InLobby) return false;

        int count = GetPlayerCount();
        if (count < MinPlayersToStart || count > MaxPlayers) return false;

        return AreAllNonHostPlayersReady();
    }

    // ═══════════════════════════════════════════════════════════
    // Host: Start Match
    // ═══════════════════════════════════════════════════════════

    public async Task<bool> StartMatchAsync()
    {
        if (!CanStartMatch())
        {
            RaiseError("Cannot start match. Check player count and readiness.");
            return false;
        }

        if (CurrentState == SessionState.StartingMatch) return false;
        CurrentState = SessionState.StartingMatch;

        try
        {
            // Lock the lobby
            var updateOptions = new UpdateLobbyOptions
            {
                IsLocked = true
            };
            CurrentLobby = await LobbyService.Instance.UpdateLobbyAsync(CurrentLobby.Id, updateOptions);

            OnMatchStarting?.Invoke();

            // Load gameplay scene via NGO scene management
            Debug.Log($"[MPSession] Loading {GameplaySceneName} via NetworkSceneManager...");
            var status = NetworkManager.Singleton.SceneManager.LoadScene(
                GameplaySceneName, LoadSceneMode.Single);

            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogError($"[MPSession] Failed to load scene: {status}");
                RaiseError($"Failed to load game scene: {status}");
                CurrentState = SessionState.InLobby;
                return false;
            }

            CurrentState = SessionState.InGame;
            StopHeartbeat();
            StopLobbyPolling();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MPSession] Start match failed: {ex.Message}");
            RaiseError($"Failed to start match: {ex.Message}");
            CurrentState = SessionState.InLobby;
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Leave / Cleanup
    // ═══════════════════════════════════════════════════════════

    public async void LeaveSession()
    {
        await CleanupAsync();
        ReturnToMenu();
    }

    public async Task CleanupAsync()
    {
        if (_isCleaningUp) return;
        _isCleaningUp = true;

        Debug.Log("[MPSession] Cleaning up session...");

        CurrentState = SessionState.Leaving;
        StopHeartbeat();
        StopLobbyPolling();

        // Unsubscribe network events
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.ConnectionApprovalCallback = null;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnTransportFailure -= OnNetworkTransportFailure;
        }

        // Shutdown NGO
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            try
            {
                NetworkManager.Singleton.Shutdown();
                Debug.Log("[MPSession] NetworkManager shut down.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MPSession] NGO shutdown warning: {ex.Message}");
            }
        }

        // Lobby cleanup
        if (CurrentLobby != null)
        {
            string lobbyId = CurrentLobby.Id;
            try
            {
                if (IsHost)
                {
                    await LobbyService.Instance.DeleteLobbyAsync(lobbyId);
                    Debug.Log($"[MPSession] Lobby {lobbyId} deleted.");
                }
                else
                {
                    string playerId = MultiplayerServiceManager.GetPlayerId();
                    if (!string.IsNullOrEmpty(playerId))
                    {
                        await LobbyService.Instance.RemovePlayerAsync(lobbyId, playerId);
                        Debug.Log($"[MPSession] Player removed from lobby {lobbyId}.");
                    }
                }
            }
            catch (LobbyServiceException ex)
            {
                Debug.LogWarning($"[MPSession] Lobby cleanup warning: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MPSession] Lobby cleanup warning: {ex.Message}");
            }
        }

        // Reset state
        CurrentLobby = null;
        IsHost = false;
        RelayJoinCode = null;
        CurrentState = SessionState.Idle;
        _isCleaningUp = false;

        OnSessionEnded?.Invoke();
        Debug.Log("[MPSession] Session cleanup complete.");
    }

    private void CleanupSync()
    {
        StopHeartbeat();
        StopLobbyPolling();
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.ConnectionApprovalCallback = null;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnTransportFailure -= OnNetworkTransportFailure;
        }
        CurrentLobby = null;
        IsHost = false;
        RelayJoinCode = null;
        CurrentState = SessionState.Idle;
    }

    private void ReturnToMenu()
    {
        Debug.Log("[MPSession] Returning to multiplayer menu...");
        OnReturnToMenu?.Invoke();
        SceneManager.LoadScene(MultiplayerMenuScene);
    }

    // ═══════════════════════════════════════════════════════════
    // Internal: Create Lobby
    // ═══════════════════════════════════════════════════════════

    private async Task<bool> CreateLobbyInternalAsync(string lobbyName, bool isPrivate)
    {
        if (CurrentState != SessionState.Idle)
        {
            RaiseError("Operation already in progress.");
            return false;
        }

        CurrentState = SessionState.Creating;

        try
        {
            if (!await MultiplayerServiceManager.EnsureInitializedAsync())
            {
                RaiseError("Failed to initialize multiplayer services.");
                CurrentState = SessionState.Idle;
                return false;
            }

            string playerName = GetPlayerName();

            // 1. Create Relay allocation (3 clients + host = 4 max)
            Debug.Log("[MPSession] Creating Relay allocation...");
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxPlayers - 1);
            RelayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log($"[MPSession] Relay join code: {RelayJoinCode}");

            // 2. Configure transport
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            RelayServerData relayServerData = allocation.ToRelayServerData(DefaultRelayProtocol);
            transport.SetRelayServerData(relayServerData);

            // 3. Create Unity Lobby
            if (string.IsNullOrWhiteSpace(lobbyName))
                lobbyName = $"{playerName}'s Game";

            var createOptions = new CreateLobbyOptions
            {
                IsPrivate = isPrivate,
                Player = CreatePlayerData(playerName),
                Data = new Dictionary<string, DataObject>
                {
                    { RelayJoinCodeKey, new DataObject(DataObject.VisibilityOptions.Member, RelayJoinCode) },
                    { GameVersionKey, new DataObject(DataObject.VisibilityOptions.Public, GameVersion) },
                    { RelayProtocolKey, new DataObject(DataObject.VisibilityOptions.Member, DefaultRelayProtocol) }
                }
            };

            CurrentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, MaxPlayers, createOptions);
            IsHost = true;
            Debug.Log($"[MPSession] Lobby created: {CurrentLobby.Id} (Code: {CurrentLobby.LobbyCode})");

            // 4. Configure NGO Host with ConnectionApproval to prevent premature spawning in Lobby
            NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
            NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;

            // Start NGO Host
            NetworkManager.Singleton.StartHost();

            // 5. Subscribe to events
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnTransportFailure += OnNetworkTransportFailure;

            // 6. Transition state FIRST before starting polling loops
            CurrentState = SessionState.InLobby;
            StartHeartbeat();
            StartLobbyPolling();

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MPSession] Create lobby failed: {ex.Message}");
            RaiseError($"Failed to create lobby: {ex.Message}");
            await CleanupAsync();
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Internal: Join Lobby by ID
    // ═══════════════════════════════════════════════════════════

    private async Task<bool> JoinLobbyByIdInternalAsync(string lobbyId)
    {
        string playerName = GetPlayerName();
        var joinOptions = new JoinLobbyByIdOptions
        {
            Player = CreatePlayerData(playerName)
        };

        CurrentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, joinOptions);
        IsHost = false;

        Debug.Log($"[MPSession] Joined lobby: {CurrentLobby.Id}");

        // Get Relay join code
        if (CurrentLobby.Data == null ||
            !CurrentLobby.Data.TryGetValue(RelayJoinCodeKey, out var relayData) ||
            string.IsNullOrEmpty(relayData.Value))
        {
            RaiseError("Lobby is missing relay connection data.");
            await CleanupAsync();
            return false;
        }

        RelayJoinCode = relayData.Value;

        string protocol = DefaultRelayProtocol;
        if (CurrentLobby.Data.TryGetValue(RelayProtocolKey, out var protoData) && !string.IsNullOrEmpty(protoData.Value))
        {
            protocol = protoData.Value;
        }

        // Join Relay
        var joinAllocation = await RelayService.Instance.JoinAllocationAsync(RelayJoinCode);
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        RelayServerData relayServerData = joinAllocation.ToRelayServerData(protocol);
        transport.SetRelayServerData(relayServerData);

        // Start NGO client
        NetworkManager.Singleton.StartClient();

        // Subscribe to events
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        NetworkManager.Singleton.OnTransportFailure += OnNetworkTransportFailure;

        CurrentState = SessionState.InLobby;
        StartLobbyPolling();

        return true;
    }

    // ═══════════════════════════════════════════════════════════
    // Player Data Helper
    // ═══════════════════════════════════════════════════════════

    private Player CreatePlayerData(string playerName)
    {
        return new Player
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                { "name", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName) },
                { "ready", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "0") }
            }
        };
    }

    // ═══════════════════════════════════════════════════════════
    // Heartbeat
    // ═══════════════════════════════════════════════════════════

    private void StartHeartbeat()
    {
        StopHeartbeat();
        _heartbeatCoroutine = StartCoroutine(HeartbeatCoroutine());
    }

    private void StopHeartbeat()
    {
        if (_heartbeatCoroutine != null)
        {
            StopCoroutine(_heartbeatCoroutine);
            _heartbeatCoroutine = null;
        }
    }

    private IEnumerator HeartbeatCoroutine()
    {
        var wait = new WaitForSecondsRealtime(HeartbeatInterval);
        while (CurrentLobby != null && IsHost)
        {
            yield return wait;
            try
            {
                if (CurrentLobby != null)
                    LobbyService.Instance.SendHeartbeatPingAsync(CurrentLobby.Id);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MPSession] Heartbeat failed: {ex.Message}");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Lobby Polling
    // ═══════════════════════════════════════════════════════════

    private void StartLobbyPolling()
    {
        StopLobbyPolling();
        _lobbyPollCoroutine = StartCoroutine(LobbyPollCoroutine());
    }

    private void StopLobbyPolling()
    {
        if (_lobbyPollCoroutine != null)
        {
            StopCoroutine(_lobbyPollCoroutine);
            _lobbyPollCoroutine = null;
        }
    }

    private IEnumerator LobbyPollCoroutine()
    {
        var wait = new WaitForSecondsRealtime(LobbyPollInterval);
        while (CurrentLobby != null && (CurrentState == SessionState.InLobby || CurrentState == SessionState.Creating || CurrentState == SessionState.Joining))
        {
            yield return wait;
            try
            {
                PollLobbyAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MPSession] Lobby poll error: {ex.Message}");
            }
        }
    }

    private async void PollLobbyAsync()
    {
        if (CurrentLobby == null || (CurrentState != SessionState.InLobby && CurrentState != SessionState.Creating && CurrentState != SessionState.Joining)) return;

        try
        {
            CurrentLobby = await LobbyService.Instance.GetLobbyAsync(CurrentLobby.Id);
            OnLobbyUpdated?.Invoke(CurrentLobby);
        }
        catch (LobbyServiceException ex)
        {
            if (ex.Reason == LobbyExceptionReason.LobbyNotFound)
            {
                Debug.Log("[MPSession] Lobby no longer exists. Returning to menu...");
                await CleanupAsync();
                ReturnToMenu();
            }
            else
            {
                Debug.LogWarning($"[MPSession] Lobby poll warning: {ex.Message}");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Network Event Handlers
    // ═══════════════════════════════════════════════════════════

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[MPSession] Client {clientId} connected via NGO.");
        if (IsHost)
        {
            PollLobbyAsync();
        }
    }

    private async void OnNetworkTransportFailure()
    {
        Debug.LogError("[MPSession] Transport failure detected! Cleaning up session...");
        RaiseError("Lost connection to Relay server. Transport failure.");
        await CleanupAsync();
        ReturnToMenu();
    }

    private async void OnClientDisconnected(ulong clientId)
    {
        // If we (the local client) were disconnected
        if (NetworkManager.Singleton == null) return;

        if (IsHost)
        {
            // If the local host itself was disconnected (e.g. transport failure or shutdown)
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.LogWarning("[MPSession] Host local client disconnected. Cleaning up...");
                RaiseError("Host lost connection to the network.");
                await CleanupAsync();
                ReturnToMenu();
                return;
            }

            // A remote client disconnected — refresh lobby immediately
            Debug.Log($"[MPSession] Remote client {clientId} disconnected from host.");
            PollLobbyAsync();
        }
        else
        {
            // We were disconnected (client)
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log("[MPSession] Local client disconnected. Cleaning up...");
                await CleanupAsync();
                ReturnToMenu();
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Error Handling
    // ═══════════════════════════════════════════════════════════

    private void RaiseError(string message)
    {
        Debug.LogWarning($"[MPSession] Error: {message}");
        OnError?.Invoke(message);
    }

    // ═══════════════════════════════════════════════════════════
    // NGO Connection Approval & Scene Events
    // ═══════════════════════════════════════════════════════════

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        // Approve client connection to the lobby session
        response.Approved = true;

        // CRITICAL: Prevent auto-spawning player prefab upon connection!
        // Spawning will only occur inside the gameplay scene (MainMultiplayer).
        response.CreatePlayerObject = false;
        response.Pending = false;
        Debug.Log($"[MPSession] Approved client {request.ClientNetworkId} without auto-spawn.");
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == GameplaySceneName)
        {
            CurrentState = SessionState.InGame;
            Debug.Log($"[MPSession] Gameplay scene '{GameplaySceneName}' loaded.");

            // Ensure MultiplayerSpawnManager exists on Host
            if (IsHost && MultiplayerSpawnManager.Instance == null)
            {
                var go = new GameObject("MultiplayerSpawnManager");
                go.AddComponent<MultiplayerSpawnManager>();
                Debug.Log("[MPSession] Auto-created MultiplayerSpawnManager in gameplay scene.");
            }
        }
    }
}
