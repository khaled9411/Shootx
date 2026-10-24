using UnityEngine;
using Unity.Netcode;
using DG.Tweening;

/// <summary>
/// Multiplayer Destructible Crate component.
/// Authoritative destruction occurs on the Server / Host via MatchManager.
/// All clients receive the event and render a dynamic destruction visual effect.
/// Gameplay colliders are disabled instantly to terminate Raycasts and Melee hits.
/// Match-scoped lifecycle: persists destroyed for the entire match and resets only on next match.
/// </summary>
public class DestructibleBox : MonoBehaviour, INetworkDamageable
{
    [Header("Box Settings")]
    [Tooltip("Health points before destruction. Exactly 1 hit destroys the box.")]
    [SerializeField] private float maxHealth = 1f;

    [Header("VFX & Particles (Optional)")]
    [Tooltip("Optional particle prefab instantiated at the crate position upon destruction.")]
    [SerializeField] private GameObject destructionParticles;

    [Tooltip("Target transform for visual animations. If null, uses this gameObject's transform.")]
    [SerializeField] private Transform visualTransform;

    [Header("Explosion Settings")]
    [Tooltip("Radius of the physical explosion force applied to nearby rigidbodies.")]
    [SerializeField] private float explosionRadius = 5f;
    [Tooltip("Magnitude of the physical explosion force.")]
    [SerializeField] private float explosionForce = 300f;
    [Tooltip("Upwards modifier for the explosion force.")]
    [SerializeField] private float upwardModifier = 1f;

    // ─── Runtime Internal State ──────────────────────────────────────────
    private int _crateIndex = -1;
    private MatchManager _matchManager;
    private Collider _crateCollider;
    private Collider[] _childColliders;
    private Renderer _crateRenderer;

    private Vector3 _initialLocalPos;
    private Quaternion _initialLocalRot;
    private Vector3 _initialLocalScale;

    private bool _isDestroyedAuthoritative = false;
    private bool _isDestroyedVisual = false;
    private Sequence _destructionSequence;

    public int CrateIndex => _crateIndex;
    public bool IsDestroyed => _isDestroyedAuthoritative;
    public bool IsVisualDestroyed => _isDestroyedVisual;
    public float MaxHealth => maxHealth;
    public float TotalDuration => 0.25f; // Updated to match the new DOTween sequence duration

    private void Awake()
    {
        CacheComponents();
        CacheInitialTransform();
    }

    private void CacheComponents()
    {
        if (_crateCollider == null)
            _crateCollider = GetComponent<Collider>();

        if (_childColliders == null || _childColliders.Length == 0)
            _childColliders = GetComponentsInChildren<Collider>(true);

        if (_crateRenderer == null)
            _crateRenderer = GetComponent<Renderer>();
        if (_crateRenderer == null)
            _crateRenderer = GetComponentInChildren<Renderer>(true);

        if (visualTransform == null)
            visualTransform = transform;
    }

    private void CacheInitialTransform()
    {
        Transform target = visualTransform != null ? visualTransform : transform;
        _initialLocalPos = target.localPosition;
        _initialLocalRot = target.localRotation;
        _initialLocalScale = target.localScale;
    }

    /// <summary>
    /// Registers this crate with the active MatchManager and assigns a deterministic map-scoped index.
    /// </summary>
    public void Initialize(int index, MatchManager manager)
    {
        CacheComponents();
        CacheInitialTransform();

        _crateIndex = index;
        _matchManager = manager;

        _isDestroyedAuthoritative = false;
        _isDestroyedVisual = false;

        KillActiveTweens();

        // Restore colliders and visibility
        SetCollidersEnabled(true);
        if (_crateRenderer != null)
            _crateRenderer.enabled = true;

        Transform target = visualTransform != null ? visualTransform : transform;
        target.localPosition = _initialLocalPos;
        target.localRotation = _initialLocalRot;
        target.localScale = _initialLocalScale;
    }

    // ─── INetworkDamageable Implementation ────────────────────────────────

    public void TakeDamage(float damage, ulong attackerClientId)
    {
        TakeDamageWithHitInfo(damage, attackerClientId, transform.position, transform.forward);
    }

    public void TakeDamage(int damage, ulong attackerClientId)
    {
        TakeDamage((float)damage, attackerClientId);
    }

    /// <summary>
    /// Preferred entry point passing directional impact information.
    /// </summary>
    public void TakeDamageWithHitInfo(float damage, ulong attackerClientId, Vector3 hitPoint, Vector3 hitDirection)
    {
        // One hit destroys the box. Idempotency guard:
        if (_isDestroyedAuthoritative) return;

        Shootx.Audio.AudioManager.Instance?.PlaySound3D(Shootx.Audio.SoundId.Impact_DestructibleBox, hitPoint);

        // Authoritative verification: Only the server can validate and destroy
        bool isNetworked = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetworked && !NetworkManager.Singleton.IsServer)
        {
            // Clients do not locally decide authoritative destruction
            return;
        }

