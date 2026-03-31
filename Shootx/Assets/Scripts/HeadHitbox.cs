using UnityEngine;

public class HeadHitbox : MonoBehaviour, IDamageable
{
    [Header("Settings")]
    [SerializeField] private EnemyAI mainEnemyAI;
    [SerializeField] private float headshotMultiplier = 2f;

    public void TakeDamage(float damage, Vector3 hitDirection)
    {
        float finalDamage = damage * headshotMultiplier;

        Debug.Log("Headshot!");

        if (mainEnemyAI != null)
        {
            mainEnemyAI.TakeDamage(finalDamage, hitDirection);
        }
    }
}