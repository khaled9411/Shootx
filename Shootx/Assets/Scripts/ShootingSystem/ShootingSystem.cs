using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;
using System.Linq;

public class ShootingSystem : MonoBehaviour
{
    [Header("Player & Weapon Setup")]
    [SerializeField] private Transform playerBody;
    [SerializeField] private Transform weaponFirePoint;
    [SerializeField] private Transform weaponPivot;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float maxRotationAngle = 60f;

    [Header("Weapon Angle Settings")]
    [SerializeField] private float weaponAngleSpeed = 0.3f;
    [SerializeField] private Ease weaponAngleEase = Ease.OutQuad;
    [SerializeField] private LayerMask zoneLayers;

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

    [Header("Cancel Settings")]
    [SerializeField] private float cancelSwipeDistance = 100f;

    [Header("Conflict Prevention")]
    [SerializeField] private LayerMask movementPointLayer;
    private Camera mainCamera;

    // Private variables
    private bool isAiming = false;
    private bool canShoot = true;
    private Vector2 touchStartPos;
    private Vector2 currentTouchPos;
    private float initialYRotation;
    private float targetRotation = 0f;
    private GameObject lastFiredBullet;

    // Weapon angle variables
    private float currentWeaponAngle = 90f;
    private float targetWeaponAngle = 0f;
    private Tween weaponAngleTween;
    private WeaponAngleZone currentZone;

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
        initialYRotation = playerBody.eulerAngles.y;

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
            DetectAndApplyWeaponAngle();
            UpdateAimRay();
            UpdatePlayerRotation();
        }
    }

    void HandleInput()
    {
        // PC Input
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
            UpdateAiming(Input.mousePosition);
        }
        else if (Input.GetMouseButtonUp(0) && isAiming)
        {
            EndAiming();
        }

        // Mobile Input
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
                UpdateAiming(touch.position);
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
    }

    void UpdateAiming(Vector2 screenPos)
    {
        currentTouchPos = screenPos;

        Vector2 swipeDelta = currentTouchPos - touchStartPos;

        if (swipeDelta.y < -cancelSwipeDistance)
        {
            CancelShot();
        }
    }

    void EndAiming()
    {
        if (isAiming)
        {
            Debug.Log("Shot Fired");
            FireBullet();
            isAiming = false;
            aimRay.enabled = false;

            ResetWeaponAngle();
        }
    }

    void CancelShot()
    {
        isAiming = false;
        aimRay.enabled = false;

        playerBody.DORotate(new Vector3(0, initialYRotation, 0), 0.3f);

        ResetWeaponAngle();
    }

    void UpdatePlayerRotation()
    {
        float horizontalDelta = (currentTouchPos.x - touchStartPos.x) / Screen.width;
        targetRotation = Mathf.Clamp(horizontalDelta * maxRotationAngle * 2f, -maxRotationAngle, maxRotationAngle);

        float newRotation = Mathf.LerpAngle(
            playerBody.eulerAngles.y,
            initialYRotation + targetRotation,
            rotationSpeed * Time.deltaTime
        );

        playerBody.rotation = Quaternion.Euler(0, newRotation, 0);
    }


    void DetectAndApplyWeaponAngle()
    {
        List<WeaponAngleZone> detectedZones = new List<WeaponAngleZone>();

        Vector3 rayOrigin = weaponFirePoint.position;
        Vector3 rayDirection = playerBody.forward;

        // Vector3 rayOrigin = new Vector3(weaponFirePoint.position.x, playerBody.position.y + 1.5f, weaponFirePoint.position.z);

        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, rayDirection, maxRayDistance, zoneLayers);
        foreach (RaycastHit hit in hits)
        {
            WeaponAngleZone zone = hit.collider.GetComponent<WeaponAngleZone>();
            if (zone != null)
            {
                detectedZones.Add(zone);
            }
        }

        if (detectedZones.Count == 0)
        {
            if (currentZone != null)
            {
                currentZone = null;
                SetWeaponAngle(90f);
            }
            return;
        }

        WeaponAngleZone bestZone = detectedZones
            .OrderByDescending(z => z.Priority)
            .ThenByDescending(z => z.GetVisibilityScore(mainCamera))
            .FirstOrDefault();

        if (bestZone != currentZone)
        {
            currentZone = bestZone;
            SetWeaponAngle(bestZone.TargetAngle);
        }
    }


    void SetWeaponAngle(float angle)
    {
        targetWeaponAngle = angle;

        weaponAngleTween?.Kill();

        weaponAngleTween = DOTween.To(
            () => currentWeaponAngle,
            x => {
                currentWeaponAngle = x;
                ApplyWeaponRotation();
            },
            targetWeaponAngle,
            weaponAngleSpeed
        ).SetEase(weaponAngleEase);
    }

    void ApplyWeaponRotation()
    {
        if (weaponPivot != null)
        {
            weaponPivot.localRotation = Quaternion.Euler(currentWeaponAngle, 0, 0);
        }
    }

    void ResetWeaponAngle()
    {
        currentZone = null;
        SetWeaponAngle(90f);
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

                if (angle > maxBounceAngle || bouncesLeft <= 0)
                {
                    break;
                }

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

    public int GetCurrentAmmo()
    {
        return currentAmmo;
    }

    public int GetMaxAmmo()
    {
        return maxAmmo;
    }

    void OnDestroy()
    {
        weaponAngleTween?.Kill();
    }
}