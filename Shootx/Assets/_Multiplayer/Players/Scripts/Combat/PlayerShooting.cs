using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-authoritative hitscan shooting system with configurable wall ricochets,
/// distance-based damage (fractional), two-tier ammunition (Basic recoverable & Extra permanent),
/// and local-only animated laser beam rendering.
/// 
/// Attach to Player prefab alongside PlayerController, PlayerHealth, and PlayerMelee.
/// </summary>
public class PlayerShooting : NetworkBehaviour
{
    public enum AmmoType
    {
        Basic,
        Extra
    }

    [Header("Weapon Data Asset (Optional)")]
    [SerializeField] private WeaponData weaponData;

    [Header("Input")]
    [SerializeField] private InputReader inputReader;

    [Header("Fire Point")]
    [Tooltip("Transform at the weapon muzzle where the hitscan trace begins. Assign in Inspector.")]
    [SerializeField] private Transform muzzlePoint;

    /// <summary>
    /// Updates the active muzzle point when a new character visual or weapon is attached.
    /// </summary>
    public void SetMuzzlePoint(Transform point)
    {
        muzzlePoint = point;
        playerAudioController?.SetMuzzleTransform(point);
    }

    [Header("Camera & Aim Reference")]
    [SerializeField] private LayerMask groundLayer = 513;

    [Header("Hitscan & Ricochet Configuration")]
    [Tooltip("Minimum time between shots (seconds). Default 0.2s.")]
    [SerializeField] private float fireCooldown = 0.2f;

    [Tooltip("Maximum total shot range across all bounce segments.")]
    [SerializeField] private float maxShotRange = 30f;

    [Tooltip("Maximum number of wall bounces allowed (0 = no bounces).")]
    [SerializeField] private int maxRicochets = 2;

    [Tooltip("Offset distance along reflection normal to prevent repeated collisions with the same surface.")]
    [SerializeField] private float surfaceOffset = 0.02f;

    [Header("Collision Layer Masks")]
    [Tooltip("Layers that bullets can collide with (Players, Walls, Obstacles, Destructibles).")]
    [SerializeField] private LayerMask hitLayers = ~0;

    [Tooltip("Designated wall layer(s) that reflect bullets. Other walls block and stop the ray.")]
    [SerializeField] private LayerMask ricochetWallMask = 0;

    [Header("Distance Damage Thresholds (Health Units)")]
    [Tooltip("Distance threshold for close-range shots.")]
    [SerializeField] private float closeRangeDistance = 6f;

    [Tooltip("Distance threshold for medium-range shots.")]
    [SerializeField] private float mediumRangeDistance = 14f;

    [Header("Distance Damage Values")]
    [Tooltip("Close range damage (default 3 health units).")]
    [SerializeField] private float closeDamage = 3f;

    [Tooltip("Medium range damage (default 1.5 health units).")]
    [SerializeField] private float mediumDamage = 1.5f;

    [Tooltip("Long range damage (default 1 health unit).")]
    [SerializeField] private float longDamage = 1f;

    [Header("Ammunition Settings")]
    [Tooltip("Starting and maximum capacity for basic ammunition (default 3).")]
    [SerializeField] private int basicAmmoCapacity = 3;

    [Header("Laser Visuals (Local Only)")]
    [Tooltip("Prefab with LaserBeamVisual component, instantiated locally for the shooter only.")]
    [SerializeField] private GameObject laserBeamPrefab;

    [Tooltip("Muzzle flash effect spawned at muzzle point.")]
    [SerializeField] private GameObject muzzleFlashPrefab;

    [Tooltip("Enable instant local prediction for the laser beam to give zero-latency feedback.")]
    [SerializeField] private bool predictLaserLocally = true;

    [Header("Mobile Aim Preview (Local Only)")]
    [Tooltip("LineRenderer used for continuous local laser trajectory preview while joystick is armed.")]
    [SerializeField] private LineRenderer aimPreviewLine;
    [SerializeField] private Color previewBeamColor = new Color(0f, 0.9f, 1f, 0.45f);
    [SerializeField] private float previewBeamWidth = 0.04f;

