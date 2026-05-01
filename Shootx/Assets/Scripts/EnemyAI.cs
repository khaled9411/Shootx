using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;
using System.Collections;

public class EnemyAI : MonoBehaviour, IDamageable
{
    [Header("Movement Settings")]
    [SerializeField] private Transform[] movementPoints;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float waitTimeAtPoint = 1f;
    //[SerializeField] private float rotationSpeed = 5f;

    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    [Header("Detection Settings")]
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private float detectionAngle = 60f;
    [SerializeField] private float detectionHeight = 3f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private LayerMask deadEnemyLayer;
    [SerializeField] private Transform playerTransform;

    [Header("Alert Settings")]
    [SerializeField] private float alertedSpeedMultiplier = 1.5f;

    [Header("Events")]
    public UnityEvent OnPlayerDetected;
    public UnityEvent OnEnemyDeath;

    // Private variables
    private int currentPointIndex = 0;
    private bool movingForward = true;
    private bool isWaiting = false;
    private bool isAlerted = false;
    private bool isDead = false;
    private PathMovementManager pathManager;
    private Tween currentMoveTween;

    private void Start()
    {
        currentHealth = maxHealth;

        if (pathManager == null)
        {
            pathManager = FindFirstObjectByType<PathMovementManager>();
            if (pathManager == null)
            {
                Debug.LogWarning("PathMovementManager not found in the scene.");
            }
        }

        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
            else
            {
                Debug.LogWarning("Player Transform not assigned and no GameObject with tag 'Player' found.");
            }
        }

        if (movementPoints.Length > 0)
        {
            transform.position = movementPoints[0].transform.position;
            StartCoroutine(PatrolRoutine());
        }
        else
        {
            Debug.LogWarning("No movement points assigned to " + gameObject.name);
        }

