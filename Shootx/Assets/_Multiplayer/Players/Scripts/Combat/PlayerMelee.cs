using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-authoritative melee attack system with client-side prediction for visuals.
///
/// Architecture mirrors PlayerShooting:
///   - Owner presses the melee key → local VFX plays instantly (prediction).
///   - Owner sends MeleeServerRpc → server validates range/angle/cooldown.
///   - Server applies damage via INetworkDamageable.TakeDamage.
///   - Server calls MeleeVisualsClientRpc so all OTHER clients see the swing.
///
/// Attach this component to the same Player prefab as PlayerController and PlayerShooting.
/// </summary>
public class PlayerMelee : NetworkBehaviour
{
    // ─── Inspector ──────────────────────────────────────────────
    [Header("Melee Configuration")]
    [SerializeField] private MeleeData meleeData;

    [Header("Input")]
    [SerializeField] private InputReader inputReader;

    [Header("Hit Origin")]
    [Tooltip("Transform used as the centre of the melee hit-check. " +
             "Defaults to this object's transform if left empty.")]
    [SerializeField] private Transform hitOrigin;

    // ─── Events (subscribe in UI / animation controllers) ───────
    /// <summary>Fires on the owner when a swing is started (for animation).</summary>
    public event Action OnMeleeSwingStarted;

    /// <summary>Fires on ANY client when the server confirms a hit.</summary>
    public event Action<Vector3> OnMeleeHitConfirmed;

    // ─── Local State ────────────────────────────────────────────
    private float lastLocalMeleeTime = float.MinValue;
    private float lastServerMeleeTime = float.MinValue;
    private bool meleeRequested;
    private PlayerHealth _playerHealth;
    private PlayerDash _playerDash;

    // ─── NetworkBehaviour ───────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _playerHealth = GetComponent<PlayerHealth>();
        _playerDash = GetComponent<PlayerDash>();

        if (!IsOwner) return;

