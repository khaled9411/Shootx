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

    private Queue<int> movementQueue = new Queue<int>();
    private bool isProcessingQueue = false;

    [HideInInspector] public MovementPoint CurrentPoint;

    void Start()
    {
        if (autoSetupPoints)
        {
            SetupPointsAutomatically();
        }

        ValidatePoints();

        InitializePlayerAtStart();

        UpdatePointsLockState();
    }

    void SetupPointsAutomatically()
    {
        MovementPoint[] allPoints = FindObjectsByType<MovementPoint>(0);
        System.Array.Sort(allPoints, (a, b) => a.PointIndex.CompareTo(b.PointIndex));
        pathPoints.Clear();
        pathPoints.AddRange(allPoints);
        Debug.Log($"{pathPoints.Count} point was found automatically");
    }

    void ValidatePoints()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerMovement>();
            if (player == null)
            {
                Debug.LogError("PlayerMovement was not found!");
                return;
            }
        }

        if (pathPoints.Count == 0)
        {
            Debug.LogWarning("There are no points on the path!");
            return;
        }
    }

    void InitializePlayerAtStart()
    {
        if (pathPoints.Count > 0 && player != null)
        {
            MovementPoint startPoint = pathPoints[0];
            CurrentPoint = startPoint;

            //player.CurrentPointIndex = 0;

            player.transform.position = startPoint.transform.position;

            startPoint.OnPlayerReached();
        }
    }

    public void MoveToPoint(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= pathPoints.Count) return;

        int currentIndex = player.CurrentPointIndex;
        if (currentIndex == targetIndex) return;

        movementQueue.Clear();
        if (targetIndex > currentIndex)
        {
            for (int i = currentIndex + 1; i <= targetIndex; i++) movementQueue.Enqueue(i);
        }
        else
        {
            for (int i = currentIndex - 1; i >= targetIndex; i--) movementQueue.Enqueue(i);
        }

        if (!isProcessingQueue) ProcessNextPointInQueue();
    }

    void ProcessNextPointInQueue()
    {
        if (movementQueue.Count == 0)
        {
            isProcessingQueue = false;
            UpdatePointsLockState();
            return;
        }

        isProcessingQueue = true;
        int nextPointIndex = movementQueue.Dequeue();
        MovementPoint nextPoint = pathPoints[nextPointIndex];

        if (nextPoint != null)
        {
            player.MoveToPoint(nextPoint.transform.position, nextPointIndex);
        }
    }

    public void OnPointReached()
    {
        UpdatePointsLockState();

        if (pathPoints[player.CurrentPointIndex] != null)
        {
            pathPoints[player.CurrentPointIndex].OnPlayerReached();
        }

        if (movementQueue.Count > 0)
        {
            ProcessNextPointInQueue();
        }
        else
        {
            isProcessingQueue = false;
        }
    }

    void UpdatePointsLockState()
    {
        int currentIndex = player.CurrentPointIndex;
        CurrentPoint = pathPoints[currentIndex];
    }
}