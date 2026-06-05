#if UNITY_EDITOR
using UnityEditor;

using UnityEngine;
using System.IO;
namespace AdManagerPro
{
    public static class AdsSymbolManager
    {
        static AdsSymbolManager()
        {
            CheckForGoogleMobileAds();
            CheckForGoogleMobileNativeAds();
        }

        public static void CheckForGoogleMobileAds()
        {
            string path = Path.Combine(Application.dataPath, "GoogleMobileAds");
            if (Directory.Exists(path))
            {
                EnableAdsSymbol();
            }
            else
            {
                Debug.LogWarning("Google Mobile Ads SDK not found.");
            }
        }
        public static void CheckForGoogleMobileNativeAds()
        {
            string path = Path.Combine(Application.dataPath, "GoogleMobileAdsNative");
            if (Directory.Exists(path))
            {
                EnableAdsNativeSymbol();
            }
            else
            {
                Debug.LogWarning("Google Mobile Ads Native SDK not found.");
            }
        }
#if UNITY_EDITOR
        private static void EnableAdsSymbol()
        {
            string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            if (!symbols.Contains("ADS"))
            {
                symbols += ";ADS";
                PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, symbols);
            }
        }
        private static void EnableAdsNativeSymbol()
        {
            string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            if (!symbols.Contains("NATIVE_ADS"))
            {
                symbols += ";NATIVE_ADS";
                PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, symbols);
            }
        }
#endif
    }
}
#endif