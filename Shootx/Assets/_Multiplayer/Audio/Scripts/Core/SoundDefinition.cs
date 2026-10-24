using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Shootx.Audio
{
    /// <summary>
    /// Configurable data-driven definition of a game sound cue.
    /// Supports random clip selection without repetition, subtle pitch randomization,
    /// 2D/3D spatial settings, concurrency limits, and Mixer routing.
    /// </summary>
    [Serializable]
    public class SoundDefinition
    {
        [Header("Sound Identification")]
        [Tooltip("Type-safe identifier used by gameplay code to trigger this sound.")]
        public SoundId id = SoundId.None;

        [Tooltip("Descriptive name or notes for the sound designer.")]
        public string displayName = string.Empty;

        [Tooltip("Routing category determining default AudioMixerGroup routing.")]
        public SoundCategory category = SoundCategory.SFX;

        [Header("Audio Clips")]
        [Tooltip("One or more clip variants. If multiple are assigned, one will be chosen randomly on playback.")]
        public AudioClip[] clips = Array.Empty<AudioClip>();

        [Header("Volume & Pitch")]
        [Range(0f, 1f)]
        [Tooltip("Base playback volume (linear).")]
        public float volume = 1f;

        [Tooltip("Enable subtle pitch randomization for repetitive audio such as footsteps or impacts.")]
        public bool randomizePitch = false;

        [Range(0.5f, 1.5f)]
        public float minPitch = 0.95f;

        [Range(0.5f, 1.5f)]
        public float maxPitch = 1.05f;

        [Header("Spatial Audio (2D vs 3D)")]
        [Tooltip("Whether this sound is spatialized in 3D world space (1.0) or flat 2D (0.0).")]
        public bool is3D = false;

        [Range(0f, 1f)]
        [Tooltip("Spatial blend: 0 = Pure 2D (UI, Announcer), 1 = Pure 3D (World events).")]
        public float spatialBlend = 0f;

        [Tooltip("Distance in meters within which the sound is heard at full volume.")]
        public float minDistance = 2f;

        [Tooltip("Maximum audible distance in meters.")]
        public float maxDistance = 35f;

        [Tooltip("Attenuation curve type over distance.")]
        public AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

        [Header("Playback Behavior & Limits")]
        [Range(0, 256)]
        [Tooltip("AudioSource priority (0 = highest priority, 256 = lowest priority).")]
        public int priority = 128;

        [Tooltip("Maximum number of instances of this sound that can play simultaneously. 0 = unlimited.")]
        [Range(0, 16)]
        public int maxConcurrency = 0;

        [Tooltip("Minimum time (seconds) between successive triggers to avoid rapid duplicate spam.")]
        public float cooldown = 0.04f;

        [Tooltip("Whether this sound should loop continuously (e.g. ambient hum).")]
        public bool loop = false;

        [Header("AudioMixer Override (Optional)")]
        [Tooltip("Optional explicit mixer group. If null, AudioManager routes based on Category.")]
        public AudioMixerGroup customMixerGroup;

        // ─── Runtime Tracking State (non-serialized) ────────────
        [NonSerialized] private int _lastClipIndex = -1;
        [NonSerialized] private float _lastPlayedTime = -999f;
        [NonSerialized] private int _activeVoiceCount = 0;

        /// <summary>
        /// Returns true if the sound has at least one valid AudioClip assigned.
        /// </summary>
        public bool HasValidClips => clips != null && clips.Length > 0 && clips[0] != null;

        /// <summary>
        /// Checks if this sound is currently allowed to play (respects cooldown and concurrency limits).
        /// </summary>
        public bool CanPlay()
        {
            if (cooldown > 0f && Time.time - _lastPlayedTime < cooldown)
            {
                return false;
            }

            if (maxConcurrency > 0 && _activeVoiceCount >= maxConcurrency)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Registers that an instance of this sound started playing.
        /// </summary>
        public void NotifyPlaybackStarted()
        {
            _lastPlayedTime = Time.time;
            _activeVoiceCount++;
        }

        /// <summary>
        /// Registers that an instance of this sound finished playing.
        /// </summary>
        public void NotifyPlaybackFinished()
        {
            _activeVoiceCount = Mathf.Max(0, _activeVoiceCount - 1);
        }

        /// <summary>
        /// Selects an AudioClip randomly from the variants array, avoiding direct consecutive repeats when possible.
        /// </summary>
        public AudioClip GetRandomClip()
        {
            if (clips == null || clips.Length == 0) return null;
            if (clips.Length == 1) return clips[0];

            int count = clips.Length;
            int selectedIndex = UnityEngine.Random.Range(0, count);

            // Avoid immediate repetition if multiple variants exist
            if (selectedIndex == _lastClipIndex)
            {
                selectedIndex = (selectedIndex + 1) % count;
            }

            _lastClipIndex = selectedIndex;
            return clips[selectedIndex];
        }

        /// <summary>
        /// Calculates playback pitch based on randomization settings.
        /// </summary>
        public float GetRandomPitch()
        {
            if (!randomizePitch) return 1f;
            return UnityEngine.Random.Range(Mathf.Min(minPitch, maxPitch), Mathf.Max(minPitch, maxPitch));
        }

        /// <summary>
        /// Validates settings and logs warnings in Editor if critical fields are misconfigured.
        /// </summary>
        public void Validate()
        {
            if (id == SoundId.None)
            {
                Debug.LogWarning($"[SoundDefinition] Sound entry '{displayName}' has SoundId set to None!");
            }

            if (is3D && spatialBlend <= 0.01f)
            {
                spatialBlend = 1f;
            }

            if (minDistance > maxDistance)
            {
                float temp = minDistance;
                minDistance = maxDistance;
                maxDistance = temp;
            }

            if (minDistance <= 0f) minDistance = 0.5f;
            if (maxDistance <= minDistance) maxDistance = minDistance + 10f;
        }
    }
}
