using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class OutfitsPanelController : MonoBehaviour
{
    // ===================================================================
    #region Data Types
    // ===================================================================

    public enum OutfitCategory { Classic, Ultimate, Base, Bundles }

    [System.Serializable]
    public class OutfitButtonUI
    {
        public Button button;
        public Image outfitImage;
        public TextMeshProUGUI nameText;
        public Image nameBg;
        public Image selectionHighlight;
        public int outfitId;
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Inspector Fields
    // ===================================================================

    [Header("=== Character Preview ===")]
    [SerializeField] private Transform characterHolder;

    [Header("=== Category Navigation ===")]
    [SerializeField] private TextMeshProUGUI categoryNameText;
    [SerializeField] private List<Image> categoryIndicators;
    [SerializeField] private Color activeIndicatorColor = Color.yellow;
    [SerializeField] private Color inactiveIndicatorColor = Color.gray;
    [SerializeField] private Button prevCategoryButton;
    [SerializeField] private Button nextCategoryButton;

    [Header("=== Outfits Grid ===")]
    [SerializeField] private Transform outfitsGridParent;
    [SerializeField] private GameObject outfitButtonPrefab;

    [Header("=== Back Button ===")]
    [SerializeField] private Button backButton;

    [Header("=== Animation Groups ===")]
    [SerializeField] private CanvasGroup characterGroup;
    [SerializeField] private CanvasGroup categoryGroup;
    [SerializeField] private CanvasGroup gridGroup;

    // ===================================================================
    #endregion

    // ===================================================================
    #region Private State
    // ===================================================================

    private OutfitCategory currentCategory = OutfitCategory.Classic;
    private List<OutfitButtonUI> spawnedButtons = new List<OutfitButtonUI>();

    private static readonly string[] CategoryNames = { "CLASSIC", "ULTIMATE", "BASE", "BUNDLES" };

    // ===================================================================
    #endregion

    // ===================================================================
    #region Unity Lifecycle
    // ===================================================================

    private void Start()
    {
        backButton?.onClick.AddListener(UIManager.Instance.HideOutfitsPanel);
        prevCategoryButton?.onClick.AddListener(GoToPrevCategory);
        nextCategoryButton?.onClick.AddListener(GoToNextCategory);
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

        FadeIn(characterGroup, 0.00f, 25f);
        FadeIn(categoryGroup, 0.10f, 0f);
        FadeIn(gridGroup, 0.18f, -20f);
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
        if (categoryNameText != null) categoryNameText.text = CategoryNames[idx];

        for (int i = 0; i < categoryIndicators.Count; i++)
        {
            if (categoryIndicators[i] == null) continue;
            bool active = i == idx;
            categoryIndicators[i].color = active ? activeIndicatorColor : inactiveIndicatorColor;
            categoryIndicators[i].transform.DOScale(active ? 1.2f : 1f, 0.2f);
        }

        if (prevCategoryButton != null) prevCategoryButton.gameObject.SetActive(idx > 0);
        if (nextCategoryButton != null) nextCategoryButton.gameObject.SetActive(idx < CategoryNames.Length - 1);
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

        List<GameDataManager.OutfitData> outfits =
            GameDataManager.Instance.GetOutfitsForCategory(currentCategory);

        foreach (var outfit in outfits)
        {
            GameObject go = Instantiate(outfitButtonPrefab, outfitsGridParent);

            OutfitButtonUI ui = new OutfitButtonUI
            {
                button = go.GetComponent<Button>(),
                outfitImage = go.transform.Find("OutfitImage")?.GetComponent<Image>(),
                nameText = go.GetComponentInChildren<TextMeshProUGUI>(),
                selectionHighlight = go.transform.Find("Highlight")?.GetComponent<Image>(),
                outfitId = outfit.id
            };

            if (ui.outfitImage != null) ui.outfitImage.sprite = outfit.icon;
            if (ui.nameText != null) ui.nameText.text = outfit.outfitName;

            SetButtonSelected(ui, outfit.id == GameDataManager.Instance.EquippedOutfitId);

            int capturedId = outfit.id;
            ui.button?.onClick.AddListener(() => OnOutfitSelected(capturedId));

            spawnedButtons.Add(ui);
        }

        if (gridGroup != null)
        {
            gridGroup.alpha = 0f;
            gridGroup.DOFade(1f, 0.3f);
        }
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Outfit Selection
    // ===================================================================

    private void OnOutfitSelected(int outfitId)
    {
        GameDataManager.Instance?.EquipOutfit(outfitId);

        foreach (var b in spawnedButtons)
            SetButtonSelected(b, b.outfitId == outfitId);

        UpdateCharacterPreview();
    }

    private void SetButtonSelected(OutfitButtonUI ui, bool selected)
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

    private void UpdateCharacterPreview()
    {
        Debug.Log("[Outfits] Update character preview");
    }

    // ===================================================================
    #endregion
}