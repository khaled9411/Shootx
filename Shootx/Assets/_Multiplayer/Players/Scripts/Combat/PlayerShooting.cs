using System;
using Unity.Netcode;
using UnityEngine;
using static Unity.Collections.CollectionHelper;

/// <summary>
/// Main shooting system for the player. Handles input, ammo management,
/// reload logic, dummy projectile spawning (client prediction), and
/// server-authoritative networked projectile spawning.
/// 
/// Attach this to the Player prefab alongside PlayerController.
/// </summary>
public class PlayerShooting : NetworkBehaviour
{
    [Header("Weapon Configuration")]
    [SerializeField] private WeaponData weaponData;

    [Header("Input")]
    [SerializeField] private InputReader inputReader;

    [Header("Fire Point")]
    [Tooltip("Transform at the weapon muzzle where projectiles spawn. Assign in the Inspector.")]
    [SerializeField] private Transform muzzlePoint;

    [Header("Camera Reference")]
    [SerializeField] private LayerMask groundLayer;

    // ─── Ammo State ─────────────────────────────────────────────
    /// <summary>
    /// Synced ammo count so the UI can read it on all clients.
    /// Only the server modifies this value.
    /// </summary>
    private NetworkVariable<int> currentAmmo = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> isReloading = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>
    /// Event fired when ammo count changes. UI can subscribe to this.
    /// Passes (currentAmmo, magazineSize).
    /// </summary>
    public event Action<int, int> OnAmmoChanged;

    /// <summary>
    /// Event fired when reload state changes. Passes isReloading.
    /// </summary>
    public event Action<bool> OnReloadStateChanged;

    // ─── Local State ────────────────────────────────────────────
    private Camera mainCamera;
    private float lastFireTime;
    private bool isFiring;
    private float reloadTimer;

    // ─── Aim State ──────────────────────────────────────────────
    private Vector3 currentAimWorldPoint;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Initialize ammo on server
        if (IsServer)
        {
            currentAmmo.Value = weaponData.magazineSize;
        }

        // Subscribe to ammo change events on all clients
        currentAmmo.OnValueChanged += HandleAmmoChanged;
        isReloading.OnValueChanged += HandleReloadStateChanged;

        // Fire initial event so UI is in sync
        OnAmmoChanged?.Invoke(currentAmmo.Value, weaponData.magazineSize);

        if (mainCamera == null) mainCamera = Camera.main;

        if (!IsOwner) return;

        // Bind Ammo UI if present in scene
        FindAnyObjectByType<MultiplayerAmmoUI>()?.Initialize(this);

