using UnityEngine;

/// <summary>
/// Lightweight local-only projectile used for client-side prediction.
/// Spawned instantly when the player fires to give immediate visual feedback,
/// then destroyed when the authoritative NetworkProjectile catches up or on timeout.
/// This projectile has NO NetworkObject — it exists only on the local client.
/// </summary>
public class DummyProjectile : MonoBehaviour
{
    private float speed;
    private float lifetime;
    private float spawnTime;
    private Vector3 direction;

    /// <summary>
    /// Initialize the dummy projectile with movement parameters.
    /// Called immediately after Instantiate by PlayerShooting.
    /// </summary>
    public void Initialize(Vector3 fireDirection, ProjectileData data)
    {
        direction = fireDirection.normalized;
        speed = data.speed;
        lifetime = data.lifetime;
        spawnTime = Time.time;

        // Orient the projectile to face its movement direction
        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    private void Update()
    {
        // Move forward
        transform.position += direction * speed * Time.deltaTime;

        // Self-destruct after lifetime expires
        if (Time.time - spawnTime >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Called externally when the real NetworkProjectile has spawned and we
    /// no longer need the dummy. Provides a clean handoff.
    /// </summary>
    public void Dismiss()
    {
        // Could add a fade-out effect here later
        Destroy(gameObject);
    }
}
