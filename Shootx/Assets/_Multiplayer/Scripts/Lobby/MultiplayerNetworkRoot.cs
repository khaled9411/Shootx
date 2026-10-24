using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Ensures a single persistent NetworkManager + UnityTransport exists throughout
/// the multiplayer flow. Instantiated when entering the multiplayer menu and
/// persists through MultiplayerLobby → MainMultiplayer.
/// 
/// Place the "MultiplayerNetworkRoot" prefab in the MultiplayerMenu scene.
/// It will DontDestroyOnLoad itself and prevent duplicates.
/// </summary>
public class MultiplayerNetworkRoot : MonoBehaviour
{
    public static MultiplayerNetworkRoot Instance { get; private set; }

    private void Awake()
    {
        // Prevent duplicates
        if (Instance != null && Instance != this)
        {
            Debug.Log("[MPNetRoot] Duplicate detected. Destroying self.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        var nm = GetComponent<NetworkManager>();
        if (nm != null && nm.NetworkConfig != null)
        {
            nm.NetworkConfig.ConnectionApproval = true;
            nm.NetworkConfig.ForceSamePrefabs = false;
        }

        Debug.Log("[MPNetRoot] Initialized and set to persist.");
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Call this to completely destroy the persistent network root.
    /// Only use when fully leaving the multiplayer flow.
    /// </summary>
    public void DestroyRoot()
    {
        if (Instance == this)
            Instance = null;
        Destroy(gameObject);
    }
}
