using DG.Tweening;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// In-game HUD that shows:
///   - Pre-match countdown (animated with DOTween)
///   - Live match timer
///   - Local player kills / kill target
///   - Local player deaths
///   - Spawn protection visual indicator
///
/// Subscribe to MatchManager events in OnEnable.
/// Attach to the HUD Canvas in MainMultiplayer.
/// </summary>
public class MatchHUD : MonoBehaviour
{
    public static MatchHUD Instance { get; private set; }

    // ─── Inspector ──────────────────────────────────────────────────────
    [Header("Countdown")]
    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private TextMeshProUGUI countdownText;

    [Header("Match Timer")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private float warningThreshold = 30f;
    [SerializeField] private Color normalTimerColor = Color.white;
    [SerializeField] private Color warningTimerColor = Color.red;

    [Header("Kill / Score HUD")]
    [SerializeField] private TextMeshProUGUI killsText;
    [SerializeField] private TextMeshProUGUI deathsText;
    [SerializeField] private TextMeshProUGUI killTargetText;

    [Header("Kill Notification")]
    [SerializeField] private GameObject killNotificationPanel;
    [SerializeField] private TextMeshProUGUI killNotificationText;
    [SerializeField] private float killNotificationDuration = 1.5f;

    [Header("Spawn Protection")]
    [SerializeField] private GameObject spawnProtectionIndicator;

    [Header("Animation")]
    [SerializeField] private float countdownPunchScale = 0.3f;
    [SerializeField] private float countdownFadeDuration = 0.3f;
    [SerializeField] private float goDuration = 0.6f;

    // ─── Private State ───────────────────────────────────────────────────
    private PlayerMatchStats _localStats;
    private bool _warningTriggered;
    private Tween _killNotificationTween;
    private Tween _countdownTween;
    private Tween _timerPulseTween;

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
    }

    private bool _boundToMatchManager;

    private void OnEnable()
    {
        SubscribeToMatchManager();
        StartCoroutine(BindMatchManagerCoroutine());
    }

    private void OnDisable()
    {
        UnsubscribeFromMatchManager();
        KillAllTweens();
    }

    private void SubscribeToMatchManager()
    {
        if (_boundToMatchManager || MatchManager.Instance == null) return;
        _boundToMatchManager = true;
        MatchManager.Instance.OnCountdownChanged += UpdateCountdown;
        MatchManager.Instance.OnMatchGoSignal += ShowGoSignal;
        MatchManager.Instance.OnMatchTimeUpdated += UpdateTimer;
        MatchManager.Instance.OnMatchStateChanged += HandleMatchStateChanged;
    }

    private void UnsubscribeFromMatchManager()
    {
        if (!_boundToMatchManager) return;
        _boundToMatchManager = false;
        if (MatchManager.Instance != null)
        {
            MatchManager.Instance.OnCountdownChanged -= UpdateCountdown;
            MatchManager.Instance.OnMatchGoSignal -= ShowGoSignal;
            MatchManager.Instance.OnMatchTimeUpdated -= UpdateTimer;
            MatchManager.Instance.OnMatchStateChanged -= HandleMatchStateChanged;
        }
    }

    private System.Collections.IEnumerator BindMatchManagerCoroutine()
    {
        while (MatchManager.Instance == null)
            yield return null;
        SubscribeToMatchManager();
    }

    private void Start()
    {
        // Find and bind local player stats
        StartCoroutine(BindLocalPlayerStats());

        if (countdownPanel != null) countdownPanel.SetActive(false);
        if (spawnProtectionIndicator != null) spawnProtectionIndicator.SetActive(false);
        if (killNotificationPanel != null) killNotificationPanel.SetActive(false);

        // Ensure MatchAudioController is present for match event sounds
        if (GetComponent<Shootx.Audio.MatchAudioController>() == null)
            gameObject.AddComponent<Shootx.Audio.MatchAudioController>();

        UpdateKillsDisplay(0);
        UpdateDeathsDisplay(0);
    }

    private System.Collections.IEnumerator BindLocalPlayerStats()
    {
        // Wait for local player object to spawn
        float waited = 0f;
        while (waited < 5f)
        {
            var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            if (localPlayer != null)
            {
                _localStats = localPlayer.GetComponent<PlayerMatchStats>();
                if (_localStats != null)
                {
                    _localStats.OnKillsChanged += UpdateKillsDisplay;
                    _localStats.OnDeathsChanged += UpdateDeathsDisplay;
                    UpdateKillsDisplay(_localStats.Kills);
                    UpdateDeathsDisplay(_localStats.Deaths);
                    break;
                }
            }
            waited += Time.deltaTime;
            yield return null;
        }
    }

