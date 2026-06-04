using UnityEngine;
using System.Collections.Generic;

public enum EnemySelectionMode
{
    GlobalIndex,
    PerEnemy,
    Random,
    WeightedRandom,
    NoChange
}

[System.Serializable]
public class PerEnemyOverride
{
    public string enemyName;
    public int childIndex = 0;
}

[System.Serializable]
public class LevelVariant
{
    public int baseLevelNumber;
    public EnemySelectionMode selectionMode = EnemySelectionMode.Random;
    public int globalChildIndex = 0;
    public List<PerEnemyOverride> perEnemyOverrides = new List<PerEnemyOverride>();
    public float[] weightedRandomWeights;
}

public class LevelVariantConfig : MonoBehaviour
{
    public static LevelVariantConfig Instance { get; private set; }
    [SerializeField] private List<LevelVariant> levelSequence = new List<LevelVariant>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    public int SequenceLength => levelSequence != null ? levelSequence.Count : 0;
    public LevelVariant GetVariantForDisplayLevel(int displayLevel)
    {
        if (levelSequence == null || levelSequence.Count == 0)
        {
            Debug.LogWarning("[LevelVariantConfig] The levelSequence is empty!");
            return null;
        }

        int index = (displayLevel - 1) % levelSequence.Count;
        return levelSequence[index];
    }

    public int GetBaseLevelForDisplayLevel(int displayLevel)
    {
        LevelVariant v = GetVariantForDisplayLevel(displayLevel);
        return v != null ? v.baseLevelNumber : 1;
    }

#if UNITY_EDITOR
    [ContextMenu("Add a new element to the sequence")]
    private void AddEntry()
    {
        levelSequence.Add(new LevelVariant
        {
            baseLevelNumber = 1,
            selectionMode = EnemySelectionMode.GlobalIndex,
            globalChildIndex = 0
        });
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("Print the current order in Console")]
    private void PrintSequence()
    {
        for (int i = 0; i < levelSequence.Count; i++)
        {
            var v = levelSequence[i];
            Debug.Log($"Level {i + 1} for player to Base:{v.baseLevelNumber} | Mode:{v.selectionMode}" +
                      (v.selectionMode == EnemySelectionMode.GlobalIndex ? $" | ChildIndex:{v.globalChildIndex}" : ""));
        }
    }
#endif
}