using System.Collections;
using UnityEngine;

public class EnemyDeathDisappear : MonoBehaviour
{
    [Header("Disappear Settings")]
    [SerializeField] private float delayBeforeDisappear = 3f;
    [SerializeField] private float blinkDuration = 2f;
    [SerializeField] private float blinkInterval = 0.15f;

    private void Start()
    {
        EnemyAI enemyAI = GetComponent<EnemyAI>();

        if (enemyAI == null)
        {
            Debug.LogWarning($"[EnemyDeathDisappear] EnemyAI was not found on {gameObject.name}");
            return;
        }

        enemyAI.OnEnemyDeath.AddListener(OnEnemyDied);
    }

    private void OnEnemyDied()
    {
        StartCoroutine(DisappearRoutine());
    }

    private IEnumerator DisappearRoutine()
    {
        yield return new WaitForSeconds(delayBeforeDisappear);

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
        {
            Debug.LogWarning($"[EnemyDeathDisappear] There is no Renderer on {gameObject.name}");
            gameObject.SetActive(false);
            yield break;
        }

        float elapsed = 0f;
        bool isVisible = true;

        while (elapsed < blinkDuration)
        {
            isVisible = !isVisible;

            foreach (Renderer r in renderers)
                r.enabled = isVisible;

            yield return new WaitForSeconds(blinkInterval);
            elapsed += blinkInterval;
        }

        gameObject.SetActive(false);
    }
}