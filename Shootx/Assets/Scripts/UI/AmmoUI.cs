using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;

public class AmmoUI : MonoBehaviour
{
    public static AmmoUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private RectTransform iconsContainer;
    [SerializeField] private GameObject ammoIconPrefab;
    [SerializeField] private CanvasGroup panelCanvasGroup;

    [Header("Panel Background")]
    [SerializeField] private Image panelBackground;
    [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.45f);

    [Header("Icon Settings")]
    [SerializeField] private float iconSize = 60f;
    [SerializeField] private float iconSpacing = 10f;
    [SerializeField] private float introStaggerDelay = 0.08f;

    private AmmoIconItem[] _icons;
    private int _lastKnownAmmo;
    private int _maxAmmo;
    private bool _initialized;
    private ShootingSystem shootingSystem;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(1f);
         shootingSystem = FindAnyObjectByType<ShootingSystem>();

        if (shootingSystem != null)
        {
            _maxAmmo = shootingSystem.GetMaxAmmo();
            _lastKnownAmmo = shootingSystem.GetCurrentAmmo();

            BuildIcons();
            ApplyPanelStyle();
            PlayIntroSequence();

            _initialized = true;
        }


    }


    private void Update()
    {
        if (!_initialized || shootingSystem == null) return;

        int current = shootingSystem.GetCurrentAmmo();
        if (current == _lastKnownAmmo) return;

        if (current < _lastKnownAmmo)
        {
            ConsumeIcon(current);
        }
        else
        {
            ReturnIcon(current - 1);
        }

        _lastKnownAmmo = current;
    }

    private void BuildIcons()
    {
        foreach (Transform child in iconsContainer)
            Destroy(child.gameObject);

        _icons = new AmmoIconItem[_maxAmmo];

        for (int i = 0; i < _maxAmmo; i++)
        {
            GameObject go = Instantiate(ammoIconPrefab, iconsContainer);

            LayoutElement le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.preferredWidth = iconSize;
            le.preferredHeight = iconSize;
            le.minWidth = iconSize;
            le.minHeight = iconSize;

            AmmoIconItem item = go.GetComponent<AmmoIconItem>();
            if (item == null) item = go.AddComponent<AmmoIconItem>();

            item.Init();
            _icons[i] = item;
        }

        float totalHeight = _maxAmmo * (iconSize + iconSpacing) + 24f;
        iconsContainer.sizeDelta = new Vector2(iconSize + 16f, totalHeight);
    }

    private void PlayIntroSequence()
    {
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.DOFade(1f, 0.5f).SetDelay(0.1f);
        }

        for (int i = 0; i < _icons.Length; i++)
        {
            int reverseIndex = _maxAmmo - 1 - i;
            float delay = 0.15f + reverseIndex * introStaggerDelay;
            _icons[i].PlayIntroAnimation(delay);
        }
    }


    private void ConsumeIcon(int newAmmo)
    {
        if (newAmmo >= 0 && newAmmo < _icons.Length)
        {
            _icons[newAmmo].PlayConsumeAnimation();

            PunchPanel();
        }
    }

    private void ReturnIcon(int returnedIndex)
    {
        if (returnedIndex >= 0 && returnedIndex < _icons.Length)
        {
            _icons[returnedIndex].PlayReturnAnimation();
        }
    }

    public void OnReload()
    {
        for (int i = 0; i < _icons.Length; i++)
        {
            float delay = i * 0.06f;
            int idx = i;
            DOVirtual.DelayedCall(delay, () => _icons[idx].PlayReturnAnimation());
        }
        _lastKnownAmmo = _maxAmmo;
    }

    private void PunchPanel()
    {
        if (iconsContainer == null) return;
        iconsContainer.DOPunchScale(new Vector3(0.05f, 0.05f, 0f), 0.25f, 5, 0.5f);
    }

    private void ApplyPanelStyle()
    {
        if (panelBackground != null)
            panelBackground.color = panelColor;
    }

    private void OnDestroy()
    {
        DOTween.Kill(iconsContainer);
    }
}