using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace Shootx.Audio
{
    /// <summary>
    /// Centralized, production-ready Audio Manager for Shootx.
    /// Manages AudioMixer routing, pooled 2D/3D spatial playback, user settings persistence,
    /// scene transition listeners, and background music crossfading.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        // ─── Singleton ──────────────────────────────────────────────
        private static AudioManager _instance;
        private static bool _isApplicationQuitting = false;

        public static AudioManager Instance
        {
            get
            {
                if (_isApplicationQuitting) return null;

                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<AudioManager>();
                    if (_instance == null)
                    {
                        GameObject prefab = Resources.Load<GameObject>("AudioManager");
                        if (prefab != null)
                        {
                            GameObject go = Instantiate(prefab);
                            go.name = "[AudioManager]";
                            _instance = go.GetComponent<AudioManager>();
                            DontDestroyOnLoad(go);
                        }
                        else
                        {
                            GameObject go = new GameObject("[AudioManager]");
                            _instance = go.AddComponent<AudioManager>();
                            DontDestroyOnLoad(go);
                        }
                    }
                }
                return _instance;
            }
        }

        // ─── Inspector Configuration ────────────────────────────────
        [Header("Audio Library Configuration")]
        [Tooltip("The central catalog defining all game sounds, clip variants, and 2D/3D settings.")]
        [SerializeField] private AudioLibrary audioLibrary;

        [Header("Audio Mixer & Groups")]
        [Tooltip("Master Unity AudioMixer with exposed volume parameters.")]
        [SerializeField] private AudioMixer audioMixer;

        [SerializeField] private AudioMixerGroup masterGroup;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private AudioMixerGroup weaponsGroup;
        [SerializeField] private AudioMixerGroup meleeGroup;
        [SerializeField] private AudioMixerGroup movementGroup;
        [SerializeField] private AudioMixerGroup dashGroup;
        [SerializeField] private AudioMixerGroup impactsGroup;
        [SerializeField] private AudioMixerGroup pickupsGroup;
        [SerializeField] private AudioMixerGroup environmentGroup;
        [SerializeField] private AudioMixerGroup uiGroup;
        [SerializeField] private AudioMixerGroup announcerGroup;

        [Header("Exposed Mixer Parameter Names")]
        [SerializeField] private string masterParam = "MasterVolume";
        [SerializeField] private string musicParam = "MusicVolume";
        [SerializeField] private string sfxParam = "SFXVolume";
        [SerializeField] private string uiParam = "UIVolume";
        [SerializeField] private string announcerParam = "AnnouncerVolume";

        // ─── Persistence Keys ───────────────────────────────────────
        private const string PrefKeyMaster = "Shootx_Audio_MasterVolume";
        private const string PrefKeyMusic = "Shootx_Audio_MusicVolume";
        private const string PrefKeySFX = "Shootx_Audio_SFXVolume";
        private const string PrefKeyUI = "Shootx_Audio_UIVolume";
        private const string PrefKeyAnnouncer = "Shootx_Audio_AnnouncerVolume";
        private const string PrefKeyMuted = "Shootx_Audio_Muted";

        // ─── Runtime State ──────────────────────────────────────────
        private AudioSourcePool _pool;
        private MusicController _musicController;
        private readonly HashSet<SoundId> _warnedMissingSounds = new HashSet<SoundId>();

        private float _masterVolume = 1f;
        private float _musicVolume = 0.8f;
        private float _sfxVolume = 1f;
        private float _uiVolume = 1f;
        private float _announcerVolume = 1f;
        private bool _isMuted = false;

        public AudioLibrary Library => audioLibrary;
        public AudioMixer Mixer => audioMixer;
        public MusicController Music => _musicController;
        public bool IsMuted => _isMuted;

        public float MasterVolume => _masterVolume;
        public float MusicVolume => _musicVolume;
        public float SFXVolume => _sfxVolume;
        public float UIVolume => _uiVolume;
        public float AnnouncerVolume => _announcerVolume;

        // ═══════════════════════════════════════════════════════════
        // Lifecycle
        // ═══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            InitializeSubsystems();
            LoadVolumePreferences();
            ApplyAllVolumesToMixer();

            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
                _instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            _isApplicationQuitting = true;
        }

        private void InitializeSubsystems()
        {
            // Audio Library auto-resolution fallback
            if (audioLibrary == null)
            {
                audioLibrary = Resources.Load<AudioLibrary>("GameAudioLibrary");
                if (audioLibrary == null)
                {
                    audioLibrary = Resources.Load<AudioLibrary>("Audio/GameAudioLibrary");
                }
            }

            if (audioLibrary != null)
            {
                audioLibrary.InitializeLookup();
            }

            // AudioMixer auto-resolution fallback
            if (audioMixer == null)
            {
                audioMixer = Resources.Load<AudioMixer>("GameAudioMixer");
                if (audioMixer == null)
                {
                    audioMixer = Resources.Load<AudioMixer>("Audio/GameAudioMixer");
                }
            }

            AutoResolveMixerGroups();

            // Pool initialization
            GameObject poolGO = new GameObject("AudioSourcePool");
            _pool = poolGO.AddComponent<AudioSourcePool>();
            _pool.Initialize(transform);

            // Music Controller initialization
            GameObject musicGO = new GameObject("MusicController");
            _musicController = musicGO.AddComponent<MusicController>();
            _musicController.Initialize(transform, musicGroup);
        }

        private void AutoResolveMixerGroups()
        {
            if (audioMixer == null) return;

            if (masterGroup == null) masterGroup = FindMixerGroup("Master");
            if (musicGroup == null) musicGroup = FindMixerGroup("Music");
            if (sfxGroup == null) sfxGroup = FindMixerGroup("SFX");
            if (uiGroup == null) uiGroup = FindMixerGroup("UI");
            if (announcerGroup == null) announcerGroup = FindMixerGroup("Announcer");
            if (weaponsGroup == null) weaponsGroup = FindMixerGroup("Weapons");
            if (meleeGroup == null) meleeGroup = FindMixerGroup("Melee");
            if (movementGroup == null) movementGroup = FindMixerGroup("Movement");
            if (dashGroup == null) dashGroup = FindMixerGroup("Dash");
            if (impactsGroup == null) impactsGroup = FindMixerGroup("Impacts");
            if (pickupsGroup == null) pickupsGroup = FindMixerGroup("Pickups");
            if (environmentGroup == null) environmentGroup = FindMixerGroup("Environment");
        }

        private AudioMixerGroup FindMixerGroup(string groupName)
        {
            if (audioMixer == null || string.IsNullOrEmpty(groupName)) return null;
            AudioMixerGroup[] matching = audioMixer.FindMatchingGroups(groupName);
            if (matching != null && matching.Length > 0)
            {
                for (int i = 0; i < matching.Length; i++)
                {
                    if (matching[i].name.Equals(groupName, StringComparison.OrdinalIgnoreCase))
                        return matching[i];
                }
                return matching[0];
            }
            return null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ValidateSingleAudioListener();
            AutoTriggerSceneMusic(scene.name);
        }

        /// <summary>
        /// Guarantees that only one active AudioListener exists in the scene.
        /// Resolves edge cases where multiple cameras or player prefabs contain duplicate listeners.
        /// </summary>
        public void ValidateSingleAudioListener()
        {
            AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            if (listeners == null || listeners.Length <= 1) return;

            bool keptOne = false;
            foreach (var listener in listeners)
            {
                if (listener == null) continue;

                // Prefer keeping AudioListener on MainCamera
                if (!keptOne && listener.CompareTag("MainCamera"))
                {
                    listener.enabled = true;
                    keptOne = true;
                }
                else if (!keptOne)
                {
                    listener.enabled = true;
                    keptOne = true;
                }
                else
                {
                    listener.enabled = false;
                    Debug.Log($"[AudioManager] Disabled duplicate AudioListener on: {listener.gameObject.name}");
                }
            }
        }

        /// <summary>
        /// Seamlessly transitions background music according to scene conventions.
        /// </summary>
        private void AutoTriggerSceneMusic(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;

            if (sceneName.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                PlayMusic(MusicTrackId.MainMenu, 1.0f);
            }
            else if (sceneName.IndexOf("Lobby", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                PlayMusic(MusicTrackId.Lobby, 1.0f);
            }
            else if (sceneName.IndexOf("Multiplayer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     sceneName.IndexOf("Main", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                PlayMusic(MusicTrackId.Gameplay, 1.2f);
            }
        }

        // ═══════════════════════════════════════════════════════════
        // Public Playback API
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Plays a sound in flat 2D space (ideal for UI, button clicks, notifications, and announcer cues).
        /// </summary>
        public AudioSource PlaySound2D(SoundId id, float volumeMultiplier = 1f, float pitchMultiplier = 1f)
        {
            if (!TryGetValidSound(id, out var def, out var clip)) return null;
            if (!def.CanPlay()) return null;

            AudioSource source = _pool.Get2DSource(def);
            if (source == null) return null;

            ConfigureAudioSource(source, def, clip, volumeMultiplier, pitchMultiplier, is3D: false);
            source.Play();

            def.NotifyPlaybackStarted();
            _pool.ScheduleAutoReturn(source, clip, source.pitch, def);
            return source;
        }

        /// <summary>
        /// Plays a sound positioned at a 3D world location (ideal for gunshots, impacts, remote footsteps, crates).
        /// </summary>
        public AudioSource PlaySound3D(SoundId id, Vector3 worldPosition, float volumeMultiplier = 1f, float pitchMultiplier = 1f)
        {
            if (!TryGetValidSound(id, out var def, out var clip)) return null;
            if (!def.CanPlay()) return null;

            AudioSource source = _pool.Get3DSource(worldPosition, def);
            if (source == null) return null;

            ConfigureAudioSource(source, def, clip, volumeMultiplier, pitchMultiplier, is3D: true);
            source.Play();

            def.NotifyPlaybackStarted();
            _pool.ScheduleAutoReturn(source, clip, source.pitch, def);
            return source;
        }

        /// <summary>
        /// Plays a sound that follows a moving GameObject throughout playback (e.g. dash trail, attached loops).
        /// </summary>
        public AudioSource PlaySoundAttached(SoundId id, Transform followTarget, float volumeMultiplier = 1f, float pitchMultiplier = 1f)
        {
            if (followTarget == null) return PlaySound2D(id, volumeMultiplier, pitchMultiplier);

            if (!TryGetValidSound(id, out var def, out var clip)) return null;
            if (!def.CanPlay()) return null;

            AudioSource source = _pool.GetAttachedSource(followTarget, def);
            if (source == null) return null;

            ConfigureAudioSource(source, def, clip, volumeMultiplier, pitchMultiplier, is3D: true);
            source.Play();

            def.NotifyPlaybackStarted();
            _pool.ScheduleAutoReturn(source, clip, source.pitch, def);
            return source;
        }

        private void ConfigureAudioSource(
            AudioSource source,
            SoundDefinition def,
            AudioClip clip,
            float volumeMultiplier,
            float pitchMultiplier,
            bool is3D)
        {
            source.clip = clip;
            source.volume = Mathf.Clamp01(def.volume * volumeMultiplier);
            source.pitch = def.GetRandomPitch() * pitchMultiplier;
            source.loop = def.loop;
            source.spatialBlend = is3D ? (def.is3D ? def.spatialBlend : 1f) : 0f;
            source.priority = def.priority;
            source.minDistance = def.minDistance;
            source.maxDistance = def.maxDistance;
            source.rolloffMode = def.rolloffMode;

            // Route to custom group or resolve from category
            source.outputAudioMixerGroup = def.customMixerGroup != null ? def.customMixerGroup : GetMixerGroupForCategory(def.category);
        }

        private bool TryGetValidSound(SoundId id, out SoundDefinition def, out AudioClip clip)
        {
            def = null;
            clip = null;

            if (id == SoundId.None) return false;

            if (audioLibrary == null)
            {
                WarnMissingSound(id, "AudioLibrary is not assigned on AudioManager!");
                return false;
            }

            if (!audioLibrary.TryGetSound(id, out def) || def == null)
            {
                WarnMissingSound(id, $"No sound definition configured for SoundId: {id}");
                return false;
            }

            clip = def.GetRandomClip();
            if (clip == null)
            {
                WarnMissingSound(id, $"Sound '{id}' has no AudioClips assigned in AudioLibrary!");
                return false;
            }

            return true;
        }

        private void WarnMissingSound(SoundId id, string message)
        {
#if UNITY_EDITOR
            if (_warnedMissingSounds.Add(id))
            {
                Debug.LogWarning($"[AudioManager] {message}");
            }
#endif
        }

        // ═══════════════════════════════════════════════════════════
        // Music API
        // ═══════════════════════════════════════════════════════════

        public void PlayMusic(MusicTrackId trackId, float fadeDuration = 1.0f)
        {
            if (_musicController == null || audioLibrary == null) return;

            if (audioLibrary.TryGetMusic(trackId, out var trackDef))
            {
                _musicController.PlayTrack(trackDef, fadeDuration);
            }
            else
            {
                _musicController.StopMusic(fadeDuration);
            }
        }

        public void StopMusic(float fadeDuration = 0.5f)
        {
            _musicController?.StopMusic(fadeDuration);
        }

        // ═══════════════════════════════════════════════════════════
        // Volume Control, AudioMixer Routing & Persistence
        // ═══════════════════════════════════════════════════════════

        public static float LinearToDecibels(float linear)
        {
            return linear > 0.0001f ? Mathf.Log10(linear) * 20f : -80f;
        }

        public static float DecibelsToLinear(float db)
        {
            return Mathf.Pow(10f, db / 20f);
        }

        public void SetMasterVolume(float linear)
        {
            _masterVolume = Mathf.Clamp01(linear);
            PlayerPrefs.SetFloat(PrefKeyMaster, _masterVolume);
            ApplyVolumeToMixer(masterParam, _isMuted ? 0f : _masterVolume);
        }

        public void SetMusicVolume(float linear)
        {
            _musicVolume = Mathf.Clamp01(linear);
            PlayerPrefs.SetFloat(PrefKeyMusic, _musicVolume);
            ApplyVolumeToMixer(musicParam, _isMuted ? 0f : _musicVolume);
        }

        public void SetSFXVolume(float linear)
        {
            _sfxVolume = Mathf.Clamp01(linear);
            PlayerPrefs.SetFloat(PrefKeySFX, _sfxVolume);
            ApplyVolumeToMixer(sfxParam, _isMuted ? 0f : _sfxVolume);
        }

        public void SetUIVolume(float linear)
        {
            _uiVolume = Mathf.Clamp01(linear);
            PlayerPrefs.SetFloat(PrefKeyUI, _uiVolume);
            ApplyVolumeToMixer(uiParam, _isMuted ? 0f : _uiVolume);
        }

        public void SetAnnouncerVolume(float linear)
        {
            _announcerVolume = Mathf.Clamp01(linear);
            PlayerPrefs.SetFloat(PrefKeyAnnouncer, _announcerVolume);
            ApplyVolumeToMixer(announcerParam, _isMuted ? 0f : _announcerVolume);
        }

        public void SetMuted(bool muted)
        {
            _isMuted = muted;
            PlayerPrefs.SetInt(PrefKeyMuted, _isMuted ? 1 : 0);
            ApplyAllVolumesToMixer();
        }

        public void ResetToDefaults()
        {
            _masterVolume = 1.0f;
            _musicVolume = 0.8f;
            _sfxVolume = 1.0f;
            _uiVolume = 1.0f;
            _announcerVolume = 1.0f;
            _isMuted = false;

            PlayerPrefs.SetFloat(PrefKeyMaster, _masterVolume);
            PlayerPrefs.SetFloat(PrefKeyMusic, _musicVolume);
            PlayerPrefs.SetFloat(PrefKeySFX, _sfxVolume);
            PlayerPrefs.SetFloat(PrefKeyUI, _uiVolume);
            PlayerPrefs.SetFloat(PrefKeyAnnouncer, _announcerVolume);
            PlayerPrefs.SetInt(PrefKeyMuted, 0);

            ApplyAllVolumesToMixer();
        }

        private void LoadVolumePreferences()
        {
            _masterVolume = PlayerPrefs.GetFloat(PrefKeyMaster, 1.0f);
            _musicVolume = PlayerPrefs.GetFloat(PrefKeyMusic, 0.8f);
            _sfxVolume = PlayerPrefs.GetFloat(PrefKeySFX, 1.0f);
            _uiVolume = PlayerPrefs.GetFloat(PrefKeyUI, 1.0f);
            _announcerVolume = PlayerPrefs.GetFloat(PrefKeyAnnouncer, 1.0f);
            _isMuted = PlayerPrefs.GetInt(PrefKeyMuted, 0) == 1;
        }

        private void ApplyAllVolumesToMixer()
        {
            ApplyVolumeToMixer(masterParam, _isMuted ? 0f : _masterVolume);
            ApplyVolumeToMixer(musicParam, _isMuted ? 0f : _musicVolume);
            ApplyVolumeToMixer(sfxParam, _isMuted ? 0f : _sfxVolume);
            ApplyVolumeToMixer(uiParam, _isMuted ? 0f : _uiVolume);
            ApplyVolumeToMixer(announcerParam, _isMuted ? 0f : _announcerVolume);
        }

        private void ApplyVolumeToMixer(string parameterName, float linearVolume)
        {
            if (audioMixer == null || string.IsNullOrEmpty(parameterName)) return;

            float db = LinearToDecibels(linearVolume);
            audioMixer.SetFloat(parameterName, db);
        }

        public AudioMixerGroup GetMixerGroupForCategory(SoundCategory category)
        {
            switch (category)
            {
                case SoundCategory.Master:
                    return masterGroup;
                case SoundCategory.Music:
                    return musicGroup;
                case SoundCategory.Weapons:
                    return weaponsGroup != null ? weaponsGroup : sfxGroup;
                case SoundCategory.Melee:
                    return meleeGroup != null ? meleeGroup : sfxGroup;
                case SoundCategory.Movement:
                    return movementGroup != null ? movementGroup : sfxGroup;
                case SoundCategory.Dash:
                    return dashGroup != null ? dashGroup : sfxGroup;
                case SoundCategory.Impacts:
                    return impactsGroup != null ? impactsGroup : sfxGroup;
                case SoundCategory.Pickups:
                    return pickupsGroup != null ? pickupsGroup : sfxGroup;
                case SoundCategory.Environment:
                    return environmentGroup != null ? environmentGroup : sfxGroup;
                case SoundCategory.UI:
                    return uiGroup;
                case SoundCategory.Announcer:
                    return announcerGroup != null ? announcerGroup : sfxGroup;
                case SoundCategory.SFX:
                default:
                    return sfxGroup;
            }
        }
    }
}
