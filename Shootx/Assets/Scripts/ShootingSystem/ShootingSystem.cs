using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class ShootingSystem : MonoBehaviour
{
    [Header("Player & Weapon Setup")]
    [SerializeField] private Transform playerBody;
    [SerializeField] private Transform weaponFirePoint;
    [SerializeField] private Transform weaponPivot;

    [Header("Aim Sensitivity")]
    [SerializeField] private float aimSensitivity = 200f;

    [Header("Horizontal Rotation Settings (Left/Right)")]
    [SerializeField] private float horizontalRotationSpeed = 10f;
    [SerializeField] private float maxHorizontalAngle = 60f;

    [Header("Vertical Rotation Settings (Up/Down)")]
    [SerializeField] private float verticalRotationSpeed = 10f;
    [SerializeField] private float maxVerticalAngleUp = 45f;
    [SerializeField] private float maxVerticalAngleDown = 30f;

    [Header("Ray Settings")]
    [SerializeField] private LineRenderer aimRay;
    [SerializeField] private Color rayColor = Color.red;
    [SerializeField] private float rayWidth = 0.05f;
    [SerializeField] private float maxRayDistance = 100f;

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

    void Start()
    {
        InitializeSystem();
        mainCamera = Camera.main;

        if (weaponPivot == null)
        {
            Debug.LogWarning("Weapon Pivot is not specified! WeaponFirePoint will be used.");
            weaponPivot = weaponFirePoint;
        }
    }

    void InitializeSystem()
    {
        currentAmmo = maxAmmo;

        initialYRotation = NormalizeAngle(playerBody.eulerAngles.y);

        if (weaponPivot != null)
            initialXRotation = NormalizeAngle(weaponPivot.localEulerAngles.x);

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

        currentYRotation = initialYRotation;
        currentXRotation = initialXRotation;

        CameraManager.Instance.OnPlayerStartAiming();
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

            CameraManager.Instance.OnPlayerStopAiming();
        }
    }

    public void CancelShot()
    {
        if (!isAiming) return;

        isAiming = false;
        aimRay.enabled = false;

        ResetRotations();
        Debug.Log("Shot Cancelled via Button");

        CameraManager.Instance.OnPlayerStopAiming();
    }

    void UpdateAimRotation()
    {
        Vector2 swipeDelta = currentTouchPos - touchStartPos;

        float horizontalDelta = swipeDelta.x / Screen.width;
        float targetYOffset = Mathf.Clamp(horizontalDelta * aimSensitivity, -maxHorizontalAngle, maxHorizontalAngle);
        float targetY = initialYRotation + targetYOffset;

        currentYRotation = Mathf.Lerp(currentYRotation, targetY, horizontalRotationSpeed * Time.deltaTime);
        playerBody.rotation = Quaternion.Euler(0, currentYRotation, 0);

        if (weaponPivot != null)
        {
            float verticalDelta = swipeDelta.y / Screen.height;
            float targetXOffset = Mathf.Clamp(-verticalDelta * aimSensitivity, -maxVerticalAngleUp, maxVerticalAngleDown);
            float targetX = initialXRotation + targetXOffset;

            currentXRotation = Mathf.Lerp(currentXRotation, targetX, verticalRotationSpeed * Time.deltaTime);
            weaponPivot.localRotation = Quaternion.Euler(currentXRotation, 0, 0);
        }
    }

    private float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        if (angle < -180f) angle += 360f;
        return angle;
    }

    void ResetRotations()
    {
        playerBody.DORotate(new Vector3(0, initialYRotation, 0), 0.3f);
        if (weaponPivot != null)
        {
            weaponPivot.DOLocalRotate(new Vector3(initialXRotation, 0, 0), 0.3f);
        }
    }

    void UpdateAimRay()
    {
        Vector3[] rayPath = CalculateRayPath(weaponFirePoint.position, weaponFirePoint.forward);
        aimRay.positionCount = rayPath.Length;
        aimRay.SetPositions(rayPath);
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
        Debug.Log("It has been refilled!");
    }

    public int GetCurrentAmmo() => currentAmmo;
    public int GetMaxAmmo() => maxAmmo;
}