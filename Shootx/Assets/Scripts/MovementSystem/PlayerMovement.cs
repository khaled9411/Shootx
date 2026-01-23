using UnityEngine;
using DG.Tweening;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float movementSpeed = 5f;
    [SerializeField] private Ease movementEase = Ease.InOutQuad;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Animation Settings")]
    [SerializeField] private Animator animator;
    [SerializeField] private string walkAnimationName = "Walk";
    [SerializeField] private string idleAnimationName = "Idle";

    [Header("Visual Feedback")]
    [SerializeField] private bool useSquashStretch = true;
    [SerializeField] private float squashAmount = 0.2f;

    [Header("Path Settings")]
    [SerializeField] private float delayBetweenPoints = 0.2f;

    private bool isMoving = false;
    private Tween movementTween;
    private Vector3 targetPosition;
    private int currentPointIndex = 0;

    public int CurrentPointIndex => currentPointIndex;
    public bool IsMoving => isMoving;

    void Update()
    {
        if (isMoving && animator == null)
        {
            Vector3 direction = (targetPosition - transform.position).normalized;
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }

    public void MoveToPoint(Vector3 destination, int destinationIndex)
    {
        if (isMoving)
        {
            Debug.Log("Player is already moving!");
            return;
        }

        targetPosition = new Vector3(destination.x, transform.position.y, destination.z);
        currentPointIndex = destinationIndex;

        float distance = Vector3.Distance(transform.position, targetPosition);
        float duration = distance / movementSpeed;

        Vector3 direction = (targetPosition - transform.position).normalized;
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.DORotateQuaternion(targetRotation, 0.3f);
        }

        StartMovement(duration);
    }

    void StartMovement(float duration)
    {
        isMoving = true;

        if (animator != null)
        {
            animator.Play(walkAnimationName);
        }

        if (useSquashStretch)
        {
            transform.DOScaleY(1 - squashAmount, 0.1f)
                .OnComplete(() => transform.DOScaleY(1, 0.1f));
        }

        movementTween = transform.DOMove(targetPosition, duration)
            .SetEase(movementEase)
            .OnUpdate(() => {
            })
            .OnComplete(() => OnMovementComplete());
    }

    void OnMovementComplete()
    {
        isMoving = false;

        if (animator != null)
        {
            animator.Play(idleAnimationName);
        }

        if (useSquashStretch)
        {
            transform.DOScaleY(1 + squashAmount, 0.1f)
                .OnComplete(() => transform.DOScaleY(1, 0.1f));
        }

        transform.DOMoveY(transform.position.y + 0.2f, 0.15f)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => {
                transform.DOMoveY(transform.position.y - 0.2f, 0.15f)
                    .SetEase(Ease.InQuad);
            });

        Collider[] nearbyPoints = Physics.OverlapSphere(transform.position, 0.5f);
        foreach (Collider col in nearbyPoints)
        {
            MovementPoint point = col.GetComponent<MovementPoint>();
            if (point != null)
            {
                point.OnPlayerReached();
            }
        }

        PathMovementManager manager = FindFirstObjectByType<PathMovementManager>();
        if (manager != null)
        {
            DOVirtual.DelayedCall(delayBetweenPoints, () => manager.OnPointReached());
        }
    }

    void OnDestroy()
    {
        movementTween?.Kill();
    }
}