using UnityEngine;
using DG.Tweening;
using Ricimi;

public class BulletController : MonoBehaviour
{
    [SerializeField] private AudioClip bulletHit;
    [SerializeField] private GameObject hitEffect;

    private Vector3 direction;
    private float speed;
    private int bouncesLeft;
    private float currentPower = 120f;
    private float bounceDecay;
    private float maxBounceAngle;

    private LayerMask collisionLayers;
    private LayerMask shootableLayers;
    private LayerMask penetrableLayers;
    private float maxDistance;

    private bool causedFreeze = false;

    public void Initialize(Vector3 dir, float spd, int bounces, float decay, float maxAngle,
                           LayerMask collision, LayerMask shootable, LayerMask penetrable, float maxDist,
                           bool freezeOnFlight = false)
    {
        direction = dir.normalized;
        speed = spd;
        bouncesLeft = bounces;
        bounceDecay = decay;
        maxBounceAngle = maxAngle;

        collisionLayers = collision;
        shootableLayers = shootable;
        penetrableLayers = penetrable;
        maxDistance = maxDist;
        causedFreeze = freezeOnFlight;
    }

    void Update()
    {
        MoveBullet();
    }

    void MoveBullet()
    {
        float distance = speed * Time.unscaledDeltaTime;

        if (Physics.Raycast(transform.position, direction, out RaycastHit hit, distance, collisionLayers))
        {
            transform.position = hit.point;

            if (hitEffect != null)
                Instantiate(hitEffect, hit.point, Quaternion.LookRotation(hit.normal));

            if (AudioManager.Instance != null && bulletHit != null)
                AudioManager.Instance.PlaySFX(bulletHit);

            if (hit.collider.TryGetComponent(out IDamageable damageable))
            {
                damageable.TakeDamage(currentPower, direction);
            }

            if (IsPenetrable(hit.collider.gameObject))
            {
                transform.position += direction * 0.1f;
                return;
            }

            if (!IsShootableForBounce(hit.collider.gameObject))
            {
                OnBulletStopped(hit);
                return;
            }

            float angle = Vector3.Angle(-direction, hit.normal);
            if (angle <= maxBounceAngle && bouncesLeft > 0)
            {
                direction = Vector3.Reflect(direction, hit.normal);
                bouncesLeft--;
                currentPower *= bounceDecay;
                speed *= bounceDecay;
                transform.DOPunchScale(Vector3.one * 0.3f, 0.1f, 1).SetUpdate(true);
            }
            else
            {
                OnBulletStopped(hit);
            }
        }
        else
        {
            transform.position += direction * distance;
        }

        if (Vector3.Distance(transform.position, Vector3.zero) > maxDistance * 2)
        {
            ReleaseFreezeIfNeeded();
            Destroy(gameObject);
        }
    }

    bool IsPenetrable(GameObject obj) => ((1 << obj.layer) & penetrableLayers) != 0;

    bool IsShootableForBounce(GameObject obj) => ((1 << obj.layer) & shootableLayers) != 0;

    void OnBulletStopped(RaycastHit hit)
    {
        Debug.Log($"The bullet hit: {hit.collider.name}");

        ReleaseFreezeIfNeeded();

        transform.DOScale(0, 0.2f).SetUpdate(true).OnComplete(() => Destroy(gameObject));
    }

    void ReleaseFreezeIfNeeded()
    {
        if (causedFreeze)
        {
            causedFreeze = false;
            BulletTimeManager.Instance?.EndBulletFreeze();
        }
    }

    void OnDestroy()
    {
        ReleaseFreezeIfNeeded();
    }
}