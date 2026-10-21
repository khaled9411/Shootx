using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles player spawning strictly within the gameplay scene (MainMultiplayer).
/// Prevents premature spawning in the lobby, and guarantees that ALL connected
/// players in the session (host and all clients) are spawned once the gameplay
/// scene is loaded.
/// </summary>
public class MultiplayerSpawnManager : MonoBehaviour
{
    public static MultiplayerSpawnManager Instance { get; private set; }

    [Header("Spawning Configuration")]
    [Tooltip("The player prefab to spawn. If null, falls back to NetworkManager.Singleton.NetworkConfig.PlayerPrefab.")]
    [SerializeField] private GameObject playerPrefab;

    [Tooltip("Predefined spawn points in the scene. If empty, default positions will be used.")]
    [SerializeField] private Transform[] spawnPoints;

    // Default spawn positions if no scene transforms are assigned (based on PVP Map 1 arena bounds)
    private static readonly Vector3[] DefaultSpawnPositions = new Vector3[]
    {
        new Vector3(-10f, 0.5f, -5f),
        new Vector3(0f,   0.5f,  4f),
        new Vector3(-10f, 0.5f,  4f),
        new Vector3(0f,   0.5f, -5f)
    };

    private static readonly Quaternion[] DefaultSpawnRotations = new Quaternion[]
    {
        Quaternion.Euler(0f, 45f, 0f),
        Quaternion.Euler(0f, 225f, 0f),
        Quaternion.Euler(0f, 135f, 0f),
        Quaternion.Euler(0f, 315f, 0f)
    };

