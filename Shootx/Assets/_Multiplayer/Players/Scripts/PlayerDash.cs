using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Client-authoritative, collision-safe Multiplayer Dash Ability.
///
/// Features:
///   - Shared 15-second cooldown across PC (Left Shift) and mobile controls.
///   - Automatic direction selection: dashes in current movement direction while moving,
///     or in the last valid nonzero movement direction while standing still (fallback: forward).
///   - Swept capsule collision checks preventing any wall or obstacle penetration.
///   - Blocks shooting and melee attacks during active dash displacement.
///   - Networked visual trail synchronization for remote players without per-frame network allocations.
///   - Graceful lifecycle management across player death, respawn, and match states.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerDash : NetworkBehaviour
{
    public enum DashState
    {
        Ready,
        Dashing,
        Cooldown
    }

    // ─── Inspector Configuration ────────────────────────────────
    [Header("Input Architecture Dependency")]
    [SerializeField] private InputReader inputReader;

    [Header("Dash Configuration")]
    [Tooltip("Total ability cooldown in seconds. Shared across PC and mobile.")]
    [SerializeField] private float dashCooldown = 15f;

    [Tooltip("Total duration of the dash movement in seconds.")]
    [SerializeField] private float dashDuration = 0.25f;

    [Tooltip("Total displacement distance in open space in world units.")]
    [SerializeField] private float dashDistance = 4f;

    [Tooltip("Safety margin offset from obstacle surfaces to prevent character penetration.")]
    [SerializeField] private float collisionSkinMargin = 0.05f;

    [Tooltip("Layers treated as solid obstacles that stop or clamp dash movement.")]
    [SerializeField] private LayerMask collisionLayers = ~((1 << 16) | (1 << 2) | (1 << 5));

    [Header("Visual Effects (Trail)")]
    [Tooltip("Optional TrailRenderer attached to this player to render the speed streak visual.")]
    [SerializeField] private TrailRenderer trailRenderer;

    [Tooltip("How long the trail persists after dash emission stops.")]
    [SerializeField] private float trailFadeDuration = 0.35f;

    // ─── Events ─────────────────────────────────────────────────
    /// <summary>Fires on the owner when dash activation is accepted and movement begins.</summary>
    public event Action OnDashStarted;

    /// <summary>Fires on the owner when dash movement ends (normally or blocked by obstacle).</summary>
    public event Action OnDashEnded;

    /// <summary>Fires on the owner when the 15-second cooldown completes and the ability is Ready.</summary>
    public event Action OnCooldownComplete;

    /// <summary>Global static event notifying UI components when the local player's dash spawns.</summary>
    public static event Action<PlayerDash> OnLocalPlayerDashSpawned;

    // ─── Properties ─────────────────────────────────────────────
    public DashState CurrentState { get; private set; } = DashState.Ready;
    public bool IsDashing => CurrentState == DashState.Dashing;
    public bool IsReady => CurrentState == DashState.Ready;

    public float DashCooldownDuration => dashCooldown;
    public float DashDuration => dashDuration;
    public float DashDistance => dashDistance;
    public Vector3 LockedDashDirection => lockedDashDirection;

    public float RemainingCooldown => Mathf.Max(0f, cooldownEndTime - Time.time);
    public float CooldownProgress => dashCooldown > 0f ? Mathf.Clamp01(RemainingCooldown / dashCooldown) : 0f;

    // ─── Internal State ─────────────────────────────────────────
    private PlayerController playerController;
    private PlayerHealth playerHealth;
    private Vector3 lockedDashDirection = Vector3.forward;
    private float dashTimer;
    private float cooldownEndTime = float.MinValue;
    private float lastServerDashTime = float.MinValue;

    private readonly RaycastHit[] sweepHits = new RaycastHit[16];
    private Coroutine remoteTrailCoroutine;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        playerHealth = GetComponent<PlayerHealth>();

        if (trailRenderer == null)
        {
            trailRenderer = GetComponentInChildren<TrailRenderer>();
        }

        if (trailRenderer != null)
        {
            trailRenderer.emitting = false;
        }
    }

    private void OnValidate()
    {
        if (dashCooldown < 0.1f) dashCooldown = 0.1f;
        if (dashDuration < 0.05f) dashDuration = 0.05f;
        if (dashDistance < 0.5f) dashDistance = 0.5f;
        if (collisionSkinMargin < 0.01f) collisionSkinMargin = 0.01f;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (playerHealth != null)
        {
            playerHealth.OnDeath += HandleDeath;
            playerHealth.OnRespawn += HandleRespawn;
        }

        if (!IsOwner) return;

        if (inputReader != null)
        {
            inputReader.dashEvent += HandleDashInput;
        }

        OnLocalPlayerDashSpawned?.Invoke(this);
    }

    public override void OnNetworkDespawn()
    {
        if (playerHealth != null)
        {
            playerHealth.OnDeath -= HandleDeath;
            playerHealth.OnRespawn -= HandleRespawn;
        }

        if (IsOwner && inputReader != null)
        {
            inputReader.dashEvent -= HandleDashInput;
        }

        ResetVisuals();
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Manage cooldown expiration
        if (CurrentState == DashState.Cooldown)
        {
            if (Time.time >= cooldownEndTime)
            {
                CurrentState = DashState.Ready;
                OnCooldownComplete?.Invoke();
            }
        }
    }

    // ─── Input & Activation ─────────────────────────────────────

    private void HandleDashInput()
    {
        TryDash();
    }

    /// <summary>
    /// Centralized entry point for dash activation requests (both PC Left Shift and Mobile button).
    /// </summary>
    public bool TryDash()
    {
        if (!IsOwner) return false;
        if (CurrentState != DashState.Ready) return false;

        // Disallow dash while dead
        if (playerHealth != null && playerHealth.IsDead) return false;

        // Disallow dash while input is locked (e.g. countdown, match ended)
        if (playerController != null && playerController.IsInputLocked) return false;

        // Disallow dash outside of active gameplay
        if (MatchManager.Instance != null && MatchManager.Instance.CurrentState != MatchState.Active)
            return false;

        // Determine dash direction from movement system
        Vector3 selectedDirection = Vector3.forward;
        if (playerController != null)
        {
            selectedDirection = playerController.GetDashDirection();
        }
        else
        {
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            selectedDirection = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;
        }

        StartDash(selectedDirection);
        return true;
    }

    private void StartDash(Vector3 direction)
    {
        lockedDashDirection = direction.normalized;
        dashTimer = 0f;

        // Cooldown starts immediately upon activation
        cooldownEndTime = Time.time + dashCooldown;
        CurrentState = DashState.Dashing;

        // Disarm any pending mobile aim release-to-fire so it cannot fire a shot during or after dash
        if (inputReader != null && inputReader.IsMobileArmed)
        {
            inputReader.SetMobileAim(Vector3.zero, isAiming: false, isArmed: false);
        }

        // Hide local laser preview immediately
        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null)
        {
            shooting.HideAimPreview(true);
        }

        // Start local visual trail
        if (trailRenderer != null)
        {
            trailRenderer.Clear();
            trailRenderer.emitting = true;
        }

        OnDashStarted?.Invoke();

        // Notify server to replicate visuals to remote clients
        DashServerRpc(transform.position, lockedDashDirection);
    }

    // ─── Movement & Swept Collision Resolution ──────────────────

    /// <summary>
    /// Called by PlayerController during ProcessDashMovement.
    /// Performs swept capsule collision tests and calculates safe horizontal displacement.
    /// </summary>
    public bool ExecuteDashStep(CharacterController cc, float dt, out Vector3 horizontalDisplacement, out bool hitObstacle)
    {
        horizontalDisplacement = Vector3.zero;
        hitObstacle = false;

        if (CurrentState != DashState.Dashing) return false;

        float dashSpeed = dashDistance / dashDuration;
        float desiredDistance = dashSpeed * dt;

        // Capsule geometry parameters
        Vector3 centerWorld = transform.position + cc.center;
        float halfHeight = Mathf.Max(0f, (cc.height * 0.5f) - cc.radius);
        Vector3 point1 = centerWorld + Vector3.up * halfHeight;
        Vector3 point2 = centerWorld - Vector3.up * halfHeight;

        // Use slightly reduced radius for the sweep check to avoid catching parallel surfaces
        float sweepRadius = Mathf.Max(0.05f, cc.radius - collisionSkinMargin);

        int hitCount = Physics.CapsuleCastNonAlloc(
            point1,
            point2,
            sweepRadius,
            lockedDashDirection,
            sweepHits,
            desiredDistance + collisionSkinMargin,
            collisionLayers,
            QueryTriggerInteraction.Ignore
        );

        float safeDistance = desiredDistance;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = sweepHits[i];

            // Ignore own colliders
            if (hit.collider == null || hit.collider.transform.root == transform.root) continue;
            if (hit.collider.isTrigger) continue;

            float distanceToWall = Mathf.Max(0f, hit.distance - collisionSkinMargin);
            if (distanceToWall < safeDistance)
            {
                safeDistance = distanceToWall;
                hitObstacle = true;
            }
        }

        horizontalDisplacement = lockedDashDirection * safeDistance;

        dashTimer += dt;
        if (dashTimer >= dashDuration)
        {
            EndDashMovement();
        }

        return true;
    }

    /// <summary>
    /// Called when the player contacts a blocking wall or obstacle during the dash.
    /// </summary>
    public void OnDashObstacleEncountered()
    {
        EndDashMovement();
    }

    private void EndDashMovement()
    {
        if (CurrentState != DashState.Dashing) return;

        CurrentState = DashState.Cooldown;

        if (trailRenderer != null)
        {
            trailRenderer.emitting = false;
        }

        OnDashEnded?.Invoke();
    }

    /// <summary>
    /// Cancels active dash movement immediately (e.g. on death or external lock),
    /// while strictly maintaining the already started 15-second cooldown.
    /// </summary>
    public void CancelDash()
    {
        if (CurrentState == DashState.Dashing)
        {
            EndDashMovement();
        }
        ResetVisuals();
    }

    // ─── Lifecycle & State Reset ────────────────────────────────

    private void HandleDeath(ulong killerId)
    {
        CancelDash();
    }

    private void HandleRespawn()
    {
        ResetVisuals();

        // If cooldown expired while dead, mark ability Ready
        if (Time.time >= cooldownEndTime)
        {
            CurrentState = DashState.Ready;
            OnCooldownComplete?.Invoke();
        }
    }

    private void ResetVisuals()
    {
        if (remoteTrailCoroutine != null)
        {
            StopCoroutine(remoteTrailCoroutine);
            remoteTrailCoroutine = null;
        }

        if (trailRenderer != null)
        {
            trailRenderer.emitting = false;
            trailRenderer.Clear();
        }
    }

    // ─── NGO Synchronization & Remote VFX ───────────────────────

    [ServerRpc]
    private void DashServerRpc(Vector3 startPos, Vector3 dashDir)
    {
        // Server-side validation
        if (playerHealth != null && playerHealth.IsDead) return;
        if (MatchManager.Instance != null && MatchManager.Instance.CurrentState != MatchState.Active) return;

        if (Time.time < lastServerDashTime + dashCooldown - 0.25f)
        {
            // Cooldown violation attempt
            return;
        }
        lastServerDashTime = Time.time;

        // Forward to all clients (owner already played local effects immediately)
        DashVisualClientRpc(startPos, dashDir);
    }

    [ClientRpc]
    private void DashVisualClientRpc(Vector3 startPos, Vector3 dashDir)
    {
        if (IsOwner) return; // Owner already played zero-latency local visuals

        PlayRemoteDashVisual();
    }

    private void PlayRemoteDashVisual()
    {
        if (trailRenderer == null) return;

        if (remoteTrailCoroutine != null)
        {
            StopCoroutine(remoteTrailCoroutine);
        }
        remoteTrailCoroutine = StartCoroutine(RemoteTrailCoroutine());
    }

    private IEnumerator RemoteTrailCoroutine()
    {
        trailRenderer.Clear();
        trailRenderer.emitting = true;

        yield return new WaitForSeconds(dashDuration);

        trailRenderer.emitting = false;
        remoteTrailCoroutine = null;
    }
}
