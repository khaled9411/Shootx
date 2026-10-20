using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    [Header("Input")]
    [SerializeField] private InputReader inputReader;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 15f;

    [Header("Camera Reference")]
    [SerializeField] private LayerMask groundLayer;

    private Camera mainCamera;
    private CharacterController characterController;
    private Vector2 currentMoveInput;
    private Vector2 currentAimInput;

    private float verticalVelocity;
    private const float gravity = -9.81f;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (mainCamera == null) mainCamera = Camera.main;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        if (inputReader != null)
        {
            inputReader.moveEvent += HandleMoveInput;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        if (inputReader != null)
        {
            inputReader.moveEvent -= HandleMoveInput;
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        currentAimInput = inputReader.aimPostion;

        ProcessMovement();
        ProcessRotation();
    }

    private void HandleMoveInput(Vector2 moveInput)
    {
        currentMoveInput = moveInput;
    }

    private void ProcessMovement()
    {
        Vector3 moveDirection = new Vector3(currentMoveInput.x, 0f, currentMoveInput.y);

        moveDirection = mainCamera.transform.TransformDirection(moveDirection);
        moveDirection.y = 0f;
        moveDirection.Normalize();

        if (characterController.isGrounded)
        {
            verticalVelocity = -0.5f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 velocity = (moveDirection * moveSpeed) + (Vector3.up * verticalVelocity);

        characterController.Move(velocity * Time.deltaTime);
    }

    private void ProcessRotation()
    {
        Ray ray = mainCamera.ScreenPointToRay(currentAimInput);
        if (Physics.Raycast(ray, out RaycastHit hitInfo, 100f, groundLayer))
        {
            Vector3 lookTarget = hitInfo.point;
            lookTarget.y = transform.position.y;
            Vector3 lookDirection = lookTarget - transform.position;

            if (lookDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }
}