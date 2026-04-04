using UnityEngine;

public class AimReticleController : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private float spinSpeed = 150f;
    [SerializeField] private float pulseSpeed = 10f;
    [SerializeField] private float pulseAmount = 0.1f;
    [SerializeField] private float surfaceOffset = 0.1f;

    private Vector3 originalScale;

    void Start()
    {
        originalScale = transform.localScale;
    }

    void Update()
    {
        transform.Rotate(Vector3.forward * spinSpeed * Time.unscaledDeltaTime);

        float scaleOffset = Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
        transform.localScale = originalScale + new Vector3(scaleOffset, scaleOffset, scaleOffset);
    }

    public void UpdatePositionAndRotation(Vector3 hitPoint, Vector3 hitNormal)
    {
        transform.position = hitPoint + (hitNormal * surfaceOffset);

        if (hitNormal != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(hitNormal);
        }
    }
}