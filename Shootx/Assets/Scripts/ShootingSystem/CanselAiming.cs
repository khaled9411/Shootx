using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using DG.Tweening;

public class CancelAiming : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public static CancelAiming Instance { get; private set; }

    [Header("Visual Settings")]
    [SerializeField] private float hoverScale = 1.2f;
    [SerializeField] private float hoverDuration = 0.2f;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = new Color(1f, 0.3f, 0.3f);

    private ShootingSystem shootingSystem;
    private CanvasGroup canvasGroup;
    private Image buttonImage;
    private RectTransform rectTransform;
    private Vector3 originalScale;
    private bool isPointerOver = false;
    public bool IsPointerOver => isPointerOver;

    private void Awake()
    {
        if (Instance != null && Instance != this) return;
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    IEnumerator Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        buttonImage = GetComponent<Image>();
        rectTransform = GetComponent<RectTransform>();
        originalScale = rectTransform.localScale;

        yield return new WaitForSeconds(2f);
        shootingSystem = FindFirstObjectByType<ShootingSystem>();
    }

    private void Update()
    {
        if (shootingSystem == null) return;

        if (shootingSystem.IsAiming())
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            CheckManualRelease();
        }
        else
        {
            if (isPointerOver) ResetVisual();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void CheckManualRelease()
    {
        bool released = false;
        Vector2 releasePos = Vector2.zero;

        // Mouse
        if (Input.GetMouseButtonUp(0))
        {
            released = true;
            releasePos = Input.mousePosition;
        }

        // Touch
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                released = true;
                releasePos = touch.position;
                break;
            }
        }

        if (!released) return;

        bool overButton = IsPointOverButton(releasePos);

        Debug.Log($"[CancelAiming] Released | overButton: {overButton} | isPointerOver: {isPointerOver}");

        if (overButton)
        {
            Debug.Log("[CancelAiming] Cancel confirmed!");
            ResetVisual();
            shootingSystem.CancelShot();
        }
    }

    public bool IsPointOverButton(Vector2 screenPoint)
    {
        if (rectTransform == null) return false;
        if (canvasGroup != null && canvasGroup.alpha <= 0.01f) return false;

        return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPoint, null);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (shootingSystem == null || !shootingSystem.IsAiming()) return;

        isPointerOver = true;
        rectTransform.DOKill();
        rectTransform.DOScale(originalScale * hoverScale, hoverDuration)
            .SetEase(Ease.OutBack);

        if (buttonImage != null)
            buttonImage.DOColor(hoverColor, hoverDuration);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ResetVisual();
    }

    public void ResetVisual()
    {
        isPointerOver = false;
        if (rectTransform != null)
        {
            rectTransform.DOKill();
            rectTransform.DOScale(originalScale, hoverDuration)
                .SetEase(Ease.OutQuad);
        }

        if (buttonImage != null)
            buttonImage.DOColor(normalColor, hoverDuration);
    }
}
