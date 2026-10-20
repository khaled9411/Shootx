using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-authoritative projectile that is synchronized across all clients.
/// The server spawns this via NetworkObject.Spawn() after receiving the fire RPC.
/// It handles movement, hit detection, and destruction across the network.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkProjectile : NetworkBehaviour
{
    /// <summary>
    /// Networked direction of travel. Set by the server at spawn time.
    /// </summary>
    private NetworkVariable<Vector3> networkDirection = new NetworkVariable<Vector3>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private ProjectileData projectileData;
    private float spawnTime;
    private ulong ownerClientId;

    /// <summary>
    /// Called by the server to configure the projectile before it starts moving.
    /// Must be called BEFORE NetworkObject.Spawn().
    /// </summary>
    public void Initialize(Vector3 fireDirection, ProjectileData data, ulong shooterClientId)
    {
        projectileData = data;
        ownerClientId = shooterClientId;
        networkDirection.Value = fireDirection.normalized;
        spawnTime = Time.time;

        // Orient the projectile
        if (fireDirection.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(fireDirection.normalized);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // On clients, orient the projectile to match the networked direction
        if (!IsServer)
        {
            Vector3 dir = networkDirection.Value;
            if (dir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(dir);
            }
        }

        spawnTime = Time.time;

        if (IsServer && projectileData != null)
        {
            foreach (var p in FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))
            {
                var col = p.GetComponentInChildren<Collider>();
                bool inMask = (projectileData.hitLayers.value & (1 << p.gameObject.layer)) != 0;
                Debug.Log($"[SERVER] {p.name} owner={p.OwnerClientId} pos={p.transform.position} " +
                          $"layer={LayerMask.LayerToName(p.gameObject.layer)} " +
                          $"collider={(col ? col.enabled.ToString() : "NONE")} inHitLayers={inMask}");
            }
        }
    }

    private void Update()
    {
        Vector3 direction = networkDirection.Value;
        if (direction.sqrMagnitude < 0.001f) return;

        float speed = projectileData != null ? projectileData.speed : 40f;
        float lifetime = projectileData != null ? projectileData.lifetime : 3f;

        // Move the projectile
        transform.position += direction * speed * Time.deltaTime;

        // Server handles lifetime and hit detection
        if (IsServer)
        {
            // Lifetime check
            if (Time.time - spawnTime >= lifetime)
            {
                DespawnProjectile();
                return;
            }

            // Hit detection using SphereCast
            if (projectileData != null)
            {
                float castDistance = speed * Time.deltaTime;
                if (Physics.SphereCast(
                    transform.position,
                    projectileData.radius,
                    direction,
                    out RaycastHit hit,
                    castDistance,
                    projectileData.hitLayers))
                {
                    OnHit(hit);
                }
            }
        }
    }

    /// <summary>
    /// Server-side hit processing.
    /// </summary>
    private void OnHit(RaycastHit hit)
    {
        // Try to find a damageable component on the hit object
        INetworkDamageable damageable = hit.collider.GetComponentInParent<INetworkDamageable>();
        if (damageable != null && projectileData != null)
        {
            Debug.Log($"Projectile hit {hit.collider.name}, dealing {projectileData.damage} damage.");
            damageable.TakeDamage(projectileData.damage, ownerClientId);
        }

        // Notify clients about the hit for visual effects
        OnHitClientRpc(hit.point, hit.normal);

        DespawnProjectile();
    }

    [ClientRpc]
    private void OnHitClientRpc(Vector3 hitPoint, Vector3 hitNormal)
    {
        // Spawn hit effect here (particles, decals, etc.)
        // Example: Instantiate(hitEffectPrefab, hitPoint, Quaternion.LookRotation(hitNormal));
    }

    private void DespawnProjectile()
    {
        if (IsServer && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }
}
