using UnityEngine;
using UnityEngine.UI;

namespace Shootx.Audio
{
    /// <summary>
    /// Provides subtle, throttled audio feedback when adjusting UI sliders (e.g. volume controls).
    /// Prevents sound spamming while smoothly sliding.
    /// </summary>
    [RequireComponent(typeof(Slider))]
    public class SliderAudioFeedback : MonoBehaviour
    {
        [SerializeField] private SoundId tickSound = SoundId.UI_SliderTick;
        [SerializeField] private float minInterval = 0.08f;
        [SerializeField] private float stepThreshold = 0.05f;

        private Slider _slider;
        private float _lastPlayedTime = -1f;
        private float _lastSoundValue = -1f;

        private void Awake()
        {
            _slider = GetComponent<Slider>();
            if (_slider != null)
            {
                _lastSoundValue = _slider.value;
                _slider.onValueChanged.AddListener(HandleValueChanged);
            }
        }

        private void OnDestroy()
        {
            if (_slider != null)
            {
                _slider.onValueChanged.RemoveListener(HandleValueChanged);
            }
        }

        private void HandleValueChanged(float value)
        {
            if (Time.unscaledTime - _lastPlayedTime < minInterval) return;
            if (Mathf.Abs(value - _lastSoundValue) < stepThreshold) return;

            _lastPlayedTime = Time.unscaledTime;
            _lastSoundValue = value;

            if (tickSound != SoundId.None)
            {
                AudioManager.Instance?.PlaySound2D(tickSound, 0.4f);
            }
        }
    }
}
