#if ADS
using UnityEngine;
using GoogleMobileAds.Api;
using System.Collections;

namespace AdManagerPro
{
    public static class RewardedAdManager
    {
        private static RewardedAd rewardedAd;
        public static bool isAdmobRewardedReady = false;
        private static MobileAdsSettings settings;
        private static string rewardedVideoID;

        static RewardedAdManager()
        {
            settings = Resources.Load<MobileAdsSettings>("MobileAdsSettings");

            if (settings == null)
            {
                Debug.LogError("MobileAdsSettings not found in Resources folder.");
                return;
            }

            MobileAds.Initialize(initStatus => { });
        }

        public static void RequestRewardedAd()
        {
            if (settings == null)
            {
                Debug.LogError("MobileAdsSettings is null.");
                return;
            }

            if (rewardedAd != null)
            {
                rewardedAd.Destroy();
                rewardedAd = null;
            }
            CheckID();
            AdRequest request = new AdRequest();

            RewardedAd.Load(rewardedVideoID, request, (RewardedAd ad, LoadAdError error) =>
            {
                if (error != null)
                {
                    Debug.LogError("Failed to load rewarded ad: " + error.GetMessage());
                    return;
                }
                else if (ad == null)
                {
                    Debug.LogError("Failed to load rewarded ad: ad is null");
                    return;
                }

                rewardedAd = ad;
                isAdmobRewardedReady = true;
                RegisterRewardedEventHandlers(ad);
            });
        }

        private static void RegisterRewardedEventHandlers(RewardedAd rewardedAd)
        {
            rewardedAd.OnAdFullScreenContentClosed += () =>
            {
                isAdmobRewardedReady = false;
                RequestRewardedAd();
            };

            rewardedAd.OnAdFullScreenContentFailed += (AdError error) =>
            {
                isAdmobRewardedReady = false;
                RequestRewardedAd();
            };

            // rewardedAd.OnAdFailedToPresentFullScreenContent += (AdError error) =>
            // {
            //     isAdmobRewardedReady = false;
            //     Debug.LogError("Rewarded ad failed to present full screen content: " + error.GetMessage());
            // };

            // rewardedAd.OnUserEarnedReward += (Reward reward) =>
            // {
            //     Debug.Log("User earned reward: " + reward.Amount);
            // };
        }

        public static void ShowRewardedAd(System.Action<bool> onAdWatched)
        {
            if (rewardedAd != null && rewardedAd.CanShowAd())
            {
                rewardedAd.Show((Reward reward) =>
                {
                    onAdWatched?.Invoke(true); // Передаем true в колбэк, если реклама была просмотрена
                });
            }
            else
            {
                Debug.LogWarning("Rewarded ad is not ready.");
                onAdWatched?.Invoke(false); // Передаем false в колбэк, если реклама не была показана
                RequestRewardedAd();
            }
        }


        private static void CheckID()
        {
            if (settings.testMode) rewardedVideoID = "ca-app-pub-3940256099942544/5224354917";
            else rewardedVideoID = settings.rewardedVideoID;
        }
    }
}
#endif