    private void OnDestroy()
    {
        if (_localStats != null)
        {
            _localStats.OnKillsChanged -= UpdateKillsDisplay;
            _localStats.OnDeathsChanged -= UpdateDeathsDisplay;
        }
        KillAllTweens();
        if (Instance == this) Instance = null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Countdown
    // ═══════════════════════════════════════════════════════════════════════

    public void UpdateCountdown(int value)
    {
        if (value <= 0) return;

        if (countdownPanel != null) countdownPanel.SetActive(true);
        if (countdownText != null)
        {
            countdownText.text = value.ToString();
            countdownText.transform.localScale = Vector3.one;
            countdownText.color = new Color(countdownText.color.r, countdownText.color.g, countdownText.color.b, 1f);

            // Pop-in scale punch and fade
            Sequence seq = DOTween.Sequence();
            seq.Append(countdownText.transform
                .DOPunchScale(Vector3.one * countdownPunchScale, 0.4f, 6, 0.5f)
                .SetEase(Ease.OutQuart));
            if (countdownFadeDuration > 0f)
            {
                seq.Join(countdownText.DOFade(0.4f, countdownFadeDuration).SetDelay(0.6f));
            }
            _countdownTween = seq;
        }
    }

    private void ShowGoSignal()
    {
        if (countdownText != null)
        {
            _countdownTween?.Kill();
            countdownText.text = "GO!";
            countdownText.transform.localScale = Vector3.one * 0.5f;
            countdownText.color = new Color(countdownText.color.r, countdownText.color.g, countdownText.color.b, 1f);

            Sequence goSeq = DOTween.Sequence();
            goSeq.Append(countdownText.transform.DOScale(1.4f, goDuration * 0.6f).SetEase(Ease.OutBack));
            goSeq.Join(countdownText.DOFade(0f, goDuration).SetDelay(goDuration * 0.4f));
            goSeq.OnComplete(() =>
            {
                if (countdownPanel != null) countdownPanel.SetActive(false);
            });
        }
        else if (countdownPanel != null)
        {
            countdownPanel.SetActive(false);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Timer
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateTimer(float remaining)
    {
        if (timerText == null) return;

        int minutes = Mathf.FloorToInt(remaining / 60f);
        int seconds = Mathf.FloorToInt(remaining % 60f);
        timerText.text = $"{minutes:00}:{seconds:00}";

        // Warning threshold pulse
        if (!_warningTriggered && remaining <= warningThreshold && remaining > 0)
        {
            _warningTriggered = true;
            timerText.color = warningTimerColor;

            _timerPulseTween?.Kill();
            _timerPulseTween = timerText.transform
                .DOPunchScale(Vector3.one * 0.15f, 0.5f, 5, 0.3f)
                .SetLoops(3);
        }
        else if (remaining > warningThreshold && _warningTriggered)
        {
            _warningTriggered = false;
            timerText.color = normalTimerColor;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Kills / Deaths
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateKillsDisplay(int kills)
    {
        if (killsText == null) return;
        int target = MatchManager.Instance != null ? (int)MatchManager.Instance.GetRemainingTime() : 10;
        // Show kill target from MatchConfig
        int killTarget = 10;
        killsText.text = $"kills: {kills} / {killTarget}";

        // Punch scale on kill
        killsText.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 5, 0.5f);

        // Brief notification
        ShowKillNotification();
    }

    private void UpdateDeathsDisplay(int deaths)
    {
        if (deathsText == null) return;
        deathsText.text = $"Death: {deaths}";
    }

    private void ShowKillNotification()
    {
        if (killNotificationPanel == null) return;
        _killNotificationTween?.Kill();

        Shootx.Audio.AudioManager.Instance?.PlaySound2D(Shootx.Audio.SoundId.Match_KillConfirmed);

        killNotificationPanel.SetActive(true);
        if (killNotificationText != null)
            killNotificationText.text = "Kill!";

        var cg = killNotificationPanel.GetComponent<CanvasGroup>();
        if (cg == null) cg = killNotificationPanel.AddComponent<CanvasGroup>();
        cg.alpha = 1f;

        _killNotificationTween = cg.DOFade(0f, 0.5f)
            .SetDelay(killNotificationDuration)
            .OnComplete(() => killNotificationPanel.SetActive(false));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Spawn Protection
    // ═══════════════════════════════════════════════════════════════════════

    public void ShowSpawnProtectionForPlayer(ulong clientId)
    {
        // Only show for local player
        if (NetworkManager.Singleton?.LocalClientId != clientId) return;
        Shootx.Audio.AudioManager.Instance?.PlaySound2D(Shootx.Audio.SoundId.Player_SpawnProtection_Start, 0.85f);
        if (spawnProtectionIndicator != null)
        {
            spawnProtectionIndicator.SetActive(true);
            spawnProtectionIndicator.transform.DOPunchScale(Vector3.one * 0.1f, 0.3f, 3, 0.3f);
        }
    }

    public void HideSpawnProtectionForPlayer(ulong clientId)
    {
        if (NetworkManager.Singleton?.LocalClientId != clientId) return;
        Shootx.Audio.AudioManager.Instance?.PlaySound2D(Shootx.Audio.SoundId.Player_SpawnProtection_End, 0.8f);
        if (spawnProtectionIndicator != null)
            spawnProtectionIndicator.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Respawn
    // ═══════════════════════════════════════════════════════════════════════

    public void OnLocalPlayerRespawned()
    {
        _warningTriggered = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Match State
    // ═══════════════════════════════════════════════════════════════════════

    private void HandleMatchStateChanged(MatchState state)
    {
        if (state == MatchState.Ending || state == MatchState.Results || state == MatchState.ReturningToLobby)
        {
            // Hide HUD elements during results
            if (timerText != null) timerText.gameObject.SetActive(false);
        }
        else if (state == MatchState.Active)
        {
            if (timerText != null) timerText.gameObject.SetActive(true);
            _warningTriggered = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Cleanup
    // ═══════════════════════════════════════════════════════════════════════

    private void KillAllTweens()
    {
        _killNotificationTween?.Kill();
        _countdownTween?.Kill();
        _timerPulseTween?.Kill();
    }
}
