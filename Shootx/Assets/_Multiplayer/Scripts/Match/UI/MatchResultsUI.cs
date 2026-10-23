using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Results screen shown at the end of every match.
///
/// Shows:
///   - Victory / Draw / Defeat banner
///   - Winner card with celebration animation
///   - Full ranked leaderboard with staggered reveal
///   - 8-second return countdown
///   - Continue button (requests early lobby return from server)
///
/// Uses DOTween Pro for all animations.
/// </summary>
public class MatchResultsUI : MonoBehaviour
{
    public static MatchResultsUI Instance { get; private set; }

    // ─── Inspector ──────────────────────────────────────────────────────
    [Header("Root Panel")]
    [SerializeField] private GameObject resultsPanel;
    [SerializeField] private CanvasGroup resultsPanelCG;

    [Header("Outcome Banner")]
    [SerializeField] private TextMeshProUGUI outcomeBannerText;
    [SerializeField] private Color victoryColor = new Color(1f, 0.84f, 0f);
    [SerializeField] private Color defeatColor = new Color(0.8f, 0.2f, 0.2f);
    [SerializeField] private Color drawColor = new Color(0.5f, 0.8f, 1f);

    [Header("Winner Card")]
    [SerializeField] private GameObject winnerCard;
    [SerializeField] private TextMeshProUGUI winnerNameText;

    [Header("Leaderboard")]
    [SerializeField] private Transform leaderboardContainer;
    [SerializeField] private GameObject leaderboardEntryPrefab;

    [Header("Return Countdown")]
    [SerializeField] private TextMeshProUGUI returnCountdownText;
    [SerializeField] private Image returnCountdownFill;

    [Header("Continue Button")]
    [SerializeField] private Button continueButton;

    [Header("Animation")]
    [SerializeField] private float panelFadeInDuration = 0.5f;
    [SerializeField] private float winnerCardPunchScale = 0.2f;
    [SerializeField] private float leaderboardEntryDelay = 0.15f;
    [SerializeField] private float returnCountdownDuration = 8f;

    // ─── Private State ─────────────────────────────────────────────────
    private MatchResultData _currentResult;
    private ulong _localClientId;
    private Coroutine _countdownCoroutine;
    private bool _returnTriggered;
    private readonly List<GameObject> _entryInstances = new List<GameObject>();
    private Sequence _celebrationSequence;

