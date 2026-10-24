using Unity.Netcode;
using UnityEngine;

namespace Shootx.Audio
{
    /// <summary>
    /// Player audio controller attached to the Player prefab.
    /// Manages 3D spatial sound events for footsteps, gunshots, melee, dash, hurt, death, and respawn.
    /// Supports both local zero-latency owner feedback and remote 3D spatialized replication
    /// without sending redundant network RPCs for continuous movement/footsteps.
    /// </summary>
    public class PlayerAudioController : NetworkBehaviour
    {
        [Header("Footstep Cadence Configuration")]
        [Tooltip("Minimum horizontal velocity (m/s) required to consider the character moving.")]
        [SerializeField] private float minVelocityThreshold = 0.5f;

        [Tooltip("Base distance in meters traveled between successive footstep sounds.")]
        [SerializeField] private float stepDistanceInterval = 1.8f;

        [Tooltip("Minimum time (seconds) between successive footstep sounds to prevent rapid machine-gun steps.")]
        [SerializeField] private float minStepTimeInterval = 0.28f;

        [Header("Transforms & Offsets")]
        [Tooltip("Custom muzzle point transform. If null, queries PlayerShooting or PlayerVisualManager.")]
        [SerializeField] private Transform muzzleTransform;

        // ─── Component Dependencies ─────────────────────────────────
        private CharacterController _characterController;
        private PlayerController _playerController;
        private PlayerHealth _playerHealth;
        private PlayerDash _playerDash;
        private PlayerShooting _playerShooting;
        private PlayerMelee _playerMelee;
        private PlayerVisualManager _visualManager;

        // ─── Footstep Cadence Tracking ──────────────────────────────
        private Vector3 _lastTrackedPosition;
        private float _accumulatedStepDistance = 0f;
        private float _lastStepTime = -999f;
        private bool _isInitialized = false;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _playerController = GetComponent<PlayerController>();
            _playerHealth = GetComponent<PlayerHealth>();
            _playerDash = GetComponent<PlayerDash>();
            _playerShooting = GetComponent<PlayerShooting>();
            _playerMelee = GetComponent<PlayerMelee>();
            _visualManager = GetComponent<PlayerVisualManager>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _lastTrackedPosition = transform.position;
            _accumulatedStepDistance = 0f;
            _lastStepTime = Time.time;
            _isInitialized = true;

            // Subscribe to Health events (all clients receive these)
            if (_playerHealth != null)
            {
                _playerHealth.OnDamageTaken += HandleDamageTaken;
                _playerHealth.OnDeath += HandleDeath;
                _playerHealth.OnRespawn += HandleRespawn;
            }

            // Subscribe to Dash events
            if (_playerDash != null)
            {
                _playerDash.OnDashStarted += HandleOwnerDashStarted;
            }

            // Subscribe to Melee events
            if (_playerMelee != null)
            {
                _playerMelee.OnMeleeSwingStarted += HandleOwnerMeleeSwing;
                _playerMelee.OnMeleeHitConfirmed += HandleMeleeHitConfirmed;
            }

            // Subscribe to Shooting events (for owner)
            if (_playerShooting != null && IsOwner)
            {
                _playerShooting.OnBasicAmmoRefunded += HandleAmmoRefunded;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (_playerHealth != null)
            {
                _playerHealth.OnDamageTaken -= HandleDamageTaken;
                _playerHealth.OnDeath -= HandleDeath;
                _playerHealth.OnRespawn -= HandleRespawn;
            }

            if (_playerDash != null)
            {
                _playerDash.OnDashStarted -= HandleOwnerDashStarted;
            }

            if (_playerMelee != null)
            {
                _playerMelee.OnMeleeSwingStarted -= HandleOwnerMeleeSwing;
                _playerMelee.OnMeleeHitConfirmed -= HandleMeleeHitConfirmed;
            }

            if (_playerShooting != null && IsOwner)
            {
                _playerShooting.OnBasicAmmoRefunded -= HandleAmmoRefunded;
            }

            _isInitialized = false;
            base.OnNetworkDespawn();
        }

        // ═══════════════════════════════════════════════════════════
        // Footstep Cadence Update (Both Local and Remote)
        // ═══════════════════════════════════════════════════════════

        private void Update()
        {
            if (!_isInitialized) return;

            UpdateFootsteps();
        }

        private void UpdateFootsteps()
        {
            // Do not play footsteps while dead, dashing, or airborne
            if (_playerHealth != null && _playerHealth.IsDead) return;
            if (_playerDash != null && _playerDash.IsDashing) return;

            // Check ground state
            bool isGrounded = true;
            if (_characterController != null && _characterController.enabled)
            {
                isGrounded = _characterController.isGrounded;
            }
            if (!isGrounded) return;

            // Calculate horizontal movement delta
            Vector3 currentPos = transform.position;
            Vector3 delta = currentPos - _lastTrackedPosition;
            delta.y = 0f;
            _lastTrackedPosition = currentPos;

            float distanceThisFrame = delta.magnitude;
            float speed = distanceThisFrame / Mathf.Max(Time.deltaTime, 0.0001f);

            if (speed < minVelocityThreshold)
            {
                // Decelerate accumulated distance when stopping to prevent immediate step on resume
                _accumulatedStepDistance = Mathf.MoveTowards(_accumulatedStepDistance, 0f, Time.deltaTime * 3f);
                return;
            }

            _accumulatedStepDistance += distanceThisFrame;

            if (_accumulatedStepDistance >= stepDistanceInterval && Time.time - _lastStepTime >= minStepTimeInterval)
            {
                _accumulatedStepDistance = 0f;
                _lastStepTime = Time.time;
                PlayFootstep();
            }
        }

