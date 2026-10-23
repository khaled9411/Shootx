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
    private Vector3 latestMobileAimDir;

    private float verticalVelocity;
    private const float gravity = -9.81f;

    private PlayerDash playerDash;
    private Vector3 _lastValidMoveDirection = Vector3.forward;

    public Vector3 CurrentMoveDirection { get; private set; }
    public Vector3 LastValidMoveDirection => _lastValidMoveDirection;
    public bool IsInputLocked => _inputLocked;

    // ─── Input Lock ─────────────────────────────────────────────
    /// <summary>
    /// When true, ALL gameplay input is ignored.
    /// Set by MatchManager during countdown, death, and match end.
    /// </summary>
    private bool _inputLocked = false;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerDash = GetComponent<PlayerDash>();
        if (mainCamera == null) mainCamera = Camera.main;
    }

    /// <summary>
    /// Lock or unlock all gameplay input (movement, rotation).
    /// Also clears cached input vectors to prevent stuck movement.
    /// </summary>
    public void SetInputLocked(bool locked)
    {
        _inputLocked = locked;
        if (locked)
        {
            // Clear cached input so there is no residual movement
            currentMoveInput = Vector2.zero;
            latestMobileAimDir = Vector3.zero;
            currentAimInput = Vector2.zero;

            // Stop Rigidbody/CC drift
            if (characterController != null && characterController.enabled)
            {
                // Apply zero move to flush velocity
                characterController.Move(Vector3.zero);
            }

            // Reset mobile inputs
            inputReader?.ResetMobileInput();
        }
    }

    /// <summary>Called when the player respawns to reset stale movement state.</summary>
    public void OnRespawned()
    {
        verticalVelocity = 0f;
        currentMoveInput = Vector2.zero;
        latestMobileAimDir = Vector3.zero;
        currentAimInput = Vector2.zero;
        CurrentMoveDirection = Vector3.zero;
        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        _lastValidMoveDirection = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;
    }

    public override void OnNetworkSpawn()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        _lastValidMoveDirection = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;

        if (!IsOwner) return;

        if (inputReader != null)
        {
            inputReader.moveEvent += HandleMoveInput;
            inputReader.mobileAimDirectionEvent += HandleMobileAimDirection;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        if (inputReader != null)
        {
            inputReader.moveEvent -= HandleMoveInput;
            inputReader.mobileAimDirectionEvent -= HandleMobileAimDirection;
        }
    }

    private void HandleMobileAimDirection(Vector3 dir)
    {
        latestMobileAimDir = dir;
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (_inputLocked) return;

        currentAimInput = inputReader.aimPostion;

        if (playerDash != null && playerDash.IsDashing)
        {
            ProcessDashMovement();
        }
        else
        {
            ProcessMovement();
        }
        ProcessRotation();
    }

    private void HandleMoveInput(Vector2 moveInput)
    {
        if (_inputLocked) return;
        currentMoveInput = moveInput;
    }

    private void ProcessMovement()
    {
        Vector3 moveDirection = new Vector3(currentMoveInput.x, 0f, currentMoveInput.y);

        moveDirection = mainCamera.transform.TransformDirection(moveDirection);
        moveDirection.y = 0f;
        moveDirection.Normalize();

        if (moveDirection.sqrMagnitude > 0.001f)
        {
            CurrentMoveDirection = moveDirection;
            _lastValidMoveDirection = moveDirection;
        }
        else
        {
            CurrentMoveDirection = Vector3.zero;
        }

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

    private void ProcessDashMovement()
    {
        if (characterController.isGrounded)
        {
            verticalVelocity = -0.5f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        if (playerDash != null && playerDash.ExecuteDashStep(characterController, Time.deltaTime, out Vector3 horizontalStep, out bool hitObstacle))
        {
            Vector3 totalStep = horizontalStep + (Vector3.up * verticalVelocity * Time.deltaTime);
            characterController.Move(totalStep);

            if (hitObstacle)
            {
                playerDash.OnDashObstacleEncountered();
            }
        }
    }

    /// <summary>
    /// Returns the dash direction according to gameplay rules:
    /// - If currently moving: actual current movement direction.
    /// - If standing still: last valid nonzero movement direction.
    /// - If never moved: player forward direction fallback.
    /// </summary>
    public Vector3 GetDashDirection()
    {
        if (CurrentMoveDirection.sqrMagnitude > 0.001f)
        {
            return CurrentMoveDirection;
        }
        if (_lastValidMoveDirection.sqrMagnitude > 0.001f)
        {
            return _lastValidMoveDirection;
        }
        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude > 0.001f)
        {
            return fwd.normalized;
        }
        return Vector3.forward;
    }

    private void ProcessRotation()
    {
        // 1. Mobile touch aiming takes precedence when active
        if (inputReader != null && inputReader.IsMobileAiming && latestMobileAimDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(latestMobileAimDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            return;
        }

        // 2. Desktop mouse aiming via ground plane raycast
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

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