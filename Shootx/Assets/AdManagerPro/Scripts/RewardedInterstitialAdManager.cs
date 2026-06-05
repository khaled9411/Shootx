#if ADS
using UnityEngine;
using GoogleMobileAds.Api;
using System.Collections;

namespace AdManagerPro
{
    public static class RewardedInterstitialAdManager
    {
        private static RewardedInterstitialAd rewardedInterstitialAd;
        public static bool isAdmobRewardedInterstitialReady = false;

        private static MobileAdsSettings settings;
        private static string rewardedInterstitialID;
        static RewardedInterstitialAdManager()
        {
            settings = Resources.Load<MobileAdsSettings>("MobileAdsSettings");

            if (settings == null)
            {
                Debug.LogError("MobileAdsSettings not found in Resources folder.");
                return;
            }

            MobileAds.Initialize(initStatus => { });
        }

        public static void RequestRewardedInterstitialAd()
        {
            if (settings == null)
            {
                Debug.LogError("MobileAdsSettings is null.");
                return;
            }

            if (rewardedInterstitialAd != null)
            {
                rewardedInterstitialAd.Destroy();
                rewardedInterstitialAd = null;
            }

            CheckID();

            AdRequest request = new AdRequest();

            RewardedInterstitialAd.Load(rewardedInterstitialID, request, (RewardedInterstitialAd ad, LoadAdError error) =>
            {
                if (error != null)
                {
                    Debug.LogError("Failed to load rewarded interstitial ad: " + error.GetMessage());
                    return;
                }
                else if (ad == null)
                {
                    Debug.LogError("Failed to load rewarded interstitial ad: ad is null");
                    return;
                }

                rewardedInterstitialAd = ad;
                isAdmobRewardedInterstitialReady = true;
                RegisterRewardedInterstitialEventHandlers(ad);
            });
        }

        private static void RegisterRewardedInterstitialEventHandlers(RewardedInterstitialAd rewardedInterstitialAd)
        {
            rewardedInterstitialAd.OnAdFullScreenContentClosed += () =>
            {
                isAdmobRewardedInterstitialReady = false;
                RequestRewardedInterstitialAd();
            };

            rewardedInterstitialAd.OnAdFullScreenContentFailed += (AdError error) =>
            {
                isAdmobRewardedInterstitialReady = false;
                RequestRewardedInterstitialAd();
            };

            // rewardedInterstitialAd.OnAdFailedToPresentFullScreenContent += (AdError error) =>
            // {
            //     isAdmobRewardedInterstitialReady = false;
            //     Debug.LogError("Rewarded interstitial ad failed to present full screen content: " + error.GetMessage());
            // };

            // rewardedInterstitialAd.OnUserEarnedReward += (Reward reward) =>
            // {
            //     Debug.Log("User earned reward: " + reward.Amount);
            // };
        }

        public static void ShowRewardedInterstitialAd(System.Action<bool> onAdWatched)
        {
            if (rewardedInterstitialAd != null && rewardedInterstitialAd.CanShowAd())
            {
                rewardedInterstitialAd.Show((Reward reward) =>
                {
                    onAdWatched?.Invoke(true); // Сообщаем, что реклама была просмотрена
                });
            }
            else
            {
                onAdWatched?.Invoke(false); // Сообщаем, что реклама не была показана
                RequestRewardedInterstitialAd();
            }
        }


        private static void CheckID()
        {
            if (settings.testMode) rewardedInterstitialID = "ca-app-pub-3940256099942544/5354046379";
            else rewardedInterstitialID = settings.rewardedInterstitialID;
        }
    }
}
#endif