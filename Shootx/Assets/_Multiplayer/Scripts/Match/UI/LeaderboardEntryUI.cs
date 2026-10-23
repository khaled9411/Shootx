using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI for a single entry in the end-of-match leaderboard.
/// Attach to the LeaderboardEntry prefab.
/// </summary>
public class LeaderboardEntryUI : MonoBehaviour
{
    [Header("Text Fields")]
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI killsText;
    [SerializeField] private TextMeshProUGUI deathsText;
    [SerializeField] private TextMeshProUGUI streakText;

    [Header("Visual Highlights")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Color winnerColor = new Color(1f, 0.84f, 0f, 0.3f);
    [SerializeField] private Color localPlayerColor = new Color(0.2f, 0.7f, 1f, 0.3f);
    [SerializeField] private Color defaultColor = new Color(1f, 1f, 1f, 0.1f);
    [SerializeField] private Color disconnectedColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);

    [Header("Winner Crown / Badge")]
    [SerializeField] private GameObject winnerBadge;

    public bool isOwned = false;

    public void Initialize(
        int rank,
        string playerName,
        int kills,
        int deaths,
        int bestStreak,
        bool isLocalPlayer,
        bool isWinner,
        bool isConnected)
    {

        isOwned = true;

        if (rankText != null)
            rankText.text = $"#{rank}";

        if (playerNameText != null)
        {
            playerNameText.text = playerName;
            if (!isConnected)
                playerNameText.text += "(disconnect)";
        }

        if (killsText != null)
            killsText.text = $"{kills}";

        if (deathsText != null)
            deathsText.text = $"{deaths}";

        if (streakText != null)
            streakText.text = $"{bestStreak}";

        if (winnerBadge != null)
            winnerBadge.SetActive(isWinner);

        // Background color
        if (backgroundImage != null)
        {
            if (!isConnected)
                backgroundImage.color = disconnectedColor;
            else if (isWinner)
                backgroundImage.color = winnerColor;
            else if (isLocalPlayer)
                backgroundImage.color = localPlayerColor;
            else
                backgroundImage.color = defaultColor;
        }
    }
}
