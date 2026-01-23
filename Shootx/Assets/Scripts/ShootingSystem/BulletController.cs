using UnityEngine;
using DG.Tweening;

public class BulletController : MonoBehaviour
{
    private Vector3 direction;
    private float speed;
    private int bouncesLeft;
    private float currentPower = 1f;
    private float bounceDecay;
    private float maxBounceAngle;
    private LayerMask shootableLayers;
    private LayerMask penetrableLayers;
    private float maxDistance;

    public void Initialize(Vector3 dir, float spd, int bounces, float decay, float maxAngle,
                          LayerMask shootable, LayerMask penetrable, float maxDist)
    {
        direction = dir.normalized;
        speed = spd;
        bouncesLeft = bounces;
        bounceDecay = decay;
        maxBounceAngle = maxAngle;
        shootableLayers = shootable;
        penetrableLayers = penetrable;
        maxDistance = maxDist;
    }

    void Update()
    {
        MoveBullet();
    }

    void MoveBullet()
    {
        float distance = speed * Time.deltaTime;
        RaycastHit hit;

        if (Physics.Raycast(transform.position, direction, out hit, distance, shootableLayers))
        {
            transform.position = hit.point;

            if (IsPenetrable(hit.collider.gameObject))
            {
                transform.position += direction * 0.1f;
                return;
            }

            float angle = Vector3.Angle(-direction, hit.normal);

            if (angle <= maxBounceAngle && bouncesLeft > 0)
            {
                direction = Vector3.Reflect(direction, hit.normal);
                bouncesLeft--;
                currentPower *= bounceDecay;
                speed *= bounceDecay;

                transform.DOPunchScale(Vector3.one * 0.3f, 0.1f, 1);
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
            Destroy(gameObject);
        }
    }

    bool IsPenetrable(GameObject obj)
    {
        return ((1 << obj.layer) & penetrableLayers) != 0;
    }

    void OnBulletStopped(RaycastHit hit)
    {
        Debug.Log($"The bullet hit: {hit.collider.name}");

        transform.DOScale(0, 0.2f).OnComplete(() => Destroy(gameObject));
    }
}