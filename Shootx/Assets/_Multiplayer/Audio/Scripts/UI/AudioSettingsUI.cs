using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Shootx.Audio
{
    /// <summary>
    /// User interface panel controller for audio volume and mute settings.
    /// Manages Master, Music, SFX, UI, and Announcer sliders, real-time AudioMixer updates,
    /// and local persistence.
    /// </summary>
    public class AudioSettingsUI : MonoBehaviour
    {
        [Header("Panel Animation")]
        [SerializeField] private CanvasGroup panelCanvasGroup;
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private float fadeDuration = 0.25f;

        [Header("Volume Sliders")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Slider uiSlider;
        [SerializeField] private Slider announcerSlider;

        [Header("Toggles & Buttons")]
        [SerializeField] private Toggle muteToggle;
        [SerializeField] private Button resetDefaultsButton;
        [SerializeField] private Button closeButton;

        private Tween _fadeTween;
        private Tween _scaleTween;

        private void Start()
        {
            BindControls();
            RefreshUIValues();
        }

        private void OnEnable()
        {
            RefreshUIValues();
        }

        private void BindControls()
        {
            if (masterSlider != null)
            {
                masterSlider.onValueChanged.AddListener(OnMasterSliderChanged);
                EnsureSliderAudio(masterSlider);
            }

            if (musicSlider != null)
            {
                musicSlider.onValueChanged.AddListener(OnMusicSliderChanged);
                EnsureSliderAudio(musicSlider);
            }

            if (sfxSlider != null)
            {
                sfxSlider.onValueChanged.AddListener(OnSFXSliderChanged);
                EnsureSliderAudio(sfxSlider);
            }

            if (uiSlider != null)
            {
                uiSlider.onValueChanged.AddListener(OnUISliderChanged);
                EnsureSliderAudio(uiSlider);
            }

            if (announcerSlider != null)
            {
                announcerSlider.onValueChanged.AddListener(OnAnnouncerSliderChanged);
                EnsureSliderAudio(announcerSlider);
            }

            if (muteToggle != null)
            {
                muteToggle.onValueChanged.AddListener(OnMuteToggleChanged);
            }

            if (resetDefaultsButton != null)
            {
                resetDefaultsButton.onClick.AddListener(OnResetDefaultsClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(ClosePanel);
            }
        }

        private void EnsureSliderAudio(Slider slider)
        {
            if (!slider.TryGetComponent<SliderAudioFeedback>(out _))
            {
                slider.gameObject.AddComponent<SliderAudioFeedback>();
            }
        }

        /// <summary>
        /// Reads currently saved settings from AudioManager and aligns all sliders and toggles.
        /// </summary>
        public void RefreshUIValues()
        {
            var audioMgr = AudioManager.Instance;
            if (audioMgr == null) return;

            if (masterSlider != null) masterSlider.SetValueWithoutNotify(audioMgr.MasterVolume);
            if (musicSlider != null) musicSlider.SetValueWithoutNotify(audioMgr.MusicVolume);
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(audioMgr.SFXVolume);
            if (uiSlider != null) uiSlider.SetValueWithoutNotify(audioMgr.UIVolume);
            if (announcerSlider != null) announcerSlider.SetValueWithoutNotify(audioMgr.AnnouncerVolume);
            if (muteToggle != null) muteToggle.SetIsOnWithoutNotify(audioMgr.IsMuted);
        }

        private void OnMasterSliderChanged(float value)
        {
            AudioManager.Instance?.SetMasterVolume(value);
        }

        private void OnMusicSliderChanged(float value)
        {
            AudioManager.Instance?.SetMusicVolume(value);
        }

        private void OnSFXSliderChanged(float value)
        {
            AudioManager.Instance?.SetSFXVolume(value);
        }

        private void OnUISliderChanged(float value)
        {
            AudioManager.Instance?.SetUIVolume(value);
        }

        private void OnAnnouncerSliderChanged(float value)
        {
            AudioManager.Instance?.SetAnnouncerVolume(value);
        }

        private void OnMuteToggleChanged(bool isMuted)
        {
            AudioManager.Instance?.SetMuted(isMuted);
            AudioManager.Instance?.PlaySound2D(isMuted ? SoundId.UI_Toggle : SoundId.UI_ActionSuccess);
        }

        private void OnResetDefaultsClicked()
        {
            AudioManager.Instance?.ResetToDefaults();
            RefreshUIValues();
            AudioManager.Instance?.PlaySound2D(SoundId.UI_ActionSuccess);
        }

        // ═══════════════════════════════════════════════════════════
        // Show / Hide Animations
        // ═══════════════════════════════════════════════════════════

        public void OpenPanel()
        {
            gameObject.SetActive(true);
            RefreshUIValues();

            AudioManager.Instance?.PlaySound2D(SoundId.UI_PanelOpen);

            if (_fadeTween != null && _fadeTween.IsActive()) _fadeTween.Kill();
            if (_scaleTween != null && _scaleTween.IsActive()) _scaleTween.Kill();

            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = 0f;
                panelCanvasGroup.interactable = true;
                panelCanvasGroup.blocksRaycasts = true;
                _fadeTween = panelCanvasGroup.DOFade(1f, fadeDuration).SetUpdate(true);
            }

            if (panelRect != null)
            {
                panelRect.localScale = Vector3.one * 0.9f;
                _scaleTween = panelRect.DOScale(1f, fadeDuration).SetEase(Ease.OutBack).SetUpdate(true);
            }
        }

        public void ClosePanel()
        {
            AudioManager.Instance?.PlaySound2D(SoundId.UI_PanelClose);

            if (_fadeTween != null && _fadeTween.IsActive()) _fadeTween.Kill();
            if (_scaleTween != null && _scaleTween.IsActive()) _scaleTween.Kill();

            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.interactable = false;
                panelCanvasGroup.blocksRaycasts = false;
                _fadeTween = panelCanvasGroup.DOFade(0f, fadeDuration * 0.8f).SetUpdate(true);
            }

            if (panelRect != null)
            {
                _scaleTween = panelRect.DOScale(0.9f, fadeDuration * 0.8f)
                    .SetEase(Ease.InQuad)
                    .SetUpdate(true)
                    .OnComplete(() => gameObject.SetActive(false));
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
