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
    [SerializeField] private ParticleSystem movementParticles;

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
        if (isMoving)
        {
            Vector3 direction = (targetPosition - transform.position).normalized;

            direction.y = 0;

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

        // ?? ????? transform.DORotateQuaternion ???? ??????? ?? ??? Update

        StartMovement(duration);

        if (GameCameraController.Instance != null)
        {
            GameCameraController.Instance.OnPlayerStartMoving();
        }
    }

    void StartMovement(float duration)
    {
        isMoving = true;

        if (animator != null)
        {
            // ??????? CrossFade ??? Play ??????? ??? ????? ??? ????? ?????
            animator.CrossFade(walkAnimationName, 0.1f);
        }

        if (movementParticles != null)
        {
            movementParticles.Play();
        }

        if (useSquashStretch)
        {
            transform.DOScaleY(1 - squashAmount, 0.1f)
                .OnComplete(() => transform.DOScaleY(1, 0.1f));
        }

        movementTween = transform.DOMove(targetPosition, duration)
            .SetEase(movementEase)
            .OnComplete(() => OnMovementComplete());
    }

    void OnMovementComplete()
    {
        isMoving = false;

        if (animator != null)
        {
            // ?????? ??? ?? Idle
            animator.CrossFade(idleAnimationName, 0.15f);
        }

        if (movementParticles != null)
        {
            movementParticles.Stop();
        }

        if (useSquashStretch)
        {
            transform.DOScaleY(1 + squashAmount, 0.1f)
                .OnComplete(() => transform.DOScaleY(1, 0.1f));
        }

        // ??? ???? ??? Y ??????? ??? ?? ????? ?????? ??? ???? ???? ????? ??? ??????
        float startY = transform.position.y;

        Sequence jumpSequence = DOTween.Sequence();
        jumpSequence.Append(transform.DOMoveY(startY + 0.2f, 0.15f).SetEase(Ease.OutQuad))
                    .Append(transform.DOMoveY(startY, 0.15f).SetEase(Ease.InQuad));

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
        // ?????? ????? ?? ??? Tweens ???????? ???? ??? Transform ??? ??????
        transform.DOKill();
    }
}