    private readonly HashSet<ulong> _spawnedClientIds = new HashSet<ulong>();
    private bool _hasSubscribedToSceneEvents;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        UnsubscribeNetworkEvents();
    }

    private void Start()
    {
        // Only the server/host handles authoritative player spawning
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        SubscribeNetworkEvents();

        // Check if clients are already loaded or if this scene was loaded directly
        StartCoroutine(InitialSpawnCheckCoroutine());
    }

    private void SubscribeNetworkEvents()
    {
        if (_hasSubscribedToSceneEvents || NetworkManager.Singleton == null) return;

        if (NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnSceneEvent += HandleSceneEvent;
            _hasSubscribedToSceneEvents = true;
        }

        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
    }

    private void UnsubscribeNetworkEvents()
    {
        if (NetworkManager.Singleton != null)
        {
            if (_hasSubscribedToSceneEvents && NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnSceneEvent -= HandleSceneEvent;
            }

            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
        _hasSubscribedToSceneEvents = false;
    }

    private void HandleSceneEvent(SceneEvent sceneEvent)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        // When all clients have finished loading the gameplay scene, spawn everyone at once!
        if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted &&
            sceneEvent.SceneName == MultiplayerSessionManager.GameplaySceneName)
        {
            Debug.Log("[MPSpawn] LoadEventCompleted: Spawning all connected players!");
            SpawnAllConnectedPlayers();
        }
        // If a specific client finishes loading the gameplay scene after the main event
        else if (sceneEvent.SceneEventType == SceneEventType.LoadComplete &&
                 sceneEvent.SceneName == MultiplayerSessionManager.GameplaySceneName)
        {
            Debug.Log($"[MPSpawn] LoadComplete for client {sceneEvent.ClientId}");
            SpawnPlayerForClient(sceneEvent.ClientId);
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        // If a client connects while we are in gameplay scene
        if (SceneManager.GetActiveScene().name == MultiplayerSessionManager.GameplaySceneName)
        {
            Debug.Log($"[MPSpawn] Client {clientId} connected during gameplay.");
            SpawnPlayerForClient(clientId);
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        _spawnedClientIds.Remove(clientId);
    }

    private IEnumerator InitialSpawnCheckCoroutine()
    {
        // Wait 1 frame for scene and network setup to settle
        yield return null;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) yield break;

        // Wait a short time to allow LoadEventCompleted to fire naturally from NGO.
        // If it doesn't (e.g. Host testing in editor directly without lobby scene transition),
        // spawn all connected players as a fallback.
        float timer = 0f;
        const float maxWait = 2.0f;

        while (timer < maxWait && _spawnedClientIds.Count == 0)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (_spawnedClientIds.Count == 0 && NetworkManager.Singleton.ConnectedClientsList.Count > 0)
        {
            Debug.Log("[MPSpawn] Spawning connected players via initial check.");
            SpawnAllConnectedPlayers();
        }
    }

    /// <summary>
    /// Spawns player avatars for ALL connected clients (Host + all Clients).
    /// </summary>
    public void SpawnAllConnectedPlayers()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        var clients = NetworkManager.Singleton.ConnectedClientsList;
        for (int i = 0; i < clients.Count; i++)
        {
            SpawnPlayerForClient(clients[i].ClientId);
        }
    }

    /// <summary>
    /// Spawns a player avatar for a specific client if not already spawned.
    /// </summary>
    public void SpawnPlayerForClient(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        // Check if client is still connected
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            Debug.LogWarning($"[MPSpawn] Cannot spawn player: Client {clientId} not in ConnectedClients.");
            return;
        }

        // Check if player object already exists
        if (client.PlayerObject != null)
        {
            Debug.Log($"[MPSpawn] Client {clientId} already has PlayerObject ({client.PlayerObject.name}).");
            _spawnedClientIds.Add(clientId);
            return;
        }

        if (_spawnedClientIds.Contains(clientId))
        {
            return;
        }

        // Determine spawn transform
        int clientIndex = GetClientIndex(clientId);
        Vector3 spawnPos = GetSpawnPosition(clientIndex);
        Quaternion spawnRot = GetSpawnRotation(clientIndex);

        // Get player prefab
        GameObject prefab = playerPrefab != null ? playerPrefab : NetworkManager.Singleton.NetworkConfig.PlayerPrefab;
        if (prefab == null)
        {
            Debug.LogError("[MPSpawn] Failed to spawn: No PlayerPrefab configured!");
            return;
        }

        // Instantiate and spawn as player object with ownership
        GameObject playerInstance = Instantiate(prefab, spawnPos, spawnRot);
        var netObj = playerInstance.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.SpawnAsPlayerObject(clientId, destroyWithScene: true);
            _spawnedClientIds.Add(clientId);
            Debug.Log($"[MPSpawn] Successfully spawned Player for Client {clientId} (Index: {clientIndex}) at {spawnPos}");
        }
        else
        {
            Debug.LogError("[MPSpawn] Instantiated player prefab is missing a NetworkObject component!");
            Destroy(playerInstance);
        }
    }

    private int GetClientIndex(ulong clientId)
    {
        var clients = NetworkManager.Singleton.ConnectedClientsList;
        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i].ClientId == clientId)
                return i;
        }
        return (int)(clientId % (ulong)DefaultSpawnPositions.Length);
    }

    public Vector3 GetSpawnPosition(int index)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int safeIndex = index % spawnPoints.Length;
            if (spawnPoints[safeIndex] != null)
                return spawnPoints[safeIndex].position;
        }

        return DefaultSpawnPositions[index % DefaultSpawnPositions.Length];
    }

    public Quaternion GetSpawnRotation(int index)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int safeIndex = index % spawnPoints.Length;
            if (spawnPoints[safeIndex] != null)
                return spawnPoints[safeIndex].rotation;
        }

        return DefaultSpawnRotations[index % DefaultSpawnRotations.Length];
    }

    /// <summary>
    /// Returns a random spawn position from available spawn points (used for player respawns).
    /// </summary>
    public static Vector3 GetRandomSpawnPosition()
    {
        if (Instance != null && Instance.spawnPoints != null && Instance.spawnPoints.Length > 0)
        {
            int randomIndex = Random.Range(0, Instance.spawnPoints.Length);
            if (Instance.spawnPoints[randomIndex] != null)
                return Instance.spawnPoints[randomIndex].position;
        }

        int defaultIndex = Random.Range(0, DefaultSpawnPositions.Length);
        return DefaultSpawnPositions[defaultIndex];
    }
}
