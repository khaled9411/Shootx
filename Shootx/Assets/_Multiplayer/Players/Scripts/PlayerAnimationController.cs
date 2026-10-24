using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Centralized multiplayer animation controller for third-person player characters.
/// 
/// System Features:
///   1. Drives animations via explicit state names (e.g. "Base Layer.Idle", "Base Layer.Running",
///      "Aimming.Aiming", "Aimming.Shoot", "Aimming.Empty").
///   2. Base Layer locomotion: reflects actual movement velocity and relative direction.
///      When moving while aiming, calculates local-space direction and feeds MoveX / MoveZ into
///      the directional locomotion Blend Tree (Forward, Backward, Left Strafe, Right Strafe).
///   3. Upper-body Aimming layer: uses Upper.mask (upper body only, lower body excluded).
///      Smoothly fades layer weight (0 -> 1 on Aiming/Shoot, 1 -> 0 when released).
///   4. Shooting transition:
///      - Plays "Aimming.Shoot" as a snappy one-shot action on the upper layer.
///      - Returns to "Aimming.Aiming" if aiming is still held.
///      - Fades upper layer to 0 and returns to base locomotion if aiming was released.
///   5. Multiplayer Synchronization:
///      - Replicates aiming state via NetworkVariable<bool>.
///      - Replicates shooting via server-confirmed ClientRpc (owner predicted, remote clients synchronized).
///      - Locomotion direction derived smoothly from replicated transform velocity on remote clients.
///   6. Lifecycle safety: Resets cleanly on Death and Respawn; never invokes Reload.
/// </summary>
public class PlayerAnimationController : NetworkBehaviour
{
    public enum UpperBodyState
    {
        Inactive,
        Aiming,
        Shooting,
        Melee
    }

    [Header("Input Dependency")]
    [SerializeField] private InputReader inputReader;

    [Header("Animator State Names")]
    [SerializeField] private string baseIdleState = "Base Layer.Idle";
    [SerializeField] private string baseRunningState = "Base Layer.Running";
    [SerializeField] private string upperEmptyState = "Aimming.Empty";
    [SerializeField] private string upperAimingState = "Aimming.Aiming";
    [SerializeField] private string upperShootState = "Aimming.Shoot";

    [Header("Animator Layer Names")]
    [Tooltip("Exact name of the upper-body aiming layer as defined in MultiplayerPlayer.controller.")]
    [SerializeField] private string upperLayerName = "Aimming";

    [Header("Blend & Transition Durations")]
    [SerializeField] private float locomotionCrossFadeDuration = 0.15f;
    [SerializeField] private float aimWeightFadeDuration = 0.15f;
    [SerializeField] private float shootCrossFadeDuration = 0.05f;
    [SerializeField] private float returnToAimFadeDuration = 0.12f;

    [Header("Movement & Velocity Thresholds")]
    [SerializeField] private float minMoveVelocityThreshold = 0.1f;
    [SerializeField] private float referenceMoveSpeed = 5f;
    [SerializeField] private float directionalDampingSpeed = 12f;

    // ─── Networked Aim State ─────────────────────────────────────
    /// <summary>
    /// Replicated aiming state. The owning player writes their local aiming status;
    /// all other clients read it to raise the upper layer and pose the character in Aiming.
    /// </summary>
    private readonly NetworkVariable<bool> isAimingNetwork = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    // ─── Cached Hashes & Indices ────────────────────────────────
    private int baseIdleHash;
    private int baseRunningHash;
    private int upperEmptyHash;
    private int upperAimingHash;
    private int upperShootHash;
    private int upperShootShortHash;
    private int moveXParamHash;
    private int moveZParamHash;

    private int baseLayerIndex = 0;
    private int upperLayerIndex = 1;

    // ─── Runtime Animation State ────────────────────────────────
    private Animator activeAnimator;
    private UpperBodyState currentUpperState = UpperBodyState.Inactive;
    private bool isCurrentlyMoving = false;

    private float currentUpperWeight = 0f;
    private float targetUpperWeight = 0f;