    // ─── Networked Ammunition State ─────────────────────────────
    /// <summary>
    /// Basic ammunition. Starts at 3. Refunded only on confirmed enemy player hit.
    /// Only server writes. Synced to all clients.
    /// </summary>
    private readonly NetworkVariable<int> basicAmmo = new NetworkVariable<int>(
        3,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>
    /// Extra ammunition. Starts at 0. Consumed permanently when fired (fired first). Never refunded.
    /// Only server writes. Synced to all clients.
    /// </summary>
    private readonly NetworkVariable<int> extraAmmo = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ─── Public Events ──────────────────────────────────────────
    /// <summary>
    /// Event fired on all clients when ammo counts change.
    /// Passes (basicAmmo, extraAmmo).
    /// </summary>
    public event Action<int, int> OnAmmoChanged;

    /// <summary>
    /// Event fired on the owner when a basic bullet is confirmed refunded by the server.
    /// </summary>
    public event Action OnBasicAmmoRefunded;

    /// <summary>
    /// Event fired on the owner when an ammunition pickup is collected.
    /// </summary>
    public event Action OnAmmoPickupCollected;

    // ─── Properties ─────────────────────────────────────────────
    public int BasicAmmo => basicAmmo.Value;
    public int ExtraAmmo => extraAmmo.Value;
    public int TotalAmmo => basicAmmo.Value + extraAmmo.Value;
    public int BasicCapacity => weaponData != null ? weaponData.basicAmmoCapacity : basicAmmoCapacity;
    public float FireCooldown => weaponData != null ? weaponData.fireCooldown : fireCooldown;

    // ─── Local State ────────────────────────────────────────────
    private Camera mainCamera;
    private float lastLocalFireTime = float.MinValue;
    private float lastServerFireTime = float.MinValue;
    private bool isFiring;
    private Vector3 currentAimWorldPoint;
    private PlayerHealth playerHealth;
    private PlayerDash playerDash;
    private PlayerAnimationController animationController;
    private Shootx.Audio.PlayerAudioController playerAudioController;

    private void Awake()
    {
        mainCamera = Camera.main;
        playerHealth = GetComponent<PlayerHealth>();
        playerDash = GetComponent<PlayerDash>();
        animationController = GetComponent<PlayerAnimationController>();
        playerAudioController = GetComponent<Shootx.Audio.PlayerAudioController>();
        ApplyWeaponDataDefaults();
    }

    private void ApplyWeaponDataDefaults()
    {
        if (weaponData == null) return;

        if (weaponData.fireCooldown > 0f) fireCooldown = weaponData.fireCooldown;
        if (weaponData.maxShotRange > 0f) maxShotRange = weaponData.maxShotRange;
        if (weaponData.maxRicochets >= 0) maxRicochets = weaponData.maxRicochets;
        if (weaponData.closeRangeDistance > 0f) closeRangeDistance = weaponData.closeRangeDistance;
        if (weaponData.mediumRangeDistance > 0f) mediumRangeDistance = weaponData.mediumRangeDistance;
        if (weaponData.closeDamage > 0f) closeDamage = weaponData.closeDamage;
        if (weaponData.mediumDamage > 0f) mediumDamage = weaponData.mediumDamage;
        if (weaponData.longDamage > 0f) longDamage = weaponData.longDamage;
        if (weaponData.basicAmmoCapacity > 0) basicAmmoCapacity = weaponData.basicAmmoCapacity;
        if (laserBeamPrefab == null && weaponData.laserBeamPrefab != null) laserBeamPrefab = weaponData.laserBeamPrefab;
        if (muzzleFlashPrefab == null && weaponData.muzzleFlashPrefab != null) muzzleFlashPrefab = weaponData.muzzleFlashPrefab;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Server initializes ammo state
        if (IsServer)
        {
            basicAmmo.Value = BasicCapacity;
            extraAmmo.Value = 0;
            NormalizeAmmoState();
        }

        // Subscribe to synced value changes on all clients
        basicAmmo.OnValueChanged += HandleBasicAmmoChanged;
        extraAmmo.OnValueChanged += HandleExtraAmmoChanged;

        // Fire initial event for UI synchronization
        OnAmmoChanged?.Invoke(basicAmmo.Value, extraAmmo.Value);

        if (mainCamera == null) mainCamera = Camera.main;

        if (!IsOwner) return;

        // Bind Ammo UI if present in scene
        FindAnyObjectByType<MultiplayerAmmoUI>()?.Initialize(this);

        // Bind Mobile HUD if present in scene
        FindAnyObjectByType<MobileControlsHUD>()?.BindLocalPlayer(playerHealth);

        // Subscribe to input events
        if (inputReader != null)
        {
            inputReader.primaryFireEvent += HandleFireInput;
            inputReader.mobileAimDirectionEvent += HandleMobileAimDirection;
            inputReader.mobileAimStateChangedEvent += HandleMobileAimStateChanged;
            inputReader.mobileFireEvent += HandleMobileFire;
        }

        if (playerHealth != null)
        {
            playerHealth.OnDeath += HandleDeath;
            playerHealth.OnRespawn += HandleRespawn;
        }
    }

    public override void OnNetworkDespawn()
    {
        basicAmmo.OnValueChanged -= HandleBasicAmmoChanged;
        extraAmmo.OnValueChanged -= HandleExtraAmmoChanged;

        if (!IsOwner) return;

        if (inputReader != null)
        {
            inputReader.primaryFireEvent -= HandleFireInput;
            inputReader.mobileAimDirectionEvent -= HandleMobileAimDirection;
            inputReader.mobileAimStateChangedEvent -= HandleMobileAimStateChanged;
            inputReader.mobileFireEvent -= HandleMobileFire;
        }

        if (playerHealth != null)
        {
            playerHealth.OnDeath -= HandleDeath;
            playerHealth.OnRespawn -= HandleRespawn;
        }

        HideAimPreview(true);
    }

    private void HandleBasicAmmoChanged(int oldValue, int newValue)
    {
        OnAmmoChanged?.Invoke(newValue, extraAmmo.Value);
    }

    private void HandleExtraAmmoChanged(int oldValue, int newValue)
    {
        OnAmmoChanged?.Invoke(basicAmmo.Value, newValue);
    }

    private void HandleFireInput(bool pressed)
    {
        isFiring = pressed;
    }

    private void HandleMobileAimDirection(Vector3 direction)
    {
        if (inputReader != null && inputReader.IsMobileArmed)
        {
            UpdateAimPreview(direction);
        }
    }

    private void HandleMobileAimStateChanged(bool isArmed)
    {
        if (isArmed)
        {
            Vector3 dir = (inputReader != null && inputReader.MobileAimDirection.sqrMagnitude > 0.001f)
                ? inputReader.MobileAimDirection
                : transform.forward;
            UpdateAimPreview(dir);
        }
        else
        {
            HideAimPreview(true);
        }
    }

    private void HandleMobileFire(Vector3 direction)
    {
        RequestMobileFire(direction);
    }

    private void HandleDeath(ulong killerId)
    {
        HideAimPreview(true);
    }

    private void HandleRespawn()
    {
        HideAimPreview(true);
    }


    private void Update()
    {
        if (!IsOwner) return;

        UpdateAimWorldPoint();

        if (isFiring)
        {
            TryFire();
            isFiring = false;
        }
    }

    /// <summary>
    /// Calculates ground plane world point from screen aim position.
    /// </summary>
    private void UpdateAimWorldPoint()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null || inputReader == null) return;

