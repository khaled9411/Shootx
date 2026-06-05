#if ADS
using UnityEngine;
using GoogleMobileAds.Api;
using System;

namespace AdManagerPro
{
    public static class AppOpenAdManager
    {
        private static AppOpenAd appOpenAd;
        public static bool isAdmobAppOpenReady = false;
        private static DateTime lastAdLoadTime;

        private static MobileAdsSettings settings;
        private static string appOpenID;
        static AppOpenAdManager()
        {
            settings = Resources.Load<MobileAdsSettings>("MobileAdsSettings");

            if (settings == null)
            {
                Debug.LogError("MobileAdsSettings not found in Resources folder.");
                return;
            }

            MobileAds.Initialize(initStatus => { });
            LoadAppOpenAd();
        }

        public static void LoadAppOpenAd()
        {
            if (settings == null)
            {
                Debug.LogError("MobileAdsSettings is null.");
                return;
            }
            CheckID();
            AdRequest request = new AdRequest();
            AppOpenAd.Load(appOpenID, request, (AppOpenAd ad, LoadAdError error) =>
            {
                if (error != null)
                {
                    Debug.LogError("Failed to load app open ad: " + error.GetMessage());
                    return;
                }
                else if (ad == null)
                {
                    Debug.LogError("Failed to load app open ad: ad is null");
                    return;
                }

                appOpenAd = ad;
                isAdmobAppOpenReady = true;
                lastAdLoadTime = DateTime.Now;
                RegisterAppOpenEventHandlers(ad);
            });
        }

        private static void RegisterAppOpenEventHandlers(AppOpenAd appOpenAd)
        {
            appOpenAd.OnAdFullScreenContentClosed += () =>
            {
                isAdmobAppOpenReady = false;
                LoadAppOpenAd();
            };

            appOpenAd.OnAdFullScreenContentFailed += (AdError error) =>
            {
                isAdmobAppOpenReady = false;
                LoadAppOpenAd();
            };
        }

        public static void ShowAppOpenAd()
        {
            if (appOpenAd != null && isAdmobAppOpenReady && IsAdAvailable())
            {
                appOpenAd.Show();
            }
            else
            {
                Debug.LogWarning("App open ad is not ready.");
                LoadAppOpenAd();
            }
        }

        private static bool IsAdAvailable()
        {
            return appOpenAd != null && isAdmobAppOpenReady && (DateTime.Now - lastAdLoadTime).TotalHours < 4;
        }

        private static void CheckID()
        {
            if (settings.testMode) appOpenID = "ca-app-pub-3940256099942544/9257395921";
            else appOpenID = settings.appOpenID;
        }
    }
}
#endif