        // Check if MatchManager is present
        if (_matchManager != null)
        {
            _matchManager.ReportCrateHit(_crateIndex, hitPoint, hitDirection);
        }
        else
        {
            // Standalone / offline fallback testing
            _isDestroyedAuthoritative = true;
            PlayDestructionSequence(hitPoint, hitDirection);
        }
    }

    /// <summary>
    /// Called on the Server by MatchManager to confirm authoritative destruction state.
    /// </summary>
    public void SetDestroyedAuthoritative()
    {
        _isDestroyedAuthoritative = true;
    }

    /// <summary>
    /// Triggers an instant explosion effect locally on this client.
    /// Immediately disables gameplay collisions and hides the box while spawning particles and applying explosion force.
    /// </summary>
    public void PlayDestructionSequence(Vector3 hitPoint, Vector3 hitDirection)
    {
        if (_isDestroyedVisual) return;
        _isDestroyedVisual = true;
        _isDestroyedAuthoritative = true;

        // Immediately disable collision on this client so raycasts and melee pass through
        SetCollidersEnabled(false);

        KillActiveTweens();

        // Play explosion sound
        Shootx.Audio.AudioManager.Instance?.PlaySound3D(Shootx.Audio.SoundId.Box_Destroyed, transform.position);

        Transform target = visualTransform != null ? visualTransform : transform;

        // Spawn explosion particles at destruction moment
        if (destructionParticles != null)
        {
            Instantiate(destructionParticles, transform.position, Quaternion.identity);
        }

        // Apply physical explosion force to nearby objects with Rigidbody
        ApplyExplosionForce(transform.position);

        // ─── STRONG DESTRUCTION EFFECT (DOTWEEN) ───
        _destructionSequence = DOTween.Sequence();

        // 1. Violent quick bulge outward to simulate internal explosion pressure
        _destructionSequence.Append(target.DOPunchScale(Vector3.one * 0.5f, 0.1f, 10, 1f));

        // 2. Add some wild rotation shake for violent impact
        _destructionSequence.Join(target.DOShakeRotation(0.15f, new Vector3(45, 45, 45), 20, 90));

        // 3. Implode / shrink to zero violently
        _destructionSequence.Append(target.DOScale(Vector3.zero, 0.15f).SetEase(Ease.InBack));

        // 4. Hide the renderer after the dramatic effect completes
        _destructionSequence.OnComplete(() =>
        {
            if (_crateRenderer != null)
                _crateRenderer.enabled = false;
        });
    }

    /// <summary>
    /// Applies an explosion force to nearby rigidbodies for extra immersion.
    /// </summary>
    private void ApplyExplosionForce(Vector3 explosionCenter)
    {
        Collider[] colliders = Physics.OverlapSphere(explosionCenter, explosionRadius);
        foreach (Collider hit in colliders)
        {
            Rigidbody rb = hit.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                rb.AddExplosionForce(explosionForce, explosionCenter, explosionRadius, upwardModifier, ForceMode.Impulse);
            }
        }
    }

    /// <summary>
    /// For late-joining clients or instant state sync: applies destroyed state without playing full animation.
    /// </summary>
    public void ApplyImmediateDestroyedState()
    {
        _isDestroyedAuthoritative = true;
        _isDestroyedVisual = true;

        KillActiveTweens();
        SetCollidersEnabled(false);

        if (_crateRenderer != null)
            _crateRenderer.enabled = false;

        Transform target = visualTransform != null ? visualTransform : transform;
        target.localScale = Vector3.zero;
    }

    /// <summary>
    /// Restores the crate to its initial pristine state for a new match.
    /// </summary>
    public void ResetCrate()
    {
        _isDestroyedAuthoritative = false;
        _isDestroyedVisual = false;

        KillActiveTweens();

        Transform target = visualTransform != null ? visualTransform : transform;
        target.localPosition = _initialLocalPos;
        target.localRotation = _initialLocalRot;
        target.localScale = _initialLocalScale;

        SetCollidersEnabled(true);
        if (_crateRenderer != null)
            _crateRenderer.enabled = true;
    }

    private void SetCollidersEnabled(bool isEnabled)
    {
        if (_crateCollider != null)
            _crateCollider.enabled = isEnabled;

        if (_childColliders != null)
        {
            for (int i = 0; i < _childColliders.Length; i++)
            {
                if (_childColliders[i] != null)
                    _childColliders[i].enabled = isEnabled;
            }
        }
    }

    private void KillActiveTweens()
    {
        if (_destructionSequence != null && _destructionSequence.IsActive())
        {
            _destructionSequence.Kill(false);
            _destructionSequence = null;
        }

        Transform target = visualTransform != null ? visualTransform : transform;
        target.DOKill(false);
    }

    private void OnDisable()
    {
        KillActiveTune_Safe();
    }

    private void OnDestroy()
    {
        KillActiveTune_Safe();
    }

    private void KillActiveTune_Safe()
    {
        KillActiveTweens();
    }
}