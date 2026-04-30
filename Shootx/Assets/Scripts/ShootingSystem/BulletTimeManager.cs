using UnityEngine;
using DG.Tweening;

public class BulletTimeManager : MonoBehaviour
{
    public static BulletTimeManager Instance { get; private set; }

    [SerializeField] private float frozenTimeScale = 0f;
    [SerializeField] private float freezeTransitionTime = 0.08f;
    [SerializeField] private float resumeTransitionTime = 0.25f;

    public bool IsFrozen { get; private set; } = false;
    private int activeFrozenBullets = 0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void StartBulletFreeze()
    {
        activeFrozenBullets++;
        if (IsFrozen) return;

        IsFrozen = true;

        DOTween.To(() => Time.timeScale, x =>
        {
            Time.timeScale = x;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
        }, frozenTimeScale, freezeTransitionTime).SetUpdate(true);

        BulletTimePPVolume.Instance?.OnFreezeStart();
    }

    public void EndBulletFreeze()
    {
        activeFrozenBullets = Mathf.Max(0, activeFrozenBullets - 1);
        if (activeFrozenBullets > 0 || !IsFrozen) return;

        IsFrozen = false;

        DOTween.To(() => Time.timeScale, x =>
        {
            Time.timeScale = x;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
        }, 1f, resumeTransitionTime).SetUpdate(true);

        BulletTimePPVolume.Instance?.OnFreezeEnd();
    }

    void OnDestroy()
    {
        if (IsFrozen) { Time.timeScale = 1f; Time.fixedDeltaTime = 0.02f; }
    }
}