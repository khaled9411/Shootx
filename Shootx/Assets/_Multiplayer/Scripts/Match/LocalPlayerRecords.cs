using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// Local-only persistent player statistics storage.
///
/// Stored in Application.persistentDataPath/player_records.json.
/// Keyed by stable Unity Authentication player ID (or fallback).
/// Not synchronized between devices. Not game-authoritative.
///
/// A MatchManager singleton uses this to record results after every finalized match.
/// </summary>
public class LocalPlayerRecords : MonoBehaviour
{
    public static LocalPlayerRecords Instance { get; private set; }

    private const string SaveFileName = "player_records.json";
    private const int MaxHistoryPerPlayer = 20;
    private const int SaveFormatVersion = 1;

    [Header("Settings")]
    [Tooltip("Maximum match-history entries to retain per player.")]
    [SerializeField] private int maxHistoryPerPlayer = MaxHistoryPerPlayer;

    private LocalRecordsFile _data;
    private string _savePath;
    private readonly HashSet<string> _recordedMatchIds = new HashSet<string>();

    // ═══════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ═══════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _savePath = Path.Combine(Application.persistentDataPath, SaveFileName);
        LoadRecords();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Public API
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Records a finalized match result for all observed participants.
    /// Safe to call multiple times — uses matchId for deduplication.
    /// </summary>
    public void RecordMatchResult(
        string matchId,
        MatchResultData result,
        Dictionary<ulong, string> authIdMap,
        Dictionary<ulong, string> nameMap)
    {
        if (string.IsNullOrEmpty(matchId))
        {
            Debug.LogWarning("[LocalRecords] Match ID is empty. Skipping record.");
            return;
        }

        if (_recordedMatchIds.Contains(matchId))
        {
            Debug.Log($"[LocalRecords] Match {matchId} already recorded. Skipping.");
            return;
        }
        _recordedMatchIds.Add(matchId);

        if (result?.Entries == null) return;

        foreach (var entry in result.Entries)
        {
            // Determine stable player ID
            string authId = entry.AuthId;
            if (string.IsNullOrEmpty(authId) && authIdMap != null)
                authIdMap.TryGetValue(entry.ClientId, out authId);
            if (string.IsNullOrEmpty(authId))
                authId = $"client_{entry.ClientId}";

            string displayName = entry.DisplayName;
            if (string.IsNullOrEmpty(displayName) && nameMap != null)
                nameMap.TryGetValue(entry.ClientId, out displayName);

            // Outcome for this player
            MatchOutcome outcome;
            if (result.IsDraw && entry.Rank == 1)
                outcome = MatchOutcome.Draw;
            else if (!result.IsDraw && entry.ClientId == result.WinnerClientId)
                outcome = MatchOutcome.Win;
            else
                outcome = MatchOutcome.Loss;

            UpdatePlayerRecord(authId, displayName ?? $"Player_{entry.ClientId}", entry, outcome, matchId, result);
        }

        SaveRecords();
        Debug.Log($"[LocalRecords] Match {matchId} recorded for {result.Entries.Count} players.");
    }

    /// <summary>Gets the persistent record for a player by auth ID.</summary>
    public PlayerRecord GetPlayerRecord(string authId)
    {
        return _data?.players?.FirstOrDefault(p => p.authId == authId);
    }