    private float currentMoveX = 0f;
    private float currentMoveZ = 0f;

    private float shootStartTime = -1f;
    private float shootClipDuration = 1.0f;

    private float meleeStartTime = -1f;
    private float meleeClipDuration = 0.5f;

    private Vector3 lastWorldPosition;
    private Vector3 smoothedVelocity;

    // Component References
    private PlayerController playerController;
    private CharacterController characterController;
    private PlayerHealth playerHealth;
    private PlayerDash playerDash;
    private PlayerMelee playerMelee;

    public UpperBodyState CurrentUpperState => currentUpperState;
    public bool IsAimingActive => isAimingNetwork.Value;
    public float UpperLayerWeight => currentUpperWeight;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        characterController = GetComponent<CharacterController>();
        playerHealth = GetComponent<PlayerHealth>();
        playerDash = GetComponent<PlayerDash>();
        playerMelee = GetComponent<PlayerMelee>();

        if (inputReader == null && playerController != null)
        {
            var field = typeof(PlayerController).GetField("inputReader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) inputReader = field.GetValue(playerController) as InputReader;
        }

        CacheStateHashes();
    }

    private void CacheStateHashes()
    {
        baseIdleHash = Animator.StringToHash(baseIdleState);
        baseRunningHash = Animator.StringToHash(baseRunningState);
        upperEmptyHash = Animator.StringToHash(upperEmptyState);
        upperAimingHash = Animator.StringToHash(upperAimingState);
        upperShootHash = Animator.StringToHash(upperShootState);
        upperShootShortHash = Animator.StringToHash("Shoot");
        moveXParamHash = Animator.StringToHash("MoveX");
        moveZParamHash = Animator.StringToHash("MoveZ");
    }

    /// <summary>
    /// Binds the active character's Animator component (called by PlayerVisualManager).
    /// </summary>
    public void BindAnimator(Animator animator)
    {
        if (animator == null) return;

        activeAnimator = animator;
        upperLayerIndex = activeAnimator.GetLayerIndex(upperLayerName);
        if (upperLayerIndex < 0)
        {
            Debug.LogWarning($"[PlayerAnimationController] Layer '{upperLayerName}' not found in controller. Falling back to layer index 1.");
            upperLayerIndex = 1;
        }

        // Initialize state
        activeAnimator.Play(baseIdleHash, baseLayerIndex, 0f);
        activeAnimator.Play(upperEmptyHash, upperLayerIndex, 0f);
        activeAnimator.SetLayerWeight(upperLayerIndex, 0f);
        currentUpperWeight = 0f;
        targetUpperWeight = 0f;
        currentUpperState = UpperBodyState.Inactive;

        // Query actual Shoot clip duration if available
        shootClipDuration = QueryClipDuration("Ranged_1H_Shoot", 1.0667f);
    }

