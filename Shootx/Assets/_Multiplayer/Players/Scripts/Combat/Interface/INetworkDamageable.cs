/// <summary>
/// Interface for any entity that can receive damage.
/// Implement this on players, enemies, destructible objects, etc.
/// </summary>
public interface INetworkDamageable
{
    /// <summary>
    /// Apply damage to this entity.
    /// </summary>
    /// <param name="damage">Amount of damage to apply.</param>
    /// <param name="attackerClientId">The NetworkClientId of the player who dealt the damage.</param>
    void TakeDamage(int damage, ulong attackerClientId);
}