using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Music Clips")]
    public AudioClip mainMenuMusic;
    public AudioClip gameplayMusic;

    [Header("SFX")]
    public AudioClip buttonClickSound;

    [Header("Transition Settings")]
    [SerializeField] private float fadeDuration = 1.5f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (mainMenuMusic != null)
        {
            musicSource.clip = mainMenuMusic;
            musicSource.loop = true;
            musicSource.volume = 0.3f;
            musicSource.Play();
        }

        AttachSoundToAllButtons();

        UIManager.OnTapToPlay += OnGameStarted;
    }

    private void OnDestroy()
    {
        UIManager.OnTapToPlay -= OnGameStarted;
    }

    private void OnGameStarted()
    {
        CrossFadeMusic(gameplayMusic);
    }

    public void OnReturnToMainMenu()
    {
        CrossFadeMusic(mainMenuMusic);
    }

    private void CrossFadeMusic(AudioClip newClip)
    {
        if (newClip == null) return;
        if (musicSource.clip == newClip && musicSource.isPlaying) return;

        musicSource.DOKill();

        musicSource.DOFade(0f, fadeDuration * 0.5f)
            .OnComplete(() =>
            {
                musicSource.Stop();
                musicSource.clip = newClip;
                musicSource.loop = true;
                musicSource.volume = 0f;
                musicSource.Play();

                musicSource.DOFade(0.3f, fadeDuration * 0.5f);
            });
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip != null)
            sfxSource.PlayOneShot(clip);
    }

    private void AttachSoundToAllButtons()
    {
        Button[] allButtons = Resources.FindObjectsOfTypeAll<Button>();
        foreach (Button btn in allButtons)
        {
            if (btn.gameObject.scene.IsValid())
            {
                btn.onClick.AddListener(() => PlaySFX(buttonClickSound));
            }
        }
    }

    public void SetSounds(bool enabled)
    {
        sfxSource.mute = !enabled;
    }

    public void SetMusic(bool enabled)
    {
        musicSource.mute = !enabled;
    }

    public void SetHaptic(bool enabled)
    {
        Debug.Log($"[Audio] Haptic: {enabled}");
    }
}