using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.Animations.Rigging;

public class ShootingSystem : MonoBehaviour
{
    [Header("Player & Weapon Setup")]
    [SerializeField] private Transform playerBody;
    [SerializeField] private Transform weaponFirePoint;
    public Transform WeaponFirePoint
    {
        get => weaponFirePoint;
        set
        {
            weaponFirePoint = value;
            if (weaponFirePoint != null)
            {
                initialXRotation = NormalizeAngle(weaponFirePoint.localEulerAngles.x);
                currentXRotation = initialXRotation;
            }
        }
    }
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private GameObject shootEfect;

    [Header("Game Feel Settings")]
    [SerializeField] private float slowMotionScale = 0.4f;
    [SerializeField] private float cameraShakeStrength = 0.3f;
    [SerializeField] private Material dottedLineMaterial;
    [SerializeField] private AimReticleController aimReticle;

    [Header("Aim Sensitivity")]
    [SerializeField] private float aimSensitivity = 200f;

    [Header("Animation Settings")]
    [SerializeField] private Animator animator;
    public Animator PlayerAnimator
    {
        get => animator;
        set => animator = value;
    }

    [Header("Horizontal Rotation Settings")]
    [SerializeField] private float horizontalRotationSpeed = 10f;
    [SerializeField] private float maxHorizontalAngle = 60f;

    [Header("Vertical Rotation Settings")]
    [SerializeField] private float verticalRotationSpeed = 10f;
    [SerializeField] private float maxVerticalAngleUp = 45f;
    [SerializeField] private float maxVerticalAngleDown = 30f;

    [Header("Aim Settings")]
    [SerializeField] private LineRenderer aimRay;
    [SerializeField] private Color rayColorDefault = Color.red;
    [SerializeField] private Color rayColorLocked = Color.green;
    [SerializeField] private float rayWidth = 0.05f;
    [SerializeField] private float maxRayDistance = 100f;
    [SerializeField] private Rig aimRig;

    [Header("Bullet Settings")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletSpeed = 20f;
    [SerializeField] private int maxBounces = 3;
    [SerializeField] private float bounceDecay = 0.8f;
    [SerializeField] private float maxBounceAngle = 80f;

    [Header("Ammo Settings")]
    [SerializeField] private int maxAmmo = 3;
    private int currentAmmo;

    [Header("Layer Settings")]
    [SerializeField] private LayerMask collisionLayers;
    [SerializeField] private LayerMask shootableLayers;
    [SerializeField] private LayerMask penetrableLayers;
    [SerializeField] private LayerMask movementPointLayer;

    [Header("Aim Assist")]
    [SerializeField] private bool aimAssistEnabled = true;
    [SerializeField] private float aimAssistBlend = 0.6f;

    private Camera mainCamera;
    private bool isAiming = false;
    private bool canShoot = true;
    private Vector2 touchStartPos;
    private Vector2 currentTouchPos;
    private float initialYRotation;
    private float initialXRotation;
    private float currentYRotation;
    private float currentXRotation;
    private GameObject lastFiredBullet;

    // --- Solo Mode Automatic Shooting & Touch Session Lifecycle ---
    private bool touchSessionActive = false;
    private int activeFingerId = -1;
    private bool isMouseSession = false;
    private bool autoShotFired = false;
    private bool sessionCanceled = false;

    // --- UI Pointer Ownership & Tracking ---
    private readonly HashSet<int> uiBlockedFingerIds = new HashSet<int>();
    private bool isMouseUIBlocked = false;
    private PlayerMovement playerMovement;

    private readonly int idleHash = Animator.StringToHash("Idle");
    private readonly int aimingHash = Animator.StringToHash("Aiming");
    private readonly int shootHash = Animator.StringToHash("Shoot");
    private readonly int reloadHash = Animator.StringToHash("Reload");

    private void Awake()
    {
        if (GetComponent<PlayerEquipmentApplier>() == null)
            gameObject.AddComponent<PlayerEquipmentApplier>();
    }

    void Start()
    {
        InitializeSystem();
        mainCamera = Camera.main;

        playerMovement = GetComponent<PlayerMovement>();
        if (playerMovement == null) playerMovement = GetComponentInParent<PlayerMovement>();
        if (playerMovement == null) playerMovement = GetComponentInChildren<PlayerMovement>();

        if (GetComponent<PlayerEquipmentApplier>() == null)
            gameObject.AddComponent<PlayerEquipmentApplier>();

        if (weaponFirePoint == null)
            Debug.LogWarning("Weapon Fire Point is not specified!");
    }

    private void OnDisable()
    {
        if (isAiming)
        {
            CancelShot();
        }
        if (touchSessionActive)
        {
            EndTouchSession();
        }
        ClearUIBlockedPointers();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            if (isAiming) CancelShot();
            if (touchSessionActive) EndTouchSession();
            ClearUIBlockedPointers();
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            if (isAiming) CancelShot();
            if (touchSessionActive) EndTouchSession();
            ClearUIBlockedPointers();
        }
    }

