using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI component for a single player slot in the lobby waiting room.
/// Supports occupied and empty states with animated transitions.
/// Used by <see cref="MultiplayerLobbyUI"/>.
/// </summary>
public class PlayerSlotUI : MonoBehaviour
{
    [Header("Slot Root")]
    [SerializeField] private CanvasGroup slotCanvasGroup;
    [SerializeField] private RectTransform slotRect;
    [SerializeField] private Image slotBackground;

    [Header("Occupied State")]
    [SerializeField] private GameObject occupiedContent;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private GameObject hostBadge;
    [SerializeField] private GameObject readyIndicator;
    [SerializeField] private GameObject notReadyIndicator;

    [Header("Empty State")]
    [SerializeField] private GameObject emptyContent;
    [SerializeField] private TextMeshProUGUI emptyText;

    [Header("Colors")]
    [SerializeField] private Color occupiedColor = new Color(0.2f, 0.25f, 0.3f, 0.9f);
    [SerializeField] private Color emptyColor = new Color(0.15f, 0.15f, 0.2f, 0.4f);
    [SerializeField] private Color readyColor = new Color(0.15f, 0.5f, 0.3f, 0.9f);

    [Header("Animation")]
    [SerializeField] private float animDuration = 0.3f;

    // ─── Internal ───────────────────────────────────────────────
    private bool _isOccupied;
    private bool _isReady;
    private string _playerId;

    public string PlayerId => _playerId;
    public bool IsOccupied => _isOccupied;
    public bool IsReady => _isReady;

    // ═══════════════════════════════════════════════════════════
    // Public API
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Set this slot to occupied with player info.
    /// </summary>
    public void SetOccupied(string playerId, string playerName, bool isHost, bool isReady, bool animate = true)
    {
        _playerId = playerId;
        _isOccupied = true;
        _isReady = isReady;

        // Show/hide states
        if (occupiedContent) occupiedContent.SetActive(true);
        if (emptyContent) emptyContent.SetActive(false);

        // Player name
        if (playerNameText)
            playerNameText.text = string.IsNullOrWhiteSpace(playerName) ? "Unknown" : playerName;

        // Host badge
        if (hostBadge) hostBadge.SetActive(isHost);

        // Ready state
        UpdateReadyVisuals(isReady);

        // Background color
        Color targetColor = isReady ? readyColor : occupiedColor;
        if (slotBackground)
        {
            if (animate)
                slotBackground.DOColor(targetColor, animDuration);
            else
                slotBackground.color = targetColor;
        }

        // Join animation
        if (animate && slotRect)
        {
            slotRect.DOKill();
            slotRect.localScale = Vector3.one * 0.85f;
            slotRect.DOScale(Vector3.one, animDuration).SetEase(Ease.OutBack);
        }
    }

    /// <summary>
    /// Set this slot to empty/waiting state.
    /// </summary>
    public void SetEmpty(bool animate = true)
    {
        _playerId = null;
        _isOccupied = false;
        _isReady = false;

        if (occupiedContent) occupiedContent.SetActive(false);
        if (emptyContent) emptyContent.SetActive(true);

        if (emptyText) emptyText.text = "Waiting for player...";

        if (slotBackground)
        {
            if (animate)
                slotBackground.DOColor(emptyColor, animDuration);
            else
                slotBackground.color = emptyColor;
        }

        if (hostBadge) hostBadge.SetActive(false);
        if (readyIndicator) readyIndicator.SetActive(false);
        if (notReadyIndicator) notReadyIndicator.SetActive(false);
    }

    /// <summary>
    /// Update only the ready state without changing other data.
    /// </summary>
    public void SetReady(bool isReady)
    {
        if (!_isOccupied) return;
        _isReady = isReady;

        UpdateReadyVisuals(isReady);

        // Animate background color transition
        Color targetColor = isReady ? readyColor : occupiedColor;
        if (slotBackground)
            slotBackground.DOColor(targetColor, animDuration);

        // Punch scale for feedback
        if (slotRect)
        {
            slotRect.DOKill();
            slotRect.DOPunchScale(Vector3.one * 0.05f, 0.25f, 5, 0.5f);
        }
    }

    /// <summary>
    /// Animate a player leaving this slot.
    /// </summary>
    public void AnimateLeave(System.Action onComplete = null)
    {
        if (slotRect)
        {
            slotRect.DOKill();
            slotRect.DOScale(Vector3.one * 0.85f, animDuration * 0.7f)
                .SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    SetEmpty(false);
                    if (slotRect) slotRect.localScale = Vector3.one;
                    onComplete?.Invoke();
                });
        }
        else
        {
            SetEmpty(false);
            onComplete?.Invoke();
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Internal
    // ═══════════════════════════════════════════════════════════

    private void UpdateReadyVisuals(bool isReady)
    {
        if (readyIndicator) readyIndicator.SetActive(isReady);
        if (notReadyIndicator) notReadyIndicator.SetActive(!isReady);
    }

    private void OnDestroy()
    {
        if (slotBackground) slotBackground.DOKill();
        if (slotRect) slotRect.DOKill();
    }
}
