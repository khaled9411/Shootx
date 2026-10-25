using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Shootx.Store;

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
        public StoreItemDefinition definition;
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Inspector Fields
    // ===================================================================

    [Header("=== Character Preview ===")]
    [SerializeField] private Transform characterHolder;

    [Header("=== Action Controls ===")]
    [SerializeField] private Button actionButton;
    [SerializeField] private TextMeshProUGUI actionButtonText;
    [SerializeField] private TextMeshProUGUI softCurrencyText;
    [SerializeField] private TextMeshProUGUI hardCurrencyText;

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
    private int selectedOutfitId = 0;
    private readonly List<OutfitButtonUI> spawnedButtons = new List<OutfitButtonUI>();

    private static readonly string[] CategoryNames = { "CLASSIC", "ULTIMATE", "BASE", "BUNDLES" };

    // ===================================================================
    #endregion

    // ===================================================================
    #region Unity Lifecycle
    // ===================================================================

    private void Awake()
    {
        EnsureActionButton();
        EnsureDragRotator();
    }

    private void Start()
    {
        backButton?.onClick.AddListener(() =>
        {
            StorePreviewManager.Instance?.ClosePreview();
            UIManager.Instance?.HideOutfitsPanel();
        });

        prevCategoryButton?.onClick.AddListener(GoToPrevCategory);
        nextCategoryButton?.onClick.AddListener(GoToNextCategory);
        actionButton?.onClick.AddListener(OnActionButtonClicked);
    }

    private void OnEnable()
    {
        PlayShowAnimation();
        selectedOutfitId = GameDataManager.Instance != null ? GameDataManager.Instance.EquippedOutfitId : 0;
        LoadCurrentCategory();
        RefreshCurrencyUI();

        // Immediately update 3D store preview
        StorePreviewManager.Instance?.ShowOutfitPreview(selectedOutfitId);

        GameDataManager.OnCurrencyChanged += RefreshCurrencyUI;
    }

    private void OnDisable()
    {
        GameDataManager.OnCurrencyChanged -= RefreshCurrencyUI;
        StorePreviewManager.Instance?.ClosePreview();
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region UI Setup & Builders
    // ===================================================================

    private void EnsureActionButton()
    {
        if (actionButton != null) return;

        // Try to find existing action button
        var btn = characterGroup != null ? characterGroup.transform.Find("ActionButton")?.GetComponent<Button>() : null;
        if (btn != null)
        {
            actionButton = btn;
            actionButtonText = btn.GetComponentInChildren<TextMeshProUGUI>();
            return;
        }

        // Dynamically create Action Button inside characterGroup or transform
        Transform parentTransform = characterGroup != null ? characterGroup.transform : transform;
        var btnGo = new GameObject("ActionButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(parentTransform, false);

        var rect = btnGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(240f, 50f);
        rect.anchoredPosition = new Vector2(0f, -140f);

        var img = btnGo.GetComponent<Image>();
        img.color = new Color(0.1f, 0.7f, 0.35f, 1f);

        actionButton = btnGo.GetComponent<Button>();
        actionButton.targetGraphic = img;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(btnGo.transform, false);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        actionButtonText = textGo.GetComponent<TextMeshProUGUI>();
        actionButtonText.text = "EQUIP";
        actionButtonText.fontSize = 18;
        actionButtonText.fontStyle = FontStyles.Bold;
        actionButtonText.alignment = TextAlignmentOptions.Center;
        actionButtonText.color = Color.white;
    }

    private void EnsureDragRotator()
    {
        if (characterHolder != null && characterHolder.GetComponent<StorePreviewDragRotator>() == null)
        {
            characterHolder.gameObject.AddComponent<StorePreviewDragRotator>();
        }
    }

    private void RefreshCurrencyUI()
    {
        if (GameDataManager.Instance == null) return;

        if (softCurrencyText != null)
            softCurrencyText.text = GameDataManager.Instance.SoftCurrency.ToString("N0");

        if (hardCurrencyText != null)
            hardCurrencyText.text = GameDataManager.Instance.HardCurrency.ToString("N0");
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

        List<StoreItemDefinition> outfitsForCat = GetOutfitsForCurrentCategory();

        foreach (var outfit in outfitsForCat)
        {
            if (outfit == null) continue;

            GameObject go = Instantiate(outfitButtonPrefab, outfitsGridParent);

            OutfitButtonUI ui = new OutfitButtonUI
            {
                button = go.GetComponent<Button>(),
                outfitImage = go.transform.Find("OutfitImage")?.GetComponent<Image>() ?? go.transform.Find("ICON")?.GetComponent<Image>(),
                nameText = go.GetComponentInChildren<TextMeshProUGUI>(),
                selectionHighlight = go.transform.Find("Highlight")?.GetComponent<Image>(),
                outfitId = outfit.numericId,
                definition = outfit
            };

            // Artwork placeholder neutral if sprite is null
            if (ui.outfitImage != null)
            {
                if (outfit.itemIcon != null)
                {
                    ui.outfitImage.sprite = outfit.itemIcon;
                    ui.outfitImage.color = Color.white;
                }
                else
                {
                    ui.outfitImage.color = new Color(1f, 1f, 1f, 0.25f);
                }
            }

            UpdateButtonState(ui);

            StoreItemDefinition capturedDef = outfit;
            ui.button?.onClick.AddListener(() => OnOutfitClicked(capturedDef, ui));

            spawnedButtons.Add(ui);
        }

        if (gridGroup != null)
        {
            gridGroup.alpha = 0f;
            gridGroup.DOFade(1f, 0.3f);
        }

        UpdateActionButtonState();
    }

    private List<StoreItemDefinition> GetOutfitsForCurrentCategory()
    {
        var list = new List<StoreItemDefinition>();
        var db = StoreCatalogDatabase.Instance;
        if (db == null) return list;

        foreach (var o in db.Outfits)
        {
            if (o == null) continue;
            bool match = false;

            switch (currentCategory)
            {
                case OutfitCategory.Classic:
                    match = o.numericId == 0 || o.numericId == 1 || o.numericId == 2 || o.numericId == 3;
                    break;
                case OutfitCategory.Base:
                    match = o.numericId == 4 || o.numericId == 5 || o.numericId == 6 || o.numericId == 7;
                    break;
                case OutfitCategory.Ultimate:
                    match = o.numericId >= 8;
                    break;
                case OutfitCategory.Bundles:
                    match = true;
                    break;
            }

            if (match) list.Add(o);
        }

        if (list.Count == 0 && db.Outfits.Count > 0)
        {
            list.AddRange(db.Outfits);
        }

        return list;
    }

    private void UpdateButtonState(OutfitButtonUI ui)
    {
        if (ui == null || ui.definition == null || GameDataManager.Instance == null) return;

        bool isOwned = GameDataManager.Instance.IsOutfitOwned(ui.outfitId);
        bool isEquipped = GameDataManager.Instance.EquippedOutfitId == ui.outfitId;
        bool isSelected = selectedOutfitId == ui.outfitId;

        if (ui.nameText != null)
        {
            if (isEquipped)
                ui.nameText.text = $"{ui.definition.displayName}\n<color=#00FF88>[EQUIPPED]</color>";
            else if (isOwned)
                ui.nameText.text = $"{ui.definition.displayName}\n<color=#FFFFFF>[OWNED]</color>";
            else
                ui.nameText.text = $"{ui.definition.displayName}\n<color=#FFD700>{ui.definition.softCurrencyPrice:N0} COINS</color>";
        }

        SetButtonSelected(ui, isSelected, isEquipped);
    }

    private void SetButtonSelected(OutfitButtonUI ui, bool selected, bool equipped)
    {
        if (ui.selectionHighlight != null)
        {
            Color hlColor = equipped
                ? new Color(0f, 1f, 0.5f, 0.9f)
                : (selected ? new Color(1f, 0.85f, 0f, 0.9f) : new Color(0.3f, 0.3f, 0.3f, 0.5f));

            ui.selectionHighlight.color = hlColor;
            ui.selectionHighlight.transform
                .DOScale(selected ? 1.05f : 1f, 0.2f)
                .SetEase(Ease.OutBack);
        }
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Outfit Selection, Preview & Purchasing
    // ===================================================================

    private void OnOutfitClicked(StoreItemDefinition outfit, OutfitButtonUI ui)
    {
        if (outfit == null || GameDataManager.Instance == null) return;

        // 1. SELECT FOR PREVIEW IMMEDIATELY
        selectedOutfitId = outfit.numericId;
        ui.button.transform.DOPunchScale(Vector3.one * 0.08f, 0.15f);

        // Update card highlights
        UpdateAllButtons();

        // Update 3D Store Preview immediately
        StorePreviewManager.Instance?.ShowOutfitPreview(selectedOutfitId);

        // Update Action Button (Buy / Equip)
        UpdateActionButtonState();
    }

    private void OnActionButtonClicked()
    {
        var db = StoreCatalogDatabase.Instance;
        if (db == null || GameDataManager.Instance == null) return;

        StoreItemDefinition outfit = db.GetOutfitByNumericId(selectedOutfitId);
        if (outfit == null) return;

        bool isOwned = GameDataManager.Instance.IsOutfitOwned(selectedOutfitId);
        bool isEquipped = GameDataManager.Instance.EquippedOutfitId == selectedOutfitId;

        if (isEquipped)
        {
            actionButton?.transform.DOPunchScale(Vector3.one * 0.05f, 0.15f);
            return;
        }

        if (!isOwned)
        {
            // Try to Purchase with Soft Currency
            bool success = GameDataManager.Instance.TryPurchaseItem(outfit);
            if (success)
            {
                actionButton?.transform.DOPunchScale(Vector3.one * 0.15f, 0.3f).SetEase(Ease.OutBack);
                UpdateAllButtons();
                UpdateActionButtonState();
                RefreshCurrencyUI();
            }
            else
            {
                // Insufficient funds feedback
                actionButton?.transform.DOShakePosition(0.3f, strength: new Vector3(8f, 0f, 0f), vibrato: 10);
                if (actionButtonText != null)
                {
                    actionButtonText.text = "<color=#FF4444>NOT ENOUGH COINS!</color>";
                    DOVirtual.DelayedCall(1.2f, UpdateActionButtonState);
                }
            }
        }
        else
        {
            // Equip Outfit
            GameDataManager.Instance.EquipOutfit(selectedOutfitId);
            actionButton?.transform.DOPunchScale(Vector3.one * 0.12f, 0.2f);
            UpdateAllButtons();
            UpdateActionButtonState();

            // Refresh preview
            StorePreviewManager.Instance?.ShowOutfitPreview(selectedOutfitId);
        }
    }

    private void UpdateActionButtonState()
    {
        if (actionButton == null || actionButtonText == null || GameDataManager.Instance == null) return;

        var db = StoreCatalogDatabase.Instance;
        StoreItemDefinition outfit = db != null ? db.GetOutfitByNumericId(selectedOutfitId) : null;
        if (outfit == null) return;

        bool isOwned = GameDataManager.Instance.IsOutfitOwned(selectedOutfitId);
        bool isEquipped = GameDataManager.Instance.EquippedOutfitId == selectedOutfitId;

        var img = actionButton.GetComponent<Image>();

        if (isEquipped)
        {
            actionButtonText.text = "EQUIPPED";
            actionButton.interactable = false;
            if (img != null) img.color = new Color(0.15f, 0.45f, 0.25f, 0.85f);
        }
        else if (isOwned)
        {
            actionButtonText.text = "EQUIP";
            actionButton.interactable = true;
            if (img != null) img.color = new Color(0.06f, 0.75f, 0.35f, 1f);
        }
        else
        {
            actionButtonText.text = $"BUY ({outfit.softCurrencyPrice:N0} COINS)";
            actionButton.interactable = true;
            if (img != null) img.color = new Color(0.9f, 0.65f, 0.05f, 1f);
        }
    }

    private void UpdateAllButtons()
    {
        foreach (var b in spawnedButtons)
            UpdateButtonState(b);
    }

    // ===================================================================
    #endregion
}