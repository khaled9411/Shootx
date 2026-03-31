using UnityEngine;
using UnityEngine.UI;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Common Audio Clips")]
    public AudioClip backgroundMusic;
    public AudioClip buttonClickSound;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (backgroundMusic != null)
        {
            PlayMusic(backgroundMusic);
        }

        AttachSoundToAllButtons();
    }

    public void PlayMusic(AudioClip clip)
    {
        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.Play();
    }

    public void PlaySFX(AudioClip clip)
    {
        sfxSource.PlayOneShot(clip);
    }

    private void AttachSoundToAllButtons()
    {
        Button[] allButtons = Resources.FindObjectsOfTypeAll<Button>();

        foreach (Button btn in allButtons)
        {
            if (btn.gameObject.scene.name != null)
            {
                btn.onClick.AddListener(() => PlaySFX(buttonClickSound));
            }
        }
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

}