#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Reflection;
using UnityEditor.SceneManagement;

namespace AdManagerPro
{
    public class MobileAdsWindow : EditorWindow
    {
        string[] adProviders = new string[] { "AdMob" };

        bool testMode = false;
        bool nativeSDK = false;
        string appID = "";
        bool adaptiveBanner = false;
        string bannerID = "";
        string interstitialID = "";
        string rewardedVideoID = "";
        string rewardedInterstitialID = "";
        string nativeID = "";
        string appOpenID = "";
        private const string settingsAssetPath = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
        MobileAdsSettings settings;
#if UNITY_EDITOR
        [MenuItem("Tools/AdManager Pro - Easy Setup")]
        public static void ShowWindow()
        {
            var window = GetWindow<MobileAdsWindow>("AdManager Pro - Easy Setup");
            window.minSize = new Vector2(400, 750);
        }
#endif
        private void OnEnable()
        {
            settings = AssetDatabase.LoadAssetAtPath<MobileAdsSettings>("Assets/AdManagerPro/Resources/MobileAdsSettings.asset");
            if (settings == null)
            {
                settings = CreateInstance<MobileAdsSettings>();
                AssetDatabase.CreateAsset(settings, "Assets/AdManagerPro/Resources/MobileAdsSettings.asset");
                AssetDatabase.SaveAssets();
            }
            AdsSymbolManager.CheckForGoogleMobileAds();
            AdsSymbolManager.CheckForGoogleMobileNativeAds();
            LoadSettings();
        }

        private void OnGUI()
        {
            GUILayout.Label("Settings", EditorStyles.boldLabel);
            GUILayout.Space(10);
            GUILayout.Label("Step 1. Setup Admob SDK", EditorStyles.whiteLabel);
            GUILayout.Space(5);
            if (GUILayout.Button("Download Admob SDK"))
            {
                Application.OpenURL("https://github.com/googleads/googleads-mobile-unity/releases");
            }

            GUILayout.Space(10);
            var label = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                wordWrap = true
            };
            GUILayout.Label("If you will be using AdMob native ads, check this box and download the SDK.", label);
            GUILayout.Space(5);
            nativeSDK = EditorGUILayout.Toggle("Admob Native", nativeSDK);

            if (nativeSDK)
            {
                GUILayout.Space(10);
                GUILayout.Label("Step 1.1 Setup Admob Native SDK", EditorStyles.whiteLabel);
                GUILayout.Space(5);
                if (GUILayout.Button("Download Admob Native SDK"))
                {
                    Application.OpenURL("https://developers.google.com/admob/unity/native");
                }
            }
            GUILayout.Space(10);
            GUILayout.Label("Step 2. Enable Ad Testing Mode for Editor", EditorStyles.whiteLabel);
            GUILayout.Space(5);
            testMode = EditorGUILayout.Toggle("Test Mode", testMode);
            GUILayout.Space(5);
            if (testMode)
            {
                EditorGUILayout.HelpBox("Warning: Test Mode is enabled. Be sure to disable it before releasing your application.", MessageType.Warning);
            }
            GUILayout.Space(10);
            GUILayout.Label("Step 3. Enter Your Ad IDs. Do not use test IDs.", EditorStyles.whiteLabel);
            GUILayout.Space(5);
            appID = EditorGUILayout.TextField("App ID", appID);
            GUILayout.Space(5);
            adaptiveBanner = EditorGUILayout.Toggle("Adaptive Banner", adaptiveBanner);
            bannerID = EditorGUILayout.TextField("Banner ID", bannerID);
            interstitialID = EditorGUILayout.TextField("Interstitial ID", interstitialID);
            rewardedVideoID = EditorGUILayout.TextField("Rewarded Video ID", rewardedVideoID);
            rewardedInterstitialID = EditorGUILayout.TextField("Rewarded Interstitial ID", rewardedInterstitialID);
            appOpenID = EditorGUILayout.TextField("App Open ID", appOpenID);
            if (nativeSDK)
            {
                nativeID = EditorGUILayout.TextField("Native Ads ID", nativeID);
            }
            GUILayout.Space(10);
            GUILayout.Label("Step 4. Save the Changes.", EditorStyles.whiteLabel);
            GUILayout.Space(5);
            if (GUILayout.Button("Save"))
            {
                SaveSettings();
            }
            GUILayout.Space(10);
            var labelStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                wordWrap = true
            };
            GUILayout.Label("If you have any questions, you can always refer to the documentation using the button below or watch the video on YouTube.", labelStyle);

            GUILayout.Space(10);
            if (GUILayout.Button("Documentation"))
            {
                Application.OpenURL("https://codeum-games.gitbook.io/admanager-pro-easy-setup");
            }
            if (GUILayout.Button("Video Tutorial"))
            {
                Application.OpenURL("");
            }
            GUILayout.Space(10);
            GUILayout.Label("To view the asset usage, click the Example Scene button.", labelStyle);
            GUILayout.Space(5);
            if (GUILayout.Button("Example Scene"))
            {
                string scenePath = "Assets/AdManagerPro/Example/Scenes/Example.unity";
                EditorSceneManager.OpenScene(scenePath);
            }
        }

        private void SaveSettings()
        {
            settings.testMode = testMode;
            settings.adaptiveBanner = adaptiveBanner;
            settings.appID = appID;
            settings.bannerID = bannerID;
            settings.interstitialID = interstitialID;
            settings.rewardedVideoID = rewardedVideoID;
            settings.rewardedInterstitialID = rewardedInterstitialID;
            settings.nativeID = nativeID;
            settings.appOpenID = appOpenID;

            var settingsID = AssetDatabase.LoadAssetAtPath<ScriptableObject>(settingsAssetPath);
            if (settingsID != null)
            {
                var type = settingsID.GetType();
                var field = type.GetField("adMobAndroidAppId", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    if (testMode)
                    {
                        field.SetValue(settingsID, "ca-app-pub-3940256099942544~3347511713");
                    }
                    else
                    {
                        field.SetValue(settingsID, appID);
                    }
                    EditorUtility.SetDirty(settingsID);
                    AssetDatabase.SaveAssets();
                }
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("Settings Saved");
        }

        public void LoadSettings()
        {
            testMode = settings.testMode;
            appID = settings.appID;
            adaptiveBanner = settings.adaptiveBanner;
            bannerID = settings.bannerID;
            interstitialID = settings.interstitialID;
            rewardedVideoID = settings.rewardedVideoID;
            rewardedInterstitialID = settings.rewardedInterstitialID;
            nativeID = settings.nativeID;
            appOpenID = settings.appOpenID;
        }
    }
}
#endif
