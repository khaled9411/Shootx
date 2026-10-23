using System;
using DG.Tweening;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Networked ammunition pickup object.
/// Grants 1 bullet according to server classification:
///   - If TotalAmmo < 3: grants 1 Basic bullet
///   - If TotalAmmo >= 3: grants 1 Extra bullet
/// 
/// Uses DOTween for idle floating and rotation.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Collider))]
public class AmmoPickup : NetworkBehaviour
{
    [Header("Idle Animation")]
    [SerializeField] private float hoverDistance = 0.25f;
    [SerializeField] private float hoverDuration = 1.2f;
    [SerializeField] private float rotationDuration = 4.0f;

    [Header("VFX")]
    [SerializeField] private GameObject collectionVfxPrefab;

    public event Action<AmmoPickup> OnConsumedByServer;

    private bool isConsumed;
    private Tween _hoverTween;
    private Tween _rotateTween;
    private Vector3 _basePosition;

    private void Awake()
    {
        // Ensure collider is marked as trigger
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        isConsumed = false;
        _basePosition = transform.position;

        StartIdleTweens();
    }

    public override void OnNetworkDespawn()
    {
        KillTweens();
        base.OnNetworkDespawn();
    }

    private void StartIdleTweens()
    {
        KillTweens();

        // Smooth vertical hover
        _hoverTween = transform.DOMoveY(_basePosition.y + hoverDistance, hoverDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);

        // Smooth 360 horizontal rotation
        _rotateTween = transform.DORotate(new Vector3(0f, 360f, 0f), rotationDuration, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Incremental);
    }

    private void KillTweens()
    {
        if (_hoverTween != null && _hoverTween.IsActive())
        {
            _hoverTween.Kill();
            _hoverTween = null;
        }

        if (_rotateTween != null && _rotateTween.IsActive())
        {
            _rotateTween.Kill();
            _rotateTween = null;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || isConsumed) return;

        PlayerShooting shooting = other.GetComponentInParent<PlayerShooting>();
        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();

        if (shooting == null || health == null || health.IsDead) return;

        // Atomic server-authoritative consumption
        isConsumed = true;

        // Authoritative ammo award
        shooting.CollectAmmoPickupServer();

        // Spawn visual collection effect for all clients
        PlayCollectionFeedbackClientRpc(transform.position);

        // Notify spawner
        OnConsumedByServer?.Invoke(this);

        // Despawn networked object
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }

    [ClientRpc]
    private void PlayCollectionFeedbackClientRpc(Vector3 position)
    {
        if (collectionVfxPrefab != null)
        {
            GameObject vfx = Instantiate(collectionVfxPrefab, position, Quaternion.identity);
            Destroy(vfx, 1.5f);
        }
    }

    private void OnDisable()
    {
        KillTweens();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        KillTweens();
    }
}
