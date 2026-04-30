using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

[Serializable]
public struct PointEnemyLink
{
    public MovementPoint point;
    public EnemyAI targetEnemy;
}

public class GameCameraController : MonoBehaviour
{
    public static GameCameraController Instance { get; private set; }

    #region Inspector Fields

    [Header("=== Cameras ===")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform playerTransform;

    [Header("=== Menu Camera Position ===")]
    [SerializeField] private Transform menuCameraAnchor;

    [Header("=== Gameplay Camera Position ===")]
    [SerializeField] private Transform gameCameraAnchor;

    [Header("=== Transition Settings ===")]
    [SerializeField] private float transitionDuration = 1.2f;
    [SerializeField] private Ease transitionEase = Ease.InOutCubic;

    [Header("=== Menu Idle Bob ===")]
    [SerializeField] private bool enableMenuBob = true;
    [SerializeField] private float bobAmplitude = 0.15f;
    [SerializeField] private float bobDuration = 3.0f;

    [Header("=== Follow Settings ===")]
    [SerializeField] public bool enableCameraFollow = true;
    [SerializeField] private float followSpeed = 5f;
    private Vector3 followOffset;

    [Header("=== FOV Settings (Static Mode) ===")]
    [SerializeField] private float idleFOV = 60f;
    [SerializeField] private float moveFOV = 75f;
    [SerializeField] private float aimFOV = 40f;
    [SerializeField] private float zoomSpeed = 0.5f;

    [Header("=== Dynamic Zoom Settings ===")]
    [SerializeField] private bool enableDynamicZoom = true;
    [SerializeField] private float dynamicMinFOV = 55f;
    [SerializeField] private float dynamicMaxFOV = 90f;
    [SerializeField] private float zoomStartDistance = 5f;
    [SerializeField] private float zoomMaxDistance = 30f;
    [SerializeField] private float fovPerExtraEnemy = 3f;
    [SerializeField] private float dynamicZoomSmoothSpeed = 2f;
    [SerializeField] private float enemyScanInterval = 0.3f;

    [Header("=== Enemy Reveal Settings ===")]
    [SerializeField] private float revealDuration = 2f;
    [SerializeField] private float autoDetectRadius = 15f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("=== Level Setup ===")]
    [SerializeField] private List<PointEnemyLink> manualLinks;

    #endregion

    #region Private State

    private Vector3 menuPos;
    private Quaternion menuRot;
    private Tween bobTween;
    private Tween fovTween;
    private bool transitioned = false;
    private Transform currentTarget = null;

    // Dynamic Zoom
    private List<EnemyAI> activeEnemies = new List<EnemyAI>();
    private float targetDynamicFOV = 60f;
    private float scanTimer = 0f;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        if (menuCameraAnchor != null)
        {
            mainCamera.transform.position = menuCameraAnchor.position;
            mainCamera.transform.rotation = menuCameraAnchor.rotation;
        }

        menuPos = mainCamera.transform.position;
        menuRot = mainCamera.transform.rotation;

        mainCamera.fieldOfView = idleFOV;
        targetDynamicFOV = idleFOV;

