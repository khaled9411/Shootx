using UnityEngine;
using System.Collections.Generic;

public class LevelVariantApplier : MonoBehaviour
{
    public static LevelVariantApplier Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private LevelVariantConfig _config;

    private const string PREFS_PREFIX = "EAC_";

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
            Log("There is no LevelVariantConfig — the shapes will not change.", true);
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
                ApplyRandomWithPersistence(allEnemies, displayLevel, variant.weightedRandomWeights, weighted: false);
                break;

            case EnemySelectionMode.WeightedRandom:
                ApplyRandomWithPersistence(allEnemies, displayLevel, variant.weightedRandomWeights, weighted: true);
                break;
        }
    }

    private void ApplyRandomWithPersistence(EnemyAppearanceController[] enemies,
                                            int displayLevel,
                                            float[] weights,
                                            bool weighted)
    {
        bool allSaved = AreAllEnemiesSaved(enemies, displayLevel);

        if (allSaved)
        {
            Log($"Display Level {displayLevel}: Applying the previously saved Random result.");
            foreach (var e in enemies)
            {
                int savedIndex = LoadEnemyIndex(displayLevel, e.gameObject.name);
                e.SetActiveChild(savedIndex);
                Log($"  {e.gameObject.name} to saved child {savedIndex}");
            }
        }
        else
        {
            Log($"Display Level {displayLevel}: Calculate Random for the first time and save it.");
            foreach (var e in enemies)
            {
                if (weighted && weights != null && weights.Length > 0)
                    e.SetWeightedRandomChild(weights);
                else
                    e.SetRandomChild();

                int chosenIndex = e.GetActiveChildIndex();
                SaveEnemyIndex(displayLevel, e.gameObject.name, chosenIndex);
                Log($"  {e.gameObject.name} to random child {chosenIndex} (saved)");
            }
        }
    }

    private string BuildKey(int displayLevel, string enemyName)
    {
        return $"{PREFS_PREFIX}{displayLevel}_{enemyName}";
    }

    private void SaveEnemyIndex(int displayLevel, string enemyName, int index)
    {
        PlayerPrefs.SetInt(BuildKey(displayLevel, enemyName), index);
        PlayerPrefs.Save();
    }

    private int LoadEnemyIndex(int displayLevel, string enemyName)
    {
        return PlayerPrefs.GetInt(BuildKey(displayLevel, enemyName), 0);
    }

    private bool AreAllEnemiesSaved(EnemyAppearanceController[] enemies, int displayLevel)
    {
        foreach (var e in enemies)
        {
            if (!PlayerPrefs.HasKey(BuildKey(displayLevel, e.gameObject.name)))
                return false;
        }
        return true;
    }

    public void ClearSavedRandom(int displayLevel, EnemyAppearanceController[] enemies)
    {
        if (enemies == null) return;
        foreach (var e in enemies)
        {
            string key = BuildKey(displayLevel, e.gameObject.name);
            if (PlayerPrefs.HasKey(key))
                PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save();
        Log($"Display Level {displayLevel}: Cleared saved Random results.");
    }

    public void ClearAllSavedRandom()
    {
        if (_config == null) return;

        EnemyAppearanceController[] allEnemies =
            FindObjectsByType<EnemyAppearanceController>(FindObjectsSortMode.None);

        for (int lvl = 1; lvl <= _config.SequenceLength; lvl++)
        {
            LevelVariant v = _config.GetVariantForDisplayLevel(lvl);
            if (v == null) continue;
            if (v.selectionMode != EnemySelectionMode.Random &&
                v.selectionMode != EnemySelectionMode.WeightedRandom) continue;

            foreach (var e in allEnemies)
                PlayerPrefs.DeleteKey(BuildKey(lvl, e.gameObject.name));
        }
        PlayerPrefs.Save();
        Log("Cleared all saved Random results.");
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

    private void Log(string msg, bool warn = false)
    {
        if (!showDebugLogs) return;
        if (warn) Debug.LogWarning($"[LevelVariantApplier] {msg}");
        else Debug.Log($"[LevelVariantApplier] {msg}");
    }
}