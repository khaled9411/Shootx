using UnityEngine;

public class HeadHitbox : MonoBehaviour, IDamageable
{
    [Header("Settings")]
    [SerializeField] private EnemyAI mainEnemyAI;
    [SerializeField] private float headshotMultiplier = 2f;

    public void TakeDamage(float damage, Vector3 hitDirection)
    {
        if (mainEnemyAI == null)
        {
            Debug.LogError($"HeadHitbox on {gameObject.name} is missing mainEnemyAI reference!");
            return;
        }

        if (mainEnemyAI.IsDead())
            return;

        float finalDamage = damage * headshotMultiplier;
        Debug.Log($"Headshot! Dealing {finalDamage} damage.");

        mainEnemyAI.TakeDamage(finalDamage, hitDirection);
    }
}