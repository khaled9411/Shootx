using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shootx.Audio
{
    /// <summary>
    /// Configuration for a background music track.
    /// </summary>
    [Serializable]
    public class MusicTrackDefinition
    {
        public MusicTrackId trackId;
        public string title = string.Empty;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.75f;
        public bool loop = true;
    }

    /// <summary>
    /// Central ScriptableObject catalog containing all sound definitions and background music tracks.
    /// Easily editable in the Unity Inspector without requiring code modifications.
    /// </summary>
    [CreateAssetMenu(fileName = "GameAudioLibrary", menuName = "Shootx/Audio/Audio Library")]
    public class AudioLibrary : ScriptableObject
    {
        [Header("Sound Definitions")]
        [SerializeField] private List<SoundDefinition> sounds = new List<SoundDefinition>();

        [Header("Background Music Tracks")]
        [SerializeField] private List<MusicTrackDefinition> musicTracks = new List<MusicTrackDefinition>();

        // Fast runtime lookup table
        private readonly Dictionary<SoundId, SoundDefinition> _soundLookup = new Dictionary<SoundId, SoundDefinition>();
        private readonly Dictionary<MusicTrackId, MusicTrackDefinition> _musicLookup = new Dictionary<MusicTrackId, MusicTrackDefinition>();
        private bool _isInitialized = false;

        public IReadOnlyList<SoundDefinition> Sounds => sounds;
        public IReadOnlyList<MusicTrackDefinition> MusicTracks => musicTracks;

        private void OnEnable()
        {
            InitializeLookup();
        }

        /// <summary>
        /// Populates runtime dictionaries for fast O(1) sound retrieval.
        /// </summary>
        public void InitializeLookup()
        {
            _soundLookup.Clear();
            if (sounds != null)
            {
                foreach (var sound in sounds)
                {
                    if (sound == null || sound.id == SoundId.None) continue;

                    if (!_soundLookup.ContainsKey(sound.id))
                    {
                        _soundLookup.Add(sound.id, sound);
                    }
                    else
                    {
                        Debug.LogWarning($"[AudioLibrary] Duplicate SoundId detected in AudioLibrary: {sound.id}! Using first occurrence.");
                    }
                }
            }

            _musicLookup.Clear();
            if (musicTracks != null)
            {
                foreach (var track in musicTracks)
                {
                    if (track == null || track.trackId == MusicTrackId.None) continue;

                    if (!_musicLookup.ContainsKey(track.trackId))
                    {
                        _musicLookup.Add(track.trackId, track);
                    }
                }
            }

            _isInitialized = true;
        }

        /// <summary>
        /// Attempts to retrieve a sound definition by its type-safe SoundId.
        /// Returns false if not found.
        /// </summary>
        public bool TryGetSound(SoundId id, out SoundDefinition definition)
        {
            if (!_isInitialized || _soundLookup.Count == 0)
            {
                InitializeLookup();
            }

            return _soundLookup.TryGetValue(id, out definition);
        }

        /// <summary>
        /// Attempts to retrieve a background music track definition.
        /// </summary>
        public bool TryGetMusic(MusicTrackId trackId, out MusicTrackDefinition definition)
        {
            if (!_isInitialized || _musicLookup.Count == 0)
            {
                InitializeLookup();
            }

            return _musicLookup.TryGetValue(trackId, out definition);
        }

        /// <summary>
        /// Adds or updates a sound definition in the library. (Useful for Editor tools)
        /// </summary>
        public void SetSoundDefinition(SoundDefinition sound)
        {
            if (sound == null || sound.id == SoundId.None) return;

            int existingIndex = sounds.FindIndex(s => s != null && s.id == sound.id);
            if (existingIndex >= 0)
            {
                sounds[existingIndex] = sound;
            }
            else
            {
                sounds.Add(sound);
            }

            InitializeLookup();
        }

        /// <summary>
        /// Adds or updates a music track in the library.
        /// </summary>
        public void SetMusicTrack(MusicTrackDefinition track)
        {
            if (track == null || track.trackId == MusicTrackId.None) return;

            int existingIndex = musicTracks.FindIndex(m => m != null && m.trackId == track.trackId);
            if (existingIndex >= 0)
            {
                musicTracks[existingIndex] = track;
            }
            else
            {
                musicTracks.Add(track);
            }

            InitializeLookup();
        }

        /// <summary>
        /// Validates the library in the Editor and returns a list of missing required audio assets.
        /// </summary>
        public List<string> ValidateLibrary()
        {
            List<string> missingReports = new List<string>();

            // Check essential SoundIds
            foreach (SoundId id in Enum.GetValues(typeof(SoundId)))
            {
                if (id == SoundId.None) continue;

                if (!TryGetSound(id, out var def))
                {
                    missingReports.Add($"Missing Sound Definition entry: {id}");
                }
                else if (!def.HasValidClips)
                {
                    missingReports.Add($"Unassigned Audio Clips for Sound: {id} ({def.displayName})");
                }
            }

            // Check Music tracks
            foreach (MusicTrackId trackId in Enum.GetValues(typeof(MusicTrackId)))
            {
                if (trackId == MusicTrackId.None) continue;

                if (!TryGetMusic(trackId, out var trackDef))
                {
                    missingReports.Add($"Missing Music Track definition: {trackId}");
                }
                else if (trackDef.clip == null)
                {
                    missingReports.Add($"Unassigned Music Clip for Track: {trackId} ({trackDef.title})");
                }
            }

            return missingReports;
        }
    }
}
