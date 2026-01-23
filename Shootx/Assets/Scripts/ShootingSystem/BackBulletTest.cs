using UnityEngine;

public class BackBulletTest : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("BackBulletTest: Trigger entered by " + other.name);
        if (other.TryGetComponent<BulletController>(out _))
        {
            Debug.Log("BackBulletTest: Bullet returned.");
            FindFirstObjectByType<ShootingSystem>().ReturnLastBullet();
        }
    }
}
