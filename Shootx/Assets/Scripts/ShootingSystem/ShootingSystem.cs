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

    private readonly int idleHash = Animator.StringToHash("Idle");
    private readonly int aimingHash = Animator.StringToHash("Aiming");
    private readonly int shootHash = Animator.StringToHash("Shoot");
    private readonly int reloadHash = Animator.StringToHash("Reload");

    void Start()
    {
        InitializeSystem();
        mainCamera = Camera.main;

        if (weaponFirePoint == null)
            Debug.LogWarning("Weapon Fire Point is not specified!");
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
    }

    void Update()
    {
        HandleInput();

        if (isAiming)
        {
            UpdateAimRotation();
            UpdateAimRay();
        }
    }

    void HandleInput()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            if (Input.touchCount > 0 && EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)) return;
            else if (Input.GetMouseButtonDown(0)) return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (IsClickingIgnoredObject(Input.mousePosition)) return;
            if (canShoot && currentAmmo > 0) StartAiming(Input.mousePosition);
        }
        else if (Input.GetMouseButton(0) && isAiming) currentTouchPos = Input.mousePosition;
        else if (Input.GetMouseButtonUp(0) && isAiming) EndAiming();

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                if (IsClickingIgnoredObject(touch.position)) return;
                if (canShoot && currentAmmo > 0) StartAiming(touch.position);
            }
            else if (touch.phase == TouchPhase.Moved && isAiming) currentTouchPos = touch.position;
            else if (touch.phase == TouchPhase.Ended && isAiming) EndAiming();
        }
    }

    bool IsClickingIgnoredObject(Vector2 screenPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, movementPointLayer)) return true;
        return false;
    }

    void StartAiming(Vector2 screenPos)
    {
        isAiming = true;
        touchStartPos = screenPos;
        currentTouchPos = screenPos;
        aimRay.enabled = true;

        if (aimReticle != null) aimReticle.gameObject.SetActive(true);

        Time.timeScale = slowMotionScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        Vector3 cameraForward = mainCamera.transform.forward;
        cameraForward.y = 0;
        playerBody.forward = cameraForward;

        initialYRotation = playerBody.eulerAngles.y;
        currentYRotation = initialYRotation;

        if (weaponFirePoint != null)
            currentXRotation = weaponFirePoint.localEulerAngles.x;

        if (aimRig != null) aimRig.weight = 1f;
        if (animator != null) animator.CrossFade(aimingHash, 0.1f);

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
        if (!isAiming) return;

        isAiming = false;
        aimRay.enabled = false;
        if (aimReticle != null) aimReticle.gameObject.SetActive(false);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

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

        Vector3[] rayPath = CalculateRayPath(fireOrigin, fireDirection, out Vector3 finalNormal);
        aimRay.positionCount = rayPath.Length;
        aimRay.SetPositions(rayPath);

        if (aimReticle != null && rayPath.Length > 1)
        {
            aimReticle.UpdatePositionAndRotation(rayPath[1], finalNormal);
        }
    }

    Vector3[] CalculateRayPath(Vector3 origin, Vector3 direction, out Vector3 finalNormal)
    {
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

    bool IsPenetrable(GameObject obj) => ((1 << obj.layer) & penetrableLayers) != 0;
    bool IsShootableForBounce(GameObject obj) => ((1 << obj.layer) & shootableLayers) != 0;

    bool CheckEnemiesInRayPath(Vector3 origin, Vector3 initialDirection)
    {
        if (Physics.Raycast(origin, initialDirection, out RaycastHit directHit, maxRayDistance, collisionLayers))
        {
            if (directHit.collider.GetComponent<IDamageable>() != null)
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
                if (hit.collider.GetComponent<IDamageable>() != null)
                    return true;
            }
        }

        return false;
    }


    void FireBullet()
    {
        if (currentAmmo <= 0) return;

        Vector3 shootDirection = weaponFirePoint.forward;
        if (aimAssistEnabled && AimAssist.Instance != null && AimAssist.Instance.HasTarget())
        {
            Vector3 toTarget = (AimAssist.Instance.GetCurrentTargetPoint() - weaponFirePoint.position).normalized;
            shootDirection = Vector3.Slerp(weaponFirePoint.forward, toTarget, aimAssistBlend);
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

}