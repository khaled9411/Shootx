using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-authoritative manager that spawns randomized ammunition pickups
/// on valid ground positions across the gameplay arena.
/// </summary>
public class AmmoPickupSpawner : MonoBehaviour
{
    public static AmmoPickupSpawner Instance { get; private set; }

    [Header("Pickup Configuration")]
    [Tooltip("The networked AmmoPickup prefab (must have NetworkObject and AmmoPickup components).")]
    [SerializeField] private GameObject pickupPrefab;

    [Tooltip("Maximum number of pickups concurrently active in the arena.")]
    [SerializeField] private int maxActivePickups = 4;

    [Tooltip("Delay in seconds before spawning a replacement pickup after one is collected.")]
    [SerializeField] private float respawnInterval = 5.0f;

    [Header("Arena Spawn Area")]
    [Tooltip("Center of the random placement area (aligned with the PVP arena center).")]
    [SerializeField] private Vector3 spawnAreaCenter = new Vector3(-5f, 0.5f, 0f);

    [Tooltip("Total dimensions of the spawn volume (X width, Y vertical search, Z depth).")]
    [SerializeField] private Vector3 spawnAreaSize = new Vector3(14f, 2f, 10f);

    [Header("Validation Masks")]
    [Tooltip("Ground layer(s) to snap pickups onto.")]
    [SerializeField] private LayerMask groundLayer = 513;

    [Tooltip("Obstacle layers (walls, crates, props) to avoid spawning inside.")]
    [SerializeField] private LayerMask obstacleLayer = 1;

    [Tooltip("Player layer to avoid spawning directly on top of characters.")]
    [SerializeField] private LayerMask playerLayer = 65536;

    [Header("Placement Rules")]
    [Tooltip("Maximum random placement attempts before skipping this spawn cycle.")]
    [SerializeField] private int maxPlacementAttempts = 15;

    [Tooltip("Minimum distance required between any two active pickups.")]
    [SerializeField] private float minPickupSpacing = 2.5f;

    private readonly List<AmmoPickup> _activePickups = new List<AmmoPickup>();
    private float _lastWarningTime;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Spawning is strictly server-authoritative
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        StartCoroutine(InitialSpawnCoroutine());
    }

    private IEnumerator InitialSpawnCoroutine()
    {
        // Wait 1 frame for network & scene objects to settle
        yield return null;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) yield break;

        // Fill arena up to maxActivePickups
        for (int i = 0; i < maxActivePickups; i++)
        {
            SpawnSinglePickup();
            yield return new WaitForSeconds(0.1f);
        }
    }

    /// <summary>
    /// Attempts to find a valid random location and spawn one networked pickup.
    /// </summary>
    public bool SpawnSinglePickup()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return false;
        if (pickupPrefab == null)
        {
            Debug.LogError("[AmmoSpawner] Cannot spawn: pickupPrefab is not assigned in the Inspector!");
            return false;
        }

        // Clean dead references
        _activePickups.RemoveAll(p => p == null || !p.isActiveAndEnabled);

        if (_activePickups.Count >= maxActivePickups) return false;

        if (TryFindValidSpawnPosition(out Vector3 validPosition))
        {
            GameObject pickupGO = Instantiate(pickupPrefab, validPosition, Quaternion.identity);
            NetworkObject netObj = pickupGO.GetComponent<NetworkObject>();
            AmmoPickup ammoPickup = pickupGO.GetComponent<AmmoPickup>();

            if (netObj != null && ammoPickup != null)
            {
                netObj.Spawn(destroyWithScene: true);
                _activePickups.Add(ammoPickup);
                ammoPickup.OnConsumedByServer += HandlePickupConsumed;
                return true;
            }
            else
            {
                Debug.LogError("[AmmoSpawner] Spawned prefab is missing NetworkObject or AmmoPickup!");
                Destroy(pickupGO);
                return false;
            }
        }
        else
        {
            if (Time.time - _lastWarningTime > 5f)
            {
                Debug.LogWarning("[AmmoSpawner] Failed to find a valid ground position after max placement attempts. Skipping spawn.");
                _lastWarningTime = Time.time;
            }
            return false;
        }
    }

    private void HandlePickupConsumed(AmmoPickup consumedPickup)
    {
        _activePickups.Remove(consumedPickup);

        if (gameObject.activeInHierarchy && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            StartCoroutine(ScheduleReplacementCoroutine());
        }
    }

    private IEnumerator ScheduleReplacementCoroutine()
    {
        yield return new WaitForSeconds(respawnInterval);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            SpawnSinglePickup();
        }
    }

    /// <summary>
    /// Finds a valid ground location that does not overlap obstacles, players, or other pickups.
    /// </summary>
    private bool TryFindValidSpawnPosition(out Vector3 resultPosition)
    {
        resultPosition = Vector3.zero;
        Vector3 halfSize = spawnAreaSize * 0.5f;

        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            float randomX = Random.Range(spawnAreaCenter.x - halfSize.x, spawnAreaCenter.x + halfSize.x);
            float randomZ = Random.Range(spawnAreaCenter.z - halfSize.z, spawnAreaCenter.z + halfSize.z);
            Vector3 rayStart = new Vector3(randomX, spawnAreaCenter.y + halfSize.y + 5f, randomZ);

            // 1. Check ground surface
            if (!Physics.Raycast(rayStart, Vector3.down, out RaycastHit groundHit, 20f, groundLayer))
            {
                continue;
            }

            Vector3 candidatePoint = groundHit.point + Vector3.up * 0.25f;

            // 2. Check within configured bounds
            if (Mathf.Abs(candidatePoint.x - spawnAreaCenter.x) > halfSize.x ||
                Mathf.Abs(candidatePoint.z - spawnAreaCenter.z) > halfSize.z)
            {
                continue;
            }

            // 3. Check for obstacle / player collision overlap
            Collider[] overlaps = Physics.OverlapSphere(
                candidatePoint + Vector3.up * 0.35f,
                0.5f,
                obstacleLayer | playerLayer
            );

            if (overlaps != null && overlaps.Length > 0)
            {
                continue;
            }

            // 4. Check spacing from existing pickups
            bool tooClose = false;
            for (int k = 0; k < _activePickups.Count; k++)
            {
                if (_activePickups[k] != null &&
                    Vector3.Distance(candidatePoint, _activePickups[k].transform.position) < minPickupSpacing)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose) continue;

            // Position is valid!
            resultPosition = candidatePoint;
            return true;
        }

        return false;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.1f, 0.9f, 0.4f, 0.3f);
        Gizmos.DrawCube(spawnAreaCenter, spawnAreaSize);
        Gizmos.color = new Color(0.1f, 0.9f, 0.4f, 0.9f);
        Gizmos.DrawWireCube(spawnAreaCenter, spawnAreaSize);
    }
#endif
}
