using UnityEngine;

public class AimAssist : MonoBehaviour
{
    public static AimAssist Instance { get; private set; }

    [Header("Enemy search settings")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float detectionRadius = 15f;
    [SerializeField] private float snapAngle = 25f;

    [Header("settings Snap")]
    [SerializeField] private float snapStrength = 8f;
    [SerializeField] private string enemyAimPointTag = "AimPoint";

    [Header("settings Rotation Pull")]
    [SerializeField] private bool enableRotationPull = true;
    [SerializeField] private float rotationPullStrength = 3f;
    [SerializeField] private float rotationPullMaxAngle = 15f;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;

    private Transform currentTarget;
    private Vector3 currentTargetPoint;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public bool FindBestTarget(Transform firePoint, LayerMask shootableLayers, out Vector3 snapDirection)
    {
        snapDirection = firePoint.forward;
        currentTarget = null;

        Collider[] enemies = Physics.OverlapSphere(firePoint.position, detectionRadius, enemyLayer);

        float bestScore = float.MaxValue;
        Transform bestTarget = null;
        Vector3 bestPoint = Vector3.zero;

        foreach (Collider enemy in enemies)
        {
            Vector3 aimPoint = GetAimPoint(enemy);

            Vector3 dirToEnemy = (aimPoint - firePoint.position).normalized;
            float angle = Vector3.Angle(firePoint.forward, dirToEnemy);

            if (angle > snapAngle) continue;

            if (!HasLineOfSight(firePoint.position, aimPoint, shootableLayers, enemy)) continue;

            if (angle < bestScore)
            {
                bestScore = angle;
                bestTarget = enemy.transform;
                bestPoint = aimPoint;
            }
        }

        if (bestTarget == null) return false;

        currentTarget = bestTarget;
        currentTargetPoint = bestPoint;

        Vector3 rawDirection = (currentTargetPoint - firePoint.position).normalized;
        snapDirection = Vector3.Slerp(firePoint.forward, rawDirection, snapStrength * Time.deltaTime);

        return true;
    }

    public void ApplyRotationPull(Transform playerBody, Transform firePoint)
    {
        if (!enableRotationPull || currentTarget == null) return;

        Vector3 toEnemy = currentTarget.position - playerBody.position;
        toEnemy.y = 0;
        if (toEnemy.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(toEnemy.normalized);
        float angleDiff = Quaternion.Angle(playerBody.rotation, targetRot);

        if (angleDiff > rotationPullMaxAngle) return;

        playerBody.rotation = Quaternion.Slerp(
            playerBody.rotation,
            targetRot,
            rotationPullStrength * Time.deltaTime
        );
    }

    private Vector3 GetAimPoint(Collider enemy)
    {
        Transform aimPoint = FindChildWithTag(enemy.transform, enemyAimPointTag);
        if (aimPoint != null) return aimPoint.position;

        return enemy.bounds.center;
    }

    private Transform FindChildWithTag(Transform parent, string tag)
    {
        foreach (Transform child in parent)
        {
            if (child.CompareTag(tag)) return child;
            Transform found = FindChildWithTag(child, tag);
            if (found != null) return found;
        }
        return null;
    }

    private bool HasLineOfSight(Vector3 from, Vector3 to, LayerMask shootableLayers, Collider targetCollider)
    {
        Vector3 dir = to - from;
        float dist = dir.magnitude;

        if (!Physics.Raycast(from, dir.normalized, out RaycastHit hit, dist, shootableLayers))
            return true;

        return hit.collider == targetCollider || hit.collider.transform.IsChildOf(targetCollider.transform);
    }

    public Transform GetCurrentTarget() => currentTarget;
    public Vector3 GetCurrentTargetPoint() => currentTargetPoint;
    public bool HasTarget() => currentTarget != null;

    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        Gizmos.color = new Color(0f, 1f, 0.5f, 0.15f);
        Gizmos.DrawSphere(transform.position, detectionRadius);

        if (currentTarget != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(currentTargetPoint, 0.3f);
            Gizmos.DrawLine(transform.position, currentTargetPoint);
        }
    }
}