    /// <summary>Gets all stored records.</summary>
    public List<PlayerRecord> GetAllRecords()
    {
        return _data?.players ?? new List<PlayerRecord>();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Record Update
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdatePlayerRecord(
        string authId,
        string displayName,
        PlayerResultEntry entry,
        MatchOutcome outcome,
        string matchId,
        MatchResultData fullResult)
    {
        if (_data.players == null) _data.players = new List<PlayerRecord>();

        var record = _data.players.FirstOrDefault(p => p.authId == authId);
        if (record == null)
        {
            record = new PlayerRecord { authId = authId };
            _data.players.Add(record);
        }

        // Update aggregate stats
        record.lastKnownName = displayName;
        record.matchesPlayed++;
        record.totalKills += entry.Kills;
        record.totalDeaths += entry.Deaths;
        record.bestKillStreak = Mathf.Max(record.bestKillStreak, entry.BestKillStreak);

        switch (outcome)
        {
            case MatchOutcome.Win:   record.wins++;   break;
            case MatchOutcome.Loss:  record.losses++; break;
            case MatchOutcome.Draw:  record.draws++;  break;
        }

        // Most recent match snapshot
        record.lastMatchResult = new MatchHistoryEntry
        {
            matchId = matchId,
            timestamp = DateTime.UtcNow.ToString("o"),
            kills = entry.Kills,
            deaths = entry.Deaths,
            rank = entry.Rank,
            outcome = outcome,
            opponents = BuildOpponentList(entry.ClientId, fullResult)
        };

        // Append to history (bounded)
        if (record.matchHistory == null) record.matchHistory = new List<MatchHistoryEntry>();
        record.matchHistory.Insert(0, record.lastMatchResult);
        int maxHistory = maxHistoryPerPlayer > 0 ? maxHistoryPerPlayer : MaxHistoryPerPlayer;
        if (record.matchHistory.Count > maxHistory)
            record.matchHistory.RemoveRange(maxHistory, record.matchHistory.Count - maxHistory);
    }

    private List<OpponentEntry> BuildOpponentList(ulong excludeClientId, MatchResultData result)
    {
        var list = new List<OpponentEntry>();
        foreach (var e in result.Entries)
        {
            if (e.ClientId == excludeClientId) continue;
            list.Add(new OpponentEntry
            {
                displayName = e.DisplayName,
                kills = e.Kills,
                deaths = e.Deaths,
                rank = e.Rank
            });
        }
        return list;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Persistence
    // ═══════════════════════════════════════════════════════════════════════

    private void LoadRecords()
    {
        try
        {
            if (File.Exists(_savePath))
            {
                string json = File.ReadAllText(_savePath);
                _data = JsonUtility.FromJson<LocalRecordsFile>(json);

                if (_data == null || _data.version != SaveFormatVersion)
                {
                    Debug.LogWarning("[LocalRecords] Save format mismatch or corrupt. Starting fresh.");
                    _data = CreateEmptyFile();
                }
                else
                {
                    Debug.Log($"[LocalRecords] Loaded {_data.players?.Count ?? 0} player records.");
                }
            }
            else
            {
                _data = CreateEmptyFile();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LocalRecords] Load failed: {ex.Message}. Starting fresh.");
            _data = CreateEmptyFile();
        }
    }

    private void SaveRecords()
    {
        try
        {
            _data.lastSaved = DateTime.UtcNow.ToString("o");
            string json = JsonUtility.ToJson(_data, prettyPrint: true);
            File.WriteAllText(_savePath, json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LocalRecords] Save failed: {ex.Message}");
        }
    }

    private LocalRecordsFile CreateEmptyFile()
    {
        return new LocalRecordsFile
        {
            version = SaveFormatVersion,
            players = new List<PlayerRecord>(),
            lastSaved = ""
        };
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// Data Models (JsonUtility-serializable)
// ═══════════════════════════════════════════════════════════════════════════

[Serializable]
public class LocalRecordsFile
{
    public int version;
    public string lastSaved;
    public List<PlayerRecord> players;
}

[Serializable]
public class PlayerRecord
{
    public string authId;
    public string lastKnownName;
    public int matchesPlayed;
    public int totalKills;
    public int totalDeaths;
    public int wins;
    public int losses;
    public int draws;
    public int bestKillStreak;
    public MatchHistoryEntry lastMatchResult;
    public List<MatchHistoryEntry> matchHistory;
}

[Serializable]
public class MatchHistoryEntry
{
    public string matchId;
    public string timestamp;
    public int kills;
    public int deaths;
    public int rank;
    public MatchOutcome outcome;
    public List<OpponentEntry> opponents;
}

[Serializable]
public class OpponentEntry
{
    public string displayName;
    public int kills;
    public int deaths;
    public int rank;
}

public enum MatchOutcome
{
    Win,
    Loss,
    Draw
}
