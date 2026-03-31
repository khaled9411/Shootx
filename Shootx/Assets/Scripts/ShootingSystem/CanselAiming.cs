using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CanselAiming : MonoBehaviour
{
    private ShootingSystem shootingSystem;
    private CanvasGroup canvasGroup;
    IEnumerator Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        yield return new WaitForSeconds(5f);
        shootingSystem = FindFirstObjectByType<ShootingSystem>();
        GetComponent<Button>().onClick.AddListener(shootingSystem.CancelShot);
    }
    private void Update()
    {
        if (shootingSystem != null && shootingSystem.IsAiming())
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
        }
        else
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
        }
    }
}
