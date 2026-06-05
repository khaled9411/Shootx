using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class LevelLoader : MonoBehaviour
{
    #region Inspector

    [Header("Testing")]
    [SerializeField] private bool useTestLevel = false;
    [SerializeField] private int testDisplayLevel = 1;

    [Header("Loading Screen")]
    [SerializeField] private CanvasGroup loadingCanvasGroup;
    [SerializeField] private Slider progressBar;

    [Header("Level Spawn")]
    [SerializeField] private Transform levelContainer;

    [Header("References")]
    [SerializeField] private ZoneProgressController zoneProgressController;

    [Header("Timing")]
    [SerializeField] private float minLoadingDuration = 1.2f;
    [SerializeField] private float fadeDuration = 0.35f;

    #endregion

    #region Private State

    private GameObject _currentLevelInstance;
    private int _currentDisplayLevel;
    private bool _gameActive;
    private int _errorCount;

    #endregion

    private void Awake()
    {
        Time.timeScale = 0f;
        _gameActive = false;
        InitLoadingScreen();
    }

    private void Start()
    {
        UIManager.OnEnterGame += HandleEnterGame;
        StartCoroutine(LoadLevelRoutine());
    }

    private void OnDestroy()
    {
        UIManager.OnEnterGame -= HandleEnterGame;
        DOTween.Kill(this);
    }

    private IEnumerator LoadLevelRoutine()
    {
        if (useTestLevel)
        {
            _currentDisplayLevel = testDisplayLevel;
            Debug.Log($"<color=yellow>[LevelLoader] TEST MODE: Display Level {_currentDisplayLevel}</color>");
        }
        else
        {
            _currentDisplayLevel = LevelStateManager.GetCurrentDisplayLevel();
        }

        int baseLevelNumber = LevelVariantConfig.Instance != null
            ? LevelVariantConfig.Instance.GetBaseLevelForDisplayLevel(_currentDisplayLevel)
            : _currentDisplayLevel;

        if (GameDataManager.Instance != null)
            GameDataManager.Instance.SetLevel(_currentDisplayLevel);

        Debug.Log($"[LevelLoader] Display:{_currentDisplayLevel} to Loading Base Level:{baseLevelNumber}");

        yield return FadeLoading(1f);
        SetProgress(0f);

        string path = $"Levels/Level{baseLevelNumber}";
        float startTime = Time.realtimeSinceStartup;
        ResourceRequest req = Resources.LoadAsync<GameObject>(path);

        while (!req.isDone)
        {
            SetProgress(req.progress * 0.85f);
            yield return null;
        }

        SetProgress(0.85f);

        if (req.asset == null)
        {
            _errorCount++;
            Debug.LogError($"[LevelLoader] Prefab not found: Resources/{path}");
            LevelStateManager.ReturnToFirstLevel();

            if (_errorCount <= 3)
            {
                Debug.LogWarning($"[LevelLoader] Fallback attempt {_errorCount}/3");
                StartCoroutine(LoadLevelRoutine());
            }
            else
            {
                Debug.LogError("[LevelLoader] Multiple load errors. Check Resources folder.");
            }
            yield break;
        }

        _errorCount = 0;

        if (_currentLevelInstance != null)
            Destroy(_currentLevelInstance);

        Transform parent = levelContainer != null ? levelContainer : transform;
        _currentLevelInstance = Instantiate(req.asset as GameObject, parent);

        SetProgress(0.92f);

        yield return null;

        if (LevelVariantApplier.Instance != null)
            LevelVariantApplier.Instance.ApplyCurrentVariant();
        else
            Debug.LogWarning("[LevelLoader] LevelVariantApplier Not present in the scene.");

        float elapsed = Time.realtimeSinceStartup - startTime;
        float remaining = minLoadingDuration - elapsed;
        if (remaining > 0f)
            yield return new WaitForSecondsRealtime(remaining);

        yield return AnimateProgressTo(1f, 0.25f);

        LevelStateManager.ClearResult();

        if (zoneProgressController != null)
            zoneProgressController.Refresh();

        Time.timeScale = 1f;

        yield return FadeLoading(0f);

        Debug.Log($"[LevelLoader] Level ready — Display:{_currentDisplayLevel} Base:{baseLevelNumber}");
    }


    private void HandleEnterGame()
    {
        if (_gameActive) return;
        _gameActive = true;
    }

    public void OnWin()
    {
        if (!_gameActive) return;
        _gameActive = false;
        Time.timeScale = 0f;

        if (GameDataManager.Instance != null)
            GameDataManager.Instance.AdvanceLevel();

        if (LevelVariantApplier.Instance != null)
        {
            var enemies = FindObjectsByType<EnemyAppearanceController>(FindObjectsSortMode.None);
            LevelVariantApplier.Instance.ClearSavedRandom(_currentDisplayLevel, enemies);
        }

        LevelStateManager.SaveWin(_currentDisplayLevel);
        LeaderboardManager.Instance.UpdatePlayerLevel(_currentDisplayLevel);
        UIManager.Instance?.ShowWinScreen();

        Debug.Log($"[LevelLoader] Win! {_currentDisplayLevel} to {_currentDisplayLevel + 1}");
    }

    public void OnLose()
    {
        if (!_gameActive) return;
        _gameActive = false;
        Time.timeScale = 0f;

        LevelStateManager.SaveLose(_currentDisplayLevel);
        UIManager.Instance?.ShowLoseScreen();

        Debug.Log($"[LevelLoader] Lose! Retry {_currentDisplayLevel}");
    }

    public void OnSkipLevel()
    {
        _gameActive = false;
        Time.timeScale = 0f;

        if (GameDataManager.Instance != null)
            GameDataManager.Instance.AdvanceLevel();

        if (LevelVariantApplier.Instance != null)
        {
            var enemies = FindObjectsByType<EnemyAppearanceController>(FindObjectsSortMode.None);
            LevelVariantApplier.Instance.ClearSavedRandom(_currentDisplayLevel, enemies);
        }

        LevelStateManager.SaveWin(_currentDisplayLevel);
        LeaderboardManager.Instance.UpdatePlayerLevel(_currentDisplayLevel);
        UIManager.Instance?.ShowWinScreen();

        Debug.Log($"[LevelLoader] Skip! {_currentDisplayLevel} to {_currentDisplayLevel + 1}");
    }

    private void InitLoadingScreen()
    {
        if (loadingCanvasGroup == null) return;
        loadingCanvasGroup.alpha = 0f;
        loadingCanvasGroup.interactable = false;
        loadingCanvasGroup.blocksRaycasts = false;
    }

    private void SetProgress(float value)
    {
        if (progressBar != null)
            progressBar.value = Mathf.Clamp01(value);
    }

    private IEnumerator FadeLoading(float target)
    {
        if (loadingCanvasGroup == null) yield break;
        loadingCanvasGroup.blocksRaycasts = true;

        yield return loadingCanvasGroup
            .DOFade(target, fadeDuration)
            .SetUpdate(true)
            .WaitForCompletion();

        bool visible = target > 0.5f;
        loadingCanvasGroup.interactable = visible;
        loadingCanvasGroup.blocksRaycasts = visible;
    }

    private IEnumerator AnimateProgressTo(float target, float duration)
    {
        if (progressBar == null) yield break;
        float current = progressBar.value;

        yield return DOTween
            .To(() => current, v => SetProgress(v), target, duration)
            .SetUpdate(true)
            .WaitForCompletion();
    }
}