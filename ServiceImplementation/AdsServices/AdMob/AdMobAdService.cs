#if ADMOB
namespace ServiceImplementation.AdsServices.AdMob
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using Core.AdsServices;
    using Core.AdsServices.CollapsibleBanner;
    using Core.AdsServices.Helpers;
    using Core.AdsServices.Signals;
    using Core.AnalyticServices;
    using Core.AnalyticServices.CommonEvents;
    using Core.AnalyticServices.Signal;
    using GameFoundation.DI;
    using GameFoundation.Signals;
    using GoogleMobileAds.Api;
    using ServiceImplementation.Configs;
    using ServiceImplementation.Configs.Ads;
    using UnityEngine;
    using UnityEngine.Scripting;
    using Debug = UnityEngine.Debug;

    public class AdMobAdService : IAdServices, IAdLoadService, IInitializable, ICollapsibleBannerAd
    {
        #region Constructor

        private readonly AdMobSettings     config;
        private readonly SignalBus         signalBus;
        private readonly IAnalyticServices analyticService;

        [Preserve]
        public AdMobAdService(ThirdPartiesConfig config, SignalBus signalBus, IAnalyticServices analyticService)
        {
            this.config          = config.AdSettings.AdMob;
            this.signalBus       = signalBus;
            this.analyticService = analyticService;
        }

        #endregion

        string IAdServices.AdPlatform => AdRevenueConstants.ARSourceAdMob;

        public string AdPlatform => AdRevenueConstants.ARSourceAdMob;

        public AdNetworkSettings AdNetworkSettings => this.config;

        #region Initialize

        private bool isInitialized = true;

        public void Initialize()
        {
            // MobileAds.Initialize(_ =>
            // {
            //     this.isInitialized = true;
            // });
        }

        public bool IsAdsInitialized() { return this.isInitialized; }

        #endregion

        #region Banner

        private BannerView bannerView;

        public void ShowBannerAd(BannerAdsPosition bannerAdsPosition = BannerAdsPosition.Bottom, int width = 320, int height = 50)
        {
            var adInfo   = new AdInfo(this.AdPlatform, this.config.DefaultBannerAdId.Id, AdFormatConstants.Banner);
            var size     = new AdSize(width, height);
            var position = bannerAdsPosition.ToAdMobAdPosition();

            if (this.bannerView is not null)
            {
                this.bannerView.SetPosition(position);
                this.bannerView.Show();

                return;
            }

            this.bannerView = new BannerView(this.config.DefaultBannerAdId.Id, size, position);

            #region Events

            this.bannerView.OnBannerAdLoaded            += OnBannerAdLoaded;
            this.bannerView.OnBannerAdLoadFailed        += (error) => this.signalBus.Fire(new BannerAdLoadFailedSignal(AdFormatConstants.Banner, error.GetMessage()));
            this.bannerView.OnAdFullScreenContentOpened += OnAdFullScreenContentOpened;
            this.bannerView.OnAdFullScreenContentClosed += OnAddFullScreenContentClosed;
            this.bannerView.OnAdClicked                 += OnAdClicked;
            this.bannerView.OnAdPaid                    += this.TrackAdRevenue(AdFormatConstants.Banner, AdFormatConstants.Banner, this.config.DefaultBannerAdId.Id);

            void OnBannerAdLoaded() { this.signalBus.Fire(new BannerAdLoadedSignal(AdFormatConstants.Banner, adInfo)); }

            void OnAdFullScreenContentOpened() { this.signalBus.Fire(new BannerAdPresentedSignal(AdFormatConstants.Banner)); }

            void OnAddFullScreenContentClosed() { this.signalBus.Fire(new BannerAdDismissedSignal(AdFormatConstants.Banner)); }

            void OnAdClicked() { this.signalBus.Fire(new BannerAdClickedSignal(AdFormatConstants.Banner, adInfo)); }

            #endregion

            this.bannerView.LoadAd(new AdRequest());
        }

        public void HideBannedAd() { this.bannerView?.Hide(); }

        public void DestroyBannerAd()
        {
            this.bannerView?.Destroy();
            this.bannerView = null;
        }

        #endregion

        #region Interstitial

        private InterstitialAd interstitialAd;

        public bool TryGetInterstitialPlacementId(string place, out string id)
        {
            return AdPlacementHelper.TryGetPlacementId(
                place,
                this.config.DefaultInterstitialAdId,
                this.AdNetworkSettings.CustomInterstitialAdIds,
                out id);
        }

        public bool IsInterstitialAdReady(string _) { return this.interstitialAd?.CanShowAd() ?? false; }

        public void LoadInterstitialAd(string place)
        {
            if (this.IsInterstitialAdReady(place)) return;

            var stopwatch = Stopwatch.StartNew();
            InterstitialAd.Load(this.config.DefaultInterstitialAdId.Id, new AdRequest(), (ad, error) =>
            {
                stopwatch.Stop();
                if (error is not null)
                {
                    this.signalBus.Fire(new InterstitialAdLoadFailedSignal(place, error.GetMessage(), stopwatch.ElapsedMilliseconds));

                    return;
                }

                var adInfo = new AdInfo(this.AdPlatform, this.config.DefaultInterstitialAdId.Id, AdFormatConstants.Interstitial);
                this.signalBus.Fire(new InterstitialAdLoadedSignal(place, stopwatch.ElapsedMilliseconds, adInfo));
                this.interstitialAd?.Destroy();
                this.interstitialAd = ad;
            });
        }

        public void ShowInterstitialAd(string place)
        {
            if (!this.IsInterstitialAdReady(place)) return;
            var adInfo = new AdInfo(this.AdPlatform, this.config.DefaultInterstitialAdId.Id, AdFormatConstants.Interstitial);

            #region Events

            this.interstitialAd.OnAdFullScreenContentOpened += OnAdFullScreenContentOpened;
            this.interstitialAd.OnAdFullScreenContentClosed += OnAddFullScreenContentClosed;
            this.interstitialAd.OnAdFullScreenContentFailed += _ => this.signalBus.Fire(new InterstitialAdDisplayedFailedSignal(place));
            this.interstitialAd.OnAdClicked                 += OnAdClicked;
            this.interstitialAd.OnAdPaid                    += this.TrackAdRevenue(AdFormatConstants.Interstitial, place, this.config.DefaultInterstitialAdId.Id);

            void OnAdFullScreenContentOpened() { this.signalBus.Fire(new InterstitialAdDisplayedSignal(place, adInfo)); }

            void OnAddFullScreenContentClosed() { this.signalBus.Fire(new InterstitialAdClosedSignal(place, adInfo)); }

            void OnAdClicked() { this.signalBus.Fire(new InterstitialAdClickedSignal(place, adInfo)); }

            #endregion

            this.interstitialAd.Show();
        }

        #endregion

        #region Rewarded

        private RewardedAd rewardedAd;

        public bool IsRewardedAdReady(string place) { return this.rewardedAd?.CanShowAd() ?? false; }

        public bool TryGetRewardPlacementId(string placement, out string id)
        {
            return AdPlacementHelper.TryGetPlacementId(
                placement,
                this.config.DefaultRewardedAdId,
                this.AdNetworkSettings.CustomRewardedAdIds,
                out id);
        }

        public void LoadRewardAds(string place)
        {
            if (this.IsRewardedAdReady(place)) return;

            var stopwatch = Stopwatch.StartNew();
            RewardedAd.Load(this.config.DefaultRewardedAdId.Id, new AdRequest(), (ad, error) =>
            {
                stopwatch.Stop();
                if (error is not null)
                {
                    this.signalBus.Fire(new RewardedAdLoadFailedSignal(place, error.GetMessage(), stopwatch.ElapsedMilliseconds));

                    return;
                }

                var adInfo = new AdInfo(this.AdPlatform, this.config.DefaultRewardedAdId.Id, AdFormatConstants.Rewarded);
                this.signalBus.Fire(new RewardedAdLoadedSignal(place, stopwatch.ElapsedMilliseconds, adInfo));
                this.rewardedAd?.Destroy();
                this.rewardedAd = ad;
            });
        }

        public void ShowRewardedAd(string place, Action onCompleted, Action onFailed)
        {
            if (!this.IsRewardedAdReady(place)) return;
            var adInfo = new AdInfo(this.AdPlatform, this.config.DefaultRewardedAdId.Id, AdFormatConstants.Rewarded);

            #region Events

            this.rewardedAd.OnAdFullScreenContentOpened += OnAdFullScreenContentOpened;
            this.rewardedAd.OnAdFullScreenContentFailed += _ => OnAdFullScreenContentFailed();
            this.rewardedAd.OnAdClicked                 += OnAdClicked;
            this.rewardedAd.OnAdPaid                    += (_) => this.signalBus.Fire(new RewardedAdEligibleSignal(place));
            this.rewardedAd.OnAdPaid                    += this.TrackAdRevenue(AdFormatConstants.Rewarded, place, this.config.DefaultRewardedAdId.Id);

            #endregion

            this.rewardedAd.Show(_ =>
            {
                this.signalBus.Fire(new RewardedAdCompletedSignal(place, adInfo));
                this.signalBus.Fire(new RewardedAdClosedSignal(place, adInfo));
                onCompleted?.Invoke();
            });
            this.signalBus.Fire(new RewardedAdCalledSignal(place, adInfo));

            return;

            void OnAdClicked() { this.signalBus.Fire(new RewardedAdClickedSignal(place, adInfo)); }

            void OnAdFullScreenContentOpened() { this.signalBus.Fire(new RewardedAdDisplayedSignal(place, adInfo)); }

            void OnAdFullScreenContentFailed()
            {
                this.signalBus.Fire(new RewardedSkippedSignal(place, adInfo));
                onFailed?.Invoke();
                onFailed = null;
            }
        }

        #endregion

        #region RemoveAds

        public void RemoveAds() { PlayerPrefs.SetInt("ADMOB_REMOVE_ADS", 1); }

        public bool IsRemoveAds() { return PlayerPrefs.HasKey("ADMOB_REMOVE_ADS"); }

        #endregion

        private Action<AdValue> TrackAdRevenue(string format, string placement, string adUnit)
        {
            return adValue =>
            {
                var adsRevenueEvent = new AdsRevenueEvent
                {
                    AdsRevenueSourceId = this.AdPlatform,
                    AdFormat           = format,
                    AdNetwork          = "AdMob",
                    AdUnit             = adUnit,
                    NetworkPlacement   = placement,
                    Revenue            = adValue.Value / 1e6,
                    Currency           = adValue.CurrencyCode,
                    CountryCode        = this.TryGetCountryCode(),
                };

                this.signalBus.Fire(new AdRevenueSignal(adsRevenueEvent));

                this.analyticService.Track(adsRevenueEvent);
            };
        }
        
        private string TryGetCountryCode()
        {
            try
            {
                return RegionInfo.CurrentRegion.TwoLetterISORegionName;
            }
#pragma warning disable 0168
            catch (Exception ignored)
#pragma warning restore 0168
            {
                // Ignored
            }

            return "US";
        }

        #region Collapsible Banner

        private bool       isAvailableShowCollapsibleBanner;
        private BannerView collapsibleBannerView;
        private string     collapsibleBannerGuid = GetNewGuid();

        public void ShowCollapsibleBannerAd(bool useNewGuid, BannerAdsPosition bannerAdsPosition = BannerAdsPosition.Bottom)
        {
            if (string.IsNullOrEmpty(this.config.CollapsibleBannerAdId.Id))
            {
                Debug.Log("mirailog: ShowCollapsibleBannerAd - CollapsibleBannerAdId is null or empty. Please check the AdMob settings.");

                return;
            }

            this.collapsibleBannerGuid = useNewGuid ? GetNewGuid() : this.collapsibleBannerGuid;

            if (this.collapsibleBannerView == null)
            {
                var adSize = this.config.IsAdaptiveBannerEnabled ? AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth) : AdSize.Banner;
                this.collapsibleBannerView = new BannerView(this.config.CollapsibleBannerAdId.Id, adSize, bannerAdsPosition.ToAdMobAdPosition());

                #region Events

                this.collapsibleBannerView.OnBannerAdLoaded += () => this.OnCollapsibleBannerLoaded(AdFormatConstants.CollapsibleBanner);
                this.collapsibleBannerView.OnBannerAdLoadFailed += error => this.OnCollapsibleBannerLoadFailed(AdFormatConstants.CollapsibleBanner, error);
                this.collapsibleBannerView.OnAdFullScreenContentOpened += () => this.OnCollapsibleBannerPresented(AdFormatConstants.CollapsibleBanner);
                this.collapsibleBannerView.OnAdFullScreenContentClosed += () => this.OnCollapsibleBannerDismissed(AdFormatConstants.CollapsibleBanner);
                this.collapsibleBannerView.OnAdClicked += () => this.OnCollapsibleBannerClicked(AdFormatConstants.CollapsibleBanner);
                this.collapsibleBannerView.OnAdPaid += this.TrackAdRevenue(AdFormatConstants.CollapsibleBanner, AdFormatConstants.CollapsibleBanner, this.config.CollapsibleBannerAdId.Id);

                #endregion
            }

            this.isAvailableShowCollapsibleBanner = true;
            var request = new AdRequest();
#if UNITY_IOS
            if(useNewGuid) AddPramsCollapsible();
#else
            AddPramsCollapsible();
#endif
            Debug.Log("mirailog: ShowCollapsibleBannerAd - Load CollapsibleBanner.");
            this.collapsibleBannerView.LoadAd(request);

            return;

            void AddPramsCollapsible()
            {
                request.Extras.Add("collapsible_request_id", this.collapsibleBannerGuid);
                request.Extras.Add("collapsible", bannerAdsPosition == BannerAdsPosition.Bottom ? "bottom" : "top");
            }
        }

        private static string GetNewGuid() => Guid.NewGuid().ToString();

        public void HideCollapsibleBannerAd()
        {
            this.isAvailableShowCollapsibleBanner = false;
            this.collapsibleBannerView?.Hide();
            Debug.Log("mirailog: HideCollapsibleBannerAd");
        }

        public void DestroyCollapsibleBannerAd()
        {
            if (this.collapsibleBannerView != null)
            {
                this.collapsibleBannerView.Destroy();
                this.collapsibleBannerView = null;
            }

            Debug.Log("mirailog: DestroyCollapsibleBannerAd");
        }

        private void OnCollapsibleBannerLoaded(string placement)
        {
            var adInfo = new AdInfo(this.AdPlatform, this.config.CollapsibleBannerAdId.Id, AdFormatConstants.CollapsibleBanner);
            this.signalBus.Fire(new CollapsibleBannerAdLoadedSignal(placement, adInfo));
            if (this.isAvailableShowCollapsibleBanner)
            {
                this.collapsibleBannerView?.Show();
            }

            Debug.Log("mirailog: OnCollapsibleBannerLoaded");
        }

        private void OnCollapsibleBannerLoadFailed(string placement, AdError adError)
        {
            Debug.Log($"mirailog: OnCollapsibleBannerLoadFailed {placement} - {adError.GetMessage()}");
            this.signalBus.Fire(new CollapsibleBannerAdLoadFailedSignal(placement, adError.GetMessage()));
        }

        private void OnCollapsibleBannerPresented(string placement)
        {
            Debug.Log("mirailog: OnCollapsibleBannerPresented");
            this.signalBus.Fire(new CollapsibleBannerAdPresentedSignal(placement));
        }

        private void OnCollapsibleBannerDismissed(string placement) { this.signalBus.Fire(new CollapsibleBannerAdDismissedSignal(placement)); }

        private void OnCollapsibleBannerClicked(string placement)
        {
            var adInfo = new AdInfo(this.AdPlatform, this.config.CollapsibleBannerAdId.Id, AdFormatConstants.CollapsibleBanner);
            this.signalBus.Fire(new CollapsibleBannerAdClickedSignal(placement, adInfo));
        }

        #endregion
    }
}
#endif