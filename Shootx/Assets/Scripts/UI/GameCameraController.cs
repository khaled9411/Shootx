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

    public enum CameraMode { Menu, IdleFollow, CombatTargeting }

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

    [Header("=== Combat Targeting Settings ===")]
    [SerializeField] private float revealMoveDuration = 1.0f;
    [SerializeField] private Ease revealMoveEase = Ease.InOutCubic;
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
    private Sequence transitionSeq;

    private bool transitioned = false;
    private Transform currentFollowTarget;

    // Combat State Variables
    private CameraMode currentMode = CameraMode.Menu;
    private List<EnemyAI> activeCombatTargets = new List<EnemyAI>();
    private MovementPoint currentActivePoint;
    private bool isTransitioningCombat = false;

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
        transitionSeq?.Kill();
    }

    private void LateUpdate()
    {
        if (!transitioned || currentFollowTarget == null || !enableCameraFollow) return;

        if (currentMode == CameraMode.IdleFollow)
        {
            Vector3 targetPos = currentFollowTarget.position + followOffset;
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, targetPos, Time.deltaTime * followSpeed);
        }
        else if (currentMode == CameraMode.CombatTargeting && !isTransitioningCombat)
        {
            activeCombatTargets.RemoveAll(e => e == null || e.IsDead());

            if (activeCombatTargets.Count == 0)
            {
                FindNewTargetsOrExit();
                return;
            }
            CalculateCombatFraming(out Vector3 targetCamPos, out Quaternion targetCamRot, out float requiredFOV);

            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, targetCamPos, Time.deltaTime * followSpeed);
            mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, targetCamRot, Time.deltaTime * followSpeed);
            mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, requiredFOV, Time.deltaTime * zoomSpeed);

            Vector3 centroid = CalculateCentroid(activeCombatTargets);
            Vector3 toEnemies = centroid - playerTransform.position;
            toEnemies.y = 0f;
            if (toEnemies.sqrMagnitude > 0.001f)
            {
                Quaternion playerTargetRot = Quaternion.LookRotation(toEnemies.normalized, Vector3.up);
                playerTransform.rotation = Quaternion.Slerp(playerTransform.rotation, playerTargetRot, Time.deltaTime * followSpeed * 2f);
            }
        }
    }

    #endregion

    #region Menu Bob & Game Transition

    private void StartMenuBob()
    {
        bobTween?.Kill();
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

        if (playerTransform != null && gameCameraAnchor != null)
            followOffset = gameCameraAnchor.position - playerTransform.position;

        currentMode = CameraMode.IdleFollow;
        SetFollowTarget(playerTransform);

        transitionSeq?.Kill();
        transitionSeq = DOTween.Sequence();
        transitionSeq.Append(mainCamera.transform.DOMove(gameCameraAnchor.position, transitionDuration).SetEase(transitionEase));
        transitionSeq.Join(mainCamera.transform.DORotateQuaternion(gameCameraAnchor.rotation, transitionDuration).SetEase(transitionEase));
        transitionSeq.Join(mainCamera.DOFieldOfView(idleFOV, transitionDuration).SetEase(transitionEase));
        transitionSeq.Play();

        //* Simulate player reaching the first point immediately after transition for demo purposes.
        OnPlayerReachedPoint(FindFirstObjectByType<PathMovementManager>().CurrentPoint);
    }

    public void ReturnToMenuCamera(Action onComplete = null)
    {
        transitioned = false;
        currentMode = CameraMode.Menu;
        currentFollowTarget = null;
        isTransitioningCombat = false;

        DOTween.Kill(mainCamera.transform);
        fovTween?.Kill();
        transitionSeq?.Kill();

        transitionSeq = DOTween.Sequence();
        transitionSeq.Append(mainCamera.transform.DOMove(menuPos, transitionDuration * 0.8f).SetEase(transitionEase));
        transitionSeq.Join(mainCamera.transform.DORotateQuaternion(menuRot, transitionDuration * 0.8f).SetEase(transitionEase));
        transitionSeq.Join(mainCamera.DOFieldOfView(idleFOV, transitionDuration * 0.8f).SetEase(transitionEase));
        transitionSeq.OnComplete(() =>
        {
            if (enableMenuBob) StartMenuBob();
            onComplete?.Invoke();
        });
        transitionSeq.Play();
    }

    #endregion

    #region Gameplay: Follow & FOV

    public void SetFollowTarget(Transform target) => currentFollowTarget = target;
    public void SetCameraFollow(bool state) => enableCameraFollow = state;

    public void OnPlayerStartMoving()
    {
        if (!transitioned || currentMode == CameraMode.CombatTargeting) return;
        TweenFOV(moveFOV);
    }

    public void OnPlayerStartAiming()
    {
        if (!transitioned || currentMode == CameraMode.CombatTargeting) return;
        TweenFOV(aimFOV);
    }

    public void OnPlayerStopAiming() => SetIdle();

    public void SetIdle()
    {
        if (!transitioned || currentMode == CameraMode.CombatTargeting) return;
        TweenFOV(idleFOV);
    }

    private void TweenFOV(float targetFOV)
    {
        fovTween?.Kill();
        fovTween = mainCamera.DOFieldOfView(targetFOV, zoomSpeed).SetEase(Ease.InOutQuad);
    }

    #endregion

    #region Combat Targeting System (New Reveal)

    public void OnPlayerReachedPoint(MovementPoint point)
    {
        if (!transitioned) return;

        currentActivePoint = point;
        List<EnemyAI> enemies = GetAliveEnemiesForPoint(point);

        if (enemies.Count > 0)
        {
            EngageTargets(enemies);
        }
        else
        {
            ExitCombatMode();
        }
    }

    private void EngageTargets(List<EnemyAI> targets)
    {
        activeCombatTargets = targets;
        currentMode = CameraMode.CombatTargeting;
        isTransitioningCombat = true;

        CalculateCombatFraming(out Vector3 targetCamPos, out Quaternion targetCamRot, out float requiredFOV);

        Vector3 centroid = CalculateCentroid(activeCombatTargets);
        Vector3 toEnemies = centroid - playerTransform.position;
        toEnemies.y = 0f;
        if (toEnemies.sqrMagnitude < 0.001f) toEnemies = playerTransform.forward;
        Quaternion playerTargetRot = Quaternion.LookRotation(toEnemies.normalized, Vector3.up);
        playerTransform.DORotateQuaternion(playerTargetRot, playerRotationDuration).SetEase(Ease.OutCubic);

        transitionSeq?.Kill();
        transitionSeq = DOTween.Sequence();
        transitionSeq.Append(mainCamera.transform.DOMove(targetCamPos, revealMoveDuration).SetEase(revealMoveEase));
        transitionSeq.Join(mainCamera.transform.DORotateQuaternion(targetCamRot, revealMoveDuration).SetEase(revealMoveEase));
        transitionSeq.Join(mainCamera.DOFieldOfView(requiredFOV, revealMoveDuration).SetEase(Ease.InOutQuad));
        transitionSeq.OnComplete(() =>
        {
            isTransitioningCombat = false;
        });
        transitionSeq.Play();
    }

    private void FindNewTargetsOrExit()
    {
        if (currentActivePoint != null)
        {
            List<EnemyAI> newEnemies = GetAliveEnemiesForPoint(currentActivePoint);
            if (newEnemies.Count > 0)
            {
                Debug.Log("[GameCamera] Switching to new target!");
                EngageTargets(newEnemies);
                return;
            }
        }

        ExitCombatMode();
    }

    public void ExitCombatMode()
    {
        if (currentMode != CameraMode.CombatTargeting) return;

        Debug.Log("[GameCamera] Exiting Combat Mode, returning to Follow");
        currentMode = CameraMode.IdleFollow;
        isTransitioningCombat = true;

        Vector3 returnPos = playerTransform.position + followOffset;
        Quaternion returnRot = gameCameraAnchor != null ? gameCameraAnchor.rotation : mainCamera.transform.rotation;

        transitionSeq?.Kill();
        transitionSeq = DOTween.Sequence();
        transitionSeq.Append(mainCamera.transform.DOMove(returnPos, returnDuration).SetEase(returnEase));
        transitionSeq.Join(mainCamera.transform.DORotateQuaternion(returnRot, returnDuration).SetEase(returnEase));
        transitionSeq.Join(mainCamera.DOFieldOfView(idleFOV, returnDuration).SetEase(Ease.InOutQuad));
        transitionSeq.OnComplete(() =>
        {
            isTransitioningCombat = false;
            SetIdle();
        });
        transitionSeq.Play();
    }

    private List<EnemyAI> GetAliveEnemiesForPoint(MovementPoint point)
    {
        List<EnemyAI> result = new List<EnemyAI>();

        foreach (var link in manualLinks)
        {
            if (link.point != point || link.enemies == null) continue;
            foreach (var e in link.enemies)
                if (e != null && !e.IsDead()) result.Add(e);
        }

        if (result.Count == 0)
        {
            Collider[] hits = Physics.OverlapSphere(point.transform.position, autoDetectRadius, enemyLayer);
            foreach (var hit in hits)
            {
                EnemyAI enemy = hit.GetComponent<EnemyAI>();
                if (enemy != null && !enemy.IsDead()) result.Add(enemy);
            }
        }
        return result;
    }

    #endregion

    #region Math Helpers

    private void CalculateCombatFraming(out Vector3 camPos, out Quaternion camRot, out float requiredFOV)
    {
        Vector3 playerPos = playerTransform.position;
        Vector3 enemyCentroid = CalculateCentroid(activeCombatTargets);

        Vector3 toEnemies = enemyCentroid - playerPos;
        toEnemies.y = 0f;
        if (toEnemies.sqrMagnitude < 0.001f) toEnemies = playerTransform.forward;
        Vector3 dirToEnemies = toEnemies.normalized;

        List<Vector3> allPositions = new List<Vector3> { playerPos };
        foreach (var e in activeCombatTargets)
        {
            allPositions.Add(e.transform.position);
        }

        Bounds subjectBounds = CalculateBounds(allPositions);
        camPos = CalculateCameraPosition(subjectBounds, dirToEnemies, out requiredFOV);
        camRot = CalculateCameraRotation(dirToEnemies);
        requiredFOV = Mathf.Clamp(requiredFOV, minRevealFOV, maxRevealFOV);
    }

    private Vector3 CalculateCentroid(List<EnemyAI> enemies)
    {
        if (enemies == null || enemies.Count == 0) return playerTransform.position;
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