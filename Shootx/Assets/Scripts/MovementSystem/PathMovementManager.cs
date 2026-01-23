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

    void Start()
    {
        if (autoSetupPoints)
        {
            SetupPointsAutomatically();
        }

        ValidatePoints();
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

        for (int i = 0; i < pathPoints.Count; i++)
        {
            if (pathPoints[i].PointIndex != i)
            {
                Debug.LogWarning($"The point at location {i} has Index = {pathPoints[i].PointIndex}. The order should be sequential!");
            }
        }
    }

    public void MoveToPoint(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= pathPoints.Count)
        {
            Debug.LogError($"Incorrect index: {targetIndex}");
            return;
        }

        int currentIndex = player.CurrentPointIndex;

        if (currentIndex == targetIndex)
        {
            Debug.Log("You are already at this point!");
            return;
        }

        movementQueue.Clear();

        if (targetIndex > currentIndex)
        {
            for (int i = currentIndex + 1; i <= targetIndex; i++)
            {
                movementQueue.Enqueue(i);
            }
        }

        else
        {
            for (int i = currentIndex - 1; i >= targetIndex; i--)
            {
                movementQueue.Enqueue(i);
            }
        }

        Debug.Log($"A path was created from {currentIndex} to {targetIndex} with a {movementQueue.Count} point count.");

        if (!isProcessingQueue)
        {
            ProcessNextPointInQueue();
        }
    }

    void ProcessNextPointInQueue()
    {
        if (movementQueue.Count == 0)
        {
            isProcessingQueue = false;
            UpdatePointsLockState();
            Debug.Log("I reached the goal!");
            return;
        }

        isProcessingQueue = true;

        int nextPointIndex = movementQueue.Dequeue();
        MovementPoint nextPoint = pathPoints[nextPointIndex];

        if (nextPoint != null)
        {
            player.MoveToPoint(nextPoint.transform.position, nextPointIndex);
        }
        else
        {
            Debug.LogError($"The point {nextPointIndex} does not exist!");
            isProcessingQueue = false;
        }
    }

    public void OnPointReached()
    {
        UpdatePointsLockState();

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

        for (int i = 0; i < pathPoints.Count; i++)
        {
            if (pathPoints[i] != null)
            {
                // Lock remote points (this can be modified as desired)

                // Currently: Allow all points
                pathPoints[i].SetLocked(false);

                // If you want to lock points that are very far away, use this:
                // bool isLocked = Mathf.Abs(i - currentIndex) > 1;
                // pathPoints[i].SetLocked(isLocked);
            }
        }
    }

    void OnDrawGizmos()
    {
        if (pathPoints == null || pathPoints.Count == 0) return;

        Gizmos.color = Color.cyan;

        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
            if (pathPoints[i] != null && pathPoints[i + 1] != null)
            {
                Gizmos.DrawLine(pathPoints[i].transform.position, pathPoints[i + 1].transform.position);
            }
        }
    }
}