        if (inputReader != null)
            inputReader.meleeEvent += HandleMeleeInput;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        if (inputReader != null)
            inputReader.meleeEvent -= HandleMeleeInput;
    }

    // ─── Input ──────────────────────────────────────────────────

    private void HandleMeleeInput()
    {
        meleeRequested = true;
    }

    // ─── Update ─────────────────────────────────────────────────

    private void Update()
    {
        if (!IsOwner) return;

        if (meleeRequested)
        {
            meleeRequested = false;
            TryMelee();
        }
    }

    // ─── Core Logic ─────────────────────────────────────────────

    private void TryMelee()
    {
        // Dead state check
        if (_playerHealth != null && _playerHealth.IsDead) return;

        // Dash state check — no melee while dashing
        if (_playerDash != null && _playerDash.IsDashing) return;

        // Match state check — no attacking outside active play
        if (MatchManager.Instance != null &&
            MatchManager.Instance.CurrentState != MatchState.Active) return;

        if (Time.time - lastLocalMeleeTime < meleeData.attackCooldown) return;
        lastLocalMeleeTime = Time.time;

        PlayLocalSlashVfx();
        OnMeleeSwingStarted?.Invoke();

        MeleeServerRpc(transform.position, transform.forward);
    }

    /// <summary>
    /// Spawns the local-only slash VFX immediately for zero-latency feel.
    /// </summary>
    private void PlayLocalSlashVfx()
    {
        if (meleeData.localSlashVfxPrefab == null) return;

        Transform origin = hitOrigin != null ? hitOrigin : transform;
        GameObject vfx = Instantiate(
            meleeData.localSlashVfxPrefab,
            origin.position,
            origin.rotation
        );
        Destroy(vfx, 2f);
    }

    // ─── Server RPC ─────────────────────────────────────────────

    /// <summary>
    /// Called by the owner. Server validates position, angle, and cooldown,
    /// then applies damage to any eligible target found.
    /// </summary>
    [ServerRpc]
    private void MeleeServerRpc(Vector3 attackerPosition, Vector3 attackerForward)
    {
        // Server-side dead check
        if (_playerHealth != null && _playerHealth.IsDead) return;

        // Match state gate
        if (MatchManager.Instance != null &&
            MatchManager.Instance.CurrentState != MatchState.Active) return;

        // Server-side cooldown guard (prevents cheating / lag abuse)
        if (Time.time - lastServerMeleeTime < meleeData.attackCooldown - 0.05f)
            return;
        lastServerMeleeTime = Time.time;

        // Find all colliders in range (ensure Box layer is included)
        int boxLayer = LayerMask.NameToLayer("Box");
        LayerMask combinedMask = meleeData.hitLayers;
        if (boxLayer != -1)
        {
            combinedMask |= (1 << boxLayer);
        }

        Collider[] hits = Physics.OverlapSphere(
            attackerPosition,
            meleeData.attackRange,
            combinedMask
        );

        bool landedHit = false;
        Vector3 firstHitPoint = attackerPosition + attackerForward * 0.5f;

        foreach (Collider col in hits)
        {
            // Skip self
            if (col.transform.root == transform.root) continue;

            // Angle check — only hit targets within the attack arc
            Vector3 flatForward = new Vector3(attackerForward.x, 0f, attackerForward.z).normalized;
            Vector3 toTarget = col.bounds.center - attackerPosition;
            Vector3 flatToTarget = new Vector3(toTarget.x, 0f, toTarget.z).normalized;

            float angle = Vector3.Angle(flatForward, flatToTarget);
            if (angle > meleeData.attackAngle * 0.5f) continue;

            // Apply damage through the interface
            INetworkDamageable damageable = col.GetComponentInParent<INetworkDamageable>();
            if (damageable == null) continue;
            Debug.Log($"Melee hit {col.name} for {meleeData.damage} damage.");

            if (damageable is DestructibleBox destructibleBox)
            {
                destructibleBox.TakeDamageWithHitInfo(meleeData.damage, OwnerClientId, col.bounds.center, attackerForward);
            }
            else
            {
                damageable.TakeDamage(meleeData.damage, OwnerClientId);
            }

            if (!landedHit)
            {
                firstHitPoint = col.bounds.center;
                landedHit = true;
            }
        }

        // Notify all clients about the swing visuals
        // (owner already played their local VFX, so we skip them below)
        MeleeVisualsClientRpc(attackerPosition, attackerForward, firstHitPoint, landedHit);
    }

    // ─── Client RPC ─────────────────────────────────────────────

    /// <summary>
    /// Runs on ALL clients. Shows the swing to non-owners and, if a hit landed,
    /// plays the impact VFX for everyone (including the owner for the impact).
    /// </summary>
    [ClientRpc]
    private void MeleeVisualsClientRpc(
        Vector3 attackerPosition,
        Vector3 attackerForward,
        Vector3 hitPoint,
        bool landedHit)
    {
        // Non-owners see the slash VFX and swing animation here (owner already played it locally)
        if (!IsOwner)
        {
            GetComponent<PlayerAnimationController>()?.TriggerRemoteMelee();
            GetComponent<Shootx.Audio.PlayerAudioController>()?.PlayMeleeSwingAudio(isLocalOwner: false);

            if (meleeData.localSlashVfxPrefab != null)
            {
                Quaternion rot = attackerForward != Vector3.zero
                    ? Quaternion.LookRotation(attackerForward)
                    : Quaternion.identity;

                GameObject vfx = Instantiate(meleeData.localSlashVfxPrefab, attackerPosition, rot);
                Destroy(vfx, 2f);
            }
        }

        // Everyone sees the impact VFX if the hit landed
        if (landedHit)
        {
            OnMeleeHitConfirmed?.Invoke(hitPoint);

            if (meleeData.networkHitVfxPrefab != null)
            {
                GameObject hitVfx = Instantiate(meleeData.networkHitVfxPrefab, hitPoint, Quaternion.identity);
                Destroy(hitVfx, 2f);
            }
        }
    }

    // ─── Gizmos (Editor only) ───────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (meleeData == null) return;

        Transform origin = hitOrigin != null ? hitOrigin : transform;

        // Range sphere
        Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.25f);
        Gizmos.DrawSphere(origin.position, meleeData.attackRange);

        // Attack arc (forward direction cone outline)
        Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.9f);
        float halfAngle = meleeData.attackAngle * 0.5f;
        Quaternion leftRot  = Quaternion.AngleAxis(-halfAngle, Vector3.up);
        Quaternion rightRot = Quaternion.AngleAxis( halfAngle, Vector3.up);
        Vector3 leftDir  = leftRot  * transform.forward * meleeData.attackRange;
        Vector3 rightDir = rightRot * transform.forward * meleeData.attackRange;

        Gizmos.DrawRay(origin.position, leftDir);
        Gizmos.DrawRay(origin.position, rightDir);
        Gizmos.DrawLine(origin.position + leftDir, origin.position + rightDir);
    }
#endif
}
