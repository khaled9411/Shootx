using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shootx.Audio
{
    /// <summary>
    /// High-performance AudioSource pooling system for 2D and 3D one-shot audio playback.
    /// Eliminates runtime allocations and garbage collection spikes caused by Instantiate/Destroy.
    /// Includes voice stealing and bounded safety limits.
    /// </summary>
    public class AudioSourcePool : MonoBehaviour
    {
        [Header("Pool Configuration")]
        [SerializeField] private int initial2DVoiceCount = 8;
        [SerializeField] private int initial3DVoiceCount = 16;
        [SerializeField] private int maxTotalVoiceCount = 48;

        private readonly Queue<AudioSource> _available2DSources = new Queue<AudioSource>();
        private readonly Queue<AudioSource> _available3DSources = new Queue<AudioSource>();
        private readonly List<PooledVoiceTracker> _activeVoices = new List<PooledVoiceTracker>();

        private Transform _pool2DRoot;
        private Transform _pool3DRoot;

        private class PooledVoiceTracker
        {
            public AudioSource source;
            public SoundDefinition soundDef;
            public Transform followTarget;
            public Coroutine returnCoroutine;
            public float startTime;
            public bool is3D;
        }

        public void Initialize(Transform parent)
        {
            transform.SetParent(parent, false);

            GameObject root2D = new GameObject("Pool_2D");
            root2D.transform.SetParent(transform, false);
            _pool2DRoot = root2D.transform;

            GameObject root3D = new GameObject("Pool_3D");
            root3D.transform.SetParent(transform, false);
            _pool3DRoot = root3D.transform;

            for (int i = 0; i < initial2DVoiceCount; i++)
            {
                CreateNewSource(is3D: false);
            }

            for (int i = 0; i < initial3DVoiceCount; i++)
            {
                CreateNewSource(is3D: true);
            }
        }

        private AudioSource CreateNewSource(bool is3D)
        {
            string name = is3D ? $"Pooled_3D_Source_{_available3DSources.Count + 1}" : $"Pooled_2D_Source_{_available2DSources.Count + 1}";
            Transform parent = is3D ? _pool3DRoot : _pool2DRoot;

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = is3D ? 1f : 0f;
            source.dopplerLevel = 0f; // Minimal mobile overhead

            go.SetActive(false);

            if (is3D)
            {
                _available3DSources.Enqueue(source);
            }
            else
            {
                _available2DSources.Enqueue(source);
            }

            return source;
        }

        /// <summary>
        /// Retrieves an available 2D AudioSource for flat audio (UI, announcer, music).
        /// </summary>
        public AudioSource Get2DSource(SoundDefinition definition)
        {
            return AcquireSource(is3D: false, Vector3.zero, null, definition);
        }

        /// <summary>
        /// Retrieves an available 3D AudioSource positioned at a specific world position.
        /// </summary>
        public AudioSource Get3DSource(Vector3 position, SoundDefinition definition)
        {
            return AcquireSource(is3D: true, position, null, definition);
        }

        /// <summary>
        /// Retrieves a 3D AudioSource that dynamically follows a moving target transform during playback.
        /// </summary>
        public AudioSource GetAttachedSource(Transform target, SoundDefinition definition)
        {
            Vector3 pos = target != null ? target.position : Vector3.zero;
            return AcquireSource(is3D: true, pos, target, definition);
        }

        private AudioSource AcquireSource(bool is3D, Vector3 position, Transform followTarget, SoundDefinition definition)
        {
            Queue<AudioSource> queue = is3D ? _available3DSources : _available2DSources;
            AudioSource source = null;

            while (queue.Count > 0)
            {
                var candidate = queue.Dequeue();
                if (candidate != null)
                {
                    source = candidate;
                    break;
                }
            }

            // Pool exhaustion handling
            if (source == null)
            {
                int currentTotal = _activeVoices.Count + _available2DSources.Count + _available3DSources.Count;
                if (currentTotal < maxTotalVoiceCount)
                {
                    source = CreateNewSource(is3D);
                    queue.Dequeue(); // Remove from queue since we're using it immediately
                }
                else
                {
                    // Voice stealing: steal oldest non-looping voice with lowest priority
                    source = StealVoice(is3D);
                }
            }

            if (source == null)
            {
                return null;
            }

            GameObject go = source.gameObject;
            go.transform.position = position;
            go.SetActive(true);

            // Configure tracker
            var tracker = new PooledVoiceTracker
            {
                source = source,
                soundDef = definition,
                followTarget = followTarget,
                startTime = Time.time,
                is3D = is3D
            };
            _activeVoices.Add(tracker);

            return source;
        }

        private AudioSource StealVoice(bool prefer3D)
        {
            PooledVoiceTracker oldestTracker = null;
            float oldestTime = float.MaxValue;

            for (int i = 0; i < _activeVoices.Count; i++)
            {
                var tracker = _activeVoices[i];
                if (tracker.source != null && !tracker.source.loop && tracker.startTime < oldestTime)
                {
                    oldestTime = tracker.startTime;
                    oldestTracker = tracker;
                }
            }

            if (oldestTracker != null)
            {
                RecycleTracker(oldestTracker);
                return oldestTracker.source;
            }

            return null;
        }

        /// <summary>
        /// Schedules the source to automatically return to the pool after the clip finishes playing.
        /// </summary>
        public void ScheduleAutoReturn(AudioSource source, AudioClip clip, float pitch, SoundDefinition definition)
        {
            var tracker = _activeVoices.Find(t => t.source == source);
            if (tracker == null) return;

            float duration = (clip != null && clip.length > 0f)
                ? (clip.length / Mathf.Max(0.1f, Mathf.Abs(pitch))) + 0.1f
                : 0.2f;

            if (tracker.returnCoroutine != null)
            {
                StopCoroutine(tracker.returnCoroutine);
            }

            tracker.returnCoroutine = StartCoroutine(ReturnAfterDelayCoroutine(tracker, duration));
        }

        private IEnumerator ReturnAfterDelayCoroutine(PooledVoiceTracker tracker, float delay)
        {
            yield return new WaitForSeconds(delay);
            RecycleTracker(tracker);
        }

        private void RecycleTracker(PooledVoiceTracker tracker)
        {
            if (tracker == null) return;

            if (tracker.returnCoroutine != null)
            {
                StopCoroutine(tracker.returnCoroutine);
                tracker.returnCoroutine = null;
            }

            if (tracker.soundDef != null)
            {
                tracker.soundDef.NotifyPlaybackFinished();
            }

            if (tracker.source != null)
            {
                ResetAudioSource(tracker.source, tracker.is3D);
                tracker.source.gameObject.SetActive(false);

                Transform parent = tracker.is3D ? _pool3DRoot : _pool2DRoot;
                tracker.source.transform.SetParent(parent, false);

                if (tracker.is3D)
                {
                    _available3DSources.Enqueue(tracker.source);
                }
                else
                {
                    _available2DSources.Enqueue(tracker.source);
                }
            }

            _activeVoices.Remove(tracker);
        }

        private void ResetAudioSource(AudioSource source, bool is3D)
        {
            source.Stop();
            source.clip = null;
            source.volume = 1f;
            source.pitch = 1f;
            source.loop = false;
            source.spatialBlend = is3D ? 1f : 0f;
            source.outputAudioMixerGroup = null;
            source.priority = 128;
            source.minDistance = 2f;
            source.maxDistance = 35f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
        }

        private void LateUpdate()
        {
            // Update positions for attached sources
            for (int i = _activeVoices.Count - 1; i >= 0; i--)
            {
                var tracker = _activeVoices[i];
                if (tracker.source == null)
                {
                    _activeVoices.RemoveAt(i);
                    continue;
                }

                if (tracker.followTarget != null)
                {
                    tracker.source.transform.position = tracker.followTarget.position;
                }
            }
        }

        /// <summary>
        /// Immediately stops and recycles all active audio voices (e.g. on match cleanup).
        /// </summary>
        public void StopAllActiveVoices()
        {
            for (int i = _activeVoices.Count - 1; i >= 0; i--)
            {
                RecycleTracker(_activeVoices[i]);
            }
            _activeVoices.Clear();
        }
    }
}
