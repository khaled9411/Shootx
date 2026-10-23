using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Server-authoritative match manager.
///
/// Responsibilities:
///   - Match state machine (WaitingForPlayers → PreMatchCountdown → Active → Ending → Results → ReturningToLobby)
///   - Map selection and initialization
///   - Synchronized pre-match countdown and match timer
///   - Kill/death attribution via PlayerHealth.OnDeath
///   - Spawn protection
///   - Winner resolution (10-kill target, timer expiry, tie-break)
///   - Final results snapshot synchronized to all clients
///   - Scene transition back to MultiplayerLobby
///
/// Attach to a GameObject in MainMultiplayer that has a NetworkObject.
/// The host/server auto-creates this when the gameplay scene loads.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class MatchManager : NetworkBehaviour
{
    // ─── Singleton ──────────────────────────────────────────────────────
    public static MatchManager Instance { get; private set; }

    // ─── Inspector ──────────────────────────────────────────────────────
    [Header("Configuration")]
    [SerializeField] private MatchConfig config;

    [Header("Map Prefabs (PVP Map 1–4)")]
    [SerializeField] private GameObject[] mapPrefabs = new GameObject[4];

    [Header("Collision Checks for Spawn Validation")]
    [SerializeField] private LayerMask spawnObstacleMask;
    [SerializeField] private float spawnCheckRadius = 0.5f;

    // ─── Match State ─────────────────────────────────────────────────────

    /// <summary>Current match state synchronized to all clients.</summary>
    private NetworkVariable<MatchState> _matchState = new NetworkVariable<MatchState>(
        MatchState.WaitingForPlayers,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// <summary>Server time when Active state began (for timer derivation).</summary>
    private NetworkVariable<double> _matchStartServerTime = new NetworkVariable<double>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// <summary>Remaining pre-match countdown value shown to clients.</summary>
    private NetworkVariable<int> _countdownValue = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// <summary>Index of the selected map prefab.</summary>
    private NetworkVariable<int> _selectedMapIndex = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // ─── Results ────────────────────────────────────────────────────────

    private NetworkVariable<bool> _resultFinalized = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // Replicated results data
    private NetworkVariable<FixedString64Bytes> _winnerName = new NetworkVariable<FixedString64Bytes>(
        "",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkVariable<ulong> _winnerClientId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkVariable<bool> _isDraw = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // ─── Events (all clients) ────────────────────────────────────────────

    public event Action<MatchState> OnMatchStateChanged;
    public event Action<int> OnCountdownChanged;       // countdown tick value
    public event Action OnMatchGoSignal;               // GO!
    public event Action<float> OnMatchTimeUpdated;     // remaining seconds
    public event Action<MatchResultData> OnResultsReady;
    public event Action OnReturningToLobby;

    // ─── Server-only State ───────────────────────────────────────────────

    private GameObject _activeMapInstance;
    private int _lastMapIndex = -1;
    private bool _sceneReturnRequested;
    private Coroutine _matchCoroutine;
    private Coroutine _returnCoroutine;
    private string _matchId;

    // Player tracking (server only)
    private readonly Dictionary<ulong, PlayerHealth> _playerHealthMap = new Dictionary<ulong, PlayerHealth>();
    private readonly Dictionary<ulong, PlayerMatchStats> _playerStatsMap = new Dictionary<ulong, PlayerMatchStats>();
    private readonly Dictionary<ulong, string> _playerNameMap = new Dictionary<ulong, string>();
    private readonly Dictionary<ulong, string> _playerAuthIdMap = new Dictionary<ulong, string>();

    // Spawn protection timers (server only)
    private readonly Dictionary<ulong, float> _spawnProtectionEndTimes = new Dictionary<ulong, float>();

    // Active respawn coroutines (server only) — cancel on match end
    private readonly Dictionary<ulong, Coroutine> _respawnCoroutines = new Dictionary<ulong, Coroutine>();

    // Cached map spawn points (server only)
    private readonly List<Transform> _activeSpawnPoints = new List<Transform>();

    // Configurable defaults if no MatchConfig assigned
    private float KillTarget => config != null ? config.killTarget : 10;
    private float MatchDuration => config != null ? config.matchDuration : 180f;
    private float PreMatchCountdown => config != null ? config.preMatchCountdown : 3f;
    private float RespawnDelay => config != null ? config.respawnDelay : 3f;
    private float SpawnProtectionDuration => config != null ? config.spawnProtectionDuration : 1.5f;
    private float MinSpawnDistance => config != null ? config.minSpawnDistanceFromPlayers : 8f;
    private float ResultsDuration => config != null ? config.resultsDisplayDuration : 8f;

    // ═══════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ═══════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _matchState.OnValueChanged += HandleMatchStateChanged;
        _countdownValue.OnValueChanged += HandleCountdownChanged;
        _selectedMapIndex.OnValueChanged += HandleMapIndexChanged;
        _resultFinalized.OnValueChanged += HandleResultFinalizedChanged;

        if (IsServer)
        {
            _matchId = System.Guid.NewGuid().ToString();
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

            _matchCoroutine = StartCoroutine(MatchLifecycleCoroutine());
        }
    }

    public override void OnNetworkDespawn()
    {
        _matchState.OnValueChanged -= HandleMatchStateChanged;
        _countdownValue.OnValueChanged -= HandleCountdownChanged;
        _selectedMapIndex.OnValueChanged -= HandleMapIndexChanged;
        _resultFinalized.OnValueChanged -= HandleResultFinalizedChanged;

        if (IsServer)
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
            }
            CancelAllRespawnCoroutines();
        }

        if (Instance == this)
            Instance = null;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (Instance == this)
            Instance = null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Update — Client-side match timer
    // ═══════════════════════════════════════════════════════════════════════

    private void Update()
    {
        if (_matchState.Value != MatchState.Active) return;
        if (_matchStartServerTime.Value <= 0) return;

        double elapsed = NetworkManager.Singleton.ServerTime.Time - _matchStartServerTime.Value;
        float remaining = Mathf.Max(0f, MatchDuration - (float)elapsed);
        OnMatchTimeUpdated?.Invoke(remaining);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Match Lifecycle Coroutine (Server only)
    // ═══════════════════════════════════════════════════════════════════════

    private IEnumerator MatchLifecycleCoroutine()
    {
        // Phase 1: Wait for players and map setup
        SetState(MatchState.WaitingForPlayers);

        // Select and load map
        yield return StartCoroutine(SelectAndLoadMapCoroutine());

        // Register all connected players
        yield return StartCoroutine(RegisterPlayersCoroutine());

        // Spawn all players
        SpawnAllPlayers();

        // Phase 2: Pre-match countdown
        SetState(MatchState.PreMatchCountdown);
        yield return StartCoroutine(RunCountdownCoroutine());

        // Phase 3: Active match
        SetState(MatchState.Active);
        _matchStartServerTime.Value = NetworkManager.Singleton.ServerTime.Time;

        // Run match timer
        float elapsed = 0f;
        while (elapsed < MatchDuration && _matchState.Value == MatchState.Active)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Only auto-end if still active (not already ended by kill threshold)
        if (_matchState.Value == MatchState.Active)
        {
            FinalizeMatch();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Map Selection and Loading
    // ═══════════════════════════════════════════════════════════════════════

    private IEnumerator SelectAndLoadMapCoroutine()
    {
        if (mapPrefabs == null || mapPrefabs.Length == 0)
        {
            Debug.LogWarning("[MatchManager] No map prefabs configured. Using existing scene objects.");
            // Find existing spawn points from the scene
            DiscoverSpawnPointsFromScene();
            yield break;
        }

        // Select a random valid map (avoid immediate repeat if configured)
        int mapCount = mapPrefabs.Length;
        int selectedIndex = UnityEngine.Random.Range(0, mapCount);

        if (config != null && config.avoidRepeatMap && mapCount > 1)
        {
            int attempts = 0;
            while (selectedIndex == _lastMapIndex && attempts < 10)
            {
                selectedIndex = UnityEngine.Random.Range(0, mapCount);
                attempts++;
            }
        }

        // Validate the selected prefab
        if (mapPrefabs[selectedIndex] == null)
        {
            Debug.LogError($"[MatchManager] Map prefab at index {selectedIndex} is null. Trying others...");
            bool found = false;
            for (int i = 0; i < mapPrefabs.Length; i++)
            {
                if (mapPrefabs[i] != null)
                {
                    selectedIndex = i;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                Debug.LogError("[MatchManager] No valid map prefabs! Cannot load map.");
                DiscoverSpawnPointsFromScene();
                yield break;
            }
        }

        _lastMapIndex = selectedIndex;
        _selectedMapIndex.Value = selectedIndex;

        // Unload previous map if any
        if (_activeMapInstance != null)
        {
            Destroy(_activeMapInstance);
            _activeMapInstance = null;
            yield return null; // Wait one frame for destruction
        }

        // Instantiate the selected map
        Debug.Log($"[MatchManager] Loading map '{mapPrefabs[selectedIndex].name}' (index {selectedIndex})");
        _activeMapInstance = Instantiate(mapPrefabs[selectedIndex], Vector3.zero, Quaternion.identity);
        _activeMapInstance.name = $"ActiveMap_{mapPrefabs[selectedIndex].name}";

        // Wait one frame for the map to initialize
        yield return null;
        yield return null;

        // Discover spawn points from the instantiated map
        DiscoverSpawnPointsFromMap(_activeMapInstance);

        // Wait for map ready (physics, colliders)
        float waitTimeout = config != null ? config.mapLoadTimeout : 30f;
        float waited = 0f;
        while (!IsMapReady() && waited < waitTimeout)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        if (!IsMapReady())
        {
            Debug.LogWarning("[MatchManager] Map load timed out. Proceeding with available spawn points.");
        }

        // Notify clients about the selected map
        NotifyMapSelectedClientRpc(selectedIndex);

        Debug.Log($"[MatchManager] Map ready. Spawn points found: {_activeSpawnPoints.Count}");
    }

    private bool IsMapReady()
    {
        return _activeMapInstance != null && _activeSpawnPoints.Count > 0;
    }

    private void DiscoverSpawnPointsFromMap(GameObject mapRoot)
    {
        _activeSpawnPoints.Clear();

        // Look for SpawnPoint tag or component within the map
        var tagged = mapRoot.GetComponentsInChildren<Transform>(true);
        foreach (var t in tagged)
        {
            if (t.CompareTag("SpawnPoint"))
            {
                _activeSpawnPoints.Add(t);
            }
        }

        // If no tagged points, look for MapSpawnPoint component
        if (_activeSpawnPoints.Count == 0)
        {
            var spawnComps = mapRoot.GetComponentsInChildren<MapSpawnPoint>(true);
            foreach (var sp in spawnComps)
            {
                _activeSpawnPoints.Add(sp.transform);
            }
        }

        // Fallback: use MultiplayerSpawnManager's assigned spawn points
        if (_activeSpawnPoints.Count == 0 && MultiplayerSpawnManager.Instance != null)
        {
            var points = MultiplayerSpawnManager.Instance.GetSpawnPoints();
            if (points != null)
                _activeSpawnPoints.AddRange(points);
        }

        Debug.Log($"[MatchManager] Discovered {_activeSpawnPoints.Count} spawn points from map.");
    }

    private void DiscoverSpawnPointsFromScene()
    {
        _activeSpawnPoints.Clear();

        // Try the existing MultiplayerSpawnManager first
        if (MultiplayerSpawnManager.Instance != null)
        {
            var points = MultiplayerSpawnManager.Instance.GetSpawnPoints();
            if (points != null)
                _activeSpawnPoints.AddRange(points);
        }

        // Fallback: tagged objects
        if (_activeSpawnPoints.Count == 0)
        {
            var tagged = GameObject.FindGameObjectsWithTag("SpawnPoint");
            foreach (var go in tagged)
                _activeSpawnPoints.Add(go.transform);
        }

        // Final fallback: MapSpawnPoint components in scene
        if (_activeSpawnPoints.Count == 0)
        {
            var comps = FindObjectsByType<MapSpawnPoint>(FindObjectsSortMode.None);
            foreach (var c in comps)
                _activeSpawnPoints.Add(c.transform);
        }

        Debug.Log($"[MatchManager] Discovered {_activeSpawnPoints.Count} spawn points from scene.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Player Registration
    // ═══════════════════════════════════════════════════════════════════════

    private IEnumerator RegisterPlayersCoroutine()
    {
        // Wait for all connected players to have spawned player objects
        float waitTime = 0f;
        const float maxWait = 5f;
        while (waitTime < maxWait)
        {
            bool allReady = true;
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject == null)
                {
                    allReady = false;
                    break;
                }
            }
            if (allReady) break;
            waitTime += Time.deltaTime;
            yield return null;
        }

        RegisterAllConnectedPlayers();
    }

    private void RegisterAllConnectedPlayers()
    {
        _playerHealthMap.Clear();
        _playerStatsMap.Clear();
        _playerNameMap.Clear();

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            RegisterPlayer(client.ClientId);
        }
    }

    private void RegisterPlayer(ulong clientId)
    {
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return;
        if (client.PlayerObject == null) return;

        var health = client.PlayerObject.GetComponent<PlayerHealth>();
        var stats = client.PlayerObject.GetComponent<PlayerMatchStats>();
        var identity = client.PlayerObject.GetComponent<PlayerIdentity>();

        if (health != null && !_playerHealthMap.ContainsKey(clientId))
        {
            _playerHealthMap[clientId] = health;
            health.OnDeath += (killerId) => HandlePlayerDeath(clientId, killerId);
        }

        if (stats != null)
        {
            _playerStatsMap[clientId] = stats;
            stats.ResetForNewMatch();
        }

        if (identity != null)
        {
            _playerNameMap[clientId] = identity.PlayerName;
            // Try to get Unity Auth ID from the session manager
            if (MultiplayerSessionManager.Instance != null)
            {
                _playerAuthIdMap[clientId] = MultiplayerServiceManager.GetPlayerId() ?? $"client_{clientId}";
            }
            else
            {
                _playerAuthIdMap[clientId] = $"client_{clientId}";
            }
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        if (_matchState.Value == MatchState.WaitingForPlayers)
        {
            StartCoroutine(RegisterPlayerDelayed(clientId));
        }
    }

    private IEnumerator RegisterPlayerDelayed(ulong clientId)
    {
        yield return new WaitForSeconds(0.5f);
        RegisterPlayer(clientId);
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        _playerHealthMap.Remove(clientId);

        if (_respawnCoroutines.TryGetValue(clientId, out var co) && co != null)
        {
            StopCoroutine(co);
            _respawnCoroutines.Remove(clientId);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Player Spawning
    // ═══════════════════════════════════════════════════════════════════════

    private void SpawnAllPlayers()
    {
        if (MultiplayerSpawnManager.Instance != null)
        {
            // Inform SpawnManager of our spawn points
            MultiplayerSpawnManager.Instance.SetSpawnPoints(_activeSpawnPoints);
            MultiplayerSpawnManager.Instance.SpawnAllConnectedPlayers();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Countdown
    // ═══════════════════════════════════════════════════════════════════════

    private IEnumerator RunCountdownCoroutine()
    {
        // Lock all player input
        SetAllPlayersInputLocked(true);

        int count = Mathf.CeilToInt(PreMatchCountdown);
        for (int i = count; i >= 1; i--)
        {
            _countdownValue.Value = i;
            yield return new WaitForSeconds(1f);
        }

        // GO signal
        _countdownValue.Value = 0;
        TriggerGoSignalClientRpc();

        // Brief delay after GO
        yield return new WaitForSeconds(0.5f);

        // Unlock player input
        SetAllPlayersInputLocked(false);
    }

    private void SetAllPlayersInputLocked(bool locked)
    {
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;
            var controller = client.PlayerObject.GetComponent<PlayerController>();
            if (controller != null)
                controller.SetInputLocked(locked);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Kill / Death Attribution
    // ═══════════════════════════════════════════════════════════════════════

    private void HandlePlayerDeath(ulong victimClientId, ulong killerClientId)
    {
        if (!IsServer) return;
        if (_matchState.Value != MatchState.Active) return;

        // Get victim stats
        if (!_playerStatsMap.TryGetValue(victimClientId, out var victimStats)) return;

        // Idempotency: only process if victim was actually alive
        if (victimStats.LifeState != PlayerLifeState.Alive &&
            victimStats.LifeState != PlayerLifeState.SpawnProtected) return;

        // Mark victim as dead
        victimStats.SetLifeState(PlayerLifeState.Dead);
        victimStats.AddDeath();

        // Award kill to attacker (if not self-kill)
        bool validKill = killerClientId != victimClientId
                      && killerClientId != ulong.MaxValue
                      && _playerStatsMap.ContainsKey(killerClientId);

        if (validKill)
        {
            var attackerStats = _playerStatsMap[killerClientId];
            attackerStats.AddKill();

            // Check win condition
            if (attackerStats.Kills >= KillTarget)
            {
                FinalizeMatch();
                // Schedule respawn is skipped — match is over
                return;
            }
        }

        // Schedule respawn
        if (_matchState.Value == MatchState.Active)
        {
            var co = StartCoroutine(RespawnPlayerCoroutine(victimClientId));
            _respawnCoroutines[victimClientId] = co;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Respawn
    // ═══════════════════════════════════════════════════════════════════════

    private IEnumerator RespawnPlayerCoroutine(ulong clientId)
    {
        if (!_playerStatsMap.TryGetValue(clientId, out var stats)) yield break;

        stats.SetLifeState(PlayerLifeState.Respawning);

        yield return new WaitForSeconds(RespawnDelay);

        // Guard: don't respawn if match ended
        if (_matchState.Value != MatchState.Active)
        {
            _respawnCoroutines.Remove(clientId);
            yield break;
        }

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) ||
            client.PlayerObject == null)
        {
            _respawnCoroutines.Remove(clientId);
            yield break;
        }

        // Find safe spawn position
        Vector3 spawnPos = GetSafeSpawnPosition(clientId);
        Quaternion spawnRot = GetSpawnRotation(spawnPos);

        // Reposition player (disable CC, move, re-enable)
        var go = client.PlayerObject.gameObject;
        var cc = go.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        go.transform.position = spawnPos;
        go.transform.rotation = spawnRot;
        if (cc != null) cc.enabled = true;

        // Restore health
        var health = go.GetComponent<PlayerHealth>();
        if (health != null)
            health.ServerRespawnRestore();

        // Restore ammo
        var shooting = go.GetComponent<PlayerShooting>();
        if (shooting != null)
            shooting.HandleRespawnServer();

        // Apply spawn protection
        stats.SetLifeState(PlayerLifeState.SpawnProtected);
        _spawnProtectionEndTimes[clientId] = Time.time + SpawnProtectionDuration;

        // Notify respawn (clears input lock, triggers visuals)
        NotifyRespawnClientRpc(clientId, spawnPos, spawnRot);

        yield return new WaitForSeconds(SpawnProtectionDuration);

        // Check still in spawn protection (not killed again)
        if (stats.LifeState == PlayerLifeState.SpawnProtected)
        {
            stats.SetLifeState(PlayerLifeState.Alive);
            EndSpawnProtectionClientRpc(clientId);
        }

        _respawnCoroutines.Remove(clientId);
    }

    private Vector3 GetSafeSpawnPosition(ulong excludeClientId)
    {
        if (_activeSpawnPoints.Count == 0)
        {
            Debug.LogWarning("[MatchManager] No spawn points available! Using world origin.");
            return new Vector3(0, 0.5f, 0);
        }

        // Collect positions of all living players (except the respawning one)
        var livingPositions = new List<Vector3>();
        foreach (var kvp in _playerStatsMap)
        {
            if (kvp.Key == excludeClientId) continue;
            if (kvp.Value.IsDead) continue;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(kvp.Key, out var c)) continue;
            if (c.PlayerObject == null) continue;
            livingPositions.Add(c.PlayerObject.transform.position);
        }

        // Score each spawn point: prefer high distance from opponents and valid position
        Transform bestPoint = null;
        float bestScore = float.MinValue;

        foreach (var sp in _activeSpawnPoints)
        {
            if (sp == null) continue;

            // Validate spawn position (no overlap with geometry)
            if (!IsSpawnPositionValid(sp.position)) continue;

            // Score = minimum distance to any living player
            float minDist = float.MaxValue;
            if (livingPositions.Count > 0)
            {
                foreach (var pos in livingPositions)
                    minDist = Mathf.Min(minDist, Vector3.Distance(sp.position, pos));
            }
            else
            {
                minDist = float.MaxValue;
            }

            if (minDist > bestScore)
            {
                bestScore = minDist;
                bestPoint = sp;
            }
        }

        if (bestPoint != null)
            return bestPoint.position;

        // Absolute fallback: first valid point, even if blocked
        Debug.LogWarning("[MatchManager] No safe spawn point found. Using first available.");
        return _activeSpawnPoints[0].position;
    }

    private bool IsSpawnPositionValid(Vector3 position)
    {
        return !Physics.CheckSphere(position, spawnCheckRadius, spawnObstacleMask, QueryTriggerInteraction.Ignore);
    }

    private Quaternion GetSpawnRotation(Vector3 spawnPos)
    {
        // Face toward center of the arena
        Vector3 toCenter = Vector3.zero - spawnPos;
        toCenter.y = 0f;
        if (toCenter.sqrMagnitude < 0.01f) return Quaternion.identity;
        return Quaternion.LookRotation(toCenter.normalized);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Spawn Protection
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Called by PlayerHealth.TakeDamage to check if spawn protection is active.
    /// Server only.
    /// </summary>
    public bool IsSpawnProtected(ulong clientId)
    {
        if (!IsServer) return false;
        if (!_playerStatsMap.TryGetValue(clientId, out var stats)) return false;
        if (stats.LifeState != PlayerLifeState.SpawnProtected) return false;
        if (!_spawnProtectionEndTimes.TryGetValue(clientId, out float endTime)) return false;
        return Time.time < endTime;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Match Finalization
    // ═══════════════════════════════════════════════════════════════════════

    private void FinalizeMatch()
    {
        if (!IsServer) return;
        if (_resultFinalized.Value) return;
        if (_matchState.Value == MatchState.Ending ||
            _matchState.Value == MatchState.Results ||
            _matchState.Value == MatchState.ReturningToLobby) return;

        SetState(MatchState.Ending);
        _resultFinalized.Value = true;

        // Lock all movement
        SetAllPlayersInputLocked(true);
        CancelAllRespawnCoroutines();

        // Determine winner
        var result = ComputeFinalResult();

        // Replicate winner info
        _winnerClientId.Value = result.WinnerClientId;
        _winnerName.Value = result.WinnerName;
        _isDraw.Value = result.IsDraw;

        // Send full results snapshot to all clients
        SendResultsToClients(result);

        SetState(MatchState.Results);

        // Save local records on host (host is also a client)
        LocalPlayerRecords.Instance?.RecordMatchResult(_matchId, result, _playerAuthIdMap, _playerNameMap);

        // Begin results countdown
        _returnCoroutine = StartCoroutine(ResultsCountdownCoroutine());
    }

    private MatchResultData ComputeFinalResult()
    {
        var entries = new List<PlayerResultEntry>();

        foreach (var kvp in _playerStatsMap)
        {
            string name = _playerNameMap.TryGetValue(kvp.Key, out var n) ? n : $"Player_{kvp.Key}";
            string authId = _playerAuthIdMap.TryGetValue(kvp.Key, out var id) ? id : $"client_{kvp.Key}";

            entries.Add(new PlayerResultEntry
            {
                ClientId = kvp.Key,
                AuthId = authId,
                DisplayName = name,
                Kills = kvp.Value.Kills,
                Deaths = kvp.Value.Deaths,
                BestKillStreak = kvp.Value.BestKillStreak,
                IsConnected = NetworkManager.Singleton.ConnectedClients.ContainsKey(kvp.Key)
            });
        }

        // Sort: kills desc, then deaths asc
        entries.Sort((a, b) =>
        {
            int killComp = b.Kills.CompareTo(a.Kills);
            if (killComp != 0) return killComp;
            return a.Deaths.CompareTo(b.Deaths);
        });

        // Assign ranks (competition ranking: 1,1,3)
        int rank = 1;
        for (int i = 0; i < entries.Count; i++)
        {
            if (i > 0 &&
                (entries[i].Kills != entries[i - 1].Kills ||
                 entries[i].Deaths != entries[i - 1].Deaths))
            {
                rank = i + 1;
            }
            entries[i].Rank = rank;
        }

        // Determine winner
        ulong winnerId = ulong.MaxValue;
        string winnerName = "";
        bool isDraw = false;

        if (entries.Count > 0)
        {
            var top = entries[0];
            // Check if multiple players share rank 1
            var tied = entries.FindAll(e => e.Rank == 1);
            if (tied.Count > 1)
            {
                isDraw = true;
                winnerName = "Draw";
            }
            else
            {
                winnerId = top.ClientId;
                winnerName = top.DisplayName;
            }
        }

        return new MatchResultData
        {
            MatchId = _matchId,
            Entries = entries,
            WinnerClientId = winnerId,
            WinnerName = winnerName,
            IsDraw = isDraw
        };
    }

    private void SendResultsToClients(MatchResultData result)
    {
        // Serialize the entries into a ClientRpc-safe format
        int count = result.Entries.Count;
        ulong[] clientIds = new ulong[count];
        int[] kills = new int[count];
        int[] deaths = new int[count];
        int[] bestStreaks = new int[count];
        int[] ranks = new int[count];
        FixedString64Bytes[] names = new FixedString64Bytes[count];
        bool[] connected = new bool[count];

        for (int i = 0; i < count; i++)
        {
            var e = result.Entries[i];
            clientIds[i] = e.ClientId;
            kills[i] = e.Kills;
            deaths[i] = e.Deaths;
            bestStreaks[i] = e.BestKillStreak;
            ranks[i] = e.Rank;
            names[i] = e.DisplayName.Length > 63 ? e.DisplayName.Substring(0, 63) : e.DisplayName;
            connected[i] = e.IsConnected;
        }

        ShowResultsClientRpc(clientIds, kills, deaths, bestStreaks, ranks, names, connected,
            result.WinnerClientId, result.IsDraw, result.MatchId);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Results Countdown and Return
    // ═══════════════════════════════════════════════════════════════════════

    private IEnumerator ResultsCountdownCoroutine()
    {
        yield return new WaitForSeconds(ResultsDuration);

        if (_matchState.Value != MatchState.ReturningToLobby)
        {
            TriggerReturnToLobby();
        }
    }

    /// <summary>Request early return from any client (validated by server).</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestEarlyReturnServerRpc()
    {
        if (_matchState.Value != MatchState.Results) return;
        TriggerReturnToLobby();
    }

    private void TriggerReturnToLobby()
    {
        if (!IsServer) return;
        if (_matchState.Value == MatchState.ReturningToLobby) return;

        SetState(MatchState.ReturningToLobby);

        // Stop the results countdown
        if (_returnCoroutine != null)
        {
            StopCoroutine(_returnCoroutine);
            _returnCoroutine = null;
        }

        // Clean up map
        if (_activeMapInstance != null)
        {
            Destroy(_activeMapInstance);
            _activeMapInstance = null;
        }

        OnReturningToLobby?.Invoke();
        NotifyReturnToLobbyClientRpc();

        // Restore session state before scene load
        if (MultiplayerSessionManager.Instance != null)
            MultiplayerSessionManager.Instance.OnMatchEnded();

        // Load lobby scene via NGO
        StartCoroutine(LoadLobbySceneCoroutine());
    }

    private IEnumerator LoadLobbySceneCoroutine()
    {
        yield return new WaitForSeconds(0.5f);

        string lobbyScene = MultiplayerSessionManager.MultiplayerLobbyScene;
        var status = NetworkManager.Singleton.SceneManager.LoadScene(lobbyScene, LoadSceneMode.Single);
        if (status != SceneEventProgressStatus.Started)
        {
            Debug.LogError($"[MatchManager] Failed to load lobby scene: {status}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // State Machine
    // ═══════════════════════════════════════════════════════════════════════

    private void SetState(MatchState newState)
    {
        if (!IsServer) return;
        _matchState.Value = newState;
        Debug.Log($"[MatchManager] State → {newState}");
    }

    private void HandleMatchStateChanged(MatchState oldState, MatchState newState)
    {
        OnMatchStateChanged?.Invoke(newState);

        // Enforce gameplay lock on all clients
        if (newState == MatchState.PreMatchCountdown || newState == MatchState.Ending || newState == MatchState.Results || newState == MatchState.ReturningToLobby)
        {
            LockLocalPlayerInput();
        }
        else if (newState == MatchState.Active)
        {
            UnlockLocalPlayerInput();
        }
    }

    private void LockLocalPlayerInput()
    {
        var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (localPlayer == null) return;
        localPlayer.GetComponent<PlayerController>()?.SetInputLocked(true);
    }

    private void UnlockLocalPlayerInput()
    {
        var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (localPlayer == null) return;
        localPlayer.GetComponent<PlayerController>()?.SetInputLocked(false);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private void CancelAllRespawnCoroutines()
    {
        foreach (var co in _respawnCoroutines.Values)
        {
            if (co != null) StopCoroutine(co);
        }
        _respawnCoroutines.Clear();
    }

    /// <summary>
    /// Whether the match is in a state where damage is valid.
    /// Called by PlayerHealth to reject damage during non-active states.
    /// </summary>
    public bool IsDamageAllowed(ulong targetClientId)
    {
        if (_matchState.Value != MatchState.Active) return false;
        if (_resultFinalized.Value) return false;
        if (IsSpawnProtected(targetClientId)) return false;
        return true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Client RPCs
    // ═══════════════════════════════════════════════════════════════════════

    [ClientRpc]
    private void TriggerGoSignalClientRpc()
    {
        OnMatchGoSignal?.Invoke();
    }

    [ClientRpc]
    private void NotifyMapSelectedClientRpc(int mapIndex)
    {
        Debug.Log($"[MatchManager] Client: Map index {mapIndex} selected.");
    }

    [ClientRpc]
    private void NotifyRespawnClientRpc(ulong clientId, Vector3 spawnPos, Quaternion spawnRot)
    {
        // Unlock local player if this is for us
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            var localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (localPlayer != null)
            {
                localPlayer.GetComponent<PlayerController>()?.OnRespawned();
                localPlayer.GetComponent<PlayerController>()?.SetInputLocked(false);
            }

            // Notify respawn to shooting/visual systems
            localPlayer?.GetComponent<PlayerHealth>()?.NotifyRespawnClient();

            MatchHUD.Instance?.OnLocalPlayerRespawned();
        }

        // Spawn protection visual
        MatchHUD.Instance?.ShowSpawnProtectionForPlayer(clientId);
    }

    [ClientRpc]
    private void EndSpawnProtectionClientRpc(ulong clientId)
    {
        MatchHUD.Instance?.HideSpawnProtectionForPlayer(clientId);
    }

    [ClientRpc]
    private void ShowResultsClientRpc(
        ulong[] clientIds,
        int[] kills,
        int[] deaths,
        int[] bestStreaks,
        int[] ranks,
        FixedString64Bytes[] names,
        bool[] connected,
        ulong winnerClientId,
        bool isDraw,
        FixedString64Bytes matchId)
    {
        var entries = new List<PlayerResultEntry>();
        for (int i = 0; i < clientIds.Length; i++)
        {
            entries.Add(new PlayerResultEntry
            {
                ClientId = clientIds[i],
                DisplayName = names[i].ToString(),
                Kills = kills[i],
                Deaths = deaths[i],
                BestKillStreak = bestStreaks[i],
                Rank = ranks[i],
                IsConnected = connected[i]
            });
        }

        var result = new MatchResultData
        {
            MatchId = matchId.ToString(),
            Entries = entries,
            WinnerClientId = winnerClientId,
            WinnerName = isDraw ? "Draw" : (entries.Find(e => e.ClientId == winnerClientId)?.DisplayName ?? ""),
            IsDraw = isDraw
        };

        // Save local records on clients
        LocalPlayerRecords.Instance?.RecordMatchResult(matchId.ToString(), result, null, null);

        OnResultsReady?.Invoke(result);
        MatchResultsUI.Instance?.ShowResults(result, NetworkManager.Singleton.LocalClientId);
    }

    [ClientRpc]
    private void NotifyReturnToLobbyClientRpc()
    {
        OnReturningToLobby?.Invoke();
        MatchResultsUI.Instance?.HideResults();
    }

    // ─── NetworkVariable Callbacks ──────────────────────────────────────────

    private void HandleCountdownChanged(int oldVal, int newVal)
    {
        OnCountdownChanged?.Invoke(newVal);
        MatchHUD.Instance?.UpdateCountdown(newVal);
    }

    private void HandleMapIndexChanged(int oldVal, int newVal)
    {
        Debug.Log($"[MatchManager] Map index changed to {newVal}");
    }

    private void HandleResultFinalizedChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            // On clients: stop gameplay
            if (!IsServer)
            {
                LockLocalPlayerInput();
            }
        }
    }

    // ─── Public API for HUD & External Access ───────────────────────────────

    public MatchState CurrentState => _matchState.Value;
    public int CountdownValue => _countdownValue.Value;
    public bool ResultFinalized => _resultFinalized.Value;
    public bool IsDraw => _isDraw.Value;
    public string WinnerName => _winnerName.Value.ToString();
    public ulong WinnerClientId => _winnerClientId.Value;

    /// <summary>Returns remaining match time in seconds (client-derived).</summary>
    public float GetRemainingTime()
    {
        if (_matchState.Value != MatchState.Active) return 0f;
        if (_matchStartServerTime.Value <= 0) return MatchDuration;

        double elapsed = NetworkManager.Singleton.ServerTime.Time - _matchStartServerTime.Value;
        return Mathf.Max(0f, MatchDuration - (float)elapsed);
    }

    /// <summary>Get current stats for a player (readable from any client).</summary>
    public PlayerMatchStats GetPlayerStats(ulong clientId)
    {
        _playerStatsMap.TryGetValue(clientId, out var stats);
        return stats;
    }

    // ─── Runtime Injection (called by Bootstrapper before Spawn) ─────────────

    /// <summary>Inject MatchConfig at runtime (before OnNetworkSpawn).</summary>
    public void SetConfig(MatchConfig cfg) => config = cfg;

    /// <summary>Inject map prefabs at runtime (before OnNetworkSpawn).</summary>
    public void SetMapPrefabs(GameObject[] prefabs, LayerMask obstacleMask)
    {
        mapPrefabs = prefabs;
        spawnObstacleMask = obstacleMask;
    }
}

/// <summary>
/// Enum for all match lifecycle states.
/// </summary>
public enum MatchState
{
    WaitingForPlayers,
    PreMatchCountdown,
    Active,
    Ending,
    Results,
    ReturningToLobby
}

/// <summary>
/// Snapshot of a single player's final result for the results screen.
/// </summary>
[Serializable]
public class PlayerResultEntry
{
    public ulong ClientId;
    public string AuthId;
    public string DisplayName;
    public int Kills;
    public int Deaths;
    public int BestKillStreak;
    public int Rank;
    public bool IsConnected;
}

/// <summary>
/// Complete match result snapshot.
/// </summary>
[Serializable]
public class MatchResultData
{
    public string MatchId;
    public List<PlayerResultEntry> Entries;
    public ulong WinnerClientId;
    public string WinnerName;
    public bool IsDraw;
}