        if (enableMenuBob) StartMenuBob();
    }

    private void OnEnable() => UIManager.OnTapToPlay += TransitionToGameCamera;
    private void OnDisable()
    {
        UIManager.OnTapToPlay -= TransitionToGameCamera;
        bobTween?.Kill();
        fovTween?.Kill();
    }

    private void LateUpdate()
    {
        if (transitioned && currentTarget != null && enableCameraFollow)
        {
            Vector3 targetPos = currentTarget.position + followOffset;
            mainCamera.transform.position = Vector3.Lerp(
                mainCamera.transform.position, targetPos, Time.deltaTime * followSpeed);
        }

        if (transitioned && enableDynamicZoom && currentTarget == playerTransform)
            UpdateDynamicZoom();
    }

    #endregion

    #region Dynamic Zoom System

    private void UpdateDynamicZoom()
    {
        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = enemyScanInterval;
            ScanActiveEnemies();
        }

        if (playerTransform == null) return;

        float maxDist = 0f;
        int aliveCount = 0;

        foreach (EnemyAI enemy in activeEnemies)
        {
            if (enemy == null || enemy.IsDead()) continue;
            float dist = Vector3.Distance(playerTransform.position, enemy.transform.position);
            if (dist > maxDist) maxDist = dist;
            aliveCount++;
        }

        float distFactor = Mathf.InverseLerp(zoomStartDistance, zoomMaxDistance, maxDist);
        float baseFOV = Mathf.Lerp(dynamicMinFOV, dynamicMaxFOV, distFactor);

        float extraFOV = Mathf.Max(0, aliveCount - 1) * fovPerExtraEnemy;

        targetDynamicFOV = Mathf.Clamp(baseFOV + extraFOV, dynamicMinFOV, dynamicMaxFOV);

        mainCamera.fieldOfView = Mathf.Lerp(
            mainCamera.fieldOfView, targetDynamicFOV, Time.deltaTime * dynamicZoomSmoothSpeed);
    }

    private void ScanActiveEnemies()
    {
        activeEnemies.Clear();
        foreach (EnemyAI e in FindObjectsByType<EnemyAI>(0))
            if (e != null && !e.IsDead()) activeEnemies.Add(e);
    }

    public void SetEnemyList(List<EnemyAI> enemies) => activeEnemies = enemies;

    public void SetDynamicZoom(bool state)
    {
        enableDynamicZoom = state;
        if (!state) TweenFOV(idleFOV);
    }

    #endregion

    #region Menu Bob & Game Transition

    private void StartMenuBob()
    {
        bobTween?.Kill();
        bobTween = mainCamera.transform
            .DOMove(menuPos + Vector3.up * bobAmplitude, bobDuration)
            .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
    }

    public void TransitionToGameCamera()
    {
        if (transitioned) return;
        transitioned = true;
        bobTween?.Kill();

        if (gameCameraAnchor == null)
        {
            Debug.LogWarning("[GameCamera] gameCameraAnchor is not assigned!");
            return;
        }

        if (playerTransform != null)
            followOffset = gameCameraAnchor.position - playerTransform.position;

        Sequence seq = DOTween.Sequence();
        seq.Append(mainCamera.transform.DOMove(gameCameraAnchor.position, transitionDuration).SetEase(transitionEase));
        seq.Join(mainCamera.transform.DORotateQuaternion(gameCameraAnchor.rotation, transitionDuration).SetEase(transitionEase));
        seq.Join(mainCamera.DOFieldOfView(idleFOV, transitionDuration).SetEase(transitionEase));
        seq.OnComplete(() =>
        {
            SetCameraTarget(playerTransform);
            ScanActiveEnemies();
        });
        seq.Play();
    }

    public void ReturnToMenuCamera(Action onComplete = null)
    {
        transitioned = false;
        currentTarget = null;
        activeEnemies.Clear();

        DOTween.Kill(mainCamera.transform);
        fovTween?.Kill();

        Sequence seq = DOTween.Sequence();
        seq.Append(mainCamera.transform.DOMove(menuPos, transitionDuration * 0.8f).SetEase(transitionEase));
        seq.Join(mainCamera.transform.DORotateQuaternion(menuRot, transitionDuration * 0.8f).SetEase(transitionEase));
        seq.Join(mainCamera.DOFieldOfView(idleFOV, transitionDuration * 0.8f).SetEase(transitionEase));
        seq.OnComplete(() => { if (enableMenuBob) StartMenuBob(); onComplete?.Invoke(); });
        seq.Play();
    }

    #endregion

    #region Gameplay Camera Mechanics

    public void SetCameraFollow(bool state) => enableCameraFollow = state;

    public void OnPlayerStartMoving()
    {
        StopAllCoroutines();
        SetCameraTarget(playerTransform);
        if (!enableDynamicZoom) TweenFOV(moveFOV);
    }

    public void OnPlayerStartAiming()
    {
        StopAllCoroutines();
        SetCameraTarget(playerTransform);
        SetDynamicZoom(false);
        TweenFOV(aimFOV);
    }

    public void OnPlayerStopAiming()
    {
        enableDynamicZoom = true;
        SetIdle();
    }

    public void SetIdle()
    {
        SetCameraTarget(playerTransform);
        if (!enableDynamicZoom) TweenFOV(idleFOV);
    }

    public void OnPlayerReachedPoint(MovementPoint point)
    {
        EnemyAI enemy = GetEnemyForPoint(point);
        if (enemy != null && !enemy.IsDead()) StartCoroutine(RevealRoutine(enemy.transform));
        else SetIdle();
    }

    private EnemyAI GetEnemyForPoint(MovementPoint point)
    {
        foreach (var link in manualLinks)
            if (link.point == point && link.targetEnemy != null) return link.targetEnemy;

        Collider[] hits = Physics.OverlapSphere(point.transform.position, autoDetectRadius, enemyLayer);
        float closestDist = float.MaxValue;
        EnemyAI closestEnemy = null;

        foreach (var hit in hits)
        {
            EnemyAI e = hit.GetComponent<EnemyAI>();
            if (e != null && !e.IsDead())
            {
                float dist = Vector3.Distance(point.transform.position, e.transform.position);
                if (dist < closestDist) { closestDist = dist; closestEnemy = e; }
            }
        }
        return closestEnemy;
    }

    private IEnumerator RevealRoutine(Transform enemyTransform)
    {
        SetDynamicZoom(false);
        SetCameraTarget(enemyTransform);
        TweenFOV(idleFOV);
        yield return new WaitForSeconds(revealDuration);
        enableDynamicZoom = true;
        SetCameraTarget(playerTransform);
    }

    private void SetCameraTarget(Transform target)
    {
        currentTarget = target;
        if (gameCameraAnchor != null && playerTransform != null)
            followOffset = gameCameraAnchor.position - playerTransform.position;
    }

    private void TweenFOV(float targetFOV)
    {
        fovTween?.Kill();
        fovTween = mainCamera.DOFieldOfView(targetFOV, zoomSpeed).SetEase(Ease.InOutQuad);
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
        Gizmos.DrawSphere(transform.position, autoDetectRadius);

        if (playerTransform != null)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(playerTransform.position, zoomStartDistance);

            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Gizmos.DrawWireSphere(playerTransform.position, zoomMaxDistance);
        }
    }

    #endregion
}