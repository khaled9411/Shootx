using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Placed in the MainMultiplayer scene (non-networked placeholder).
/// When the host loads this scene, this bootstrapper auto-creates the
/// MatchManager NetworkObject (server-only) and the LocalPlayerRecords.
///
/// This prevents any scene from needing a pre-placed MatchManager NetworkObject
/// while still ensuring exactly one MatchManager exists per match.
/// </summary>
public class MatchManagerBootstrapper : MonoBehaviour
{
    public static MatchManagerBootstrapper Instance { get; private set; }

    [Header("Match UI Prefab")]
    [Tooltip("UI Canvas with MatchHUD and MatchResultsUI. Created on all clients if missing.")]
    [SerializeField] private GameObject matchUIPrefab;

    [Header("Match Manager Prefab")]
    [Tooltip("Prefab with MatchManager + NetworkObject. Created by host only.")]
    [SerializeField] private GameObject matchManagerPrefab;

    [Header("Configuration")]
    [SerializeField] private MatchConfig matchConfig;

    [Header("Map Prefabs")]
    [SerializeField] private GameObject[] mapPrefabs = new GameObject[4];

    [Header("Spawn Validation")]
    [SerializeField] private UnityEngine.LayerMask spawnObstacleMask;

    public GameObject[] MapPrefabs => mapPrefabs;
    public UnityEngine.LayerMask SpawnObstacleMask => spawnObstacleMask;

    private bool _bootstrapped;

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
            Instance = null;
    }

    private void Start()
    {
        if (_bootstrapped) return;
        _bootstrapped = true;

        // Ensure LocalPlayerRecords exists on all clients
        if (LocalPlayerRecords.Instance == null)
        {
            var lpr = new GameObject("LocalPlayerRecords");
            lpr.AddComponent<LocalPlayerRecords>();
        }

        // Ensure MatchUI exists on all clients
        if (MatchHUD.Instance == null)
        {
            if (matchUIPrefab != null)
            {
                Instantiate(matchUIPrefab);
            }
            else
            {
                var res = Resources.Load<GameObject>("MatchUI");
                if (res != null) Instantiate(res);
            }
        }

        // On clients: if MatchManager already exists, ensure map prefabs are passed
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            if (MatchManager.Instance != null && mapPrefabs != null && mapPrefabs.Length > 0)
            {
                MatchManager.Instance.SetMapPrefabs(mapPrefabs, spawnObstacleMask);
            }
            return;
        }

        // Only the host/server creates the MatchManager
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        // Create MatchManager
        if (MatchManager.Instance != null)
        {
            Debug.Log("[Bootstrapper] MatchManager already active; injecting map prefabs & config.");
            if (mapPrefabs != null && mapPrefabs.Length > 0)
                MatchManager.Instance.SetMapPrefabs(mapPrefabs, spawnObstacleMask);
            if (matchConfig != null)
                MatchManager.Instance.SetConfig(matchConfig);
            return;
        }

        if (matchManagerPrefab != null)
        {
            var go = Instantiate(matchManagerPrefab);
            var mm = go.GetComponent<MatchManager>();
            if (mm != null)
            {
                // Inject config and map references at runtime
                if (matchConfig != null)
                    InjectMatchConfig(mm, matchConfig);
                if (mapPrefabs != null && mapPrefabs.Length > 0)
                    InjectMapPrefabs(mm, mapPrefabs, spawnObstacleMask);
            }
            var netObj = go.GetComponent<NetworkObject>();
            if (netObj != null)
                netObj.Spawn(destroyWithScene: true);
            Debug.Log("[Bootstrapper] MatchManager spawned from prefab (destroyWithScene=true).");
        }
        else
        {
            // Create inline
            var go = new GameObject("MatchManager");
            var mm = go.AddComponent<MatchManager>();
            var netObj = go.AddComponent<NetworkObject>();

            if (matchConfig != null)
                InjectMatchConfig(mm, matchConfig);
            if (mapPrefabs != null && mapPrefabs.Length > 0)
                InjectMapPrefabs(mm, mapPrefabs, spawnObstacleMask);

            netObj.Spawn(destroyWithScene: true);
            Debug.Log("[Bootstrapper] MatchManager created inline and spawned (destroyWithScene=true).");
        }
    }

    private void InjectMatchConfig(MatchManager mm, MatchConfig config)
    {
        // Use reflection to set the private serialized field, or expose a public setter
        mm.SetConfig(config);
    }

    private void InjectMapPrefabs(MatchManager mm, GameObject[] prefabs, UnityEngine.LayerMask obstacleMask)
    {
        mm.SetMapPrefabs(prefabs, obstacleMask);
    }
}
