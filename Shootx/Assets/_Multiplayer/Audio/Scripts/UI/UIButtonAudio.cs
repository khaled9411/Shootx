using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shootx.Audio
{
    /// <summary>
    /// Lightweight, reusable component attached to UI buttons to trigger audio feedback on click or hover.
    /// Preserves existing OnClick gameplay callbacks without interference.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public class UIButtonAudio : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
    {
        [Header("Sound Cues")]
        [Tooltip("Sound triggered when the button is successfully clicked.")]
        [SerializeField] private SoundId clickSound = SoundId.UI_ButtonClick;

        [Tooltip("Optional sound triggered on pointer hover (PC mouse only).")]
        [SerializeField] private SoundId hoverSound = SoundId.None;

        [Header("Volume Adjustments")]
        [Range(0f, 1f)]
        [SerializeField] private float volumeScale = 1f;

        private Selectable _selectable;

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
        }

        public void SetClickSound(SoundId sound)
        {
            clickSound = sound;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_selectable != null && !_selectable.interactable) return;
            if (clickSound == SoundId.None) return;

            AudioManager.Instance?.PlaySound2D(clickSound, volumeScale);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // Only trigger hover on non-touch (mouse) pointers
            if (eventData.pointerId < 0 && hoverSound != SoundId.None)
            {
                if (_selectable != null && !_selectable.interactable) return;
                AudioManager.Instance?.PlaySound2D(hoverSound, volumeScale * 0.7f);
            }
        }

        /// <summary>
        /// Helper utility to recursively add or configure UIButtonAudio components on all Buttons under a hierarchy.
        /// </summary>
        public static void RegisterHierarchy(Transform root, SoundId defaultSound = SoundId.UI_ButtonClick)
        {
            if (root == null) return;

            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                if (!btn.TryGetComponent<UIButtonAudio>(out var btnAudio))
                {
                    btnAudio = btn.gameObject.AddComponent<UIButtonAudio>();
                    btnAudio.clickSound = defaultSound;
                }
            }
        }
    }
}
