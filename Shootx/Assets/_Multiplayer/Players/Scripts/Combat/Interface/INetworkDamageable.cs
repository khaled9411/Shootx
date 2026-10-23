/// <summary>
/// Interface for any entity that can receive damage.
/// Implement this on players, enemies, destructible objects, etc.
/// </summary>
public interface INetworkDamageable
{
    /// <summary>
    /// Apply fractional damage to this entity.
    /// </summary>
    /// <param name="damage">Amount of damage to apply (units, e.g. 1.5f).</param>
    /// <param name="attackerClientId">The NetworkClientId of the player who dealt the damage.</param>
    void TakeDamage(float damage, ulong attackerClientId);

    /// <summary>
    /// Apply integer damage to this entity (for backward compatibility with melee, etc.).
    /// </summary>
    /// <param name="damage">Amount of damage to apply.</param>
    /// <param name="attackerClientId">The NetworkClientId of the player who dealt the damage.</param>
    void TakeDamage(int damage, ulong attackerClientId);
}