        OnPlayerDetected.AddListener(FindFirstObjectByType<LevelLoader>().OnLose);
    }

    private void Update()
    {
        if (!isDead && !isWaiting)
        {
            CheckForPlayer();
            CheckForDeadEnemies();
        }
    }

    private IEnumerator PatrolRoutine()
    {
        while (!isDead)
        {
            if (movementPoints.Length == 0) yield break;

            isWaiting = true;
            yield return new WaitForSeconds(waitTimeAtPoint);
            isWaiting = false;

            int nextPointIndex = GetNextPointIndex();

            if (nextPointIndex != -1)
            {
                Transform nextPoint = movementPoints[nextPointIndex];
                float actualSpeed = isAlerted ? moveSpeed * alertedSpeedMultiplier : moveSpeed;

                Vector3 direction = (nextPoint.transform.position - transform.position).normalized;
                if (direction != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    yield return transform.DORotateQuaternion(targetRotation, 0.3f).WaitForCompletion();
                }
                float distance = Vector3.Distance(transform.position, nextPoint.transform.position);
                float duration = distance / actualSpeed;

                currentMoveTween = transform.DOMove(nextPoint.transform.position, duration)
                    .SetEase(Ease.Linear);

                yield return currentMoveTween.WaitForCompletion();

                currentPointIndex = nextPointIndex;
            }
        }
    }

    private int GetNextPointIndex()
    {
        if (movementPoints.Length <= 1) return -1;

        if (movingForward)
        {
            if (currentPointIndex < movementPoints.Length - 1)
            {
                return currentPointIndex + 1;
            }
            else
            {
                movingForward = false;
                return currentPointIndex - 1;
            }
        }
        else
        {
            if (currentPointIndex > 0)
            {
                return currentPointIndex - 1;
            }
            else
            {
                movingForward = true;
                return currentPointIndex + 1;
            }
        }
    }

    private void CheckForPlayer()
    {
        if (playerTransform == null) return;

        Vector3 directionToPlayer = playerTransform.position - transform.position;
        float distanceToPlayer = directionToPlayer.magnitude;

        if (distanceToPlayer <= detectionRange)
        {
            float heightDifference = Mathf.Abs(playerTransform.position.y - transform.position.y);

            if (heightDifference <= detectionHeight)
            {
                float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);

                if (angleToPlayer <= detectionAngle / 2f)
                {
                    if (Physics.Raycast(transform.position + Vector3.up, directionToPlayer.normalized,
                        out RaycastHit hit, distanceToPlayer))
                    {
                        if (hit.transform == playerTransform)
                        {
                            Debug.Log(gameObject.name + " detected the player!");
                            OnPlayerDetected?.Invoke();
                        }
                    }
                }
            }
        }
    }

    private void CheckForDeadEnemies()
    {
        Collider[] deadEnemies = Physics.OverlapSphere(transform.position, detectionRange, deadEnemyLayer);

        foreach (Collider deadEnemy in deadEnemies)
        {

            OnPlayerDetected?.Invoke();
            //Vector3 directionToCorpse = deadEnemy.transform.position - transform.position;
            //float angleToCorpse = Vector3.Angle(transform.forward, directionToCorpse);

            //if (angleToCorpse <= detectionAngle / 2f)
            //{
            //    if (pathManager != null && pathManager.CurrentPoint != null && playerTransform != null)
            //    {
            //        Vector3 playerLastKnownPos = pathManager.CurrentPoint.transform.position;
            //        Vector3 directionToLastPos = playerLastKnownPos - transform.position;

            //        if (Physics.Raycast(transform.position + Vector3.up, directionToLastPos.normalized,
            //            out RaycastHit hit, detectionRange))
            //        {
            //            if (hit.transform == playerTransform)
            //            {
            //                isAlerted = true;
            //                OnPlayerDetected?.Invoke();
            //                break;
            //            }
            //        }
            //    }
            //}
        }
    }

    public void TakeDamage(float damage, Vector3 hitDirection)
    {
        if (isDead) return;

        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            FindFirstObjectByType<ShootingSystem>().ReturnLastBullet();
            Die(hitDirection);
        }
    }

    private void Die(Vector3 hitDirection)
    {
        isDead = true;
        currentMoveTween?.Kill();
        StopAllCoroutines();

        OnEnemyDeath?.Invoke();

        int deadLayer = LayerMask.NameToLayer("DeadEnemy");
        gameObject.layer = deadLayer;

        foreach (Transform child in transform.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = deadLayer;
        }

        EnemyAnimationController animController = GetComponentInChildren<EnemyAnimationController>();
        if (animController != null)
        {
            animController.ActivateRagdoll(hitDirection, 10f);
        }
    }

    public Transform[] GetMovementPoints()
    {
        return movementPoints;
    }

    public float GetCurrentHealth()
    {
        return currentHealth;
    }

    public bool IsDead()
    {
        return isDead;
    }

    public bool IsAlerted()
    {
        return isAlerted;
    }

    public bool IsWaiting()
    {
        return isWaiting;
    }

    private void OnDrawGizmosSelected()
    {
        if (isDead) return;

        Gizmos.color = Color.yellow;

        Vector3 forward = transform.forward * detectionRange;
        Vector3 rightBoundary = Quaternion.Euler(0, detectionAngle / 2f, 0) * forward;
        Vector3 leftBoundary = Quaternion.Euler(0, -detectionAngle / 2f, 0) * forward;

        Gizmos.DrawLine(transform.position, transform.position + rightBoundary);
        Gizmos.DrawLine(transform.position, transform.position + leftBoundary);

        Vector3 previousPoint = transform.position + rightBoundary;
        for (int i = 1; i <= 20; i++)
        {
            float angle = Mathf.Lerp(-detectionAngle / 2f, detectionAngle / 2f, i / 20f);
            Vector3 point = transform.position + Quaternion.Euler(0, angle, 0) * forward;
            Gizmos.DrawLine(previousPoint, point);
            previousPoint = point;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position + transform.forward * (detectionRange / 2f),
            new Vector3(detectionRange * Mathf.Tan(detectionAngle / 2f * Mathf.Deg2Rad) * 2f,
            detectionHeight * 2f, detectionRange));
    }
}