using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using VkNet;
using VkNet.Model;
using VkNet.Model.RequestParams;

namespace VkNetExtend.LongPollService
{
    using Models;

    public class VkNetExtLongPollWatcher : ILongPollWatcher
    {
        protected readonly ILogger _logger;
        protected readonly LongPollWatcherOptions _options;
        protected readonly VkApi _api;

        #region Properties

        public bool Enabled { get; protected set; } = false;
        public bool Active { get; protected set; } = false;

        /// <summary>
        /// Last value of "ts" property retrieved from Long Poll server
        /// </summary>
        public ulong? Ts { get; set; } = null;
        /// <summary>
        /// Last value of "new_pts" property retrieved from Long Poll server.
        /// Used for retrieving actions, that stores forever
        /// </summary>
        public ulong? Pts { get; set; } = null;

        #endregion

        /// <summary>
        /// Current amount of passed steps where nothing happened
        /// </summary>
        protected int CurrentSleepSteps { get; set; } = 1;
        private Timer _watchTimer;

        public event LongPollNewMessagesDelegate NewMessages;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="options"></param>
        /// <param name="api"></param>
        /// <exception cref="ArgumentNullException">Options or Api must be defined</exception>
        public VkNetExtLongPollWatcher(ILogger logger,
            LongPollWatcherOptions options,
            VkApi api)
        {
            _logger = logger;
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _api = api ?? throw new ArgumentNullException(nameof(api));
            if (!_api.IsAuthorized)
                throw new ArgumentException("Api is not authorized.", nameof(api));
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="options"></param>
        /// <param name="api"></param>
        /// <exception cref="ArgumentNullException">Options or Api must be defined</exception>
        public VkNetExtLongPollWatcher(LongPollWatcherOptions options,
            VkApi api)
        {
            _logger = null;
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _api = api ?? throw new ArgumentNullException(nameof(api));
        }
        
        protected async Task<LongPollServerResponse> GetLongPollServerAsync()
        {
            _logger?.Log(LogLevel.Trace, $"Start retrieving Long Poll Server for {ApiTargetDescriptor()}.");
            try
            {
                var res = await _api.Messages.GetLongPollServerAsync(!Pts.HasValue, _options.LongPollVersion);

                if (!string.IsNullOrEmpty(res.Ts) && ulong.TryParse(res.Ts, out ulong respTs))
                    Ts = respTs;
                else
                {
                    _logger?.LogError($"Unable to parse response TS");
                    throw new InvalidCastException($"Unable to parse response TS. Value - \"{res.Ts}\", expected type - {nameof(UInt64)}.");
                }

                if (res.Pts.HasValue)
                    Pts = res.Pts;

                return res;
            }
            catch (Exception ex)
            {
                _logger?.Log(LogLevel.Error, $"Error retrieving Long Poll Server{(_api.UserId.HasValue ? $" for user {_api.UserId.Value}" : "")}.{Environment.NewLine}" +
                    $"{(ex.InnerException != null ? ex.InnerException.Message : ex.Message)}");
                throw;
            }
        }

        protected async Task<LongPollHistoryResponse> GetLongPollHistoryAsync(long? maxMsgId = null)
        {
            if (!Ts.HasValue)
                await GetLongPollHistoryAsync();

            var req = new MessagesGetLongPollHistoryParams()
            {
                Ts = Ts.Value,
                Pts = Pts,

                EventsLimit = 250,
                Fields = _options.HistoryFields,
                MaxMsgId = null,

                PreviewLength = _options.HistoryPreviewLength,
                Onlines = _options.HistoryOnlines,
                LpVersion = _options.LongPollVersion
            };

            var res = await _api.Messages.GetLongPollHistoryAsync(req).ConfigureAwait(true);
            Pts = res.NewPts;

            return res;
        }

        protected async Task WatchStepAsync(object state)
        {
            var history = await GetLongPollHistoryAsync().ConfigureAwait(true);

            if (history.Messages.Count > 0)
            {
                CurrentSleepSteps = 1;

                NewMessages?.Invoke(this, history.Messages);
            }
            else
            {
                if (CurrentSleepSteps < _options.MaxSleepSteps)
                    CurrentSleepSteps++;
            }

            _watchTimer?.Change(CurrentSleepSteps * _options.StepSleepTimeMsec, Timeout.Infinite);
        }
        private async void _watchStep(object state) => await WatchStepAsync(state);

        public async Task StartWatchAsync(StartWatchModel model)
        {
            if (!Active)
            {
                _logger?.Log(LogLevel.Trace, $"Starting watcher for {ApiTargetDescriptor()}{(model.Pts.HasValue ? $" with Pts: {model.Pts.Value}" : "")}.");

                Active = true;
                Pts = model.Pts;

                if (!Enabled)
                {
                    _logger?.Log(LogLevel.Trace, $"Watcher started for {ApiTargetDescriptor()}.");

                    await GetLongPollServerAsync().ConfigureAwait(true);
                    _watchTimer = new Timer(new TimerCallback(_watchStep), null, 0, Timeout.Infinite);
                }
                else
                {
                    _logger?.Log(LogLevel.Trace, $"Watcher resumed for {ApiTargetDescriptor()}.");
                }
            }
            else
                _logger?.Log(LogLevel.Trace, $"Attemption to start active watcher for {ApiTargetDescriptor()}.");
        }

        public void StopWatch()
        {
            if (Active)
            {
                Active = false;

                _logger?.Log(LogLevel.Trace, $"Watcher paused for {ApiTargetDescriptor()}{(Ts.HasValue ? $" on TS:{Ts.Value}" : "")}.");
            }
        }

        #region Helper

        protected string ApiTargetDescriptor(bool upperCapitals = false)
        {
            if (upperCapitals)
                return _api.UserId.HasValue ? $"User {_api.UserId.Value}" : $"Token {_api.Token}";
            else
                return _api.UserId.HasValue ? $"user {_api.UserId.Value}" : $"token {_api.Token}";
        }
        
        #endregion
    }
}