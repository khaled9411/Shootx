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

    // ===================================================================
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

    [Header("=== FOV Settings (Zoom - Top Down) ===")]
    [SerializeField] private float idleFOV = 60f;
    [SerializeField] private float moveFOV = 75f;
    [SerializeField] private float aimFOV = 40f;
    [SerializeField] private float zoomSpeed = 0.5f;

    [Header("=== Enemy Reveal Settings ===")]
    [SerializeField] private float revealDuration = 2f;
    [SerializeField] private float autoDetectRadius = 15f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("=== Level Setup ===")]
    [SerializeField] private List<PointEnemyLink> manualLinks;

    #endregion

    // ===================================================================
    #region Private State

    private Vector3 menuPos;
    private Quaternion menuRot;
    private Tween bobTween;
    private Tween fovTween;
    private bool transitioned = false;
    private Transform currentTarget;

    #endregion

    // ===================================================================
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

        if (enableMenuBob) StartMenuBob();
    }

    private void OnEnable()
    {
        UIManager.OnTapToPlay += TransitionToGameCamera;
    }

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
            Vector3 targetPosition = currentTarget.position + followOffset;
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, targetPosition, Time.deltaTime * followSpeed);
        }
    }

    #endregion

    // ===================================================================
    #region Menu Idle Bob & Game Transition

    private void StartMenuBob()
    {
        bobTween?.Kill();

        bobTween = mainCamera.transform
            .DOMove(menuPos + Vector3.up * bobAmplitude, bobDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
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
        {
            followOffset = gameCameraAnchor.position - playerTransform.position;
        }

        Sequence seq = DOTween.Sequence();

        seq.Append(mainCamera.transform.DOMove(gameCameraAnchor.position, transitionDuration).SetEase(transitionEase));
        seq.Join(mainCamera.transform.DORotateQuaternion(gameCameraAnchor.rotation, transitionDuration).SetEase(transitionEase));
        seq.Join(mainCamera.DOFieldOfView(idleFOV, transitionDuration).SetEase(transitionEase));

        seq.OnComplete(() =>
        {
            SetCameraTarget(playerTransform);
        });

        seq.Play();
    }

    public void ReturnToMenuCamera(System.Action onComplete = null)
    {
        transitioned = false;
        currentTarget = null;

        DOTween.Kill(mainCamera.transform);
        fovTween?.Kill();

        Sequence seq = DOTween.Sequence();
        seq.Append(mainCamera.transform.DOMove(menuPos, transitionDuration * 0.8f).SetEase(transitionEase));
        seq.Join(mainCamera.transform.DORotateQuaternion(menuRot, transitionDuration * 0.8f).SetEase(transitionEase));
        seq.Join(mainCamera.DOFieldOfView(idleFOV, transitionDuration * 0.8f).SetEase(transitionEase));

        seq.OnComplete(() =>
        {
            if (enableMenuBob) StartMenuBob();
            onComplete?.Invoke();
        });
        seq.Play();
    }

    #endregion

    // ===================================================================
    #region Gameplay Camera Mechanics (Replaced Cinemachine)

    public void SetCameraFollow(bool state)
    {
        enableCameraFollow = state;
    }

    public void OnPlayerStartMoving()
    {
        StopAllCoroutines();
        SetCameraTarget(playerTransform);
        TweenFOV(moveFOV);
    }

    public void OnPlayerStartAiming()
    {
        StopAllCoroutines();
        SetCameraTarget(playerTransform);
        TweenFOV(aimFOV);
    }

    public void OnPlayerStopAiming()
    {
        SetIdle();
    }

    public void SetIdle()
    {
        SetCameraTarget(playerTransform);
        TweenFOV(idleFOV);
    }

    public void OnPlayerReachedPoint(MovementPoint point)
    {
        EnemyAI enemyToReveal = GetEnemyForPoint(point);

        if (enemyToReveal != null && !enemyToReveal.IsDead())
        {
            StartCoroutine(RevealRoutine(enemyToReveal.transform));
        }
        else
        {
            SetIdle();
        }
    }

    private EnemyAI GetEnemyForPoint(MovementPoint point)
    {
        foreach (var link in manualLinks)
        {
            if (link.point == point && link.targetEnemy != null)
                return link.targetEnemy;
        }

        Collider[] hits = Physics.OverlapSphere(point.transform.position, autoDetectRadius, enemyLayer);
        float closestDist = float.MaxValue;
        EnemyAI closestEnemy = null;

        foreach (var hit in hits)
        {
            EnemyAI enemy = hit.GetComponent<EnemyAI>();
            if (enemy != null && !enemy.IsDead())
            {
                float dist = Vector3.Distance(point.transform.position, enemy.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestEnemy = enemy;
                }
            }
        }
        return closestEnemy;
    }

    private IEnumerator RevealRoutine(Transform enemyTransform)
    {
        SetCameraTarget(enemyTransform);
        TweenFOV(idleFOV);

        yield return new WaitForSeconds(revealDuration);

        SetIdle();
    }

    private void SetCameraTarget(Transform target)
    {
        currentTarget = target;
    }

    private void TweenFOV(float targetFOV)
    {
        fovTween?.Kill();

        fovTween = mainCamera.DOFieldOfView(targetFOV, zoomSpeed).SetEase(Ease.InOutQuad);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawSphere(transform.position, autoDetectRadius);
    }

    #endregion
}