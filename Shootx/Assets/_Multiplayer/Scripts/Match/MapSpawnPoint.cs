using UnityEngine;

/// <summary>
/// Marks a Transform as a valid spawn point for the selected PVP map.
/// Add this component to spawn point GameObjects inside each PVP Map prefab.
/// The MatchManager discovers these automatically.
/// </summary>
public class MapSpawnPoint : MonoBehaviour
{
    [Tooltip("Optional identifier for debugging.")]
    public string spawnId = "";

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0.3f, 0.5f);
        Gizmos.DrawSphere(transform.position, 0.4f);
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, transform.forward * 1.2f);
    }
}
