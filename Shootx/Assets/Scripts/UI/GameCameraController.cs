using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

[Serializable]
public struct PointEnemyLink
{
    public MovementPoint point;
    public List<EnemyAI> enemies;
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

    [Header("=== FOV Settings ===")]
    [SerializeField] private float idleFOV = 60f;
    [SerializeField] private float moveFOV = 75f;
    [SerializeField] private float aimFOV = 40f;
    [SerializeField] private float zoomSpeed = 0.5f;

    [Header("=== Enemy Reveal Settings ===")]
    [SerializeField] private float revealMoveDuration = 1.0f;
    [SerializeField] private Ease revealMoveEase = Ease.InOutCubic;
    [SerializeField] private float revealHoldDuration = 2.0f;
    [SerializeField] private float returnDuration = 0.8f;
    [SerializeField] private Ease returnEase = Ease.InOutCubic;
    [SerializeField] private float autoDetectRadius = 15f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("=== Framing Settings ===")]
    [SerializeField] private float framingPadding = 2.5f;
    [SerializeField] private float cameraTiltAngle = 55f;
    [SerializeField] private float minRevealFOV = 35f;
    [SerializeField] private float maxRevealFOV = 85f;

    [Header("=== Player Rotation ===")]
    [SerializeField] private float playerRotationDuration = 0.35f;

    [Header("=== Level Setup ===")]
    [SerializeField] private List<PointEnemyLink> manualLinks;

    #endregion

    #region Private State

    private Vector3 menuPos;
    private Quaternion menuRot;
    private Vector3 followOffset;

    private Tween bobTween;
    private Tween fovTween;

    private bool transitioned = false;
    private bool isRevealing = false;

    private Transform currentFollowTarget;
    private Coroutine revealCoroutine;

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
            Debug.Log("[GameCamera] Positioned at menu anchor.");
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
        if (!transitioned || currentFollowTarget == null) return;
        if (!enableCameraFollow || isRevealing) return;

        Vector3 targetPos = currentFollowTarget.position + followOffset;
        mainCamera.transform.position = Vector3.Lerp(
            mainCamera.transform.position,
            targetPos,
            Time.deltaTime * followSpeed
        );
        Debug.Log($"[GameCamera] Following target at {targetPos}");
    }

    #endregion

    #region Menu Bob & Game Transition

    private void StartMenuBob()
    {
        bobTween?.Kill();

        Debug.Log("[GameCamera] Starting menu bobbing.");
        mainCamera.transform.position = menuPos;

        bobTween = mainCamera.transform
            .DOMoveY(menuPos.y + bobAmplitude, bobDuration)
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
            followOffset = gameCameraAnchor.position - playerTransform.position;

        Debug.Log("[GameCamera] Transitioning to gameplay camera.");
        Sequence seq = DOTween.Sequence();
        seq.Append(mainCamera.transform.DOMove(gameCameraAnchor.position, transitionDuration).SetEase(transitionEase));
        seq.Join(mainCamera.transform.DORotateQuaternion(gameCameraAnchor.rotation, transitionDuration).SetEase(transitionEase));
        seq.Join(mainCamera.DOFieldOfView(idleFOV, transitionDuration).SetEase(transitionEase));
        seq.OnComplete(() => SetFollowTarget(playerTransform));
        seq.Play();
    }

    public void ReturnToMenuCamera(Action onComplete = null)
    {
        transitioned = false;
        isRevealing = false;
        currentFollowTarget = null;

        if (revealCoroutine != null) StopCoroutine(revealCoroutine);
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

    #region Gameplay: Follow & FOV

    public void SetFollowTarget(Transform target)
    {
        currentFollowTarget = target;
    }

    public void SetCameraFollow(bool state) => enableCameraFollow = state;

    public void OnPlayerStartMoving()
    {
        if (!transitioned || isRevealing) return;
        SetFollowTarget(playerTransform);
        TweenFOV(moveFOV);
    }

    public void OnPlayerStartAiming()
    {
        if (!transitioned || isRevealing) return;
        SetFollowTarget(playerTransform);
        TweenFOV(aimFOV);
    }

    public void OnPlayerStopAiming() => SetIdle();

    public void SetIdle()
    {
        if (!transitioned || isRevealing) return;
        SetFollowTarget(playerTransform);
        TweenFOV(idleFOV);
    }

    private void TweenFOV(float targetFOV)
    {
        fovTween?.Kill();
        Debug.Log($"[GameCamera] Tweening FOV to {targetFOV}");
        fovTween = mainCamera.DOFieldOfView(targetFOV, zoomSpeed).SetEase(Ease.InOutQuad);
    }

    #endregion

    #region Enemy Reveal System

    public void OnPlayerReachedPoint(MovementPoint point)
    {

        if (!transitioned) return;

        List<EnemyAI> enemies = GetAliveEnemiesForPoint(point);

        if (enemies.Count == 0)
        {
            SetIdle();
            return;
        }

        if (revealCoroutine != null) StopCoroutine(revealCoroutine);
        revealCoroutine = StartCoroutine(RevealEnemiesRoutine(enemies));
    }

    private List<EnemyAI> GetAliveEnemiesForPoint(MovementPoint point)
    {
        List<EnemyAI> result = new List<EnemyAI>();

        foreach (var link in manualLinks)
        {
            if (link.point != point) continue;
            if (link.enemies == null) continue;
            foreach (var e in link.enemies)
                if (e != null && !e.IsDead()) result.Add(e);
        }

        if (result.Count == 0)
        {
            Collider[] hits = Physics.OverlapSphere(
                point.transform.position, autoDetectRadius, enemyLayer);
            foreach (var hit in hits)
            {
                EnemyAI enemy = hit.GetComponent<EnemyAI>();
                if (enemy != null && !enemy.IsDead())
                    result.Add(enemy);
            }
        }

        return result;
    }

    private IEnumerator RevealEnemiesRoutine(List<EnemyAI> enemies)
    {
        isRevealing = true;
        enableCameraFollow = false;

        Vector3 playerPos = playerTransform.position;
        Vector3 enemyCentroid = CalculateCentroid(enemies);

        Vector3 toEnemies = enemyCentroid - playerPos;
        toEnemies.y = 0f;
        if (toEnemies.sqrMagnitude < 0.001f) toEnemies = playerTransform.forward;
        Vector3 dirToEnemies = toEnemies.normalized;

        Quaternion playerTargetRot = Quaternion.LookRotation(dirToEnemies, Vector3.up);
        playerTransform.DORotateQuaternion(playerTargetRot, playerRotationDuration)
                       .SetEase(Ease.OutCubic);

        List<Vector3> allPositions = new List<Vector3> { playerPos };
        foreach (var e in enemies)
        {
            Transform[] patrolPoints = e.GetMovementPoints();

            if (patrolPoints != null && patrolPoints.Length > 0)
            {
                foreach (Transform pt in patrolPoints)
                {
                    if (pt != null)
                    {
                        allPositions.Add(pt.position);
                    }
                }
            }
            else
            {
                allPositions.Add(e.transform.position);
            }
        }

        Bounds subjectBounds = CalculateBounds(allPositions);

        float requiredFOV;
        Vector3 targetCamPos = CalculateCameraPosition(subjectBounds, dirToEnemies, out requiredFOV);
        Quaternion targetCamRot = CalculateCameraRotation(dirToEnemies);

        float clampedFOV = Mathf.Clamp(requiredFOV, minRevealFOV, maxRevealFOV);

        Debug.Log($"[GameCamera] Revealing enemies. TargetPos: {targetCamPos}, TargetRot: {targetCamRot.eulerAngles}, RequiredFOV: {requiredFOV}");
        Sequence revealSeq = DOTween.Sequence();
        revealSeq.Append(
            mainCamera.transform.DOMove(targetCamPos, revealMoveDuration).SetEase(revealMoveEase));
        revealSeq.Join(
            mainCamera.transform.DORotateQuaternion(targetCamRot, revealMoveDuration).SetEase(revealMoveEase));
        revealSeq.Join(
            mainCamera.DOFieldOfView(clampedFOV, revealMoveDuration).SetEase(Ease.InOutQuad));
        revealSeq.Play();

        yield return new WaitForSeconds(revealMoveDuration + revealHoldDuration);

        Vector3 returnPos = playerTransform.position + followOffset;
        Quaternion returnRot = gameCameraAnchor != null
                               ? gameCameraAnchor.rotation
                               : mainCamera.transform.rotation;

        Sequence returnSeq = DOTween.Sequence();
        returnSeq.Append(
            mainCamera.transform.DOMove(returnPos, returnDuration).SetEase(returnEase));
        returnSeq.Join(
            mainCamera.transform.DORotateQuaternion(returnRot, returnDuration).SetEase(returnEase));
        returnSeq.Join(
            mainCamera.DOFieldOfView(idleFOV, returnDuration).SetEase(Ease.InOutQuad));
        returnSeq.OnComplete(() =>
        {
            isRevealing = false;
            enableCameraFollow = true;
            SetFollowTarget(playerTransform);
        });
        returnSeq.Play();
    }

    #endregion

    #region Math Helpers

    private Vector3 CalculateCentroid(List<EnemyAI> enemies)
    {
        Vector3 sum = Vector3.zero;
        foreach (var e in enemies) sum += e.transform.position;
        return sum / enemies.Count;
    }

    private Bounds CalculateBounds(List<Vector3> positions)
    {
        Bounds b = new Bounds(positions[0], Vector3.zero);
        for (int i = 1; i < positions.Count; i++) b.Encapsulate(positions[i]);
        return b;
    }

    private Vector3 CalculateCameraPosition(Bounds bounds, Vector3 dirToEnemies, out float requiredFOV)
    {
        float boundsRadius = bounds.extents.magnitude + framingPadding;

        float halfFOVRad = idleFOV * 0.5f * Mathf.Deg2Rad;
        float requiredDist = boundsRadius / Mathf.Tan(halfFOVRad);
        requiredFOV = 2f * Mathf.Atan2(boundsRadius, requiredDist) * Mathf.Rad2Deg;

        float pitchRad = cameraTiltAngle * Mathf.Deg2Rad;
        float horizontalDist = requiredDist * Mathf.Cos(pitchRad);
        float verticalDist = requiredDist * Mathf.Sin(pitchRad);

        Vector3 cameraHorizDir = -dirToEnemies;
        Vector3 centroid = bounds.center;

        return centroid + cameraHorizDir * horizontalDist + Vector3.up * verticalDist;
    }

    private Quaternion CalculateCameraRotation(Vector3 dirToEnemies)
    {
        Vector3 right = Vector3.Cross(Vector3.up, dirToEnemies).normalized;
        Vector3 tiltedForward = Quaternion.AngleAxis(cameraTiltAngle, right) * dirToEnemies;
        return Quaternion.LookRotation(tiltedForward, Vector3.up);
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
        Vector3 gizmoCenter = Application.isPlaying && playerTransform != null
                              ? playerTransform.position
                              : transform.position;
        Gizmos.DrawSphere(gizmoCenter, autoDetectRadius);
    }

    #endregion
}