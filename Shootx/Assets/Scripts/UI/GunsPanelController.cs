using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Shootx.Store;

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
        public TextMeshProUGUI statusText;
        public int gunId;
        public StoreItemDefinition definition;
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Inspector Fields
    // ===================================================================

    [Header("=== Weapon Info Panel ===")]
    [SerializeField] private TextMeshProUGUI weaponNameText;
    [SerializeField] private TextMeshProUGUI damageValueText;
    [SerializeField] private TextMeshProUGUI magazineValueText;
    [SerializeField] private TextMeshProUGUI fireTypeText;
    [SerializeField] private Image fireTypeIcon;
    [SerializeField] private TextMeshProUGUI skinIndexText;
    [SerializeField] private Button skinPrevButton;
    [SerializeField] private Button skinNextButton;

    [Header("=== Action Controls ===")]
    [SerializeField] private Button actionButton;
    [SerializeField] private TextMeshProUGUI actionButtonText;
    [SerializeField] private TextMeshProUGUI softCurrencyText;
    [SerializeField] private TextMeshProUGUI hardCurrencyText;

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
    private int selectedGunId = 0;
    private readonly List<GunButtonUI> spawnedButtons = new List<GunButtonUI>();

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
            UIManager.Instance?.HideGunsPanel();
        });

        prevCategoryButton?.onClick.AddListener(GoToPrevCategory);
        nextCategoryButton?.onClick.AddListener(GoToNextCategory);
        actionButton?.onClick.AddListener(OnActionButtonClicked);
    }

    private void OnEnable()
    {
        PlayShowAnimation();
        selectedGunId = GameDataManager.Instance != null ? GameDataManager.Instance.EquippedGunId : 0;
        LoadCurrentCategory();
        RefreshCurrencyUI();

        // Immediately update 3D store preview
        StorePreviewManager.Instance?.ShowWeaponPreview(selectedGunId);

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
        var btn = infoGroup != null ? infoGroup.transform.Find("ActionButton")?.GetComponent<Button>() : null;
        if (btn != null)
        {
            actionButton = btn;
            actionButtonText = btn.GetComponentInChildren<TextMeshProUGUI>();
            return;
        }

        // Dynamically create Action Button inside infoGroup
        Transform parentTransform = infoGroup != null ? infoGroup.transform : transform;
        var btnGo = new GameObject("ActionButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(parentTransform, false);

        var rect = btnGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(240f, 50f);
        rect.anchoredPosition = new Vector2(0f, -125f);

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

        FadeIn(characterGroup, 0.08f, 25f);
        FadeIn(categoryGroup, 0.16f, 0f);
        FadeIn(gridGroup, 0.24f, -20f);
    }

    public void PlayHideAnimation(System.Action onComplete)
    {
        StorePreviewManager.Instance?.ClosePreview();
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
            prevCategoryButton.gameObject.SetActive(idx > 0);

        if (nextCategoryButton != null)
            nextCategoryButton.gameObject.SetActive(idx < CategoryNames.Length - 1);
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

        List<StoreItemDefinition> weaponsForCat = GetWeaponsForCurrentCategory();

        foreach (var weapon in weaponsForCat)
        {
            if (weapon == null) continue;

            GameObject go = Instantiate(gunButtonPrefab, gunsGridParent);
            GunButtonUI ui = new GunButtonUI
            {
                button = go.GetComponent<Button>(),
                gunImage = go.GetComponentInChildren<Image>(),
                selectionHighlight = go.transform.Find("Highlight")?.GetComponent<Image>(),
                statusText = go.GetComponentInChildren<TextMeshProUGUI>(),
                gunId = weapon.numericId,
                definition = weapon
            };

            // Artwork placeholder neutral if sprite is null
            if (ui.gunImage != null)
            {
                if (weapon.itemIcon != null)
                {
                    ui.gunImage.sprite = weapon.itemIcon;
                    ui.gunImage.color = Color.white;
                }
                else
                {
                    ui.gunImage.color = new Color(1f, 1f, 1f, 0.25f);
                }
            }

            UpdateButtonState(ui);

            StoreItemDefinition capturedDef = weapon;
            ui.button?.onClick.AddListener(() => OnGunClicked(capturedDef, ui));

            spawnedButtons.Add(ui);
        }

        if (gridGroup != null)
        {
            gridGroup.alpha = 0f;
            gridGroup.DOFade(1f, 0.3f);
        }

        UpdateGunInfo(selectedGunId);
        UpdateActionButtonState();
    }

    private List<StoreItemDefinition> GetWeaponsForCurrentCategory()
    {
        var list = new List<StoreItemDefinition>();
        var db = StoreCatalogDatabase.Instance;
        if (db == null) return list;

        foreach (var w in db.Weapons)
        {
            if (w == null) continue;
            bool match = false;

            switch (currentCategory)
            {
                case GunCategory.Pistols:
                    match = w.numericId == 0 || w.numericId == 1 || w.numericId == 2;
                    break;
                case GunCategory.SubMachineGuns:
                    match = w.numericId == 3 || w.numericId == 5;
                    break;
                case GunCategory.AutomaticGuns:
                    match = w.numericId == 4 || w.numericId == 6;
                    break;
                case GunCategory.SuperGuns:
                    match = w.numericId == 7 || w.numericId == 8;
                    break;
                case GunCategory.UltimateGuns:
                    match = w.numericId == 9 || w.numericId == 10 || w.numericId == 11;
                    break;
                case GunCategory.BaseGuns:
                    match = w.rarity == ItemRarity.Common || w.rarity == ItemRarity.Uncommon;
                    break;
                case GunCategory.BundleGuns:
                    match = w.rarity == ItemRarity.Rare || w.rarity == ItemRarity.Epic;
                    break;
                case GunCategory.VIPGuns:
                    match = w.numericId == 12 || w.numericId == 13 || w.rarity == ItemRarity.Legendary;
                    break;
            }

            if (match) list.Add(w);
        }

        // Fallback: if category has 0 items, show all weapons
        if (list.Count == 0 && db.Weapons.Count > 0)
        {
            list.AddRange(db.Weapons);
        }

        return list;
    }

    private void UpdateButtonState(GunButtonUI ui)
    {
        if (ui == null || ui.definition == null || GameDataManager.Instance == null) return;

        bool isOwned = GameDataManager.Instance.IsGunOwned(ui.gunId);
        bool isEquipped = GameDataManager.Instance.EquippedGunId == ui.gunId;
        bool isSelected = selectedGunId == ui.gunId;

        if (ui.statusText != null)
        {
            if (isEquipped)
                ui.statusText.text = "<color=#00FF88>EQUIPPED</color>";
            else if (isOwned)
                ui.statusText.text = "<color=#FFFFFF>OWNED</color>";
            else
                ui.statusText.text = $"<color=#FFD700>{ui.definition.softCurrencyPrice:N0} COINS</color>";
        }

        if (ui.selectionHighlight != null)
        {
            Color hlColor = isEquipped
                ? new Color(0f, 1f, 0.5f, 0.9f)
                : (isSelected ? new Color(1f, 0.85f, 0f, 0.9f) : new Color(0.3f, 0.3f, 0.3f, 0.5f));

            ui.selectionHighlight.color = hlColor;
            ui.selectionHighlight.transform.DOScale(isSelected ? 1.05f : 1f, 0.2f).SetEase(Ease.OutBack);
        }
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Gun Selection, Preview & Purchasing
    // ===================================================================

    private void OnGunClicked(StoreItemDefinition weapon, GunButtonUI ui)
    {
        if (weapon == null || GameDataManager.Instance == null) return;

        // 1. SELECT FOR PREVIEW IMMEDIATELY
        selectedGunId = weapon.numericId;
        ui.button.transform.DOPunchScale(Vector3.one * 0.08f, 0.15f);

        // Update card highlights
        UpdateAllButtons();

        // Update stats & labels
        UpdateGunInfo(selectedGunId);

        // Update 3D Store Preview immediately
        StorePreviewManager.Instance?.ShowWeaponPreview(selectedGunId);

        // Update Action Button (Buy / Equip)
        UpdateActionButtonState();
    }

    private void OnActionButtonClicked()
    {
        var db = StoreCatalogDatabase.Instance;
        if (db == null || GameDataManager.Instance == null) return;

        StoreItemDefinition weapon = db.GetWeaponByNumericId(selectedGunId);
        if (weapon == null) return;

        bool isOwned = GameDataManager.Instance.IsGunOwned(selectedGunId);
        bool isEquipped = GameDataManager.Instance.EquippedGunId == selectedGunId;

        if (isEquipped)
        {
            // Already equipped
            actionButton?.transform.DOPunchScale(Vector3.one * 0.05f, 0.15f);
            return;
        }

        if (!isOwned)
        {
            // Try to Purchase with Soft Currency
            bool success = GameDataManager.Instance.TryPurchaseItem(weapon);
            if (success)
            {
                actionButton?.transform.DOPunchScale(Vector3.one * 0.15f, 0.3f).SetEase(Ease.OutBack);
                UpdateAllButtons();
                UpdateGunInfo(selectedGunId);
                UpdateActionButtonState();
                RefreshCurrencyUI();
            }
            else
            {
                // Insufficient funds feedback
                actionButton?.transform.DOShakePosition(0.3f, strength: new Vector3(8f, 0f, 0f), vibrato: 10);
                if (actionButtonText != null)
                {
                    string oldText = actionButtonText.text;
                    actionButtonText.text = "<color=#FF4444>NOT ENOUGH COINS!</color>";
                    DOVirtual.DelayedCall(1.2f, UpdateActionButtonState);
                }
            }
        }
        else
        {
            // Equip weapon
            GameDataManager.Instance.EquipGun(selectedGunId);
            actionButton?.transform.DOPunchScale(Vector3.one * 0.12f, 0.2f);
            UpdateAllButtons();
            UpdateGunInfo(selectedGunId);
            UpdateActionButtonState();

            // Refresh preview
            StorePreviewManager.Instance?.ShowWeaponPreview(selectedGunId);
        }
    }

    private void UpdateActionButtonState()
    {
        if (actionButton == null || actionButtonText == null || GameDataManager.Instance == null) return;

        var db = StoreCatalogDatabase.Instance;
        StoreItemDefinition weapon = db != null ? db.GetWeaponByNumericId(selectedGunId) : null;
        if (weapon == null) return;

        bool isOwned = GameDataManager.Instance.IsGunOwned(selectedGunId);
        bool isEquipped = GameDataManager.Instance.EquippedGunId == selectedGunId;

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
            actionButtonText.text = $"BUY ({weapon.softCurrencyPrice:N0} COINS)";
            actionButton.interactable = true;
            if (img != null) img.color = new Color(0.9f, 0.65f, 0.05f, 1f);
        }
    }

    private void UpdateAllButtons()
    {
        foreach (var b in spawnedButtons)
            UpdateButtonState(b);
    }

    private void UpdateGunInfo(int gunId)
    {
        var db = StoreCatalogDatabase.Instance;
        StoreItemDefinition data = db != null ? db.GetWeaponByNumericId(gunId) : null;
        if (data == null) return;

        bool isOwned = GameDataManager.Instance != null && GameDataManager.Instance.IsGunOwned(gunId);
        bool isEquipped = GameDataManager.Instance != null && GameDataManager.Instance.EquippedGunId == gunId;

        string statusLabel = isEquipped ? "<color=#00FF88>[EQUIPPED]</color>"
            : (isOwned ? "<color=#AAAAAA>[OWNED]</color>" : $"<color=#FFD700>[PRICE: {data.softCurrencyPrice:N0} COINS]</color>");

        if (weaponNameText != null)
            weaponNameText.text = $"{data.displayName} {statusLabel}";

        // Show base stats
        if (damageValueText != null) damageValueText.text = (30 + gunId * 5).ToString();
        if (magazineValueText != null) magazineValueText.text = (20 + (gunId % 4) * 10).ToString();
        if (fireTypeText != null) fireTypeText.text = gunId % 2 == 0 ? "SINGLE" : "BURST";

        if (skinIndexText != null) skinIndexText.text = data.rarity.ToString().ToUpper();
    }

    // ===================================================================
    #endregion
}