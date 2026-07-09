using DG.Tweening;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CoinUIEffect : MonoBehaviour
{
    public static CoinUIEffect Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject coinIconPrefab;
    [SerializeField] private RectTransform targetIcon;
    [SerializeField] private Canvas parentCanvas;
    [SerializeField] private TextMeshProUGUI coinCounterText;

    [Header("Spawn Settings")]
    [SerializeField] private float spawnRadius = 50f;
    [SerializeField] private float spawnDelay = 0.06f;

    [Header("Travel Settings")]
    [SerializeField] private float travelDuration = 0.65f;
    [SerializeField] private Ease travelEase = Ease.InCubic;

    [Header("Coin Scale Animation")]
    [SerializeField] private float coinAppearScale = 1.2f;
    [SerializeField] private float coinAppearDuration = 0.15f;

    [Header("Target Icon Punch")]
    [SerializeField] private float punchScaleAmount = 0.35f;
    [SerializeField] private float punchDuration = 0.3f;

    [Header("Counter Text Punch")]
    [SerializeField] private float textPunchAmount = 0.25f;
    [SerializeField] private float textPunchDuration = 0.25f;

    private RectTransform canvasRect;
    private Camera uiCamera;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (parentCanvas == null)
            parentCanvas = FindFirstObjectByType<Canvas>();

        if (parentCanvas != null)
        {
            canvasRect = parentCanvas.GetComponent<RectTransform>();
            uiCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
        }
    }

    public void PlayCoinEffect(Vector3 worldPosition, int coinCount, int totalAmount)
    {
        if (coinIconPrefab == null || targetIcon == null || canvasRect == null)
        {
            Debug.LogWarning("[CoinUIEffect] Missing references!");
            return;
        }

        StartCoroutine(SpawnCoins(worldPosition, coinCount));
    }

    private IEnumerator SpawnCoins(Vector3 worldPosition, int coinCount)
    {
        Vector2 screenPos = Camera.main.WorldToScreenPoint(worldPosition);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, uiCamera, out Vector2 spawnAnchoredPos);
        Vector2 targetScreenPos = RectTransformUtility.WorldToScreenPoint(uiCamera, targetIcon.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, targetScreenPos, uiCamera, out Vector2 targetAnchoredPos);

        int arrivedCoins = 0;

        for (int i = 0; i < coinCount; i++)
        {
            GameObject coinObj = Instantiate(coinIconPrefab, canvasRect);
            RectTransform coinRect = coinObj.GetComponent<RectTransform>();

            coinRect.anchorMin = new Vector2(0.5f, 0.5f);
            coinRect.anchorMax = new Vector2(0.5f, 0.5f);

            Vector2 randomOffset = Random.insideUnitCircle * spawnRadius;
            coinRect.anchoredPosition = spawnAnchoredPos + randomOffset;
            coinRect.localScale = Vector3.zero;

            coinRect.DOScale(coinAppearScale, coinAppearDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (coinRect == null) return;

                    coinRect.DOAnchorPos(targetAnchoredPos, travelDuration)
                        .SetEase(travelEase)
                        .SetUpdate(true)
                        .OnComplete(() =>
                        {
                            arrivedCoins++;
                            PunchTargetIcon();

                            if (arrivedCoins >= coinCount)
                                PunchCoinCounter();

                            Destroy(coinObj);
                        });
                });

            yield return new WaitForSecondsRealtime(spawnDelay);
        }
    }

    private void PunchTargetIcon()
    {
        if (targetIcon == null) return;
        targetIcon.DOKill(true);
        targetIcon.localScale = Vector3.one;
        targetIcon.DOPunchScale(Vector3.one * punchScaleAmount, punchDuration, 5, 0.5f)
            .SetUpdate(true);
    }

    private void PunchCoinCounter()
    {
        if (coinCounterText == null) return;
        coinCounterText.transform.DOKill(true);
        coinCounterText.transform.localScale = Vector3.one;
        coinCounterText.transform
            .DOPunchScale(Vector3.one * textPunchAmount, textPunchDuration, 5, 0.5f)
            .SetUpdate(true);

        coinCounterText.DOColor(Color.yellow, textPunchDuration * 0.5f)
            .SetUpdate(true)
            .OnComplete(() =>
                coinCounterText.DOColor(Color.white, textPunchDuration * 0.5f).SetUpdate(true));
    }
}