using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace Shootx.Audio
{
    /// <summary>
    /// Dedicated controller for smooth background music playback and seamless crossfading.
    /// Employs a dual-AudioSource setup to avoid clicks, pops, and abrupt restarts.
    /// </summary>
    public class MusicController : MonoBehaviour
    {
        private AudioSource _sourceA;
        private AudioSource _sourceB;
        private bool _isSourceAActive = true;

        private MusicTrackId _currentTrackId = MusicTrackId.None;
        private Coroutine _fadeCoroutine;

        public MusicTrackId CurrentTrackId => _currentTrackId;
        public bool IsPlaying => (_sourceA != null && _sourceA.isPlaying) || (_sourceB != null && _sourceB.isPlaying);

        public void Initialize(Transform parent, AudioMixerGroup musicGroup)
        {
            transform.SetParent(parent, false);

            GameObject goA = new GameObject("MusicSource_A");
            goA.transform.SetParent(transform, false);
            _sourceA = goA.AddComponent<AudioSource>();
            ConfigureSource(_sourceA, musicGroup);

            GameObject goB = new GameObject("MusicSource_B");
            goB.transform.SetParent(transform, false);
            _sourceB = goB.AddComponent<AudioSource>();
            ConfigureSource(_sourceB, musicGroup);
        }

        private void ConfigureSource(AudioSource source, AudioMixerGroup musicGroup)
        {
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f; // Pure 2D
            source.outputAudioMixerGroup = musicGroup;
            source.priority = 32; // Higher priority than SFX
            source.volume = 0f;
        }

        public void SetMixerGroup(AudioMixerGroup musicGroup)
        {
            if (_sourceA != null) _sourceA.outputAudioMixerGroup = musicGroup;
            if (_sourceB != null) _sourceB.outputAudioMixerGroup = musicGroup;
        }

        /// <summary>
        /// Plays the given music track with a smooth crossfade.
        /// If the same track is already playing, skips restarting to maintain continuity.
        /// </summary>
        public void PlayTrack(MusicTrackDefinition track, float fadeDuration = 1.0f)
        {
            if (track == null || track.clip == null)
            {
                StopMusic(fadeDuration);
                _currentTrackId = MusicTrackId.None;
                return;
            }

            // Already playing this exact track: do not restart
            if (_currentTrackId == track.trackId && IsPlaying)
            {
                return;
            }

            _currentTrackId = track.trackId;

            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }

            _fadeCoroutine = StartCoroutine(CrossfadeCoroutine(track, fadeDuration));
        }

        private IEnumerator CrossfadeCoroutine(MusicTrackDefinition newTrack, float duration)
        {
            AudioSource incoming = _isSourceAActive ? _sourceB : _sourceA;
            AudioSource outgoing = _isSourceAActive ? _sourceA : _sourceB;
            _isSourceAActive = !_isSourceAActive;

            incoming.clip = newTrack.clip;
            incoming.loop = newTrack.loop;
            incoming.volume = 0f;
            incoming.Play();

            float targetVolume = Mathf.Clamp01(newTrack.volume);
            float startOutgoingVol = outgoing.isPlaying ? outgoing.volume : 0f;
            float elapsed = 0f;

            if (duration <= 0.05f)
            {
                incoming.volume = targetVolume;
                outgoing.Stop();
                outgoing.volume = 0f;
                yield break;
            }

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                incoming.volume = Mathf.Lerp(0f, targetVolume, t);
                if (outgoing.isPlaying)
                {
                    outgoing.volume = Mathf.Lerp(startOutgoingVol, 0f, t);
                }

                yield return null;
            }

            incoming.volume = targetVolume;
            outgoing.Stop();
            outgoing.volume = 0f;
            outgoing.clip = null;
            _fadeCoroutine = null;
        }

        /// <summary>
        /// Smoothly fades out and stops any currently playing music track.
        /// </summary>
        public void StopMusic(float fadeDuration = 0.5f)
        {
            _currentTrackId = MusicTrackId.None;

            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }

            _fadeCoroutine = StartCoroutine(FadeOutCoroutine(fadeDuration));
        }

        private IEnumerator FadeOutCoroutine(float duration)
        {
            float elapsed = 0f;
            float startVolA = _sourceA.volume;
            float startVolB = _sourceB.volume;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (_sourceA.isPlaying) _sourceA.volume = Mathf.Lerp(startVolA, 0f, t);
                if (_sourceB.isPlaying) _sourceB.volume = Mathf.Lerp(startVolB, 0f, t);

                yield return null;
            }

            _sourceA.Stop();
            _sourceB.Stop();
            _sourceA.volume = 0f;
            _sourceB.volume = 0f;
            _sourceA.clip = null;
            _sourceB.clip = null;
            _fadeCoroutine = null;
        }
    }
}