    private float QueryClipDuration(string clipNameSubstring, float fallback)
    {
        if (activeAnimator == null || activeAnimator.runtimeAnimatorController == null) return fallback;

        foreach (var clip in activeAnimator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && clip.name.IndexOf(clipNameSubstring, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return clip.length;
            }
        }
        return fallback;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        lastWorldPosition = transform.position;

        if (playerHealth != null)
        {
            playerHealth.OnDeath += HandleDeath;
            playerHealth.OnRespawn += HandleRespawn;
        }

        if (playerMelee != null)
        {
            playerMelee.OnMeleeSwingStarted += HandleLocalMeleeSwing;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (playerHealth != null)
        {
            playerHealth.OnDeath -= HandleDeath;
            playerHealth.OnRespawn -= HandleRespawn;
        }

        if (playerMelee != null)
        {
            playerMelee.OnMeleeSwingStarted -= HandleLocalMeleeSwing;
        }

        base.OnNetworkDespawn();
    }

    private void Update()
    {
        if (activeAnimator == null || !activeAnimator.enabled) return;

        // Death Guard: suppress movement and aiming transitions while dead
        if (playerHealth != null && playerHealth.IsDead)
        {
            UpdateDeathState();
            return;
        }

        // 1. Aim input evaluation (Owner sets replicated NetworkVariable)
        UpdateAimInput();

        // 2. Base locomotion state & directional blend
        UpdateLocomotion();

        // 3. Upper body aiming, shooting, and layer weight interpolation
        UpdateUpperBody();
    }

    // ─── 1. Aiming Evaluation ───────────────────────────────────

    private void UpdateAimInput()
    {
        if (!IsOwner) return;

        bool aiming = EvaluateAimInput();

        // Dash and input locks cancel aiming
        if (playerDash != null && playerDash.IsDashing) aiming = false;
        if (playerController != null && playerController.IsInputLocked) aiming = false;

        if (aiming != isAimingNetwork.Value)
        {
            isAimingNetwork.Value = aiming;
        }
    }

    private bool EvaluateAimInput()
    {
        // A. Mobile Aim: pulling/arming the AimFireJoystick
        if (inputReader != null && (inputReader.IsMobileAiming || inputReader.IsMobileArmed))
        {
            return true;
        }

        // B. Desktop Aim: Right Mouse Button or Gamepad Left Trigger
        if (Mouse.current != null && Mouse.current.rightButton.isPressed)
        {
            return true;
        }

        if (Gamepad.current != null && Gamepad.current.leftTrigger.isPressed)
        {
            return true;
        }

        return false;
    }

    // ─── 2. Locomotion & Directional Movement ───────────────────

    private void UpdateLocomotion()
    {
        Vector3 worldVelocity;

        if (IsOwner)
        {
            if (characterController != null && characterController.enabled)
            {
                worldVelocity = characterController.velocity;
                worldVelocity.y = 0f;
            }
            else if (playerController != null)
            {
                worldVelocity = playerController.CurrentMoveDirection * referenceMoveSpeed;
            }
            else
            {
                worldVelocity = Vector3.zero;
            }
        }
        else
        {
            // Derive smooth velocity from replicated NetworkTransform position delta
            Vector3 positionDelta = transform.position - lastWorldPosition;
            positionDelta.y = 0f;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 rawVelocity = positionDelta / dt;
            smoothedVelocity = Vector3.Lerp(smoothedVelocity, rawVelocity, Time.deltaTime * 15f);
            worldVelocity = smoothedVelocity;
        }
        lastWorldPosition = transform.position;

        float speed = worldVelocity.magnitude;
        bool shouldMove = speed > minMoveVelocityThreshold;

        // Calculate directional movement relative to the character's facing orientation
        Vector3 localDir = transform.InverseTransformDirection(worldVelocity);
        float targetX = 0f;
        float targetZ = 0f;

        if (shouldMove)
        {
            float maxSpd = Mathf.Max(referenceMoveSpeed, 1f);
            targetX = Mathf.Clamp(localDir.x / maxSpd, -1f, 1f);
            targetZ = Mathf.Clamp(localDir.z / maxSpd, -1f, 1f);
        }

        currentMoveX = Mathf.Lerp(currentMoveX, targetX, Time.deltaTime * directionalDampingSpeed);
        currentMoveZ = Mathf.Lerp(currentMoveZ, targetZ, Time.deltaTime * directionalDampingSpeed);

        activeAnimator.SetFloat(moveXParamHash, currentMoveX);
        activeAnimator.SetFloat(moveZParamHash, currentMoveZ);

        // Transition between Idle and Running states
        if (shouldMove && !isCurrentlyMoving)
        {
            isCurrentlyMoving = true;
            activeAnimator.CrossFadeInFixedTime(baseRunningHash, locomotionCrossFadeDuration, baseLayerIndex);
        }
        else if (!shouldMove && isCurrentlyMoving)
        {
            isCurrentlyMoving = false;
            activeAnimator.CrossFadeInFixedTime(baseIdleHash, locomotionCrossFadeDuration, baseLayerIndex);
        }
    }

    // ─── 3. Upper-Body Aiming & Shooting ────────────────────────

    private void UpdateUpperBody()
    {
        bool isAiming = isAimingNetwork.Value;

        switch (currentUpperState)
        {
            case UpperBodyState.Inactive:
                targetUpperWeight = 0f;
                if (isAiming)
                {
                    currentUpperState = UpperBodyState.Aiming;
                    targetUpperWeight = 1f;
                    activeAnimator.CrossFadeInFixedTime(upperAimingHash, aimWeightFadeDuration, upperLayerIndex);
                }
                break;

            case UpperBodyState.Aiming:
                targetUpperWeight = 1f;
                if (!isAiming)
                {
                    currentUpperState = UpperBodyState.Inactive;
                    targetUpperWeight = 0f;
                    activeAnimator.CrossFadeInFixedTime(upperEmptyHash, aimWeightFadeDuration, upperLayerIndex);
                }
                break;

            case UpperBodyState.Shooting:
                targetUpperWeight = 1f;
                CheckShootAnimationCompletion(isAiming);
                break;

            case UpperBodyState.Melee:
                targetUpperWeight = 1f;
                if (Time.time - meleeStartTime >= meleeClipDuration)
                {
                    EndMeleeState(isAiming);
                }
                break;
        }

        // Smoothly interpolate upper-body layer weight
        if (!Mathf.Approximately(currentUpperWeight, targetUpperWeight))
        {
            currentUpperWeight = Mathf.MoveTowards(
                currentUpperWeight,
                targetUpperWeight,
                Time.deltaTime / Mathf.Max(aimWeightFadeDuration, 0.01f)
            );
            activeAnimator.SetLayerWeight(upperLayerIndex, currentUpperWeight);
        }
    }

    private void CheckShootAnimationCompletion(bool stillAiming)
    {
        if (shootStartTime < 0f) return;

        AnimatorStateInfo stateInfo = activeAnimator.GetCurrentAnimatorStateInfo(upperLayerIndex);
        bool inShootState = (stateInfo.shortNameHash == upperShootShortHash || stateInfo.fullPathHash == upperShootHash);

        // Completion check: state normalized time >= 0.90 or safety timeout
        bool clipFinished = inShootState && stateInfo.normalizedTime >= 0.90f;
        bool timeout = (Time.time - shootStartTime) > (shootClipDuration * 0.95f);

        if (clipFinished || timeout)
        {
            EndShootingState(stillAiming);
        }
    }

    private void EndShootingState(bool stillAiming)
    {
        shootStartTime = -1f;

        if (stillAiming)
        {
            // Case A: Player is still aiming -> return smoothly to Aiming
            currentUpperState = UpperBodyState.Aiming;
            targetUpperWeight = 1f;
            activeAnimator.CrossFadeInFixedTime(upperAimingHash, returnToAimFadeDuration, upperLayerIndex);
        }
        else
        {
            // Case B: Player released aim -> fade upper layer to 0 and reveal base locomotion
            currentUpperState = UpperBodyState.Inactive;
            targetUpperWeight = 0f;
            activeAnimator.CrossFadeInFixedTime(upperEmptyHash, aimWeightFadeDuration, upperLayerIndex);
        }
    }

    // ─── 4. Shooting Triggers & Synchronization ─────────────────

    /// <summary>
    /// Called locally on the owner immediately when an authoritative shot request is initiated (zero-latency prediction).
    /// </summary>
    public void TriggerLocalShoot()
    {
        if (!IsOwner) return;
        PlayShootAnimationInternal();
    }

    /// <summary>
    /// Broadcast by the server when a shot is accepted. Runs on all clients.
    /// Non-owners execute the shoot animation; owner skips to prevent duplicate playback.
    /// </summary>
    [ClientRpc]
    public void PlayShootClientRpc()
    {
        if (IsOwner) return; // Owner already played predicted shoot animation!
        PlayShootAnimationInternal();
        GetComponent<Shootx.Audio.PlayerAudioController>()?.PlayGunshotAudio(isLocalOwner: false);
    }

    private void PlayShootAnimationInternal()
    {
        if (activeAnimator == null || !activeAnimator.enabled) return;
        if (playerHealth != null && playerHealth.IsDead) return;

        currentUpperState = UpperBodyState.Shooting;
        shootStartTime = Time.time;

        // Force upper layer weight to full immediately for instant shot reaction
        targetUpperWeight = 1f;
        currentUpperWeight = 1f;
        activeAnimator.SetLayerWeight(upperLayerIndex, 1f);

        activeAnimator.Play(upperShootHash, upperLayerIndex, 0f);
    }

    // ─── 5. Melee Integration ───────────────────────────────────

    private void HandleLocalMeleeSwing()
    {
        if (!IsOwner) return;
        PlayMeleeAnimationInternal();
    }

    /// <summary>
    /// Called when melee visual is triggered on remote clients.
    /// </summary>
    public void TriggerRemoteMelee()
    {
        if (IsOwner) return;
        PlayMeleeAnimationInternal();
    }

    private void PlayMeleeAnimationInternal()
    {
        if (activeAnimator == null || !activeAnimator.enabled) return;
        if (playerHealth != null && playerHealth.IsDead) return;

        currentUpperState = UpperBodyState.Melee;
        meleeStartTime = Time.time;
        targetUpperWeight = 1f;
        currentUpperWeight = 1f;
        activeAnimator.SetLayerWeight(upperLayerIndex, 1f);

        // If Melee punch exists in Upper Layer, play it; otherwise play Shoot or quick upper action
        activeAnimator.Play(upperShootHash, upperLayerIndex, 0f);
    }

    private void EndMeleeState(bool stillAiming)
    {
        meleeStartTime = -1f;
        if (stillAiming)
        {
            currentUpperState = UpperBodyState.Aiming;
            targetUpperWeight = 1f;
            activeAnimator.CrossFadeInFixedTime(upperAimingHash, returnToAimFadeDuration, upperLayerIndex);
        }
        else
        {
            currentUpperState = UpperBodyState.Inactive;
            targetUpperWeight = 0f;
            activeAnimator.CrossFadeInFixedTime(upperEmptyHash, aimWeightFadeDuration, upperLayerIndex);
        }
    }

    // ─── 6. Death and Respawn Lifecycle ─────────────────────────

    private void HandleDeath(ulong killerId)
    {
        currentUpperState = UpperBodyState.Inactive;
        targetUpperWeight = 0f;
        currentUpperWeight = 0f;
        shootStartTime = -1f;
        meleeStartTime = -1f;
        isCurrentlyMoving = false;
        currentMoveX = 0f;
        currentMoveZ = 0f;

        if (IsOwner)
        {
            isAimingNetwork.Value = false;
        }

        if (activeAnimator != null && activeAnimator.enabled)
        {
            activeAnimator.SetLayerWeight(upperLayerIndex, 0f);
            activeAnimator.Play(upperEmptyHash, upperLayerIndex, 0f);
            activeAnimator.SetFloat(moveXParamHash, 0f);
            activeAnimator.SetFloat(moveZParamHash, 0f);
        }
    }

    private void HandleRespawn()
    {
        currentUpperState = UpperBodyState.Inactive;
        targetUpperWeight = 0f;
        currentUpperWeight = 0f;
        shootStartTime = -1f;
        meleeStartTime = -1f;
        isCurrentlyMoving = false;
        currentMoveX = 0f;
        currentMoveZ = 0f;

        if (IsOwner)
        {
            isAimingNetwork.Value = false;
        }

        if (activeAnimator != null && activeAnimator.enabled)
        {
            activeAnimator.SetLayerWeight(upperLayerIndex, 0f);
            activeAnimator.Play(baseIdleHash, baseLayerIndex, 0f);
            activeAnimator.Play(upperEmptyHash, upperLayerIndex, 0f);
            activeAnimator.SetFloat(moveXParamHash, 0f);
            activeAnimator.SetFloat(moveZParamHash, 0f);
        }
    }

    private void UpdateDeathState()
    {
        if (activeAnimator != null && activeAnimator.enabled)
        {
            if (currentUpperWeight > 0f)
            {
                currentUpperWeight = 0f;
                activeAnimator.SetLayerWeight(upperLayerIndex, 0f);
            }
        }
    }
}
