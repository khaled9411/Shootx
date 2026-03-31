using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class LevelLoader : MonoBehaviour
{
    // ===================================================================
    #region Inspector

    [Header("Testing")]
    [SerializeField] private bool useTestLevel = false;
    [SerializeField] private int testLevelNumber = 1;

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

    // ===================================================================
    #region Private State

    private GameObject _currentLevelInstance;
    private int _currentLevelNumber;
    private bool _gameActive;

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

    // ===================================================================
    #region Loading Routine

    private IEnumerator LoadLevelRoutine()
    {
        if (useTestLevel)
        {
            _currentLevelNumber = testLevelNumber;
            Debug.Log($"<color=yellow>[LevelLoader] TEST MODE ON: Overriding level load to Level {_currentLevelNumber}</color>");
        }
        else
        {
            _currentLevelNumber = LevelStateManager.GetLevelToLoad();
        }

        if (GameDataManager.Instance != null)
            GameDataManager.Instance.SetLevel(_currentLevelNumber);

        Debug.Log($"[LevelLoader] Starting load for level {_currentLevelNumber}");

        yield return FadeLoading(1f);

        SetProgress(0f);

        string path = $"Levels/Level{_currentLevelNumber}";
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
            Debug.LogError($"[LevelLoader] Prefab Not found in: Resources/{path}");
            yield break;
        }

        if (_currentLevelInstance != null)
            Destroy(_currentLevelInstance);

        Transform parent = levelContainer != null ? levelContainer : transform;
        _currentLevelInstance = Instantiate(req.asset as GameObject, parent);

        SetProgress(0.92f);

        float elapsed = Time.realtimeSinceStartup - startTime;
        float remaining = minLoadingDuration - elapsed;
        if (remaining > 0f)
            yield return new WaitForSecondsRealtime(remaining);

        yield return AnimateProgressTo(1f, 0.25f);

        LevelStateManager.ClearResult();

        if (zoneProgressController != null)
            zoneProgressController.Refresh();

        Time.timeScale = 1f;
        Debug.Log("[LevelLoader] Game started — physics resumed.");

        yield return FadeLoading(0f);

        //UIManager.Instance?.ShowMainUI();

        Debug.Log($"[LevelLoader] Level {_currentLevelNumber} ready — waiting for player.");
    }

    #endregion

    // ===================================================================
    #region Enter Game

    private void HandleEnterGame()
    {
        if (_gameActive) return;
        _gameActive = true;
    }

    #endregion

    // ===================================================================
    #region Public Win / Lose API

    public void OnWin()
    {
        if (!_gameActive) return;

        _gameActive = false;
        Time.timeScale = 0f;

        if (GameDataManager.Instance != null)
            GameDataManager.Instance.AdvanceLevel();

        LevelStateManager.SaveWin(_currentLevelNumber);

        UIManager.Instance?.ShowWinScreen();

        Debug.Log($"[LevelLoader] Win! Next level = {_currentLevelNumber + 1}");
    }

    public void OnLose()
    {
        if (!_gameActive) return;

        _gameActive = false;
        Time.timeScale = 0f;

        LevelStateManager.SaveLose(_currentLevelNumber);

        UIManager.Instance?.ShowLoseScreen();

        Debug.Log($"[LevelLoader] Lose! Retry level = {_currentLevelNumber}");
    }

    #endregion

    // ===================================================================
    #region Helpers

    private void InitLoadingScreen()
    {
        if (loadingCanvasGroup == null) return;
        loadingCanvasGroup.alpha = 0f;
        loadingCanvasGroup.interactable = false;
        loadingCanvasGroup.blocksRaycasts = false;
    }

    private void SetProgress(float value)
    {
        value = Mathf.Clamp01(value);

        if (progressBar != null)
            progressBar.value = value;
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

    #endregion
}