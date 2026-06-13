using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using DG.Tweening;

public class BulletTimePPVolume : MonoBehaviour
{
    public static BulletTimePPVolume Instance { get; private set; }

    [SerializeField] private Volume ppVolume;
    [SerializeField] private Volume baseVolume;

    [Header("Vignette")]
    private float vignetteNormal;
    [SerializeField] private float vignetteFreeze = 0.55f;
    [SerializeField] private float vignetteDuration = 0.3f;

    [Header("Chromatic Aberration")]
    private float chromaticNormal;
    [SerializeField] private float chromaticShot = 1f;
    [SerializeField] private float chromaticFreeze = 0.35f;
    [SerializeField] private float chromaticDuration = 0.15f;

    [Header("Color Grading")]
    private float saturationNormal;
    [SerializeField] private float saturationFreeze = -40f;
    [SerializeField] private float saturationDuration = 0.3f;

    private Vignette vignette;
    private ChromaticAberration chromaticAberration;
    private ColorAdjustments colorAdjustments;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (ppVolume != null && ppVolume.profile != null)
        {
            ppVolume.profile.TryGet(out vignette);
            ppVolume.profile.TryGet(out chromaticAberration);
            ppVolume.profile.TryGet(out colorAdjustments);
        }

        if (baseVolume != null && baseVolume.profile != null)
        {
            if (baseVolume.profile.TryGet(out Vignette baseVignette))
                vignetteNormal = baseVignette.intensity.value;

            if (baseVolume.profile.TryGet(out ChromaticAberration baseCA))
                chromaticNormal = baseCA.intensity.value;

            if (baseVolume.profile.TryGet(out ColorAdjustments baseColor))
                saturationNormal = baseColor.saturation.value;
        }
    }

    public void OnFreezeStart()
    {
        if (vignette != null)
            DOTween.To(() => vignette.intensity.value,
                x => vignette.intensity.Override(x),
                vignetteFreeze, vignetteDuration).SetUpdate(true);

        if (chromaticAberration != null)
        {
            DOTween.To(() => chromaticAberration.intensity.value,
                x => chromaticAberration.intensity.Override(x),
                chromaticShot, chromaticDuration * 0.3f).SetUpdate(true)
                .OnComplete(() =>
                {
                    DOTween.To(() => chromaticAberration.intensity.value,
                        x => chromaticAberration.intensity.Override(x),
                        chromaticFreeze, chromaticDuration).SetUpdate(true);
                });
        }

        if (colorAdjustments != null)
            DOTween.To(() => colorAdjustments.saturation.value,
                x => colorAdjustments.saturation.Override(x),
                saturationFreeze, saturationDuration).SetUpdate(true);
    }

    public void OnFreezeEnd()
    {
        if (vignette != null)
            DOTween.To(() => vignette.intensity.value,
                x => vignette.intensity.Override(x),
                vignetteNormal, vignetteDuration).SetUpdate(true);

        if (chromaticAberration != null)
            DOTween.To(() => chromaticAberration.intensity.value,
                x => chromaticAberration.intensity.Override(x),
                chromaticNormal, chromaticDuration).SetUpdate(true);

        if (colorAdjustments != null)
            DOTween.To(() => colorAdjustments.saturation.value,
                x => colorAdjustments.saturation.Override(x),
                saturationNormal, saturationDuration).SetUpdate(true);
    }
}