using UnityEngine;
using System.Collections.Generic;
public class LevelVariantApplier : MonoBehaviour
{
    public static LevelVariantApplier Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private LevelVariantConfig _config;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _config = GetComponent<LevelVariantConfig>();
        if (_config == null)
            _config = FindFirstObjectByType<LevelVariantConfig>();
    }

    public void ApplyCurrentVariant()
    {
        int displayLevel = LevelStateManager.GetCurrentDisplayLevel();

        if (_config == null)
        {
            Log("No LevelVariantConfig — The shapes will not change.", true);
            return;
        }

        LevelVariant variant = _config.GetVariantForDisplayLevel(displayLevel);

        if (variant == null || variant.selectionMode == EnemySelectionMode.NoChange)
        {
            Log($"Display Level {displayLevel}: NoChange — The shapes remain the same in the prefab.");
            return;
        }

        EnemyAppearanceController[] allEnemies =
            FindObjectsByType<EnemyAppearanceController>(FindObjectsSortMode.None);

        if (allEnemies.Length == 0)
        {
            Log("No EnemyAppearanceController was found in the scene.", true);
            return;
        }

        Log($"Display Level {displayLevel} to Base:{variant.baseLevelNumber} | Mode:{variant.selectionMode} | Enemies:{allEnemies.Length}");

        switch (variant.selectionMode)
        {
            case EnemySelectionMode.GlobalIndex:
                ApplyGlobal(allEnemies, variant.globalChildIndex);
                break;

            case EnemySelectionMode.PerEnemy:
                ApplyPerEnemy(allEnemies, variant.perEnemyOverrides, variant.globalChildIndex);
                break;

            case EnemySelectionMode.Random:
                ApplyRandom(allEnemies);
                break;

            case EnemySelectionMode.WeightedRandom:
                ApplyWeightedRandom(allEnemies, variant.weightedRandomWeights);
                break;
        }
    }


    private void ApplyGlobal(EnemyAppearanceController[] enemies, int childIndex)
    {
        foreach (var e in enemies)
        {
            e.SetActiveChild(childIndex);
            Log($"  {e.gameObject.name} to child {childIndex}");
        }
    }

    private void ApplyPerEnemy(EnemyAppearanceController[] enemies,
                               List<PerEnemyOverride> overrides, int fallbackIndex)
    {
        var map = new Dictionary<string, int>();
        if (overrides != null)
            foreach (var o in overrides)
                map[o.enemyName] = o.childIndex;

        foreach (var e in enemies)
        {
            if (map.TryGetValue(e.gameObject.name, out int idx))
            {
                e.SetActiveChild(idx);
                Log($"  {e.gameObject.name} to per-enemy child {idx}");
            }
            else
            {
                e.SetActiveChild(fallbackIndex);
                Log($"  {e.gameObject.name} to fallback child {fallbackIndex}");
            }
        }
    }

    private void ApplyRandom(EnemyAppearanceController[] enemies)
    {
        foreach (var e in enemies)
        {
            e.SetRandomChild();
            Log($"  {e.gameObject.name} to random child {e.GetActiveChildIndex()}");
        }
    }

    private void ApplyWeightedRandom(EnemyAppearanceController[] enemies, float[] weights)
    {
        foreach (var e in enemies)
        {
            e.SetWeightedRandomChild(weights);
            Log($"  {e.gameObject.name} to weighted child {e.GetActiveChildIndex()}");
        }
    }

    private void Log(string msg, bool warn = false)
    {
        if (!showDebugLogs) return;
        if (warn) Debug.LogWarning($"[LevelVariantApplier] {msg}");
        else Debug.Log($"[LevelVariantApplier] {msg}");
    }
}