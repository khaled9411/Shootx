using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

public class MultiplayerFollowCamera : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private CinemachineCamera followCamera;


    [Header("Settings")]
    [SerializeField] private int ownerPriority = 15;
    public override void OnNetworkSpawn()
    {

        if (IsOwner)
        {
            followCamera.Priority = ownerPriority;
        }
    }
}
