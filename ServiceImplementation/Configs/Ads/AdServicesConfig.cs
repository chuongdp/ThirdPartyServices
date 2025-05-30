namespace ServiceImplementation.Configs.Ads
{
    using System;
    using GameFoundation.DI;
    using GameFoundation.Signals;
    using ServiceImplementation.FireBaseRemoteConfig;
    using UnityEngine.Scripting;

    public class AdServicesConfig : IInitializable, IDisposable
    {
        #region Constructor

        private readonly SignalBus           signalBus;
        private readonly IRemoteConfig       remoteConfig;
        private readonly RemoteConfigSetting remoteConfigSetting;

        [Preserve]
        public AdServicesConfig(SignalBus signalBus, IRemoteConfig remoteConfig, RemoteConfigSetting remoteConfigSetting)
        {
            this.signalBus           = signalBus;
            this.remoteConfig        = remoteConfig;
            this.remoteConfigSetting = remoteConfigSetting;
        }

        #endregion

        public void Initialize()
        {
            this.signalBus.Subscribe<RemoteConfigFetchedSucceededSignal>(this.FetchRemoteConfig);

            this.FetchRemoteConfig(); // Init default value
        }

        public void Dispose() { this.signalBus.Unsubscribe<RemoteConfigFetchedSucceededSignal>(this.FetchRemoteConfig); }

        #region General

        public bool EnableBannerAd                      { get; private set; }
        public bool EnableInterstitialAd                { get; private set; }
        public bool EnableMRECAd                        { get; private set; }
        public bool EnableAOAAd                         { get; private set; }
        public bool EnableRewardedAd                    { get; private set; }
        public bool EnableRewardedInterstitialAd        { get; private set; }
        public bool EnableNativeAd                      { get; private set; }
        public bool EnableCollapsibleBanner             { get; private set; }
        public int  IntervalLoadAds                     { get; private set; }
        public bool EnableAds                           { get; private set; } = false;

        #endregion

        #region AOA

        public float AOALoadingThreshold       { get; private set; }
        public int   MinPauseSecondToShowAoaAd { get; private set; }
        public int   AOAStartSession           { get; private set; }
        public bool  UseAoaAdmob               { get; private set; }

        #endregion

        #region Interstitial

        /// <summary>
        ///     The interval between two interstitial ads, we also count the rewarded interstitial ads
        /// </summary>
        public int InterstitialAdInterval { get; private set; }

        /// <summary>
        ///     The level to start showing interstitial ads
        /// </summary>
        public int InterstitialAdStartLevel { get; private set; }

        /// <summary>
        ///     Places to show interstitial ads, "" means all places
        /// </summary>
        public string[] InterstitialAdActivePlacements { get; private set; }

        /// <summary>
        ///     This delay will be applied for the first session
        /// </summary>
        public int DelayFirstInterstitialAdInterval { get; private set; }

        /// <summary>
        ///     From the second session, this delay will be applied
        /// </summary>
        public int DelayFirstInterNewSession { get; private set; }

        /// <summary>
        ///     Reset the interstitial ad interval after showing a rewarded ad
        /// </summary>
        public bool ResetInterAdIntervalAfterRewardAd { get; private set; }

        #endregion

        #region Rewarded

        /// <summary>
        ///     Places to free reward ads, "" means no places
        /// </summary>
        public string[] RewardedAdFreePlacements { get; private set; }

        #endregion

        #region Collapsible

        /// <summary>
        ///     The interval between two banner ads
        /// </summary>
        public int CollapsibleBannerADInterval { get; private set; }

        /// <summary>
        ///     The interval refresh collapsible banner expand
        /// </summary>
        public int CollapsibleBannerExpandOnRefreshInterval { get; private set; }

        /// <summary>
        ///     Enable fallback to banner ad when collapsible banner ad is not available
        /// </summary>
        public bool EnableCollapsibleBannerFallback { get; private set; }

        /// <summary>
        ///     Auto refresh collapsible banner ad each <see cref="CollapsibleBannerExpandOnRefreshInterval"/>
        /// </summary>
        public bool CollapsibleBannerAutoRefreshEnabled { get; private set; }

        /// <summary>
        ///     Auto expand collapsible banner ad when refresh
        /// </summary>
        public bool CollapsibleBannerExpandOnRefreshEnabled { get; private set; }

        #endregion

        private void FetchRemoteConfig()
        {
            #region General

            this.EnableAds                    = RemoteConfigHelpers.GetBoolRemoteValue(this.remoteConfig, this.remoteConfigSetting, RemoteConfigKey.EnableAds);
            this.EnableBannerAd               = RemoteConfigHelpers.GetBoolRemoteValue(this.remoteConfig, this.remoteConfigSetting, RemoteConfigKey.EnableBannerAD) && this.EnableAds;
            this.EnableInterstitialAd         = RemoteConfigHelpers.GetBoolRemoteValue(this.remoteConfig, this.remoteConfigSetting, RemoteConfigKey.EnableInterstitialAD) && this.EnableAds;
            this.EnableMRECAd                 = RemoteConfigHelpers.GetBoolRemoteValue(this.remoteConfig, this.remoteConfigSetting, RemoteConfigKey.EnableMrecAD) && this.EnableAds;
            this.EnableAOAAd                  = RemoteConfigHelpers.GetBoolRemoteValue(this.remoteConfig, this.remoteConfigSetting, RemoteConfigKey.EnableAoaAD) && this.EnableAds;
            this.EnableRewardedAd             = RemoteConfigHelpers.GetBoolRemoteValue(this.remoteConfig, this.remoteConfigSetting, RemoteConfigKey.EnableRewardedAD) && this.EnableAds;
            this.EnableRewardedInterstitialAd = RemoteConfigHelpers.GetBoolRemoteValue(this.remoteConfig, this.remoteConfigSetting, RemoteConfigKey.EnableRewardedInterstitialAD) && this.EnableAds;
            this.EnableNativeAd               = RemoteConfigHelpers.GetBoolRemoteValue(this.remoteConfig, this.remoteConfigSetting, RemoteConfigKey.EnableNativeAD) && this.EnableAds;
#if MIRAI_COLLAPSIBLE_BANNER
            this.EnableCollapsibleBanner = RemoteConfigHelpers.GetBoolRemoteValue(this.remoteConfig, this.remoteConfigSetting, RemoteConfigKey.EnableCollapsibleBanner) && this.EnableAds;
#endif
            this.IntervalLoadAds = RemoteConfigHelpers.GetIntRemoteValue(this.remoteConfig, this.remoteConfigSetting, RemoteConfigKey.IntervalLoadAds);

            #endregion

            #region AOA

            this.AOALoadingThreshold       = RemoteConfigHelpers.GetFloatRemoteValue(this.remoteConfig, this.remoteConfigSetting, RemoteConfigKey.AOALoadingThreshold);
            this.MinPauseSecondToShowAoaAd = RemoteConfigHelpers.GetIntRemoteValue(this.remoteConfig, this.remoteConfigSetting, RemoteConfigKey.MinPauseSecondToShowAoaAD);
            this.AOAStartSession           = RemoteConfigHelpers.GetIntRemoteValue(this.remoteConfig, this.remoteConfigSetting, RemoteConfigKey.AoaStartSession);
            this.UseAoaAdmob               = RemoteConfigHelpers.GetBoolRemoteValue(this.remoteConfig, this.remoteConfigSetting, RemoteConfigKey.UseAoaAdmob);

            #endregion

            #region Interstitial

            this.InterstitialAdInterval            = RemoteConfigHelpers.GetIntRemoteValue(this.remoteConfig, this.remoteConfigSetting, RemoteConfigKey.InterstitialADInterval);
            this.InterstitialAdStartLevel          = RemoteConfigHelpers.GetIntRemoteValue(this.remoteConfig, this.remoteConfigSetting, RemoteConfigKey.InterstitialADStartLevel);
            this.InterstitialAdActivePlacements    = RemoteConfigHelpers.GetStringRemoteValue(this.remoteConfig, this.remoteConfigSetting, RemoteConfigKey.InterstitialAdActivePlacements).Split(',');
            this.DelayFirstInterstitialAdInterval  = RemoteConfigHelpers.GetIntRemoteValue(this.remoteConfig, this.remoteConfigSetting, RemoteConfigKey.DelayFirstIntersADInterval);
            this.DelayFirstInterNewSession         = RemoteConfigHelpers.GetIntRemoteValue(this.remoteConfig, this.remoteConfigSetting, RemoteConfigKey.DelayFirstIntersNewSession);
            this.ResetInterAdIntervalAfterRewardAd = RemoteConfigHelpers.GetBoolRemoteValue(this.remoteConfig, this.remoteConfigSetting, RemoteConfigKey.ResetInterAdIntervalAfterRewardAd);

            #endregion

            #region Rewarded

            this.RewardedAdFreePlacements = RemoteConfigHelpers.GetStringRemoteValue(this.remoteConfig, this.remoteConfigSetting, RemoteConfigKey.RewardedAdFreePlacements).Split(',');

            #endregion

            #region Collapsible

            this.CollapsibleBannerADInterval = RemoteConfigHelpers.GetIntRemoteValue(this.remoteConfig, this.remoteConfigSetting, RemoteConfigKey.CollapsibleBannerADInterval);
            this.CollapsibleBannerExpandOnRefreshInterval =
                RemoteConfigHelpers.GetIntRemoteValue(this.remoteConfig, this.remoteConfigSetting, RemoteConfigKey.CollapsibleBannerExpandOnRefreshInterval);
            this.EnableCollapsibleBannerFallback         = RemoteConfigHelpers.GetBoolRemoteValue(this.remoteConfig, this.remoteConfigSetting, RemoteConfigKey.EnableCollapsibleBannerFallback);
            this.CollapsibleBannerAutoRefreshEnabled     = RemoteConfigHelpers.GetBoolRemoteValue(this.remoteConfig, this.remoteConfigSetting, RemoteConfigKey.CollapsibleBannerAutoRefreshEnabled);
            this.CollapsibleBannerExpandOnRefreshEnabled = RemoteConfigHelpers.GetBoolRemoteValue(this.remoteConfig, this.remoteConfigSetting, RemoteConfigKey.CollapsibleBannerExpandOnRefreshEnabled);

            #endregion
        }
    }
}