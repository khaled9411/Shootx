using System.Collections;
using DG.Tweening;
using UnityEngine;

public class AmmoRefundVFX : MonoBehaviour
{
    [Header("Flight Settings")]
    [SerializeField] private float flightDuration = 0.5f;
    [SerializeField] private float popHeight = 1.5f;


    private Transform _target;

    public void Initialize(Transform target)
    {
        this._target = target;

        Vector3 randomOffset = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));
        Vector3 popTarget = transform.position + randomOffset;

        transform.DOJump(popTarget, popHeight, 1, 0.2f)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                StartCoroutine(FlyToTargetRoutine());
            });
    }

    private IEnumerator FlyToTargetRoutine()
    {
        float elapsed = 0f;
        Vector3 startPos = transform.position;

        while (elapsed < flightDuration)
        {
            if (_target == null)
            {
                Destroy(gameObject);
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = elapsed / flightDuration;

            float curve = t * t * t;

            transform.position = Vector3.Lerp(startPos, _target.position, curve);

            transform.LookAt(_target.position);

            yield return null;
        }

        Destroy(gameObject);
    }
}