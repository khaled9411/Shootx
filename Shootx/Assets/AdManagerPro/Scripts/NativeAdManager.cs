#if NATIVE_ADS
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GoogleMobileAds.Api;
using System.Collections;
using System;

namespace AdManagerPro
{
    public static class NativeAdManager
    {
        private static NativeAd nativeAd;
        public static bool isNativeAdReady = false;

        private static MobileAdsSettings settings;
        private static string nativeID;
        public static Image adImage;
        public static TMP_Text adHeadlineText;
        public static TMP_Text adBodyText;
        static NativeAdManager()
        {
            settings = Resources.Load<MobileAdsSettings>("MobileAdsSettings");

            if (settings == null)
            {
                Debug.LogError("MobileAdsSettings not found in Resources folder.");
                return;
            }

            MobileAds.Initialize(initStatus => { });
            CheckID();
            RequestNativeAd();
        }



        public static void RequestNativeAd()
        {
            AdLoader adLoader = new AdLoader.Builder(nativeID).ForNativeAd().Build();
            adLoader.OnNativeAdLoaded += HandleNativeAdLoaded;
            adLoader.OnAdFailedToLoad += HandleNativeAdFailedToLoad;

            adLoader.LoadAd(new AdRequest());
        }

        private static void HandleNativeAdLoaded(object sender, NativeAdEventArgs e)
        {
            Debug.Log("Native ad loaded");
            nativeAd = e.nativeAd;


            Texture2D iconTexture = nativeAd.GetIconTexture();
            Sprite sprite = Sprite.Create(iconTexture, new Rect(0, 0, iconTexture.width, iconTexture.height), Vector2.one * 0.5f);

            adImage.sprite = sprite;

            string headline = nativeAd.GetHeadlineText();
            adHeadlineText.text = headline;

            string bodyText = nativeAd.GetBodyText();
            adBodyText.text = bodyText;

        }

        private static void HandleNativeAdFailedToLoad(object sender, AdFailedToLoadEventArgs e)
        {
            Debug.LogError("Native ad failed to load: " + e.LoadAdError.GetMessage());
            isNativeAdReady = false;
            RequestNativeAd();
        }

        public static void ShowNativeAd(Image adImage, TMP_Text adHeadlineText, TMP_Text adBodyText)
        {
            if (isNativeAdReady && nativeAd != null)
            {

            }
            else
            {
                Debug.LogWarning("Native ad is not ready.");
                RequestNativeAd();
            }
        }

        public static void DestroyNativeAd()
        {
            if (nativeAd != null)
            {
                nativeAd.Destroy();
                nativeAd = null;
            }
        }

        private static void CheckID()
        {
            if (settings.testMode) nativeID = "ca-app-pub-3940256099942544/2247696110";
            else nativeID = settings.nativeID;
        }
    }
}
#endif