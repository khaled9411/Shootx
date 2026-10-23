using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;

/// <summary>
/// Holds per-player match statistics, synchronized by the server.
/// Lives as a NetworkBehaviour on the player prefab.
/// </summary>
public class PlayerMatchStats : NetworkBehaviour
{
    // ─── Networked Stats ────────────────────────────────────────────────

    private NetworkVariable<int> _kills = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<int> _deaths = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<int> _killStreak = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<int> _bestKillStreak = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>Authoritative player life state.</summary>
    private NetworkVariable<PlayerLifeState> _lifeState = new NetworkVariable<PlayerLifeState>(
        PlayerLifeState.Alive,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // ─── Public Properties ──────────────────────────────────────────────

    public int Kills => _kills.Value;
    public int Deaths => _deaths.Value;
    public int KillStreak => _killStreak.Value;
    public int BestKillStreak => _bestKillStreak.Value;
    public PlayerLifeState LifeState => _lifeState.Value;
    public bool IsAlive => _lifeState.Value == PlayerLifeState.Alive || _lifeState.Value == PlayerLifeState.SpawnProtected;
    public bool IsDead => _lifeState.Value == PlayerLifeState.Dead || _lifeState.Value == PlayerLifeState.Respawning;

    // ─── Events (all clients) ────────────────────────────────────────────

    public event Action<int> OnKillsChanged;
    public event Action<int> OnDeathsChanged;
    public event Action<int> OnKillStreakChanged;
    public event Action<PlayerLifeState, PlayerLifeState> OnLifeStateChanged;

    // ─── Lifecycle ───────────────────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _kills.OnValueChanged += (o, n) => OnKillsChanged?.Invoke(n);
        _deaths.OnValueChanged += (o, n) => OnDeathsChanged?.Invoke(n);
        _killStreak.OnValueChanged += (o, n) => OnKillStreakChanged?.Invoke(n);
        _lifeState.OnValueChanged += (o, n) => OnLifeStateChanged?.Invoke(o, n);
    }

    public override void OnNetworkDespawn()
    {
        _kills.OnValueChanged -= (o, n) => OnKillsChanged?.Invoke(n);
        _deaths.OnValueChanged -= (o, n) => OnDeathsChanged?.Invoke(n);
        _killStreak.OnValueChanged -= (o, n) => OnKillStreakChanged?.Invoke(n);
        _lifeState.OnValueChanged -= (o, n) => OnLifeStateChanged?.Invoke(o, n);
    }

    // ─── Server-Only Mutations ───────────────────────────────────────────

    /// <summary>Resets all stats for a new match. Call on server only.</summary>
    public void ResetForNewMatch()
    {
        if (!IsServer) return;
        _kills.Value = 0;
        _deaths.Value = 0;
        _killStreak.Value = 0;
        _bestKillStreak.Value = 0;
        _lifeState.Value = PlayerLifeState.Alive;
    }

    /// <summary>Registers a confirmed kill. Server only.</summary>
    public void AddKill()
    {
        if (!IsServer) return;
        _kills.Value++;
        _killStreak.Value++;
        if (_killStreak.Value > _bestKillStreak.Value)
            _bestKillStreak.Value = _killStreak.Value;
    }

    /// <summary>Registers a confirmed death. Server only.</summary>
    public void AddDeath()
    {
        if (!IsServer) return;
        _deaths.Value++;
        _killStreak.Value = 0;
    }

    /// <summary>Transitions to a new life state. Server only.</summary>
    public void SetLifeState(PlayerLifeState newState)
    {
        if (!IsServer) return;
        _lifeState.Value = newState;
    }
}

/// <summary>Explicit life-state enum for authoritative lifecycle control.</summary>
public enum PlayerLifeState
{
    Alive,
    Dead,
    Respawning,
    SpawnProtected
}