    private void ClearUIBlockedPointers()
    {
        uiBlockedFingerIds.Clear();
        isMouseUIBlocked = false;
    }

    public bool IsGameplayActive()
    {
        // 1. Check UIManager: Main menu, Shop, Guns, Outfits, Settings, etc.
        if (UIManager.Instance != null && !UIManager.Instance.isgameactive())
        {
            return false;
        }

        // 2. Check WinLosePauseUI: Pause menu, Win screen, Lose screen
        if (WinLosePauseUI.Instance != null && (WinLosePauseUI.Instance.IsPaused || WinLosePauseUI.Instance.IsAnyScreenActive))
        {
            return false;
        }

        // 3. Check LevelLoader: Loading screen, Level win/lose/skip transitions
        if (LevelLoader.Instance != null && !LevelLoader.Instance.IsGameActive)
        {
            return false;
        }

        // 4. Check WinLossManager: Game ended state
        if (WinLossManager.Instance != null && WinLossManager.Instance.IsGameEnded)
        {
            return false;
        }

        // 5. Engine-level pause (Time.timeScale == 0) while not aiming
        if (Time.timeScale <= 0f && !isAiming)
        {
            return false;
        }

        // 6. Player movement state (cannot aim or shoot while running between points)
        if (playerMovement != null && playerMovement.IsMoving)
        {
            return false;
        }

        // 7. Ammo and shooting ability
        if (!canShoot || currentAmmo <= 0)
        {
            return false;
        }

        return true;
    }

    public bool IsPointerOverUI(int pointerId, Vector2 screenPosition)
    {
        if (EventSystem.current == null) return false;

        // 1. EventSystem cached pointer state for this pointer ID
        if (pointerId >= 0)
        {
            if (EventSystem.current.IsPointerOverGameObject(pointerId))
                return true;
        }
        else
        {
            if (EventSystem.current.IsPointerOverGameObject(-1) || EventSystem.current.IsPointerOverGameObject())
                return true;
        }

        // 2. Direct raycast against GraphicRaycasters
        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        for (int i = 0; i < results.Count; i++)
        {
            GameObject hitObj = results[i].gameObject;
            if (hitObj != null)
            {
                return true;
            }
        }

        return false;
    }

    void InitializeSystem()
    {
        currentAmmo = maxAmmo;
        initialYRotation = NormalizeAngle(playerBody.eulerAngles.y);

        if (weaponFirePoint != null)
            initialXRotation = NormalizeAngle(weaponFirePoint.localEulerAngles.x);

        if (aimRay == null)
            aimRay = gameObject.AddComponent<LineRenderer>();

        aimRay.useWorldSpace = true;
        aimRay.startWidth = rayWidth;
        aimRay.endWidth = rayWidth;
        aimRay.enabled = false;

        if (dottedLineMaterial != null)
            aimRay.material = dottedLineMaterial;
        else
            aimRay.material = new Material(Shader.Find("Sprites/Default"));

        aimRay.sortingOrder = 5;

        // Fallback safeguard: ensure collisionLayers includes shootable and penetrable if unassigned
        if (collisionLayers.value == 0)
        {
            collisionLayers = shootableLayers | penetrableLayers;
        }
    }

