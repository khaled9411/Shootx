using UnityEngine;

namespace Shootx.Audio
{
    /// <summary>
    /// Audio hook for ammunition pickups.
    /// Distinguishes between 3D world collection sound heard by observers
    /// and private tactile confirmation heard by the collector.
    /// </summary>
    public class AmmoPickupAudio : MonoBehaviour
    {
        [SerializeField] private SoundId worldCollectSound = SoundId.Pickup_Ammo_World;
        [SerializeField] private SoundId localFeedbackSound = SoundId.Pickup_Ammo_Local;

        public void PlayWorldCollectAudio(Vector3 pickupPosition)
        {
            if (worldCollectSound != SoundId.None)
            {
                AudioManager.Instance?.PlaySound3D(worldCollectSound, pickupPosition, 0.9f);
            }
        }

        public void PlayLocalCollectorFeedback()
        {
            if (localFeedbackSound != SoundId.None)
            {
                AudioManager.Instance?.PlaySound2D(localFeedbackSound, 1.0f);
            }
        }
    }
}
