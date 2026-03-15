using UnityEngine;

[RequireComponent(typeof(Animator))]
public class EnemyAnimationController : MonoBehaviour
{
    private Animator animator;
    private EnemyAI enemyAI;
    private Rigidbody[] ragdollRigidbodies;
    private Collider[] ragdollColliders;

    private Vector3 lastPosition;
    private float currentSpeed;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        enemyAI = GetComponentInParent<EnemyAI>();

        ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();
        ragdollColliders = GetComponentsInChildren<Collider>();

        DeactivateRagdoll();
    }

    private void Start()
    {
        lastPosition = transform.position;
    }

    private void Update()
    {
        if (enemyAI != null && enemyAI.IsDead()) return;

        UpdateAnimationParameters();
    }

    private void UpdateAnimationParameters()
    {
        Vector3 movementDelta = transform.position - lastPosition;
        currentSpeed = movementDelta.magnitude / Time.deltaTime;
        lastPosition = transform.position;

        animator.SetFloat("Speed", enemyAI.IsWaiting() ? 0f : currentSpeed);

        animator.SetBool("IsAlerted", enemyAI.IsAlerted());
    }

    public void ActivateRagdoll()
    {
        animator.enabled = false;

        foreach (Rigidbody rb in ragdollRigidbodies)
        {
            rb.isKinematic = false;
        }

        foreach (Collider col in ragdollColliders)
        {
            col.enabled = true;
        }

        // GetComponent<Collider>().enabled = false; 
    }

    private void DeactivateRagdoll()
    {
        foreach (Rigidbody rb in ragdollRigidbodies)
        {
            if (rb.gameObject != this.gameObject)
            {
                rb.isKinematic = true;
            }
        }
    }
}