        Vector2 aimScreenPos = inputReader.aimPostion;
        Ray ray = mainCamera.ScreenPointToRay(aimScreenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, 200f, groundLayer))
        {
            currentAimWorldPoint = hit.point;
        }
        else
        {
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0, transform.position.y, 0));
            if (groundPlane.Raycast(ray, out float distance))
            {
                currentAimWorldPoint = ray.GetPoint(distance);
            }
        }
    }

    // ─── Fire Request (Client) ──────────────────────────────────

    private void TryFire()
    {
        // Alive check
        if (playerHealth != null && playerHealth.IsDead) return;

        // Dash state check — no shooting while dashing
        if (playerDash != null && playerDash.IsDashing) return;

        // Match state check — no shooting outside active play
        if (MatchManager.Instance != null && MatchManager.Instance.CurrentState != MatchState.Active)
            return;

        // Local cooldown check
        if (Time.time - lastLocalFireTime < FireCooldown) return;

        // Local ammo prediction check
        if (TotalAmmo <= 0)
        {
            playerAudioController?.PlayDryFireAudio();
            return;
        }

        lastLocalFireTime = Time.time;

        Vector3 spawnPos = muzzlePoint != null ? muzzlePoint.position : transform.position + Vector3.up * 0.9f + transform.forward * 0.5f;
        Vector3 fireDirection = currentAimWorldPoint - spawnPos;
        fireDirection.y = 0f;
        if (fireDirection.sqrMagnitude < 0.001f) fireDirection = transform.forward;
        fireDirection.Normalize();

        // Local-only instant muzzle flash, predicted shoot animation, and gunshot sound
        SpawnMuzzleFlash(spawnPos, fireDirection);
        animationController?.TriggerLocalShoot();
        playerAudioController?.PlayGunshotAudio(isLocalOwner: true);

        // Optional local-only predicted laser visual
        if (predictLaserLocally)
        {
            List<Vector3> predictedPath = CalculateTrajectory(spawnPos, fireDirection, out _, out _);
            RenderLocalLaser(predictedPath);
        }

        // Request authoritative server execution
        FireServerRpc(spawnPos, fireDirection);
    }

    // ─── Mobile Fire & Continuous Preview ───────────────────────

    /// <summary>
    /// Executes a single authoritative shot from a mobile joystick release.
    /// Reuses the exact same cooldown, ammo, muzzle flash, and hitscan server RPC.
    /// </summary>
    public bool RequestMobileFire(Vector3 aimDirection)
    {
        if (!IsOwner) return false;
        if (playerHealth != null && playerHealth.IsDead) return false;
        if (playerDash != null && playerDash.IsDashing) return false;
        if (MatchManager.Instance != null && MatchManager.Instance.CurrentState != MatchState.Active)
            return false;
        if (Time.time - lastLocalFireTime < FireCooldown) return false;
        if (TotalAmmo <= 0)
        {
            playerAudioController?.PlayDryFireAudio();
            return false;
        }

        lastLocalFireTime = Time.time;
        HideAimPreview(true);

        Vector3 spawnPos = muzzlePoint != null ? muzzlePoint.position : transform.position + Vector3.up * 0.9f + transform.forward * 0.5f;
        if (aimDirection.sqrMagnitude < 0.001f) aimDirection = transform.forward;
        aimDirection.y = 0f;
        aimDirection.Normalize();

        SpawnMuzzleFlash(spawnPos, aimDirection);
        animationController?.TriggerLocalShoot();
        playerAudioController?.PlayGunshotAudio(isLocalOwner: true);

        if (predictLaserLocally)
        {
            List<Vector3> predictedPath = CalculateTrajectory(spawnPos, aimDirection, out _, out _);
            RenderLocalLaser(predictedPath);
        }

        FireServerRpc(spawnPos, aimDirection);
        return true;
    }

    /// <summary>
    /// Continuously renders a local-only laser preview while the aim joystick is armed.
    /// Uses CalculateTrajectory with serverFiredType == null (NO damage, NO ammo refund).
    /// </summary>
    public void UpdateAimPreview(Vector3 aimDirection)
    {
        if (!IsOwner) return;
        if ((playerHealth != null && playerHealth.IsDead) ||
            (playerDash != null && playerDash.IsDashing) ||
            (MatchManager.Instance != null && MatchManager.Instance.CurrentState != MatchState.Active))
        {
            HideAimPreview(true);
            return;
        }

        EnsureAimPreviewLine();
        if (aimPreviewLine == null) return;

        Vector3 spawnPos = muzzlePoint != null ? muzzlePoint.position : transform.position + Vector3.up * 0.9f + transform.forward * 0.5f;
        if (aimDirection.sqrMagnitude < 0.001f) aimDirection = transform.forward;
        aimDirection.y = 0f;
        aimDirection.Normalize();

        List<Vector3> trajectory = CalculateTrajectory(spawnPos, aimDirection, out _, out _, serverFiredType: null);

        aimPreviewLine.enabled = true;
        aimPreviewLine.positionCount = trajectory.Count;
        for (int i = 0; i < trajectory.Count; i++)
        {
            aimPreviewLine.SetPosition(i, trajectory[i]);
        }
    }

    /// <summary>
    /// Hides the local laser preview immediately upon disarm, cancel, death, or fire.
    /// </summary>
    public void HideAimPreview(bool immediate = false)
    {
        if (aimPreviewLine != null)
        {
            aimPreviewLine.positionCount = 0;
            aimPreviewLine.enabled = false;
        }
    }

    private void EnsureAimPreviewLine()
    {
        if (aimPreviewLine != null) return;

        GameObject previewGO = new GameObject("AimPreviewLine");
        previewGO.transform.SetParent(transform, false);
        aimPreviewLine = previewGO.AddComponent<LineRenderer>();
        aimPreviewLine.useWorldSpace = true;
        aimPreviewLine.startWidth = previewBeamWidth;
        aimPreviewLine.endWidth = previewBeamWidth;
        aimPreviewLine.startColor = previewBeamColor;
        aimPreviewLine.endColor = previewBeamColor;
        aimPreviewLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        aimPreviewLine.receiveShadows = false;

        Material lineMat = null;
        if (laserBeamPrefab != null && laserBeamPrefab.TryGetComponent<LineRenderer>(out var prefabLR) && prefabLR.sharedMaterial != null)
        {
            lineMat = new Material(prefabLR.sharedMaterial);
        }
        else
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) lineMat = new Material(shader);
        }

        if (lineMat != null)
        {
            aimPreviewLine.material = lineMat;
        }

        aimPreviewLine.positionCount = 0;
        aimPreviewLine.enabled = false;
    }


    // ─── Server-Authoritative Shooting ──────────────────────────

    [ServerRpc]
    private void FireServerRpc(Vector3 clientSpawnPos, Vector3 fireDirection)
    {
        // 1. Validate player is alive
        if (playerHealth != null && playerHealth.IsDead) return;

        // 1b. Validate match is active
        if (MatchManager.Instance != null && MatchManager.Instance.CurrentState != MatchState.Active)
            return;

        // 2. Validate cooldown
        if (Time.time - lastServerFireTime < FireCooldown - 0.03f) return;
        lastServerFireTime = Time.time;

        // 3. Firing Priority: Extra ammo fired first, then basic
        AmmoType firedType;
        if (extraAmmo.Value > 0)
        {
            firedType = AmmoType.Extra;
            extraAmmo.Value--;
        }
        else if (basicAmmo.Value > 0)
        {
            firedType = AmmoType.Basic;
            basicAmmo.Value--;
        }
        else
        {
            // Out of ammo, reject shot
            return;
        }

        // Ensure ammo invariant: ExtraAmmo > 0 => BasicAmmo == Capacity
        NormalizeAmmoState();

        // 4. Validate firing origin on server
        Vector3 spawnPos = muzzlePoint != null ? muzzlePoint.position : transform.position + Vector3.up * 0.9f + transform.forward * 0.5f;
        fireDirection.y = 0f;
        if (fireDirection.sqrMagnitude < 0.001f) fireDirection = transform.forward;
        fireDirection.Normalize();

        // 5. Authoritative hitscan raycast calculation
        bool hitEnemyPlayer = false;
        bool wasRefunded = false;
        List<Vector3> trajectory = CalculateTrajectory(spawnPos, fireDirection, out hitEnemyPlayer, out wasRefunded, firedType);

        // 6. Broadcast confirmed shoot animation to remote clients
        if (animationController == null) animationController = GetComponent<PlayerAnimationController>();
        animationController?.PlayShootClientRpc();

        // 7. Notify ALL CLIENTS about the result and confirmed laser trajectory
        BroadcastShotEffectsClientRpc(trajectory.ToArray(), hitEnemyPlayer, wasRefunded);
    }

    /// <summary>
    /// Calculates the full Raycast trajectory with ricochets and distance damage.
    /// If firedType is passed, server applies authoritative damage and basic-ammo refund.
    /// </summary>
    private List<Vector3> CalculateTrajectory(
        Vector3 origin,
        Vector3 initialDirection,
        out bool hitEnemyPlayer,
        out bool wasRefunded,
        AmmoType? serverFiredType = null)
    {
        hitEnemyPlayer = false;
        wasRefunded = false;

        List<Vector3> trajectory = new List<Vector3> { origin };
        Vector3 currentOrigin = origin;
        Vector3 currentDir = initialDirection;
        float remainingRange = maxShotRange;
        float cumulativeDistance = 0f;
        int bounces = 0;

        while (bounces <= maxRicochets && remainingRange > 0.05f)
        {
            Ray ray = new Ray(currentOrigin, currentDir);
            if (Physics.Raycast(ray, out RaycastHit hit, remainingRange, hitLayers, QueryTriggerInteraction.Ignore))
            {
                // Ignore shooter's own colliders
                if (hit.collider.transform.root == transform.root)
                {
                    currentOrigin = hit.point + currentDir * surfaceOffset;
                    remainingRange -= Vector3.Distance(ray.origin, currentOrigin);
                    continue;
                }

                float segmentDist = hit.distance;
                cumulativeDistance += segmentDist;
                remainingRange -= segmentDist;
                trajectory.Add(hit.point);

                // Check for player
                PlayerHealth targetHealth = hit.collider.GetComponentInParent<PlayerHealth>();
                INetworkDamageable damageable = hit.collider.GetComponentInParent<INetworkDamageable>();

                if (targetHealth != null && targetHealth.transform.root != transform.root)
                {
                    // Hit an enemy player!
                    hitEnemyPlayer = true;

                    if (serverFiredType.HasValue && IsServer)
                    {
                        float damage = CalculateDistanceDamage(cumulativeDistance);
                        targetHealth.TakeDamage(damage, OwnerClientId);

                        // Basic ammo refund rule
                        if (serverFiredType.Value == AmmoType.Basic)
                        {
                            basicAmmo.Value = Mathf.Min(basicAmmo.Value + 1, BasicCapacity);
                            wasRefunded = true;
                            NormalizeAmmoState();
                        }
                    }

                    // Stop ray immediately: do not penetrate or bounce off players
                    break;
                }
                else if (damageable != null && hit.collider.transform.root != transform.root)
                {
                    // Hit a destructible object!
                    if (serverFiredType.HasValue && IsServer)
                    {
                        float damage = CalculateDistanceDamage(cumulativeDistance);
                        if (damageable is DestructibleBox destructibleBox)
                        {
                            destructibleBox.TakeDamageWithHitInfo(damage, OwnerClientId, hit.point, currentDir);
                        }
                        else
                        {
                            damageable.TakeDamage(damage, OwnerClientId);
                        }
                    }

                    // Stop ray immediately: do not penetrate or bounce off destructibles
                    break;
                }
                else if (ricochetWallMask.value != 0 && ((1 << hit.collider.gameObject.layer) & ricochetWallMask.value) != 0 && bounces < maxRicochets)
                {
                    // Hit a designated ricochet wall!
                    Vector3 reflectDir = Vector3.Reflect(currentDir, hit.normal);
                    reflectDir.y = 0f;
                    reflectDir.Normalize();

                    currentDir = reflectDir;
                    currentOrigin = hit.point + hit.normal * surfaceOffset;
                    bounces++;
                }
                else
                {
                    // Ordinary wall or obstacle: stops ray without reflection
                    break;
                }
            }
            else
            {
                // Reached max effective range in open air
                Vector3 endPt = currentOrigin + currentDir * remainingRange;
                trajectory.Add(endPt);
                break;
            }
        }

        return trajectory;
    }

    /// <summary>
    /// Calculates distance-based damage according to cumulative ray path distance.
    /// </summary>
    private float CalculateDistanceDamage(float cumulativeDistance)
    {
        if (cumulativeDistance <= closeRangeDistance)
        {
            return closeDamage; // default 3
        }
        else if (cumulativeDistance <= mediumRangeDistance)
        {
            return mediumDamage; // default 1.5
        }
        else if (cumulativeDistance <= maxShotRange)
        {
            return longDamage; // default 1
        }
        return 0f;
    }

    // ─── Local-Only Laser Visuals ───────────────────────────────

    [ClientRpc]
    private void BroadcastShotEffectsClientRpc(Vector3[] trajectoryPoints, bool hitEnemy, bool wasRefunded)
    {
        if (IsOwner)
        {
            if (!predictLaserLocally)
            {
                RenderLocalLaser(trajectoryPoints);
            }

            if (wasRefunded)
            {
                OnBasicAmmoRefunded?.Invoke();
            }
        }
        else
        {
            RenderLocalLaser(trajectoryPoints);

            Vector3 spawnPos = muzzlePoint != null ? muzzlePoint.position : transform.position + Vector3.up * 0.9f + transform.forward * 0.5f;
            Vector3 direction = trajectoryPoints.Length > 1 ? (trajectoryPoints[1] - trajectoryPoints[0]).normalized : transform.forward;
            SpawnMuzzleFlash(spawnPos, direction);

            playerAudioController?.PlayGunshotAudio(isLocalOwner: false);
        }

        if (trajectoryPoints != null && trajectoryPoints.Length > 1)
        {
            for (int i = 1; i < trajectoryPoints.Length; i++)
            {
                if (i < trajectoryPoints.Length - 1)
                {
                    Shootx.Audio.AudioManager.Instance?.PlaySound3D(Shootx.Audio.SoundId.Impact_Ricochet, trajectoryPoints[i]);
                }
                else if (!hitEnemy)
                {
                    Shootx.Audio.AudioManager.Instance?.PlaySound3D(Shootx.Audio.SoundId.Impact_Wall, trajectoryPoints[i]);
                }
            }
        }
    }

    private void RenderLocalLaser(IReadOnlyList<Vector3> points)
    {
        if (laserBeamPrefab == null || points == null || points.Count < 2) return;

        GameObject laserGO = Instantiate(laserBeamPrefab);
        if (laserGO.TryGetComponent<LaserBeamVisual>(out var laserVisual))
        {
            laserVisual.AnimatePath(points);
        }
        else if (laserGO.TryGetComponent<LineRenderer>(out var lr))
        {
            lr.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++)
            {
                lr.SetPosition(i, points[i]);
            }
            Destroy(laserGO, 0.15f);
        }
    }

    private void SpawnMuzzleFlash(Vector3 position, Vector3 direction)
    {
        if (muzzleFlashPrefab == null) return;

        Quaternion rotation = direction.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(direction)
            : Quaternion.identity;

        GameObject flash = Instantiate(muzzleFlashPrefab, position, rotation);
        Destroy(flash, 0.5f);
    }

    // ─── Authoritative Ammunition Operations ────────────────────

    /// <summary>
    /// Authoritative server-side pickup collection logic.
    /// Follows exact classification: TotalAmmo < 3 => +1 Basic, else => +1 Extra.
    /// </summary>
    public void CollectAmmoPickupServer()
    {
        if (!IsServer) return;
        if (playerHealth != null && playerHealth.IsDead) return;

        int total = basicAmmo.Value + extraAmmo.Value;
        if (total < BasicCapacity)
        {
            basicAmmo.Value = Mathf.Min(basicAmmo.Value + 1, BasicCapacity);
        }
        else
        {
            extraAmmo.Value++;
        }

        NormalizeAmmoState();
        NotifyAmmoPickupCollectedClientRpc();
    }

    [ClientRpc]
    private void NotifyAmmoPickupCollectedClientRpc()
    {
        if (!IsOwner) return;
        Shootx.Audio.AudioManager.Instance?.PlaySound2D(Shootx.Audio.SoundId.Pickup_Ammo_Local);
        OnAmmoPickupCollected?.Invoke();
    }

    /// <summary>
    /// Called on server upon player respawn: resets basic ammo to 3, preserves extra ammo.
    /// </summary>
    public void HandleRespawnServer()
    {
        if (!IsServer) return;
        basicAmmo.Value = BasicCapacity;
        // extraAmmo.Value is intentionally preserved!
        NormalizeAmmoState();
    }

    /// <summary>
    /// Guarantees the invariant: ExtraAmmo > 0 => BasicAmmo == BasicCapacity.
    /// Eliminates impossible states like (Basic 2, Extra 1).
    /// </summary>
    private void NormalizeAmmoState()
    {
        if (!IsServer) return;
        if (extraAmmo.Value > 0 && basicAmmo.Value < BasicCapacity)
        {
            basicAmmo.Value = BasicCapacity;
        }
    }
}
