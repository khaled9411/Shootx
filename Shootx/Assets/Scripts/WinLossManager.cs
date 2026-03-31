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

    #endregion

    // ===================================================================
    #region Private State

    private int _totalEnemies;
    private bool _isGameEnded = false;
    private bool _isCheckingLoss = false;

    #endregion

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(2f);
        InitializeEnemies();

        if (shootingSystem == null)
        {
            shootingSystem = FindFirstObjectByType<ShootingSystem>();
            if (shootingSystem == null)
            {
                Debug.LogWarning("[WinLossManager] ShootingSystem is missing!");
            }
        }
    }

    private void Update()
    {
        if (_isGameEnded || shootingSystem == null) return;

        if (shootingSystem.GetCurrentAmmo() == 0 && !_isCheckingLoss && _totalEnemies > 0)
        {
            StartCoroutine(CheckLossRoutine());
        }
    }

    // ===================================================================
    #region Core Logic

    private void InitializeEnemies()
    {
        EnemyAI[] enemiesInScene = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        _totalEnemies = enemiesInScene.Length;

        foreach (EnemyAI enemy in enemiesInScene)
        {
            if (enemy != null)
            {
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

        if (_totalEnemies <= 0)
        {
            TriggerWin();
        }
    }

    private IEnumerator CheckLossRoutine()
    {
        _isCheckingLoss = true;

        yield return new WaitForSeconds(loseCheckDelay);

        if (!_isGameEnded && shootingSystem.GetCurrentAmmo() == 0 && _totalEnemies > 0)
        {
            TriggerLose();
        }

        _isCheckingLoss = false;
    }

    private void TriggerWin()
    {
        _isGameEnded = true;
        StopAllCoroutines();

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
        _isGameEnded = true;

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