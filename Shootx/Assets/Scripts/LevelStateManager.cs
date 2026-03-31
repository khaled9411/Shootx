using UnityEngine;

public static class LevelStateManager
{
    private const string KEY_LAST_RESULT = "LastGameResult";
    private const string KEY_LEVEL_TO_LOAD = "LevelToLoad";

    public enum GameResult { None = 0, Win = 1, Lose = 2 }


    public static void SaveWin(int currentLevel)
    {
        PlayerPrefs.SetInt(KEY_LAST_RESULT, (int)GameResult.Win);
        PlayerPrefs.SetInt(KEY_LEVEL_TO_LOAD, currentLevel + 1);
        PlayerPrefs.Save();
        Debug.Log($"[LevelStateManager] Win saved > next level = {currentLevel + 1}");
    }

    public static void SaveLose(int currentLevel)
    {
        PlayerPrefs.SetInt(KEY_LAST_RESULT, (int)GameResult.Lose);
        PlayerPrefs.SetInt(KEY_LEVEL_TO_LOAD, currentLevel);
        PlayerPrefs.Save();
        Debug.Log($"[LevelStateManager] Lose saved > retry level = {currentLevel}");
    }

    public static GameResult GetLastResult()
        => (GameResult)PlayerPrefs.GetInt(KEY_LAST_RESULT, (int)GameResult.None);

    public static int GetLevelToLoad()
    {
        if (!PlayerPrefs.HasKey(KEY_LEVEL_TO_LOAD))
            return GameDataManager.Instance != null
                ? GameDataManager.Instance.CurrentLevel
                : 1;

        return PlayerPrefs.GetInt(KEY_LEVEL_TO_LOAD, 1);
    }

    public static void ClearResult()
    {
        PlayerPrefs.SetInt(KEY_LAST_RESULT, (int)GameResult.None);
        PlayerPrefs.DeleteKey(KEY_LEVEL_TO_LOAD);
        PlayerPrefs.Save();
    }

}