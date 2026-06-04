using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LeaderboardItemUI : MonoBehaviour
{
    public TMP_Text rankText;
    public TMP_Text nameText;
    public TMP_Text levelText;
    public Image backgroundImage;

    public void Setup(int rank, string playerName, int level)
    {
        rankText.text = rank.ToString();
        nameText.text = string.IsNullOrEmpty(playerName) ? "Unknown" : playerName;
        levelText.text = level.ToString();
    }

    public void SetColor(Color color)
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = color;
        }
        else
        {
            rankText.color = color;
            nameText.color = color;
            levelText.color = color;
        }
    }
}