        private void PlayFootstep()
        {
            Vector3 footPos = transform.position;
            if (IsOwner)
            {
                // Local player: hybrid 3D at foot position
                AudioManager.Instance?.PlaySound3D(SoundId.Movement_Footstep, footPos, volumeMultiplier: 0.85f);
            }
            else
            {
                // Remote player: purely spatial 3D
                AudioManager.Instance?.PlaySound3D(SoundId.Movement_Footstep, footPos, volumeMultiplier: 0.75f);
            }
        }

        // ═══════════════════════════════════════════════════════════
        // Weapon & Shooting Audio
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Plays the gunshot audio at the active weapon muzzle.
        /// Called locally by owner when initiating predictive shot, or on remote clients via animation/event.
        /// </summary>
        public void PlayGunshotAudio(bool isLocalOwner)
        {
            Vector3 firePos = GetMuzzlePosition();
            float vol = isLocalOwner ? 1.0f : 0.85f;

            AudioManager.Instance?.PlaySound3D(SoundId.Weapon_Revolver_Shoot, firePos, volumeMultiplier: vol);
        }

        /// <summary>
        /// Plays empty ammunition dry-fire feedback for the local player.
        /// </summary>
        public void PlayDryFireAudio()
        {
            Vector3 firePos = GetMuzzlePosition();
            AudioManager.Instance?.PlaySound3D(SoundId.Weapon_DryFire, firePos, volumeMultiplier: 0.9f);
        }

        private void HandleAmmoRefunded()
        {
            AudioManager.Instance?.PlaySound2D(SoundId.Weapon_BasicAmmoRefunded, volumeMultiplier: 0.8f);
        }

        private Vector3 GetMuzzlePosition()
        {
            if (muzzleTransform != null) return muzzleTransform.position;

            if (_visualManager != null && _visualManager.CurrentHandSocket != null)
            {
                return _visualManager.CurrentHandSocket.position;
            }

            return transform.position + Vector3.up * 1f + transform.forward * 0.5f;
        }

        public void SetMuzzleTransform(Transform t)
        {
            muzzleTransform = t;
        }

        // ═══════════════════════════════════════════════════════════
        // Melee Audio
        // ═══════════════════════════════════════════════════════════

        private void HandleOwnerMeleeSwing()
        {
            PlayMeleeSwingAudio(isLocalOwner: true);
        }

        public void PlayMeleeSwingAudio(bool isLocalOwner)
        {
            AudioManager.Instance?.PlaySound3D(SoundId.Melee_Swing, transform.position, volumeMultiplier: isLocalOwner ? 1f : 0.85f);
        }

        private void HandleMeleeHitConfirmed(Vector3 hitPoint)
        {
            AudioManager.Instance?.PlaySound3D(SoundId.Melee_Hit_Player, hitPoint, volumeMultiplier: 1f);
        }

        // ═══════════════════════════════════════════════════════════
        // Dash Audio
        // ═══════════════════════════════════════════════════════════

        private void HandleOwnerDashStarted()
        {
            PlayDashWhooshAudio(isLocalOwner: true);
        }

        public void PlayDashWhooshAudio(bool isLocalOwner)
        {
            AudioManager.Instance?.PlaySound3D(SoundId.Dash_Whoosh, transform.position, volumeMultiplier: isLocalOwner ? 1f : 0.85f);
        }

        public void PlayDashImpactAudio(Vector3 contactPoint)
        {
            AudioManager.Instance?.PlaySound3D(SoundId.Dash_Impact, contactPoint, volumeMultiplier: 0.9f);
        }

        // ═══════════════════════════════════════════════════════════
        // Health, Damage, Death, Respawn Audio
        // ═══════════════════════════════════════════════════════════

        private void HandleDamageTaken(float damage, ulong attackerClientId)
        {
            if (_playerHealth != null && _playerHealth.IsDead) return;

            if (IsOwner)
            {
                AudioManager.Instance?.PlaySound2D(SoundId.Player_Hurt, volumeMultiplier: 0.95f);
            }
            else
            {
                AudioManager.Instance?.PlaySound3D(SoundId.Player_Hurt, transform.position, volumeMultiplier: 0.85f);
            }
        }

        private void HandleDeath(ulong killerClientId)
        {
            // Reset accumulated footsteps immediately
            _accumulatedStepDistance = 0f;

            if (IsOwner)
            {
                AudioManager.Instance?.PlaySound2D(SoundId.Player_Death, volumeMultiplier: 1.0f);
            }
            else
            {
                AudioManager.Instance?.PlaySound3D(SoundId.Player_Death, transform.position, volumeMultiplier: 0.95f);
            }
        }

        private void HandleRespawn()
        {
            _accumulatedStepDistance = 0f;
            _lastTrackedPosition = transform.position;

            AudioManager.Instance?.PlaySound3D(SoundId.Player_Respawn, transform.position, volumeMultiplier: 1.0f);
        }

        public void PlaySpawnProtectionStarted()
        {
            AudioManager.Instance?.PlaySound3D(SoundId.Player_SpawnProtection_Start, transform.position, volumeMultiplier: 0.9f);
        }

        public void PlaySpawnProtectionEnded()
        {
            AudioManager.Instance?.PlaySound3D(SoundId.Player_SpawnProtection_End, transform.position, volumeMultiplier: 0.85f);
        }
    }
}
