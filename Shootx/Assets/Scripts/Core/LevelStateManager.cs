using UnityEngine;

public static class LevelStateManager
{
    private const string KEY_DISPLAY_LEVEL = "CurrentDisplayLevel";
    private const string KEY_RESULT = "LevelResult";

    public static int GetCurrentDisplayLevel()
    {
        return PlayerPrefs.GetInt(KEY_DISPLAY_LEVEL, 1);
    }

    public static int GetLevelToLoad()
    {
        int displayLevel = GetCurrentDisplayLevel();

        if (LevelVariantConfig.Instance == null)
        {
            Debug.LogWarning("[LevelStateManager] LevelVariantConfig.Instance = null — Loading Level1 as fallback.");
            return 1;
        }

        return LevelVariantConfig.Instance.GetBaseLevelForDisplayLevel(displayLevel);
    }


    public static void SaveWin(int displayLevel)
    {
        PlayerPrefs.SetString(KEY_RESULT, "Win");
        PlayerPrefs.SetInt(KEY_DISPLAY_LEVEL, displayLevel + 1);
        PlayerPrefs.Save();
    }

    public static void SaveLose(int displayLevel)
    {
        PlayerPrefs.SetString(KEY_RESULT, "Lose");
        PlayerPrefs.Save();
    }

    public static void ClearResult()
    {
        PlayerPrefs.DeleteKey(KEY_RESULT);
    }

    public static string GetLastResult()
    {
        return PlayerPrefs.GetString(KEY_RESULT, "");
    }

    public static void ReturnToFirstLevel()
    {
        PlayerPrefs.SetInt(KEY_DISPLAY_LEVEL, 1);
        PlayerPrefs.Save();
    }

    public static void PrintCurrentState()
    {
        int display = GetCurrentDisplayLevel();
        int baseLevel = LevelVariantConfig.Instance != null
            ? LevelVariantConfig.Instance.GetBaseLevelForDisplayLevel(display)
            : -1;

        Debug.Log($"[LevelStateManager] Display Level: {display} | Base Level (to load): {baseLevel}");
    }
}