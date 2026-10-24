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

    // Crate tracking (match-scoped)
    private readonly List<DestructibleBox> _activeCrates = new List<DestructibleBox>();
    private readonly HashSet<int> _destroyedCrateIndices = new HashSet<int>();

    public int ActiveCrateCount => _activeCrates.Count;
    public int DestroyedCrateCount => _destroyedCrateIndices.Count;

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
        else
        {
            // On clients: if map was already selected before we spawned, load it now
            if (_selectedMapIndex.Value >= 0)
            {
                LoadMapClient(_selectedMapIndex.Value);
            }
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

        UnloadActiveMap();

        if (Instance == this)
            Instance = null;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        UnloadActiveMap();
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

        // Spawn all players in the selected map first
        SpawnAllPlayers();

        // Wait a frame and register all connected players
        yield return null;
        yield return StartCoroutine(RegisterPlayersCoroutine());

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
        EnsureMapPrefabsLoaded();

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

        // Unload previous map if any
        if (_activeMapInstance != null)
        {
            UnloadActiveMap();
            yield return null; // Wait one frame for destruction
        }

        // Instantiate the selected map on Server
        Debug.Log($"[MatchManager] Server loading map '{mapPrefabs[selectedIndex].name}' (index {selectedIndex})");
        _activeMapInstance = Instantiate(mapPrefabs[selectedIndex], Vector3.zero, Quaternion.identity);
        _activeMapInstance.name = $"ActiveMap_{mapPrefabs[selectedIndex].name}";
        CleanUpLocalMapNetworkComponents(_activeMapInstance);
        RegisterActiveMapCrates(_activeMapInstance);

        // Update NetworkVariable so clients synchronize
        _selectedMapIndex.Value = selectedIndex;

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

        // Notify clients about the selected map via RPC
        NotifyMapSelectedClientRpc(selectedIndex);

        Debug.Log($"[MatchManager] Map ready on server. Spawn points found: {_activeSpawnPoints.Count}");
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

    /// <summary>
    /// Ensures that a client's player object components (health, stats, identity) are registered in MatchManager.
    /// Safe to call multiple times.
    /// </summary>
    public void EnsurePlayerRegistered(ulong clientId)
    {
        if (_playerStatsMap.ContainsKey(clientId) && _playerHealthMap.ContainsKey(clientId))
            return;

        RegisterPlayer(clientId);
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

    /// <summary>
    /// Explicit callback called by PlayerHealth on the server when a player dies.
    /// Ensures victim and killer registration and schedules respawn + stats tracking.
    /// </summary>
    public void ReportPlayerDeath(ulong victimClientId, ulong killerClientId)
    {
        if (!IsServer) return;
        HandlePlayerDeath(victimClientId, killerClientId);
    }

    private void HandlePlayerDeath(ulong victimClientId, ulong killerClientId)
    {
        if (!IsServer) return;
        if (_matchState.Value != MatchState.Active) return;

        // Ensure victim and killer are registered in stats maps
        EnsurePlayerRegistered(victimClientId);
        if (killerClientId != ulong.MaxValue && killerClientId != victimClientId)
        {
            EnsurePlayerRegistered(killerClientId);
        }

        // Get victim stats
        _playerStatsMap.TryGetValue(victimClientId, out var victimStats);

        // Idempotency: only process if victim was actually alive or protected
        if (victimStats != null)
        {
            if (victimStats.LifeState != PlayerLifeState.Alive &&
                victimStats.LifeState != PlayerLifeState.SpawnProtected)
            {
                Debug.Log($"[MatchManager] Victim {victimClientId} is already in state {victimStats.LifeState}. Skipping duplicate death handling.");
                return;
            }

            // Mark victim as dead
            victimStats.SetLifeState(PlayerLifeState.Dead);
            victimStats.AddDeath();
        }

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

        // Schedule respawn - always guarantee respawn when match is active
        if (_matchState.Value == MatchState.Active)
        {
            if (_respawnCoroutines.TryGetValue(victimClientId, out var existingCo) && existingCo != null)
            {
                StopCoroutine(existingCo);
            }
            var co = StartCoroutine(RespawnPlayerCoroutine(victimClientId));
            _respawnCoroutines[victimClientId] = co;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Respawn
    // ═══════════════════════════════════════════════════════════════════════

    private IEnumerator RespawnPlayerCoroutine(ulong clientId)
    {
        EnsurePlayerRegistered(clientId);
        if (_playerStatsMap.TryGetValue(clientId, out var stats))
        {
            stats.SetLifeState(PlayerLifeState.Respawning);
        }

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

        // Restore health (ServerRespawnRestore will set currentHealth to MaxHealth, isDead = false, input unlocked, and NotifyRespawnClientRpc)
        var health = go.GetComponent<PlayerHealth>();
        if (health != null)
            health.ServerRespawnRestore();

        // Restore ammo
        var shooting = go.GetComponent<PlayerShooting>();
        if (shooting != null)
            shooting.HandleRespawnServer();

        // Apply spawn protection
        if (stats != null)
            stats.SetLifeState(PlayerLifeState.SpawnProtected);
        _spawnProtectionEndTimes[clientId] = Time.time + SpawnProtectionDuration;

        // Notify respawn (clears input lock, triggers visuals on client)
        NotifyRespawnClientRpc(clientId, spawnPos, spawnRot);

        yield return new WaitForSeconds(SpawnProtectionDuration);

        // Check still in spawn protection (not killed again)
        if (stats != null && stats.LifeState == PlayerLifeState.SpawnProtected)
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

        // Clean up map on server
        UnloadActiveMap();

        OnReturningToLobby?.Invoke();
        NotifyReturnToLobbyClientRpc();

        // 1. Despawn active pickups so none leak into the lobby scene
        if (AmmoPickupSpawner.Instance != null)
        {
            AmmoPickupSpawner.Instance.DespawnAllPickups();
        }

        // 2. Despawn all player avatars on server so clients don't keep old PlayerObjects
        if (MultiplayerSpawnManager.Instance != null)
        {
            MultiplayerSpawnManager.Instance.DespawnAllPlayers();
        }
        else
        {
            DespawnAllConnectedPlayers();
        }

        // 3. Restore session state and let the persistent MultiplayerSessionManager load the lobby scene and despawn this MatchManager
        if (MultiplayerSessionManager.Instance != null)
        {
            MultiplayerSessionManager.Instance.OnMatchEnded();
            MultiplayerSessionManager.Instance.LoadLobbySceneAndCleanupMatch(this);
        }
        else
        {
            // Fallback if MultiplayerSessionManager is absent
            StartCoroutine(LoadLobbySceneCoroutine());
        }
    }

    private void DespawnAllConnectedPlayers()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null && client.PlayerObject.IsSpawned)
            {
                client.PlayerObject.Despawn(true);
            }
        }
    }

    private IEnumerator LoadLobbySceneCoroutine()
    {
        yield return new WaitForSeconds(0.4f);

        string lobbyScene = MultiplayerSessionManager.MultiplayerLobbyScene;
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }

        yield return null;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            var status = NetworkManager.Singleton.SceneManager.LoadScene(lobbyScene, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogError($"[MatchManager] Failed to load lobby scene: {status}");
            }
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
        Debug.Log($"[MatchManager] Client: Map index {mapIndex} selected via RPC.");
        if (!IsServer && mapIndex >= 0)
        {
            LoadMapClient(mapIndex);
        }
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
                var cc = localPlayer.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                localPlayer.transform.position = spawnPos;
                localPlayer.transform.rotation = spawnRot;
                if (cc != null) cc.enabled = true;

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
        UnloadActiveMap();
        OnReturningToLobby?.Invoke();
        MatchResultsUI.Instance?.HideResults();
    }

    [ClientRpc]
    private void NotifyCrateDestroyedClientRpc(int crateIndex, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (crateIndex >= 0 && crateIndex < _activeCrates.Count)
        {
            var crate = _activeCrates[crateIndex];
            if (crate != null)
            {
                crate.PlayDestructionSequence(hitPoint, hitDirection);
            }
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestMapCrateStateServerRpc(RpcParams rpcParams = default)
    {
        if (_destroyedCrateIndices.Count == 0) return;

        ulong senderClientId = rpcParams.Receive.SenderClientId;
        int[] destroyed = new int[_destroyedCrateIndices.Count];
        _destroyedCrateIndices.CopyTo(destroyed);

        SyncDestroyedCratesRpc(destroyed, RpcTarget.Single(senderClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void SyncDestroyedCratesRpc(int[] destroyedIndices, RpcParams rpcParams = default)
    {
        if (destroyedIndices == null || destroyedIndices.Length == 0) return;

        foreach (int idx in destroyedIndices)
        {
            if (idx >= 0 && idx < _activeCrates.Count)
            {
                var crate = _activeCrates[idx];
                if (crate != null)
                {
                    crate.ApplyImmediateDestroyedState();
                }
            }
        }
    }

    // ─── NetworkVariable Callbacks ──────────────────────────────────────────

    private void HandleCountdownChanged(int oldVal, int newVal)
    {
        OnCountdownChanged?.Invoke(newVal);
        MatchHUD.Instance?.UpdateCountdown(newVal);
    }

    private void HandleMapIndexChanged(int oldVal, int newVal)
    {
        Debug.Log($"[MatchManager] Map index changed from {oldVal} to {newVal}");
        if (!IsServer && newVal >= 0)
        {
            LoadMapClient(newVal);
        }
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
    public GameObject ActiveMapInstance => _activeMapInstance;
    public int SelectedMapIndex => _selectedMapIndex.Value;

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

    /// <summary>Inject map prefabs at runtime (before or after OnNetworkSpawn).</summary>
    public void SetMapPrefabs(GameObject[] prefabs, LayerMask obstacleMask)
    {
        mapPrefabs = prefabs;
        spawnObstacleMask = obstacleMask;

        // If client was waiting for map prefabs and map index was already received, load now
        if (!IsServer && _selectedMapIndex.Value >= 0 && _activeMapInstance == null)
        {
            LoadMapClient(_selectedMapIndex.Value);
        }
    }

    // ─── Client-side Map Loading & Helpers ──────────────────────────────────

    /// <summary>
    /// Loads the selected map prefab locally on a client.
    /// Idempotent: will not reload if the requested map is already loaded.
    /// </summary>
    private void LoadMapClient(int mapIndex)
    {
        if (IsServer) return;
        if (mapIndex < 0) return;

        // Already loaded this map
        if (_activeMapInstance != null && _lastMapIndex == mapIndex)
        {
            return;
        }

        EnsureMapPrefabsLoaded();

        if (mapPrefabs == null || mapIndex >= mapPrefabs.Length || mapPrefabs[mapIndex] == null)
        {
            Debug.LogError($"[MatchManager] Client failed to load map at index {mapIndex}: invalid index or null prefab reference.");
            return;
        }

        // Unload previous map if any
        UnloadActiveMap();

        _lastMapIndex = mapIndex;
        Debug.Log($"[MatchManager] Client loading map '{mapPrefabs[mapIndex].name}' (index {mapIndex})");
        _activeMapInstance = Instantiate(mapPrefabs[mapIndex], Vector3.zero, Quaternion.identity);
        _activeMapInstance.name = $"ActiveMap_{mapPrefabs[mapIndex].name}";
        CleanUpLocalMapNetworkComponents(_activeMapInstance);
        RegisterActiveMapCrates(_activeMapInstance);
        if (!IsServer)
        {
            RequestMapCrateStateServerRpc();
        }
    }

    /// <summary>
    /// Destroys the currently active map instance if present.
    /// </summary>
    private void UnloadActiveMap()
    {
        if (_activeMapInstance != null)
        {
            Destroy(_activeMapInstance);
            _activeMapInstance = null;
        }
        _lastMapIndex = -1;
        _activeCrates.Clear();
        _destroyedCrateIndices.Clear();
    }

    /// <summary>
    /// Discovers and registers all DestructibleBox components on the newly instantiated map root.
    /// Hierarchy preorder traversal ensures deterministic index matching on server and clients.
    /// </summary>
    private void RegisterActiveMapCrates(GameObject mapRoot)
    {
        _activeCrates.Clear();
        _destroyedCrateIndices.Clear();

        if (mapRoot == null) return;

        var crates = mapRoot.GetComponentsInChildren<DestructibleBox>(true);
        for (int i = 0; i < crates.Length; i++)
        {
            crates[i].Initialize(i, this);
            _activeCrates.Add(crates[i]);
        }

        Debug.Log($"[MatchManager] Registered {_activeCrates.Count} destructible crates on map '{mapRoot.name}' (IsServer: {IsServer}).");
    }

    /// <summary>
    /// Server-authoritative report of a crate hit by firearm or melee.
    /// Validates match state and crate state before transitioning to destroyed and notifying clients.
    /// </summary>
    public void ReportCrateHit(int crateIndex, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (!IsServer) return;

        // Only active match or countdown allows crate destruction
        if (_matchState.Value != MatchState.Active && _matchState.Value != MatchState.PreMatchCountdown)
            return;

        if (crateIndex < 0 || crateIndex >= _activeCrates.Count)
            return;

        var crate = _activeCrates[crateIndex];
        if (crate == null || crate.IsDestroyed)
            return;

        crate.SetDestroyedAuthoritative();
        _destroyedCrateIndices.Add(crateIndex);

        NotifyCrateDestroyedClientRpc(crateIndex, hitPoint, hitDirection);
    }

    /// <summary>
    /// Explicit reset mechanism for in-place match restart without map reload.
    /// </summary>
    public void ResetAllActiveCrates()
    {
        _destroyedCrateIndices.Clear();
        for (int i = 0; i < _activeCrates.Count; i++)
        {
            if (_activeCrates[i] != null)
            {
                _activeCrates[i].ResetCrate();
            }
        }
    }

    /// <summary>
    /// Strips NetworkTransform and NetworkObject components from the locally instantiated map instance
    /// so that NGO does not treat it as an unspawned rogue network entity.
    /// </summary>
    private void CleanUpLocalMapNetworkComponents(GameObject mapInstance)
    {
        if (mapInstance == null) return;

        var netTransforms = mapInstance.GetComponentsInChildren<Unity.Netcode.Components.NetworkTransform>(true);
        foreach (var nt in netTransforms)
        {
            nt.enabled = false;
            Destroy(nt);
        }

        var netObjs = mapInstance.GetComponentsInChildren<NetworkObject>(true);
        foreach (var no in netObjs)
        {
            no.enabled = false;
            Destroy(no);
        }
    }

    /// <summary>
    /// Ensures mapPrefabs array is populated, falling back to MatchManagerBootstrapper if needed.
    /// </summary>
    public void EnsureMapPrefabsLoaded()
    {
        if (mapPrefabs != null && mapPrefabs.Length > 0 && mapPrefabs[0] != null)
            return;

        if (MatchManagerBootstrapper.Instance != null &&
            MatchManagerBootstrapper.Instance.MapPrefabs != null &&
            MatchManagerBootstrapper.Instance.MapPrefabs.Length > 0)
        {
            mapPrefabs = MatchManagerBootstrapper.Instance.MapPrefabs;
            spawnObstacleMask = MatchManagerBootstrapper.Instance.SpawnObstacleMask;
            Debug.Log($"[MatchManager] Injected {mapPrefabs.Length} map prefabs from MatchManagerBootstrapper.Instance.");
            return;
        }

        var bootstrapper = FindAnyObjectByType<MatchManagerBootstrapper>();
        if (bootstrapper != null &&
            bootstrapper.MapPrefabs != null &&
            bootstrapper.MapPrefabs.Length > 0)
        {
            mapPrefabs = bootstrapper.MapPrefabs;
            spawnObstacleMask = bootstrapper.SpawnObstacleMask;
            Debug.Log($"[MatchManager] Injected {mapPrefabs.Length} map prefabs from scene MatchManagerBootstrapper.");
            return;
        }
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