    // ═══════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ═══════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (resultsPanel != null) resultsPanel.SetActive(false);
        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinueClicked);
    }

    private void OnDestroy()
    {
        KillAllTweens();
        if (Instance == this) Instance = null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Show Results
    // ═══════════════════════════════════════════════════════════════════════

    public void ShowResults(MatchResultData result, ulong localClientId)
    {
        if (_currentResult != null) return; // idempotency guard

        _currentResult = result;
        _localClientId = localClientId;
        _returnTriggered = false;

        if (resultsPanel != null) resultsPanel.SetActive(true);
        if (resultsPanelCG != null)
        {
            resultsPanelCG.alpha = 0f;
            resultsPanelCG.DOFade(1f, panelFadeInDuration);
        }

        StartCoroutine(AnimateResultsSequence(result, localClientId));
    }

    public void HideResults()
    {
        KillAllTweens();
        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }

        if (resultsPanelCG != null)
        {
            resultsPanelCG.DOFade(0f, 0.3f).OnComplete(() =>
            {
                if (resultsPanel != null) resultsPanel.SetActive(false);
                ClearLeaderboard();
            });
        }
        else if (resultsPanel != null)
        {
            resultsPanel.SetActive(false);
            ClearLeaderboard();
        }

        _currentResult = null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Animation Sequence
    // ═══════════════════════════════════════════════════════════════════════

    private IEnumerator AnimateResultsSequence(MatchResultData result, ulong localClientId)
    {
        yield return new WaitForSeconds(panelFadeInDuration + 0.1f);

        // 1. Determine local player's outcome
        bool localWon = false, localDraw = false;
        if (result.IsDraw)
        {
            var entry = result.Entries.Find(e => e.ClientId == localClientId);
            localDraw = entry != null && entry.Rank == 1;
            localWon = localDraw;
        }
        else
        {
            localWon = result.WinnerClientId == localClientId;
        }

        // 2. Outcome banner
        if (outcomeBannerText != null)
        {
            if (localWon && !result.IsDraw)
            {
                outcomeBannerText.text = "VICTORY!";
                outcomeBannerText.color = victoryColor;
            }
            else if (result.IsDraw && localDraw)
            {
                outcomeBannerText.text = "DRAW!";
                outcomeBannerText.color = drawColor;
            }
            else
            {
                outcomeBannerText.text = "LOSS!";
                outcomeBannerText.color = defeatColor;
            }

            outcomeBannerText.transform.localScale = Vector3.zero;
            outcomeBannerText.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack);
        }

        yield return new WaitForSeconds(0.6f);

        // 3. Winner card
        if (winnerCard != null)
        {
            winnerCard.SetActive(true);
            if (winnerNameText != null)
            {
                winnerNameText.text = result.IsDraw ? "Draw" : result.WinnerName;
            }

            winnerCard.transform.localScale = Vector3.zero;
            _celebrationSequence = DOTween.Sequence();
            _celebrationSequence.Append(winnerCard.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack));
            _celebrationSequence.Append(winnerCard.transform.DOPunchScale(
                Vector3.one * winnerCardPunchScale, 0.4f, 5, 0.5f));
        }

        yield return new WaitForSeconds(0.8f);

        // 4. Staggered leaderboard reveal
        ClearLeaderboard();
        foreach (var entry in result.Entries)
        {
            SpawnLeaderboardEntry(entry, localClientId);
            yield return new WaitForSeconds(leaderboardEntryDelay);
        }

        yield return new WaitForSeconds(0.3f);

        // 5. Return countdown
        _countdownCoroutine = StartCoroutine(ReturnCountdownCoroutine());
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Leaderboard
    // ═══════════════════════════════════════════════════════════════════════

    private void SpawnLeaderboardEntry(PlayerResultEntry entry, ulong localClientId)
    {
        if (leaderboardContainer == null || leaderboardEntryPrefab == null) return;

        GameObject go = null /*= Instantiate(leaderboardEntryPrefab, leaderboardContainer)*/;

        for (int i = 0; i < leaderboardContainer.childCount; i++)
        {
           if(leaderboardContainer.GetChild(i) != null && 
                leaderboardContainer.GetChild(i).gameObject.TryGetComponent<LeaderboardEntryUI>(out LeaderboardEntryUI leaderboard) &&
                !leaderboard.isOwned)
            {
                go = leaderboard.gameObject;
                break;
            }
        }
        _entryInstances.Add(go);

        // Fill in the entry UI via the LeaderboardEntryUI component
        LeaderboardEntryUI ui = null;
        if (go != null)
            ui = go.GetComponent<LeaderboardEntryUI>();

        if (ui != null)
        {
            bool isLocal = entry.ClientId == localClientId;
            bool isWinner = !_currentResult.IsDraw
                ? entry.ClientId == _currentResult.WinnerClientId
                : entry.Rank == 1;

            ui.Initialize(
                rank: entry.Rank,
                playerName: entry.DisplayName + (isLocal ? " (You)" : ""),
                kills: entry.Kills,
                deaths: entry.Deaths,
                bestStreak: entry.BestKillStreak,
                isLocalPlayer: isLocal,
                isWinner: isWinner,
                isConnected: entry.IsConnected
            );
        }
        else
        {
            // Fallback: find TextMeshPro children by index
            var texts = go.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length >= 4)
            {
                texts[0].text = $"#{entry.Rank}";
                texts[1].text = entry.DisplayName + (entry.ClientId == localClientId ? " (You)" : "");
                texts[2].text = $"{entry.Kills} Kills";
                texts[3].text = $"{entry.Deaths} Deaths";
            }
        }

        // Animate slide-in
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            Vector2 origPos = rt.anchoredPosition;
            rt.anchoredPosition = origPos + Vector2.right * 80f;

            DOTween.Sequence()
                .Append(rt.DOAnchorPos(origPos, 0.3f).SetEase(Ease.OutCubic))
                .Join(cg.DOFade(1f, 0.25f));
        }
        else
        {
            cg.DOFade(1f, 0.25f);
        }
    }

    private void ClearLeaderboard()
    {
        foreach (var go in _entryInstances)
        {
            if (go != null) Destroy(go);
        }
        _entryInstances.Clear();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Return Countdown
    // ═══════════════════════════════════════════════════════════════════════

    private IEnumerator ReturnCountdownCoroutine()
    {
        float remaining = returnCountdownDuration;
        while (remaining > 0f)
        {
            remaining -= Time.deltaTime;
            UpdateReturnCountdownDisplay(remaining);
            yield return null;
        }

        // Countdown expired — request return
        TriggerReturn();
    }

    private void UpdateReturnCountdownDisplay(float remaining)
    {
        if (returnCountdownText != null)
            returnCountdownText.text = Mathf.CeilToInt(remaining).ToString();

        if (returnCountdownFill != null)
            returnCountdownFill.fillAmount = Mathf.Clamp01(remaining / returnCountdownDuration);
    }

    private void OnContinueClicked()
    {
        TriggerReturn();
    }

    private void TriggerReturn()
    {
        if (_returnTriggered) return;
        _returnTriggered = true;

        if (continueButton != null) continueButton.interactable = false;

        // Request early return via server RPC
        if (MatchManager.Instance != null)
            MatchManager.Instance.RequestEarlyReturnServerRpc();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Cleanup
    // ═══════════════════════════════════════════════════════════════════════

    private void KillAllTweens()
    {
        DOTween.Kill(this);
        _celebrationSequence?.Kill();
    }
}
