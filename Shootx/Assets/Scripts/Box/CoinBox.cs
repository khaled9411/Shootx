using UnityEngine;
using DG.Tweening;

public class CoinBox : MonoBehaviour, IDamageable
{
    [Header("Coin Reward Settings")]
    [SerializeField] private int coinsPerHit = 10;
    [SerializeField] private int coinsUICount = 6;
    [SerializeField] private int maxHits = 3;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject hitVFX;
    [SerializeField] private AudioClip coinBoxSound;

    [Header("Box Destroy Settings")]
    [SerializeField] private float destroyDelay = 0.5f;
    [SerializeField] private Vector3 destroyPunchScale = new Vector3(0.3f, 0.3f, 0.3f);

    private int remainingHits;
    private bool isDead = false;

    private void Start()
    {
        remainingHits = maxHits;
    }

    public void TakeDamage(float damage, Vector3 hitDirection)
    {
        if (isDead) return;

        remainingHits--;

        if (hitVFX != null)
            Instantiate(hitVFX, transform.position, Quaternion.identity);

        if (AudioManager.Instance != null && coinBoxSound != null)
            AudioManager.Instance.PlaySFX(coinBoxSound);

        transform.DOKill();
        transform.DOPunchScale(destroyPunchScale, 0.25f, 5, 0.5f).SetUpdate(true);
        FindFirstObjectByType<ShootingSystem>()?.ReturnLastBullet();

        if (GameDataManager.Instance != null)
            GameDataManager.Instance.AddSoftCurrency(coinsPerHit);

        if (CoinUIEffect.Instance != null)
        {
            Vector3 spawnWorldPos = transform.position + Vector3.up * 0.5f;
            CoinUIEffect.Instance.PlayCoinEffect(spawnWorldPos, coinsUICount, coinsPerHit);
        }

        if (remainingHits <= 0)
            DestroyBox();
    }

    private void DestroyBox()
    {
        isDead = true;
        transform.DOKill();
        transform.DOScale(Vector3.zero, destroyDelay)
            .SetEase(Ease.InBack)
            .SetUpdate(true)
            .OnComplete(() => Destroy(gameObject));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, Vector3.one);
    }
}