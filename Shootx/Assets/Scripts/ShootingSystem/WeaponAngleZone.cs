using UnityEngine;

public class WeaponAngleZone : MonoBehaviour
{
    [Header("Zone Settings")]
    [SerializeField] private float targetWeaponAngle = 0f;
    [SerializeField] private int priority = 0;
    [SerializeField] private bool showGizmo = true;
    [SerializeField] private Color gizmoColor = new Color(1f, 0.5f, 0f, 0.3f);

    private BoxCollider zoneCollider;

    public float TargetAngle => targetWeaponAngle;
    public int Priority => priority;

    void Awake()
    {
        zoneCollider = GetComponent<BoxCollider>();
        if (zoneCollider == null)
        {
            zoneCollider = gameObject.AddComponent<BoxCollider>();
        }
        zoneCollider.isTrigger = true;
    }

    public float GetVisibilityScore(Camera cam)
    {
        if (cam == null || zoneCollider == null) return 0f;

        Vector3 viewportPoint = cam.WorldToViewportPoint(transform.position);

        if (viewportPoint.x < 0 || viewportPoint.x > 1 ||
            viewportPoint.y < 0 || viewportPoint.y > 1 ||
            viewportPoint.z < 0)
        {
            return 0f;
        }

        float centerDistance = Vector2.Distance(
            new Vector2(viewportPoint.x, viewportPoint.y),
            new Vector2(0.5f, 0.5f)
        );

        float visibilityScore = 1f - Mathf.Clamp01(centerDistance * 2f);

        return visibilityScore;
    }

    void OnDrawGizmos()
    {
        if (!showGizmo) return;

        Gizmos.color = gizmoColor;

        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(col.center, col.size);
            Gizmos.DrawWireCube(col.center, col.size);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 center = transform.position;
        Vector3 direction = Quaternion.Euler(targetWeaponAngle, 0, 0) * Vector3.forward;
        Gizmos.DrawRay(center, direction * 3f);
        Gizmos.DrawSphere(center + direction * 3f, 0.2f);
    }
}