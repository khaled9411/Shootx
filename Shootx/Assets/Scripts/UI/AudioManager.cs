using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetHaptic(bool enabled)
    {
        Debug.Log($"[Audio] Haptic: {enabled}");
    }

    public void SetSounds(bool enabled)
    {
        Debug.Log($"[Audio] Sounds: {enabled}");
        AudioListener.volume = enabled ? 1f : 0f;
    }

    public void SetMusic(bool enabled)
    {
        Debug.Log($"[Audio] Music: {enabled}");
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, Vector3.zero);
    }
}