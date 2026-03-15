using System;
using UnityEngine;
using Unity.Cinemachine;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;

[Serializable]
public struct PointEnemyLink
{
    public MovementPoint point;
    public EnemyAI targetEnemy;
}

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    [Header("Cinemachine Setup")]
    [SerializeField] private CinemachineCamera mainCam;
    [SerializeField] private Transform playerTransform;

    [Header("FOV Settings (Zoom - Top Down)")]
    [SerializeField] private float idleFOV = 60f;
    [SerializeField] private float moveFOV = 75f;
    [SerializeField] private float aimFOV = 40f;
    [SerializeField] private float zoomSpeed = 0.5f;

    [Header("Enemy Reveal Settings")]
    [SerializeField] private float revealDuration = 2f;
    [SerializeField] private float autoDetectRadius = 15f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Level Setup")]
    [SerializeField] private List<PointEnemyLink> manualLinks;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void OnPlayerStartMoving()
    {
        StopAllCoroutines();
        SetCameraTarget(playerTransform);
        TweenFOV(moveFOV); // Zoom out
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

    public void SetIdle()
    {
        SetCameraTarget(playerTransform);
        TweenFOV(idleFOV);
    }

    public void OnPlayerStartAiming()
    {
        StopAllCoroutines();
        SetCameraTarget(playerTransform);
        TweenFOV(aimFOV); // Zoom in
    }

    public void OnPlayerStopAiming()
    {
        SetIdle();
    }

    private void SetCameraTarget(Transform target)
    {
        mainCam.Follow = target;
        // mainCam.LookAt = target; 
    }

    private void TweenFOV(float targetFOV)
    {
        DOTween.To(() => mainCam.Lens.FieldOfView, x =>
        {
            var lens = mainCam.Lens;
            lens.FieldOfView = x;
            mainCam.Lens = lens;
        }, targetFOV, zoomSpeed).SetEase(Ease.InOutQuad);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawSphere(transform.position, autoDetectRadius);
    }
}