using UnityEngine;

namespace AdManagerPro
{
    public class MobileAdsSettings : ScriptableObject
    {
        public bool testMode;
        public string appID;
        public bool adaptiveBanner;
        public string bannerID;
        public string interstitialID;
        public string rewardedVideoID;
        public string rewardedInterstitialID;
        public string nativeID;
        public string appOpenID;
    }
}