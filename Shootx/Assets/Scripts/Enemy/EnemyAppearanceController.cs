using UnityEngine;

public class EnemyAppearanceController : MonoBehaviour
{
    [SerializeField] private GameObject[] visualChildren;
    [SerializeField] private int defaultChildIndex = 0;

    public void SetActiveChild(int index)
    {
        if (visualChildren == null || visualChildren.Length == 0)
        {
            Debug.LogWarning($"[EnemyAppearanceController] No visual children on {gameObject.name}");
            return;
        }

        if (index < 0 || index >= visualChildren.Length)
        {
            Debug.LogWarning($"[EnemyAppearanceController] The index {index} is out of range on {gameObject.name} (number of shapes: {visualChildren.Length})");
            return;
        }

        ApplyChildIndex(index);
    }

    public void SetRandomChild()
    {
        if (visualChildren == null || visualChildren.Length == 0) return;
        int randomIndex = Random.Range(0, visualChildren.Length);
        ApplyChildIndex(randomIndex);
    }

    public void SetWeightedRandomChild(float[] weights)
    {
        if (visualChildren == null || visualChildren.Length == 0) return;
        if (weights == null || weights.Length != visualChildren.Length)
        {
            Debug.LogWarning($"[EnemyAppearanceController] The weights do not match the number of shapes on {gameObject.name}");
            SetRandomChild();
            return;
        }

        float totalWeight = 0f;
        foreach (float w in weights) totalWeight += w;

        float rand = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (rand <= cumulative)
            {
                ApplyChildIndex(i);
                return;
            }
        }

        // fallback
        ApplyChildIndex(visualChildren.Length - 1);
    }

    public void ResetToDefault()
    {
        ApplyChildIndex(defaultChildIndex);
    }

    public int GetActiveChildIndex()
    {
        if (visualChildren == null) return -1;
        for (int i = 0; i < visualChildren.Length; i++)
        {
            if (visualChildren[i] != null && visualChildren[i].activeSelf)
                return i;
        }
        return -1;
    }

    public int GetChildCount()
    {
        return visualChildren != null ? visualChildren.Length : 0;
    }

    private void ApplyChildIndex(int index)
    {
        for (int i = 0; i < visualChildren.Length; i++)
        {
            if (visualChildren[i] != null)
                visualChildren[i].SetActive(i == index);
        }
    }
}