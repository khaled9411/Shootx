#if ADS
using UnityEngine;
using GoogleMobileAds.Api;
using System;

namespace AdManagerPro
{
    public static class BannerAdManager
    {
        private static BannerView bannerView;
        public static bool isAdmobBannerReady = false;

        private static MobileAdsSettings settings;

        private static string bannerID;
        static BannerAdManager()
        {
            settings = Resources.Load<MobileAdsSettings>("MobileAdsSettings");

            if (settings == null)
            {
                Debug.LogError("MobileAdsSettings not found in Resources folder.");
                return;
            }

            MobileAds.Initialize(initStatus => { });
        }

        public static void RequestAdBanner(AdPosition adPosition = AdPosition.Bottom)
        {
            if (settings == null)
            {
                Debug.LogError("MobileAdsSettings is null.");
                return;
            }

            if (bannerView != null)
            {
                bannerView.Destroy();
            }

            AdSize adSize;
            if (settings.adaptiveBanner)
            {
                adSize = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
            }
            else
            {
                adSize = AdSize.Banner;
            }
            CheckID();
            bannerView = new BannerView(bannerID, adSize, adPosition);
            ListenToBannerAdEvents();
            AdRequest request = new AdRequest();
            bannerView.LoadAd(request);
        }

        private static void ListenToBannerAdEvents()
        {
            bannerView.OnBannerAdLoaded += () =>
            {
                isAdmobBannerReady = true;
                ShowAdBanner();
            };

            bannerView.OnBannerAdLoadFailed += (LoadAdError error) =>
            {
                isAdmobBannerReady = false;
                Debug.LogError("Failed to load banner ad: " + error.GetMessage());
            };
        }

        public static void ShowAdBanner()
        {
            if (bannerView != null && isAdmobBannerReady)
            {
                bannerView.Show();  
            }
            else
            {
                Debug.LogWarning("Banner ad is not ready.");
            }
        }

        public static void HideAdBanner()
        {
            if (bannerView != null)
            {
                bannerView.Hide();
            }
        }

        public static void DestroyAdBanner()
        {
            if (bannerView != null)
            {
                bannerView.Destroy();
                bannerView = null;
                isAdmobBannerReady = false;
            }
        }

        private static void CheckID(){
            if (settings.testMode)
            {
                if (settings.adaptiveBanner) bannerID = "ca-app-pub-3940256099942544/9214589741";
                else bannerID = "ca-app-pub-3940256099942544/6300978111";
            }
            else{
                bannerID = settings.bannerID;
            }
        }
    }
}
#endif