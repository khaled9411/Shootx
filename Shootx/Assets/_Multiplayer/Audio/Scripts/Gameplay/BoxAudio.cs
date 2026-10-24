using UnityEngine;

namespace Shootx.Audio
{
    /// <summary>
    /// Audio hook for destructible crates and boxes.
    /// Plays 3D spatial sounds for bullet/melee impacts and authoritative crate destruction.
    /// </summary>
    public class BoxAudio : MonoBehaviour
    {
        [SerializeField] private SoundId impactSound = SoundId.Impact_DestructibleBox;
        [SerializeField] private SoundId destroySound = SoundId.Box_Destroyed;

        public void PlayImpactSound(Vector3 hitPoint)
        {
            if (impactSound != SoundId.None)
            {
                AudioManager.Instance?.PlaySound3D(impactSound, hitPoint, 0.85f);
            }
        }

        public void PlayDestroySound(Vector3 cratePosition)
        {
            if (destroySound != SoundId.None)
            {
                AudioManager.Instance?.PlaySound3D(destroySound, cratePosition, 1.0f);
            }
        }
    }
}
