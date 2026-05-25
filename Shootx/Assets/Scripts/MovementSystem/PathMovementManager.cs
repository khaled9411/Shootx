using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathMovementManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMovement player;
    [SerializeField] private List<MovementPoint> pathPoints = new List<MovementPoint>();

    [Header("Path Settings")]
    [SerializeField] private bool autoSetupPoints = true;

    private bool isMoving = false;

    [HideInInspector] public MovementPoint CurrentPoint;

    void Start()
    {
        if (autoSetupPoints)
            SetupPointsAutomatically();

        ValidatePoints();
        InitializePlayerAtStart();
    }

    void SetupPointsAutomatically()
    {
        MovementPoint[] allPoints = FindObjectsByType<MovementPoint>(0);
        System.Array.Sort(allPoints, (a, b) => a.PointIndex.CompareTo(b.PointIndex));
        pathPoints.Clear();
        pathPoints.AddRange(allPoints);
        Debug.Log($"{pathPoints.Count} points found automatically");
    }

    void ValidatePoints()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerMovement>();
            if (player == null)
            {
                Debug.LogError("PlayerMovement not found!");
                return;
            }
        }

        if (pathPoints.Count == 0)
            Debug.LogWarning("No points on path!");
    }

    void InitializePlayerAtStart()
    {
        if (pathPoints.Count > 0 && player != null)
        {
            CurrentPoint = pathPoints[0];
            player.transform.position = CurrentPoint.transform.position;
            CurrentPoint.OnPlayerReached();
        }
    }

    public void MoveToNeighbor(MovementPoint targetPoint)
    {
        if (isMoving || targetPoint == null) return;

        if (!IsNeighborOfCurrent(targetPoint))
        {
            Debug.LogWarning("Target is not a direct neighbor of current point!");
            return;
        }

        isMoving = true;

        CurrentPoint?.OnPlayerLeft();

        int targetIndex = pathPoints.IndexOf(targetPoint);

        player.MoveToPoint(targetPoint.transform.position, targetIndex);
    }

    bool IsNeighborOfCurrent(MovementPoint target)
    {
        if (CurrentPoint == null) return false;

        return target == CurrentPoint.neighborUp ||
               target == CurrentPoint.neighborDown ||
               target == CurrentPoint.neighborLeft ||
               target == CurrentPoint.neighborRight;
    }

    public void OnPointReached()
    {
        isMoving = false;

        int currentIndex = player.CurrentPointIndex;

        if (currentIndex >= 0 && currentIndex < pathPoints.Count)
        {
            CurrentPoint = pathPoints[currentIndex];
            CurrentPoint.OnPlayerReached();
        }
    }


    public MovementPoint GetNeighborUp() => CurrentPoint?.neighborUp;
    public MovementPoint GetNeighborDown() => CurrentPoint?.neighborDown;
    public MovementPoint GetNeighborLeft() => CurrentPoint?.neighborLeft;
    public MovementPoint GetNeighborRight() => CurrentPoint?.neighborRight;
}