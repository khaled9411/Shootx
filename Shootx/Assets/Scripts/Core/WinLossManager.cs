using System.Collections;
using UnityEngine;

public class WinLossManager : MonoBehaviour
{
    // ===================================================================
    #region Inspector

    [Header("Core References")]
    [SerializeField] private LevelLoader levelLoader;
    [SerializeField] private ShootingSystem shootingSystem;

    [Header("Settings")]
    [SerializeField] private float loseCheckDelay = 2.5f;
    [SerializeField] private float winCheckDelay = 2.0f;

    #endregion

    // ===================================================================
    #region Private State

    private int _totalEnemies;
    private bool _isGameEnded = false;
    private bool _isCheckingLoss = false;
    private bool _isCheckingWin = false;

    #endregion

    // ===================================================================
    #region Unity Lifecycle

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnEnable()
    {
        LevelLoader.OnLevelLoaded += HandleLevelLoaded;
        UIManager.OnEnterGame += HandleEnterGame;
    }

    private void OnDisable()
    {
        LevelLoader.OnLevelLoaded -= HandleLevelLoaded;
        UIManager.OnEnterGame -= HandleEnterGame;
    }

    private IEnumerator Start()
    {
        EnsureReferences();
        InitializeEnemies();

        // Safety check after level loading routine completes
        yield return new WaitForSecondsRealtime(1.2f);
        if (!_isGameEnded && (_totalEnemies <= 0 || shootingSystem == null))
        {
            EnsureReferences();
            InitializeEnemies();
        }
    }

    private void Update()
    {
        if (_isGameEnded) return;

        if (shootingSystem == null)
        {
            shootingSystem = FindFirstObjectByType<ShootingSystem>();
            if (shootingSystem == null) return;
        }

        // If enemies haven't been indexed yet but now exist in scene, initialize them
        if (_totalEnemies <= 0 && !_isCheckingWin)
        {
            EnemyAI[] enemiesInScene = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
            if (enemiesInScene.Length > 0)
            {
                InitializeEnemies();
            }
        }

        if (shootingSystem.GetCurrentAmmo() <= 0 && !_isCheckingLoss && _totalEnemies > 0)
        {
            StartCoroutine(CheckLossRoutine());
        }
    }

    #endregion

    // ===================================================================
    #region Core Logic

    private void HandleLevelLoaded()
    {
        EnsureReferences();
        InitializeEnemies();
    }

    private void HandleEnterGame()
    {
        EnsureReferences();
        if (_totalEnemies <= 0 && !_isGameEnded)
        {
            InitializeEnemies();
        }
    }

    private void EnsureReferences()
    {
        if (levelLoader == null)
        {
            levelLoader = FindFirstObjectByType<LevelLoader>();
        }

        if (shootingSystem == null)
        {
            shootingSystem = FindFirstObjectByType<ShootingSystem>();
        }
    }

    public void InitializeEnemies()
    {
        EnsureReferences();

        EnemyAI[] enemiesInScene = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        _totalEnemies = enemiesInScene.Length;

        foreach (EnemyAI enemy in enemiesInScene)
        {
            if (enemy != null)
            {
                enemy.OnEnemyDeath.RemoveListener(OnEnemyKilled);
                enemy.OnEnemyDeath.AddListener(OnEnemyKilled);
            }
        }

        Debug.Log($"[WinLossManager] Found {_totalEnemies} enemies in the scene.");
    }

    private void OnEnemyKilled()
    {
        if (_isGameEnded) return;

        _totalEnemies--;
        Debug.Log($"[WinLossManager] Enemy killed. Remaining: {_totalEnemies}");

        if (_totalEnemies <= 0 && !_isCheckingWin)
        {
            StartCoroutine(CheckWinRoutine());
        }
    }

    private IEnumerator CheckWinRoutine()
    {
        _isCheckingWin = true;

        yield return new WaitForSeconds(winCheckDelay);

        if (!_isGameEnded)
        {
            TriggerWin();
        }

        _isCheckingWin = false;
    }

    private IEnumerator CheckLossRoutine()
    {
        _isCheckingLoss = true;

        yield return new WaitForSeconds(loseCheckDelay);

        if (!_isGameEnded && shootingSystem != null && shootingSystem.GetCurrentAmmo() <= 0 && _totalEnemies > 0)
        {
            TriggerLose();
        }

        _isCheckingLoss = false;
    }

    private void TriggerWin()
    {
        if (_isGameEnded) return;
        _isGameEnded = true;
        StopAllCoroutines();

        EnsureReferences();

        if (levelLoader != null)
        {
            levelLoader.OnWin();
        }
        else
        {
            Debug.LogError("[WinLossManager] LevelLoader reference is missing!");
        }
    }

    private void TriggerLose()
    {
        if (_isGameEnded) return;
        _isGameEnded = true;
        StopAllCoroutines();

        EnsureReferences();

        if (levelLoader != null)
        {
            levelLoader.OnLose();
        }
        else
        {
            Debug.LogError("[WinLossManager] LevelLoader reference is missing!");
        }
    }

    #endregion
}
