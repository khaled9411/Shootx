using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class GunsPanelController : MonoBehaviour
{
    // ===================================================================
    #region Data Types
    // ===================================================================

    public enum GunCategory
    {
        Pistols, SubMachineGuns, AutomaticGuns,
        SuperGuns, UltimateGuns, BaseGuns, BundleGuns, VIPGuns
    }

    [System.Serializable]
    public class GunButtonUI
    {
        public Button button;
        public Image gunImage;
        public Image selectionHighlight;
        public int gunId;
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Inspector Fields - Info Panel (Top Left)
    // ===================================================================

    [Header("=== Weapon Info Panel ===")]
    [SerializeField] private TextMeshProUGUI weaponNameText;
    [SerializeField] private TextMeshProUGUI damageValueText;
    [SerializeField] private TextMeshProUGUI magazineValueText;
    [SerializeField] private TextMeshProUGUI fireTypeText;
    [SerializeField] private Image fireTypeIcon;
    [SerializeField] private TextMeshProUGUI skinIndexText;      // "SKIN 1"
    [SerializeField] private Button skinPrevButton;
    [SerializeField] private Button skinNextButton;

    [Header("=== Character Preview (Top Right) ===")]
    [SerializeField] private Transform characterHolder;

    [Header("=== Category Navigation (Top) ===")]
    [SerializeField] private TextMeshProUGUI categoryNameText;
    [SerializeField] private List<Image> categoryIndicators;
    [SerializeField] private Color activeIndicatorColor = Color.yellow;
    [SerializeField] private Color inactiveIndicatorColor = Color.gray;

    [Header("=== Navigation Arrows ===")]
    [SerializeField] private Button prevCategoryButton;
    [SerializeField] private Button nextCategoryButton;

    [Header("=== Guns Grid ===")]
    [SerializeField] private Transform gunsGridParent;
    [SerializeField] private GameObject gunButtonPrefab;

    [Header("=== Back Button ===")]
    [SerializeField] private Button backButton;

    [Header("=== Animation Roots ===")]
    [SerializeField] private CanvasGroup infoGroup;
    [SerializeField] private CanvasGroup characterGroup;
    [SerializeField] private CanvasGroup categoryGroup;
    [SerializeField] private CanvasGroup gridGroup;

    // ===================================================================
    #endregion

    // ===================================================================
    #region Private State
    // ===================================================================

    private GunCategory currentCategory = GunCategory.Pistols;
    private int currentSkinIndex = 0;
    private int selectedGunId = -1;
    private List<GunButtonUI> spawnedButtons = new List<GunButtonUI>();

    private static readonly string[] CategoryNames =
    {
        "PISTOLS", "SUBMACHINE GUNS", "AUTOMATIC GUNS", "SUPER GUNS",
        "ULTIMATE GUNS", "BASE GUNS", "BUNDLE GUNS", "VIP GUNS"
    };

    // ===================================================================
    #endregion

    // ===================================================================
    #region Unity Lifecycle
    // ===================================================================

    private void Start()
    {
        backButton?.onClick.AddListener(UIManager.Instance.HideGunsPanel);
        prevCategoryButton?.onClick.AddListener(GoToPrevCategory);
        nextCategoryButton?.onClick.AddListener(GoToNextCategory);
        skinPrevButton?.onClick.AddListener(PrevSkin);
        skinNextButton?.onClick.AddListener(NextSkin);
    }

    private void OnEnable()
    {
        PlayShowAnimation();
        LoadCurrentCategory();
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Show / Hide Animation
    // ===================================================================

    private void PlayShowAnimation()
    {
        Sequence seq = DOTween.Sequence();

        void FadeIn(CanvasGroup g, float delay, float slideY = 0f)
        {
            if (g == null) return;
            g.alpha = 0f;
            if (slideY != 0f) g.transform.localPosition += Vector3.up * slideY;

            seq.Insert(delay, g.DOFade(1f, 0.35f));
            if (slideY != 0f)
                seq.Insert(delay, g.transform.DOLocalMoveY(
                    g.transform.localPosition.y - slideY, 0.35f).SetEase(Ease.OutCubic));
        }

        //FadeIn(infoGroup, 0.00f, infoGroup.gameObject.transform.localPosition.y);
        FadeIn(characterGroup, 0.08f, 25f);
        FadeIn(categoryGroup, 0.16f, 0f);
        FadeIn(gridGroup, 0.24f, -20f);
    }

    public void PlayHideAnimation(System.Action onComplete)
    {
        Sequence seq = DOTween.Sequence();

        void FadeOut(CanvasGroup g, float delay)
        {
            if (g == null) return;
            seq.Insert(delay, g.DOFade(0f, 0.25f));
        }

        FadeOut(gridGroup, 0.00f);
        FadeOut(categoryGroup, 0.06f);
        FadeOut(characterGroup, 0.10f);
        FadeOut(infoGroup, 0.10f);

        seq.OnComplete(() => onComplete?.Invoke());
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Category Navigation
    // ===================================================================

    private void GoToPrevCategory()
    {
        if (currentCategory == 0) return;
        currentCategory--;
        OnCategoryChanged();
    }

    private void GoToNextCategory()
    {
        if ((int)currentCategory >= CategoryNames.Length - 1) return;
        currentCategory++;
        OnCategoryChanged();
    }

    private void OnCategoryChanged()
    {
        UpdateCategoryUI();
        LoadCurrentCategory();
    }

    private void UpdateCategoryUI()
    {
        int idx = (int)currentCategory;

        if (categoryNameText != null)
            categoryNameText.text = CategoryNames[idx];

        for (int i = 0; i < categoryIndicators.Count; i++)
        {
            if (categoryIndicators[i] == null) continue;
            bool active = i == idx;
            categoryIndicators[i].color = active ? activeIndicatorColor : inactiveIndicatorColor;
            categoryIndicators[i].transform.DOScale(active ? 1.2f : 1f, 0.2f);
        }

        if (prevCategoryButton != null)
        {
            prevCategoryButton.gameObject.SetActive(idx > 0);
        }
        if (nextCategoryButton != null)
        {
            nextCategoryButton.gameObject.SetActive(idx < CategoryNames.Length - 1);
        }
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Grid Population
    // ===================================================================

    private void LoadCurrentCategory()
    {
        UpdateCategoryUI();

        foreach (var b in spawnedButtons)
            if (b.button != null) Destroy(b.button.gameObject);
        spawnedButtons.Clear();

        if (GameDataManager.Instance == null) return;

        List<GameDataManager.GunData> guns =
            GameDataManager.Instance.GetGunsForCategory(currentCategory);

        foreach (var gun in guns)
        {
            GameObject go = Instantiate(gunButtonPrefab, gunsGridParent);
            GunButtonUI ui = new GunButtonUI
            {
                button = go.GetComponent<Button>(),
                gunImage = go.GetComponentInChildren<Image>(),
                selectionHighlight = go.transform.Find("Highlight")?.GetComponent<Image>(),
                gunId = gun.id
            };

            if (ui.gunImage != null) ui.gunImage.sprite = gun.icon;
            SetButtonSelected(ui, gun.id == GameDataManager.Instance.EquippedGunId);

            int capturedId = gun.id;
            ui.button?.onClick.AddListener(() => OnGunSelected(capturedId));

            spawnedButtons.Add(ui);
        }

        if (gridGroup != null)
        {
            gridGroup.alpha = 0f;
            gridGroup.DOFade(1f, 0.3f);
        }

        if (GameDataManager.Instance.EquippedGunId >= 0)
            UpdateGunInfo(GameDataManager.Instance.EquippedGunId);
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Gun Selection
    // ===================================================================

    private void OnGunSelected(int gunId)
    {
        selectedGunId = gunId;
        GameDataManager.Instance?.EquipGun(gunId);

        foreach (var b in spawnedButtons)
            SetButtonSelected(b, b.gunId == gunId);

        UpdateGunInfo(gunId);
        UpdateCharacterPreview();
    }

    private void SetButtonSelected(GunButtonUI ui, bool selected)
    {
        if (ui.selectionHighlight != null)
        {
            ui.selectionHighlight.color = selected
                ? new Color(1f, 0.85f, 0f)
                : new Color(0.4f, 0.4f, 0.4f);

            ui.selectionHighlight.transform
                .DOScale(selected ? 1.05f : 1f, 0.2f)
                .SetEase(Ease.OutBack);
        }
    }

    private void UpdateGunInfo(int gunId)
    {
        if (GameDataManager.Instance == null) return;
        GameDataManager.GunData data = GameDataManager.Instance.GetGunData(gunId);
        if (data == null) return;

        if (weaponNameText != null) weaponNameText.text = data.weaponName;
        if (damageValueText != null) damageValueText.text = data.damage.ToString();
        if (magazineValueText != null) magazineValueText.text = data.magazine.ToString();
        if (fireTypeText != null) fireTypeText.text = data.isBurst ? "BURST" : "SINGLE";
        if (fireTypeIcon != null) fireTypeIcon.sprite = data.isBurst ? data.burstIcon : data.singleIcon;

        currentSkinIndex = 0;
        UpdateSkinUI(data);
    }

    private void UpdateSkinUI(GameDataManager.GunData data)
    {
        bool hasSkins = data.skins != null && data.skins.Count > 1;
        if (skinIndexText != null) skinIndexText.text = hasSkins ? $"SKIN {currentSkinIndex + 1}" : "SKIN 1";
        if (skinPrevButton != null) skinPrevButton.gameObject.SetActive(hasSkins && currentSkinIndex > 0);
        if (skinNextButton != null) skinNextButton.gameObject.SetActive(hasSkins && currentSkinIndex < data.skins.Count - 1);
    }

    private void PrevSkin()
    {
        if (currentSkinIndex <= 0) return;
        currentSkinIndex--;
        GameDataManager.GunData data = GameDataManager.Instance?.GetGunData(selectedGunId);
        if (data != null) UpdateSkinUI(data);
    }

    private void NextSkin()
    {
        GameDataManager.GunData data = GameDataManager.Instance?.GetGunData(selectedGunId);
        if (data == null || data.skins == null) return;
        if (currentSkinIndex >= data.skins.Count - 1) return;
        currentSkinIndex++;
        UpdateSkinUI(data);
    }

    private void UpdateCharacterPreview()
    {
        Debug.Log("[Guns] Update character preview");
    }

    // ===================================================================
    #endregion
}