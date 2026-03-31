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

    [Header("Aim Sensitivity")]
    [SerializeField] private float aimSensitivity = 200f;

    [Header("Animation Settings")]
    [SerializeField] private Animator animator;

    [Header("Horizontal Rotation Settings (Left/Right)")]
    [SerializeField] private float horizontalRotationSpeed = 10f;
    [SerializeField] private float maxHorizontalAngle = 60f;

    [Header("Vertical Rotation Settings (Up/Down)")]
    [SerializeField] private float verticalRotationSpeed = 10f;
    [SerializeField] private float maxVerticalAngleUp = 45f;
    [SerializeField] private float maxVerticalAngleDown = 30f;

    [Header("Aim Settings")]
    [SerializeField] private LineRenderer aimRay;
    [SerializeField] private Color rayColor = Color.red;
    [SerializeField] private float rayWidth = 0.05f;
    [SerializeField] private float maxRayDistance = 100f;
    [SerializeField] private Transform aimTarget;
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
    [SerializeField] private LayerMask shootableLayers;
    [SerializeField] private LayerMask penetrableLayers;

    [Header("Conflict Prevention")]
    [SerializeField] private LayerMask movementPointLayer;
    private Camera mainCamera;

    // Private variables
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
        {
            Debug.LogWarning("weapon Fire Point Pivot is not specified! WeaponFirePoint will be used.");
        }
    }

    void InitializeSystem()
    {
        currentAmmo = maxAmmo;

        initialYRotation = NormalizeAngle(playerBody.eulerAngles.y);

        if (weaponFirePoint != null)
            initialXRotation = NormalizeAngle(weaponFirePoint.localEulerAngles.x);

        if (aimRay == null)
        {
            aimRay = gameObject.AddComponent<LineRenderer>();
        }

        aimRay.useWorldSpace = true;
        aimRay.startColor = rayColor;
        aimRay.endColor = rayColor;
        aimRay.startWidth = rayWidth;
        aimRay.endWidth = rayWidth;
        aimRay.enabled = false;

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
            if (Input.touchCount > 0 && EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId))
                return;
            else if (Input.GetMouseButtonDown(0))
                return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (IsClickingIgnoredObject(Input.mousePosition)) return;

            if (canShoot && currentAmmo > 0)
            {
                StartAiming(Input.mousePosition);
            }
        }
        else if (Input.GetMouseButton(0) && isAiming)
        {
            currentTouchPos = Input.mousePosition;
        }
        else if (Input.GetMouseButtonUp(0) && isAiming)
        {
            EndAiming();
        }

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                if (IsClickingIgnoredObject(touch.position)) return;

                if (canShoot && currentAmmo > 0)
                {
                    StartAiming(touch.position);
                }
            }
            else if (touch.phase == TouchPhase.Moved && isAiming)
            {
                currentTouchPos = touch.position;
            }
            else if (touch.phase == TouchPhase.Ended && isAiming)
            {
                EndAiming();
            }
        }
    }

    bool IsClickingIgnoredObject(Vector2 screenPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f, movementPointLayer))
        {
            return true;
        }
        return false;
    }

    void StartAiming(Vector2 screenPos)
    {
        isAiming = true;
        touchStartPos = screenPos;
        currentTouchPos = screenPos;
        aimRay.enabled = true;

        Vector3 cameraForward = mainCamera.transform.forward;
        cameraForward.y = 0;
        playerBody.forward = cameraForward;

        initialYRotation = playerBody.eulerAngles.y;
        currentYRotation = initialYRotation;

        if (weaponFirePoint != null)
            currentXRotation = weaponFirePoint.localEulerAngles.x;

        if (aimRig != null) aimRig.weight = 1f;
        if (animator != null) animator.CrossFade(aimingHash, 0.1f);

        GameCameraController.Instance.OnPlayerStartAiming();
    }

    void EndAiming()
    {
        if (isAiming)
        {
            Debug.Log("Shot Fired");
            FireBullet();
            isAiming = false;
            aimRay.enabled = false;

            ResetRotations();

            if (aimRig != null) aimRig.weight = 0f;

            if (animator != null) animator.CrossFade(idleHash, 0.1f);

            GameCameraController.Instance.OnPlayerStopAiming();
        }
    }

    public void CancelShot()
    {
        if (!isAiming) return;

        isAiming = false;
        aimRay.enabled = false;

        ResetRotations();
        Debug.Log("Shot Cancelled via Button");

        if (aimRig != null) aimRig.weight = 0f;

        if (animator != null) animator.CrossFade(idleHash, 0.1f);

        GameCameraController.Instance.OnPlayerStopAiming();
    }

    void UpdateAimRotation()
    {
        Vector2 swipeDelta = currentTouchPos - touchStartPos;

        float horizontalDelta = swipeDelta.x / Screen.width;
        float targetYOffset = Mathf.Clamp(horizontalDelta * aimSensitivity, -maxHorizontalAngle, maxHorizontalAngle);
        float targetY = initialYRotation + targetYOffset;

        currentYRotation = Mathf.Lerp(currentYRotation, targetY, horizontalRotationSpeed * Time.deltaTime);
        playerBody.rotation = Quaternion.Euler(0, currentYRotation, 0);

        if (weaponFirePoint != null)
        {
            float verticalDelta = swipeDelta.y / Screen.height;
            float targetXOffset = Mathf.Clamp(-verticalDelta * aimSensitivity, -maxVerticalAngleUp, maxVerticalAngleDown);
            float targetX = initialXRotation + targetXOffset;

            currentXRotation = Mathf.Lerp(currentXRotation, targetX, verticalRotationSpeed * Time.deltaTime);
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
        playerBody.DORotate(new Vector3(0, initialYRotation, 0), 0.3f);
        if (weaponFirePoint != null)
        {
            weaponFirePoint.DOLocalRotate(new Vector3(initialXRotation, 0, 0), 0.3f);
        }
    }

    void UpdateAimRay()
    {
        Vector3[] rayPath = CalculateRayPath(weaponFirePoint.position, weaponFirePoint.forward);
        aimRay.positionCount = rayPath.Length;
        aimRay.SetPositions(rayPath);

        if (aimTarget != null && rayPath.Length > 1)
        {
            aimTarget.position = rayPath[1];
        }
    }

    Vector3[] CalculateRayPath(Vector3 origin, Vector3 direction)
    {
        List<Vector3> points = new List<Vector3>();
        points.Add(origin);

        Vector3 currentPos = origin + (direction.normalized * 0.1f);
        Vector3 currentDir = direction.normalized;
        int bouncesLeft = maxBounces;

        for (int i = 0; i <= maxBounces; i++)
        {
            RaycastHit hit;

            if (Physics.Raycast(currentPos, currentDir, out hit, maxRayDistance, shootableLayers, QueryTriggerInteraction.Ignore))
            {
                if (IsPenetrable(hit.collider.gameObject))
                {
                    currentPos = hit.point + currentDir * 0.1f;
                    points.Add(hit.point);
                    continue;
                }

                points.Add(hit.point);
                float angle = Vector3.Angle(-currentDir, hit.normal);

                if (angle > maxBounceAngle || bouncesLeft <= 0) break;

                currentDir = Vector3.Reflect(currentDir, hit.normal);
                currentPos = hit.point + currentDir * 0.01f;
                bouncesLeft--;
            }
            else
            {
                points.Add(currentPos + currentDir * maxRayDistance);
                break;
            }
        }

        return points.ToArray();
    }

    bool IsPenetrable(GameObject obj)
    {
        return ((1 << obj.layer) & penetrableLayers) != 0;
    }

    void FireBullet()
    {
        if (currentAmmo <= 0) return;

        currentAmmo--;
        if (animator != null) animator.CrossFade(shootHash, 0.02f);

        AudioManager.Instance.PlaySFX(shootSound);
        Instantiate(shootEfect, weaponFirePoint.position, Quaternion.LookRotation(weaponFirePoint.forward));
        GameObject bullet = Instantiate(bulletPrefab, weaponFirePoint.position, Quaternion.identity);
        lastFiredBullet = bullet;

        BulletController bulletCtrl = bullet.GetComponent<BulletController>();
        if (bulletCtrl != null)
        {
            bulletCtrl.Initialize(
                weaponFirePoint.forward,
                bulletSpeed,
                maxBounces,
                bounceDecay,
                maxBounceAngle,
                shootableLayers,
                penetrableLayers,
                maxRayDistance
            );
        }

        weaponFirePoint.DOPunchPosition(-weaponFirePoint.forward * 0.2f, 0.2f, 5);
        Debug.Log($"A shot has been fired! Remaining: {currentAmmo}");

        Invoke(nameof(ReturnToIdle), 1f);
    }

    public void ReturnLastBullet()
    {
        if (currentAmmo < maxAmmo)
        {
            currentAmmo++;
            if (lastFiredBullet != null)
            {
                Destroy(lastFiredBullet);
                lastFiredBullet = null;
            }
            Debug.Log($"The shot has been retrieved! Remaining: {currentAmmo}");
        }
    }

    public void ReloadAmmo()
    {
        currentAmmo = maxAmmo;

        if (animator != null) animator.CrossFade(reloadHash, 0.1f);

        AmmoUI.Instance?.OnReload();
        Debug.Log("It has been refilled!");
    }

    private void ReturnToIdle()
    {
        if (!isAiming && animator != null)
        {
            animator.CrossFade(idleHash, 0.2f);
        }
    }

    public int GetCurrentAmmo() => currentAmmo;
    public int GetMaxAmmo() => maxAmmo;

    public bool IsAiming() => isAiming;
}