        // Subscribe to input events
        if (inputReader != null)
        {
            inputReader.primaryFireEvent += HandleFireInput;
        }
    }

    public override void OnNetworkDespawn()
    {
        currentAmmo.OnValueChanged -= HandleAmmoChanged;
        isReloading.OnValueChanged -= HandleReloadStateChanged;

        if (!IsOwner) return;

        if (inputReader != null)
        {
            inputReader.primaryFireEvent -= HandleFireInput;
        }
    }

    private void HandleAmmoChanged(int oldValue, int newValue)
    {
        OnAmmoChanged?.Invoke(newValue, weaponData.magazineSize);
    }

    private void HandleReloadStateChanged(bool oldValue, bool newValue)
    {
        OnReloadStateChanged?.Invoke(newValue);
    }

    // ─── Input Handling ─────────────────────────────────────────

    private void HandleFireInput(bool pressed)
    {
        isFiring = pressed;
    }

    // ─── Update Loop ────────────────────────────────────────────

    private void Update()
    {
        if (!IsOwner) return;

        UpdateAimWorldPoint();

        // Semi-auto: fire once per press
        if (isFiring)
        {
            TryFire();
            // Semi-auto behavior: consume the press
            isFiring = false;
        }
    }

    /// <summary>
    /// Converts the screen-space aim position to a world point on the ground plane.
    /// Works for both mouse (PC) and touch (mobile) input.
    /// </summary>
    private void UpdateAimWorldPoint()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        Vector2 aimScreenPos = inputReader.aimPostion;
        Ray ray = mainCamera.ScreenPointToRay(aimScreenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, 200f, groundLayer))
        {
            currentAimWorldPoint = hit.point;
        }
        else
        {
            // Fallback: project onto Y=player height plane
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0, transform.position.y, 0));
            if (groundPlane.Raycast(ray, out float distance))
            {
                currentAimWorldPoint = ray.GetPoint(distance);
            }
        }
    }

    // ─── Fire Logic ─────────────────────────────────────────────

    private void TryFire()
    {
        // Can't fire while reloading
        if (isReloading.Value) return;

        // Cooldown check (local, to avoid unnecessary RPCs)
        if (Time.time - lastFireTime < weaponData.fireCooldown) return;

        // Ammo check (local prediction — server will also validate)
        if (currentAmmo.Value <= 0)
        {
            // Auto-reload when trying to fire with empty magazine
            RequestReloadServerRpc();
            return;
        }

        lastFireTime = Time.time;

        // Calculate fire direction from muzzle toward aim point
        Vector3 spawnPos = muzzlePoint != null ? muzzlePoint.position : transform.position + transform.forward * 0.5f;
        Vector3 fireDirection = (currentAimWorldPoint - spawnPos);
        fireDirection.y = 0f; // Keep projectiles on horizontal plane for top-down
        fireDirection.Normalize();

        // ─── Client-side prediction: spawn dummy projectile immediately ───
        SpawnDummyProjectile(spawnPos, fireDirection);

        // ─── Spawn muzzle flash locally ───
        SpawnMuzzleFlash(spawnPos, fireDirection);

        // ─── Request server to spawn the real networked projectile ───
        FireServerRpc(spawnPos, fireDirection);
    }

    /// <summary>
    /// Spawns a local-only dummy projectile for instant visual feedback.
    /// This eliminates the perceived delay of waiting for the server round-trip.
    /// </summary>
    private void SpawnDummyProjectile(Vector3 position, Vector3 direction)
    {
        if (weaponData.dummyProjectilePrefab == null) return;

        GameObject dummyGO = Instantiate(weaponData.dummyProjectilePrefab, position, Quaternion.identity);
        DummyProjectile dummy = dummyGO.GetComponent<DummyProjectile>();
        if (dummy != null)
        {
            dummy.Initialize(direction, weaponData.projectileData);
        }

        if(dummyGO.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.linearVelocity = direction * weaponData.projectileData.speed;
        }
    }

    /// <summary>
    /// Spawns a muzzle flash effect at the fire point.
    /// </summary>
    private void SpawnMuzzleFlash(Vector3 position, Vector3 direction)
    {
        if (weaponData.muzzleFlashPrefab == null) return;

        Quaternion rotation = Quaternion.LookRotation(direction);
        GameObject flash = Instantiate(weaponData.muzzleFlashPrefab, position, rotation);

        // Auto-destroy after a short time
        Destroy(flash, 0.5f);
    }

    // ─── Server RPCs ────────────────────────────────────────────

    /// <summary>
    /// Client requests the server to spawn a real projectile.
    /// Server validates ammo and cooldown before spawning.
    /// </summary>
    [ServerRpc]
    private void FireServerRpc(Vector3 spawnPosition, Vector3 fireDirection)
    {
        // Server-side validation
        if (isReloading.Value) return;
        if (currentAmmo.Value <= 0) return;

        // Deduct ammo
        currentAmmo.Value--;

        // Spawn the networked projectile
        SpawnNetworkProjectile(spawnPosition, fireDirection);

        // Notify all OTHER clients to show the muzzle flash (the owner already showed it)
        FireVisualsClientRpc(spawnPosition, fireDirection);
    }

    /// <summary>
    /// Server spawns the authoritative networked projectile.
    /// </summary>
    private void SpawnNetworkProjectile(Vector3 position, Vector3 direction)
    {
        if (weaponData.networkProjectilePrefab == null) return;

        GameObject projectileGO = Instantiate(
            weaponData.networkProjectilePrefab,
            position,
            Quaternion.identity
        );

        NetworkProjectile netProjectile = projectileGO.GetComponent<NetworkProjectile>();
        if (netProjectile != null)
        {
            netProjectile.Initialize(direction, weaponData.projectileData, OwnerClientId);
        }

        if (projectileGO.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.linearVelocity = direction * weaponData.projectileData.speed;
        }

        // Spawn on the network so all clients see it
        projectileGO.GetComponent<NetworkObject>().Spawn();
    }

    /// <summary>
    /// Shows fire visuals (muzzle flash) on non-owner clients.
    /// The owner already spawned their own dummy + flash immediately.
    /// </summary>
    [ClientRpc]
    private void FireVisualsClientRpc(Vector3 position, Vector3 direction)
    {
        // Skip owner — they already have the visuals
        if (IsOwner) return;

        SpawnMuzzleFlash(position, direction);
    }

    // ─── Reload ─────────────────────────────────────────────────

    /// <summary>
    /// Call this from UI button (mobile) or key press to trigger reload.
    /// Can also be called publicly from InputReader if you add a reload action.
    /// </summary>
    public void RequestReload()
    {
        if (!IsOwner) return;
        if (isReloading.Value) return;
        if (currentAmmo.Value >= weaponData.magazineSize) return;

        RequestReloadServerRpc();
    }

    [ServerRpc]
    private void RequestReloadServerRpc()
    {
        if (isReloading.Value) return;
        if (currentAmmo.Value >= weaponData.magazineSize) return;

        isReloading.Value = true;

        // Start reload coroutine on the server
        StartCoroutine(ReloadCoroutine());
    }

    private System.Collections.IEnumerator ReloadCoroutine()
    {
        yield return new WaitForSeconds(weaponData.reloadTime);

        currentAmmo.Value = weaponData.magazineSize;
        isReloading.Value = false;
    }
}
