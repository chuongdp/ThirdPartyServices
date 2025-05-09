#if FIREBASE_REMOTE_CONFIG
namespace ServiceImplementation.FireBaseRemoteConfig
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Firebase;
    using Firebase.Extensions;
    using Firebase.RemoteConfig;
    using GameFoundation.DI;
    using GameFoundation.Scripts.Utilities.LogService;
    using GameFoundation.Signals;
    using UnityEngine.Scripting;

    /// <summary>
    /// We need to use MonoBehaviour to use Firebase Remote Config
    /// </summary>
    public class FirebaseRemoteConfigMobile : IRemoteConfig, IInitializable
    {
        private readonly ILogService logger;
        private readonly SignalBus   signalBus;

        [Preserve]
        public FirebaseRemoteConfigMobile(ILogService logger, SignalBus signalBus)
        {
            this.logger    = logger;
            this.signalBus = signalBus;
        }

        public bool IsConfigFetchedSucceed { get; private set; }

        public void Initialize()
        {
            this.logger.Log($"mirailog: FirebaseRemoteConfig InitFirebase");
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
            {
                var dependencyStatus = task.Result;

                this.logger.Log($"mirailog: FirebaseRemoteConfig CheckAndFixDependenciesAsync {dependencyStatus}");
                if (dependencyStatus == DependencyStatus.Available)
                    this.FetchDataAsync();
                else
                    this.logger.Error($"Could not resolve all Firebase dependencies: {dependencyStatus}");
            });
        }

        private Task FetchDataAsync()
        {
            var fetchTask =
                FirebaseRemoteConfig.DefaultInstance.FetchAsync(
                    TimeSpan.Zero);

            return fetchTask.ContinueWithOnMainThread(this.FetchComplete);
        }

        private void FetchComplete(Task fetchTask)
        {
            if (fetchTask.IsCanceled)
                this.logger.Log($"mirailog: FirebaseRemoteConfig Fetch canceled.");
            else if (fetchTask.IsFaulted)
                this.logger.Log($"mirailog: FirebaseRemoteConfig Fetch encountered an error.");
            else if (fetchTask.IsCompleted) this.logger.Log($"mirailog: FirebaseRemoteConfig Fetch completed successfully!");

            var info = FirebaseRemoteConfig.DefaultInstance.Info;
            this.logger.Log($"mirailog: FirebaseRemoteConfig FetchComplete {info.LastFetchStatus}");

            switch (info.LastFetchStatus)
            {
                case LastFetchStatus.Success:
                    FirebaseRemoteConfig.DefaultInstance.ActivateAsync().ContinueWithOnMainThread(task =>
                    {
                        this.logger.Log($"mirailog: FirebaseRemoteConfig Remote data loaded and ready (last fetch time {info.FetchTime}).");
                        this.IsConfigFetchedSucceed = true;
                        this.signalBus.Fire(new RemoteConfigFetchedSucceededSignal(this));
                    });

                    break;
                case LastFetchStatus.Failure:
                    switch (info.LastFetchFailureReason)
                    {
                        case FetchFailureReason.Error:
                            this.logger.Log($"mirailog: FirebaseRemoteConfig Fetch failed for unknown reason");

                            break;
                        case FetchFailureReason.Throttled:
                            this.logger.Log($"mirailog: FirebaseRemoteConfig Fetch throttled until " + info.ThrottledEndTime);

                            break;
                        case FetchFailureReason.Invalid: break;
                        default:                         throw new ArgumentOutOfRangeException();
                    }

                    break;
                case LastFetchStatus.Pending:
                    this.logger.Log($"mirailog: FirebaseRemoteConfig Latest Fetch call still pending.");

                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        #region Get Data Remote Config

        public string GetRemoteConfigStringValue(string key, string defaultValue)
        {
            return !this.HasKey(key) ? defaultValue : FirebaseRemoteConfig.DefaultInstance.GetValue(key).StringValue;
        }

        public bool GetRemoteConfigBoolValue(string key, bool defaultValue)
        {
            if (!this.HasKey(key) || !this.IsConfigFetchedSucceed) return defaultValue;

            var value = FirebaseRemoteConfig.DefaultInstance.GetValue(key).StringValue;

            return bool.TryParse(value, out var result) && result;
        }

        public long GetRemoteConfigLongValue(string key, long defaultValue)
        {
            if (!this.HasKey(key) || !this.IsConfigFetchedSucceed) return defaultValue;

            var value = FirebaseRemoteConfig.DefaultInstance.GetValue(key).StringValue;

            return long.TryParse(value, out var result) ? result : defaultValue;
        }

        public double GetRemoteConfigDoubleValue(string key, double defaultValue)
        {
            if (!this.HasKey(key) || !this.IsConfigFetchedSucceed) return defaultValue;

            var value = FirebaseRemoteConfig.DefaultInstance.GetValue(key).StringValue;

            return double.TryParse(value, out var result) ? result : defaultValue;
        }

        public int GetRemoteConfigIntValue(string key, int defaultValue)
        {
            if (!this.HasKey(key) || !this.IsConfigFetchedSucceed) return defaultValue;

            var value = FirebaseRemoteConfig.DefaultInstance.GetValue(key).StringValue;

            return int.TryParse(value, out var result) ? result : defaultValue;
        }

        public float GetRemoteConfigFloatValue(string key, float defaultValue)
        {
            if (!this.HasKey(key) || !this.IsConfigFetchedSucceed) return defaultValue;

            var value = FirebaseRemoteConfig.DefaultInstance.GetValue(key).StringValue;

            return float.TryParse(value, out var result) ? result : defaultValue;
        }

        private bool HasKey(string key)
        {
            return FirebaseRemoteConfig.DefaultInstance.Keys != null && FirebaseRemoteConfig.DefaultInstance.Keys.Contains(key);
        }

        #endregion
    }
}
#endif