    void Update()
    {
        if (!IsGameplayActive())
        {
            if (isAiming)
            {
                CancelShot();
            }
            if (touchSessionActive)
            {
                EndTouchSession();
            }
            ClearUIBlockedPointers();
            return;
        }

        HandleInput();

        if (isAiming)
        {
            UpdateAimRotation();
            UpdateAimRay();
        }
    }

    void HandleInput()
    {
        // 1. Process Touch Input if touches exist
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);

                // Handle UI-blocked fingers: keep quarantined until released
                if (uiBlockedFingerIds.Contains(touch.fingerId))
                {
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        uiBlockedFingerIds.Remove(touch.fingerId);
                    }
                    continue;
                }

                // If no aiming session is active, check if this touch can start aiming
                if (!touchSessionActive)
                {
                    if (touch.phase == TouchPhase.Began)
                    {
                        if (IsPointerOverUI(touch.fingerId, touch.position))
                        {
                            uiBlockedFingerIds.Add(touch.fingerId);
                            continue;
                        }

                        if (IsClickingIgnoredObject(touch.position))
                        {
                            continue;
                        }

                        if (canShoot && currentAmmo > 0)
                        {
                            touchSessionActive = true;
                            activeFingerId = touch.fingerId;
                            isMouseSession = false;
                            autoShotFired = false;
                            sessionCanceled = false;
                            StartAiming(touch.position);
                        }
                    }
                }
                else if (!isMouseSession && touch.fingerId == activeFingerId)
                {
                    // This is the active aiming touch
                    if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                    {
                        if (isAiming)
                        {
                            if (IsOverCancelButton(touch.position))
                            {
                                CancelShot();
                            }
                            else
                            {
                                currentTouchPos = touch.position;
                            }
                        }
                    }
                    else if (touch.phase == TouchPhase.Ended)
                    {
                        HandlePointerUp(touch.position);
                    }
                    else if (touch.phase == TouchPhase.Canceled)
                    {
                        HandleTouchCanceled();
                    }
                }
            }

            // Safety cleanup if the active aiming touch was lost from Input.touches
            if (touchSessionActive && !isMouseSession)
            {
                bool activeTouchFound = false;
                for (int i = 0; i < Input.touchCount; i++)
                {
                    if (Input.GetTouch(i).fingerId == activeFingerId)
                    {
                        activeTouchFound = true;
                        break;
                    }
                }
                if (!activeTouchFound)
                {
                    HandlePointerUp(currentTouchPos);
                }
            }

            return;
        }

        // 2. Process Mouse Input (when touchCount == 0)
        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverUI(-1, Input.mousePosition))
            {
                isMouseUIBlocked = true;
                return;
            }

            if (IsClickingIgnoredObject(Input.mousePosition))
            {
                return;
            }

            if (!touchSessionActive && canShoot && currentAmmo > 0)
            {
                touchSessionActive = true;
                activeFingerId = -1;
                isMouseSession = true;
                autoShotFired = false;
                sessionCanceled = false;
                StartAiming(Input.mousePosition);
            }
        }
        else if (isMouseUIBlocked)
        {
            if (Input.GetMouseButtonUp(0))
            {
                isMouseUIBlocked = false;
            }
            // Held or dragged mouse that began over UI is completely blocked
            return;
        }
        else if (touchSessionActive && isMouseSession)
        {
            if (Input.GetMouseButton(0))
            {
                if (isAiming)
                {
                    if (IsOverCancelButton(Input.mousePosition))
                    {
                        CancelShot();
                    }
                    else
                    {
                        currentTouchPos = Input.mousePosition;
                    }
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                HandlePointerUp(Input.mousePosition);
            }
        }
    }

    private void HandlePointerUp(Vector2 screenPos)
    {
        if (!touchSessionActive) return;

        // If aiming and released over Cancel button, cancel session without shooting
        if (isAiming && IsOverCancelButton(screenPos))
        {
            CancelShot();
            EndTouchSession();
            return;
        }

        // If session was canceled, discard release event without shooting
        if (sessionCanceled)
        {
            EndTouchSession();
            return;
        }

        // Case 1: If an automatic shot already occurred, this session is consumed.
        // Ignore PointerUp/TouchEnd and do NOT fire any additional bullet.
        if (autoShotFired)
        {
            EndTouchSession();
            return;
        }

        // Case 2: Standard release shot when no enemy was auto-detected
        if (isAiming)
        {
            EndAiming();
            EndTouchSession();
            return;
        }

        EndTouchSession();
    }

    private void HandleTouchCanceled()
    {
        if (!touchSessionActive) return;

        if (isAiming && !autoShotFired)
        {
            CancelShot();
        }

        EndTouchSession();
    }

    private void EndTouchSession()
    {
        touchSessionActive = false;
        activeFingerId = -1;
        isMouseSession = false;
        autoShotFired = false;
        sessionCanceled = false;
    }

    private bool IsOverCancelButton(Vector2 screenPos)
    {
        if (CancelAiming.Instance != null)
        {
            return CancelAiming.Instance.IsPointOverButton(screenPos) || CancelAiming.Instance.IsPointerOver;
        }
        return false;
    }

    bool IsClickingIgnoredObject(Vector2 screenPos)
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return false;

        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, movementPointLayer)) return true;
        return false;
    }

    void StartAiming(Vector2 screenPos)
    {
        if (mainCamera == null) mainCamera = Camera.main;

        isAiming = true;
        touchStartPos = screenPos;
        currentTouchPos = screenPos;
        aimRay.enabled = true;

        if (aimReticle != null) aimReticle.gameObject.SetActive(true);

        Time.timeScale = slowMotionScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        if (mainCamera != null)
        {
            Vector3 cameraForward = mainCamera.transform.forward;
            cameraForward.y = 0;
            if (cameraForward.sqrMagnitude > 0.001f)
                playerBody.forward = cameraForward;
        }

        initialYRotation = playerBody.eulerAngles.y;
        currentYRotation = initialYRotation;

        if (weaponFirePoint != null)
            currentXRotation = weaponFirePoint.localEulerAngles.x;

        if (aimRig != null)
            aimRig.weight = 1f;

        if (animator != null)
        {
            animator.CrossFade(aimingHash, 0.1f);
        }
        else
        {
            Debug.LogError("ShootingSystem Animator is NULL!");
        }

        GameCameraController.Instance?.OnPlayerStartAiming();
    }

    void EndAiming()
    {
        if (isAiming)
        {
            isAiming = false;
            aimRay.enabled = false;
            if (aimReticle != null) aimReticle.gameObject.SetActive(false);

            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;

            FireBullet();
            ResetRotations();

            if (aimRig != null) aimRig.weight = 0f;
            if (animator != null) animator.CrossFade(idleHash, 0.1f);

            GameCameraController.Instance?.OnPlayerStopAiming();
        }
    }

    public void CancelShot()
    {
        // Once automatic shot has already fired and state has transitioned to Idle,
        // the session is already consumed; nothing to cancel.
        if (autoShotFired) return;

        sessionCanceled = true;

        if (!isAiming) return;

        isAiming = false;
        aimRay.enabled = false;
        if (aimReticle != null) aimReticle.gameObject.SetActive(false);

        bool isPaused = WinLosePauseUI.Instance != null && WinLosePauseUI.Instance.IsPaused;
        if (!isPaused && Time.timeScale > 0f)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }

        ResetRotations();

        if (aimRig != null) aimRig.weight = 0f;
        if (animator != null) animator.CrossFade(idleHash, 0.1f);

        GameCameraController.Instance?.OnPlayerStopAiming();
    }

    void UpdateAimRotation()
    {
        Vector2 swipeDelta = currentTouchPos - touchStartPos;
        float horizontalDelta = swipeDelta.x / Screen.width;
        float targetYOffset = Mathf.Clamp(horizontalDelta * aimSensitivity, -maxHorizontalAngle, maxHorizontalAngle);
        float targetY = initialYRotation + targetYOffset;

        currentYRotation = Mathf.Lerp(currentYRotation, targetY, horizontalRotationSpeed * Time.unscaledDeltaTime);
        playerBody.rotation = Quaternion.Euler(0, currentYRotation, 0);

        if (weaponFirePoint != null)
        {
            float verticalDelta = swipeDelta.y / Screen.height;
            float targetXOffset = Mathf.Clamp(-verticalDelta * aimSensitivity, -maxVerticalAngleUp, maxVerticalAngleDown);
            float targetX = initialXRotation + targetXOffset;

            currentXRotation = Mathf.Lerp(currentXRotation, targetX, verticalRotationSpeed * Time.unscaledDeltaTime);
            weaponFirePoint.localRotation = Quaternion.Euler(currentXRotation, 0, 0);
        }
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180) angle -= 360;
        while (angle < -180) angle += 360;
        return angle;
    }

    void ResetRotations()
    {
        playerBody.DORotate(new Vector3(0, initialYRotation, 0), 0.3f).SetUpdate(true);
        if (weaponFirePoint != null)
        {
            weaponFirePoint.DOLocalRotate(new Vector3(initialXRotation, 0, 0), 0.3f).SetUpdate(true);
        }
    }

    void UpdateAimRay()
    {
        Vector3 fireOrigin = weaponFirePoint.position;
        Vector3 fireDirection = weaponFirePoint.forward;
        bool hasLockedTarget = false;

        if (aimAssistEnabled && AimAssist.Instance != null)
        {
            bool foundTarget = AimAssist.Instance.FindBestTarget(weaponFirePoint, shootableLayers, out Vector3 assistDirection);
            if (foundTarget)
            {
                hasLockedTarget = true;
                fireDirection = Vector3.Slerp(fireDirection, assistDirection, aimAssistBlend);
                AimAssist.Instance.ApplyRotationPull(playerBody, weaponFirePoint);
            }
        }

        Color currentRayColor = hasLockedTarget ? rayColorLocked : rayColorDefault;
        aimRay.startColor = currentRayColor;
        aimRay.endColor = currentRayColor;

        if (aimRay.material != null)
        {
            aimRay.material.mainTextureOffset -= new Vector2(Time.unscaledDeltaTime * 4f, 0);
        }

        Vector3[] rayPath = CalculateRayPath(fireOrigin, fireDirection, out Vector3 finalNormal, out EnemyAI detectedEnemy);
        aimRay.positionCount = rayPath.Length;
        aimRay.SetPositions(rayPath);

        if (aimReticle != null && rayPath.Length > 1)
        {
            aimReticle.UpdatePositionAndRotation(rayPath[1], finalNormal);
        }

        // Automatic shot immediately upon enemy detection (Solo Mode)
        if (detectedEnemy != null && !autoShotFired && !sessionCanceled)
        {
            TriggerAutomaticEnemyShot(fireDirection);
        }
    }

    private void TriggerAutomaticEnemyShot(Vector3 aimDirection)
    {
        if (autoShotFired || sessionCanceled || currentAmmo <= 0 || !canShoot) return;

        // Mark session as having fired an automatic shot (consumed)
        autoShotFired = true;

        // Immediately transition from Aiming to Idle
        isAiming = false;
        aimRay.enabled = false;
        if (aimReticle != null) aimReticle.gameObject.SetActive(false);

        bool isPaused = WinLosePauseUI.Instance != null && WinLosePauseUI.Instance.IsPaused;
        if (!isPaused && Time.timeScale > 0f)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }

        // Fire exactly one automatic bullet using existing shooting pipeline
        FireBullet(aimDirection);

        // Restore regular idle presentation
        ResetRotations();

        if (aimRig != null) aimRig.weight = 0f;
        if (animator != null) animator.CrossFade(idleHash, 0.1f);

        GameCameraController.Instance?.OnPlayerStopAiming();
    }

    Vector3[] CalculateRayPath(Vector3 origin, Vector3 direction, out Vector3 finalNormal)
    {
        return CalculateRayPath(origin, direction, out finalNormal, out _);
    }

    Vector3[] CalculateRayPath(Vector3 origin, Vector3 direction, out Vector3 finalNormal, out EnemyAI detectedEnemy)
    {
        detectedEnemy = null;
        List<Vector3> points = new List<Vector3>();
        points.Add(origin);

        Vector3 currentPos = origin + (direction.normalized * 0.1f);
        Vector3 currentDir = direction.normalized;
        int bouncesLeft = maxBounces;

        finalNormal = -currentDir;

        for (int i = 0; i <= maxBounces; i++)
        {
            if (Physics.Raycast(currentPos, currentDir, out RaycastHit hit, maxRayDistance, collisionLayers, QueryTriggerInteraction.Ignore))
            {
                if (IsPenetrable(hit.collider.gameObject))
                {
                    currentPos = hit.point + currentDir * 0.1f;
                    points.Add(hit.point);
                    continue;
                }

                points.Add(hit.point);
                finalNormal = hit.normal;

                // Check if hit object is a valid alive enemy
                if (IsValidAliveEnemy(hit.collider, out EnemyAI enemy))
                {
                    detectedEnemy = enemy;
                    break;
                }

                if (!IsShootableForBounce(hit.collider.gameObject))
                {
                    break;
                }

                float angle = Vector3.Angle(-currentDir, hit.normal);

                if (angle > maxBounceAngle || bouncesLeft <= 0) break;

                currentDir = Vector3.Reflect(currentDir, hit.normal);
                currentPos = hit.point + currentDir * 0.01f;
                bouncesLeft--;
            }
            else
            {
                points.Add(currentPos + currentDir * maxRayDistance);
                finalNormal = -currentDir;
                break;
            }
        }
        return points.ToArray();
    }

    private bool IsValidAliveEnemy(Collider col, out EnemyAI aliveEnemy)
    {
        aliveEnemy = null;
        if (col == null) return false;

        int deadEnemyLayer = LayerMask.NameToLayer("DeadEnemy");
        if (deadEnemyLayer != -1 && col.gameObject.layer == deadEnemyLayer)
            return false;

        aliveEnemy = col.GetComponent<EnemyAI>();
        if (aliveEnemy == null)
        {
            HeadHitbox head = col.GetComponent<HeadHitbox>();
            if (head != null && head.MainEnemyAI != null)
            {
                aliveEnemy = head.MainEnemyAI;
            }
            else
            {
                aliveEnemy = col.GetComponentInParent<EnemyAI>();
            }
        }

        if (aliveEnemy != null && !aliveEnemy.IsDead() && aliveEnemy.GetCurrentHealth() > 0)
        {
            if (deadEnemyLayer != -1 && aliveEnemy.gameObject.layer == deadEnemyLayer)
                return false;

            return true;
        }

        aliveEnemy = null;
        return false;
    }

    bool IsPenetrable(GameObject obj) => ((1 << obj.layer) & penetrableLayers) != 0;
    bool IsShootableForBounce(GameObject obj) => ((1 << obj.layer) & shootableLayers) != 0;

    bool CheckEnemiesInRayPath(Vector3 origin, Vector3 initialDirection)
    {
        if (Physics.Raycast(origin, initialDirection, out RaycastHit directHit, maxRayDistance, collisionLayers))
        {
            if (IsValidAliveEnemy(directHit.collider, out _))
                return true;
        }

        Vector3[] path = CalculateRayPath(origin + initialDirection * 0.1f, initialDirection, out _);

        for (int i = 0; i < path.Length - 1; i++)
        {
            Vector3 segmentDir = path[i + 1] - path[i];
            float segmentLength = segmentDir.magnitude;

            if (segmentLength < 0.01f) continue;

            RaycastHit[] hits = Physics.RaycastAll(path[i], segmentDir.normalized, segmentLength, collisionLayers);
            foreach (var hit in hits)
            {
                if (IsValidAliveEnemy(hit.collider, out _))
                    return true;
            }
        }

        return false;
    }

    void FireBullet()
    {
        FireBullet(null);
    }

    void FireBullet(Vector3? customDirection)
    {
        if (currentAmmo <= 0) return;

        Vector3 shootDirection;
        if (customDirection.HasValue)
        {
            shootDirection = customDirection.Value;
        }
        else
        {
            shootDirection = weaponFirePoint.forward;
            if (aimAssistEnabled && AimAssist.Instance != null && AimAssist.Instance.HasTarget())
            {
                Vector3 toTarget = (AimAssist.Instance.GetCurrentTargetPoint() - weaponFirePoint.position).normalized;
                shootDirection = Vector3.Slerp(weaponFirePoint.forward, toTarget, aimAssistBlend);
            }
        }

        currentAmmo--;
        if (animator != null) animator.CrossFade(shootHash, 0.02f);

#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif

        if (mainCamera != null)
        {
            mainCamera.transform.DOComplete();
            mainCamera.transform.DOShakePosition(0.2f, cameraShakeStrength, 10, 90).SetUpdate(true);
        }

        AudioManager.Instance?.PlaySFX(shootSound);
        if (shootEfect != null) Instantiate(shootEfect, weaponFirePoint.position, Quaternion.LookRotation(shootDirection));

        GameObject bullet = Instantiate(bulletPrefab, weaponFirePoint.position, Quaternion.identity);
        lastFiredBullet = bullet;

        BulletController bulletCtrl = bullet.GetComponent<BulletController>();
        if (bulletCtrl != null)
        {
            bool enemyInPath = CheckEnemiesInRayPath(weaponFirePoint.position, shootDirection);

            if (enemyInPath)
                BulletTimeManager.Instance?.StartBulletFreeze();

            bulletCtrl.Initialize(shootDirection, bulletSpeed, maxBounces, bounceDecay,
                                   maxBounceAngle, collisionLayers, shootableLayers, penetrableLayers, maxRayDistance,
                                   freezeOnFlight: enemyInPath);
        }

        weaponFirePoint.DOPunchPosition(-weaponFirePoint.forward * 0.2f, 0.2f, 5).SetUpdate(true);
        Invoke(nameof(ReturnToIdle), 1f);
    }

    public void ReturnLastBullet()
    {
        if (currentAmmo < maxAmmo)
        {
            currentAmmo++;
            if (lastFiredBullet != null) Destroy(lastFiredBullet);
        }
    }

    public void ReloadAmmo()
    {
        currentAmmo = maxAmmo;
        if (animator != null) animator.CrossFade(reloadHash, 0.1f);
        AmmoUI.Instance?.OnReload();
    }

    private void ReturnToIdle()
    {
        if (!isAiming && animator != null) animator.CrossFade(idleHash, 0.2f);
    }

    public int GetCurrentAmmo() => currentAmmo;
    public int GetMaxAmmo() => maxAmmo;
    public bool IsAiming() => isAiming;
    public bool HasFiredAutoShot() => autoShotFired;
    public bool IsTouchSessionActive() => touchSessionActive;
    public bool IsFingerUIBlocked(int fingerId) => uiBlockedFingerIds.Contains(fingerId);
    public bool IsMouseUIBlocked() => isMouseUIBlocked;
    public bool CheckIsGameplayActive() => IsGameplayActive();
}
