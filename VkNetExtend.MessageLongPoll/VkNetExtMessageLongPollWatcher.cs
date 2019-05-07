using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using VkNet;
using VkNet.Model;
using VkNet.Model.RequestParams;

namespace VkNetExtend.MessageLongPoll
{
    using Models;
    using Logger;
    using VkNet.Exception;

    public class VkNetExtMessageLongPollWatcher : IMessageLongPollWatcher
    {
        protected readonly ILogger _logger;
        protected readonly MessageLongPollWatcherOptions _options;
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
        public event LongPollNewEventsDelegate NewEvents;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="options"></param>
        /// <param name="api"></param>
        /// <exception cref="ArgumentNullException">Options or Api must be defined</exception>
        public VkNetExtMessageLongPollWatcher(ILogger logger,
            MessageLongPollWatcherOptions options,
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
        public VkNetExtMessageLongPollWatcher(MessageLongPollWatcherOptions options,
            VkApi api)
        {
            _logger = new ConsoleLogger();
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
            {
                _logger?.LogTrace($"Watcher for {ApiTargetDescriptor()} \"{nameof(Ts)}\" property is not defined.");
                await GetLongPollServerAsync().ConfigureAwait(true);
            }

            var req = new MessagesGetLongPollHistoryParams()
            {                
                Ts = Ts.Value,
                Pts = Pts,

                Fields = _options.HistoryFields,
                MaxMsgId = null,

                PreviewLength = _options.HistoryPreviewLength,
                Onlines = _options.HistoryOnlines,
                LpVersion = _options.LongPollVersion
            };

            _logger?.LogTrace($"Loading long pool history for {ApiTargetDescriptor()}. Ts: {Ts?.ToString() ?? "null"}, Pts: {Pts?.ToString() ?? "null"}.");
            try
            {
                var res = await _api.Messages.GetLongPollHistoryAsync(req).ConfigureAwait(true);
                if (res.NewPts != Pts)
                {
                    Pts = res.NewPts;
                    _logger?.LogTrace($"Watcher {ApiTargetDescriptor()} new \"{nameof(Pts)}\" value is {res.NewPts}.{Environment.NewLine}" +
                        $"Recieve {res.History.Count} new events. {res.UnreadMessages} unread messages.");
                }

                return res;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Exception occured when loading long poll history for {ApiTargetDescriptor()}.{Environment.NewLine}" +
                    $"{(ex.InnerException == null ? ex.Message : ex.InnerException.Message)}");
                return null;
            }
        }

        protected async Task WatchStepAsync(object state)
        {
            if (Active)
            {
                LongPollHistoryResponse history = null;
                try
                {
                    history = await GetLongPollHistoryAsync().ConfigureAwait(true);
                }
                catch (VkApiException apiEx)
                {
                    _logger?.LogError($"Error while loading long poll history.{Environment.NewLine}{apiEx.Message}");
                }

                if (history != null)
                {
                    if (history.History.Count > 0)
                    {
                        CurrentSleepSteps = 1;

                        NewEvents?.Invoke(this, history);
                        if (history.Messages.Count > 0)
                            NewMessages?.Invoke(this, history.Messages);
                    }
                    else
                    {
                        if (CurrentSleepSteps < _options.MaxSleepSteps)
                            CurrentSleepSteps++;
                    }
                    _watchTimer?.Change(CurrentSleepSteps * _options.StepSleepTimeMsec, Timeout.Infinite);
                }
                else
                {
                    // TODO: Define situation when exceptions occured
                    _watchTimer?.Change(CurrentSleepSteps * _options.StepSleepTimeMsec, Timeout.Infinite);
                }
            }
        }
        private void _watchStep(object state) => WatchStepAsync(state).GetAwaiter().GetResult();

        public async Task StartWatchAsync(StartWatchModel model)
        {
            if (!Active)
            {
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger?.Log(LogLevel.Trace, $"Starting messages watcher for {ApiTargetDescriptor()}." +
                        $"{(model.Ts.HasValue ? $" Ts: {model.Ts.Value}." : "")}{(model.Pts.HasValue ? $" Pts: {model.Pts.Value}." : "")}");
                }
                else
                    _logger?.Log(LogLevel.Information, $"Starting messages watcher{(_api.UserId.HasValue ? $" for user {_api.UserId.Value}." : ".")}");

                Ts = model.Ts;
                Pts = model.Pts;

                Active = true;

                if (!Enabled)
                {
                    Enabled = true;
                    try
                    {
                        await GetLongPollServerAsync().ConfigureAwait(true);
                        _logger?.Log(LogLevel.Trace, $"Watcher started for {ApiTargetDescriptor()}.");
                    }
                    catch (Exception)
                    {
                        Enabled = false;
                        _logger?.Log(LogLevel.Error, $"Failed to start watcher for {ApiTargetDescriptor()}.");
                    }
                }
                else
                {
                    _logger?.Log(LogLevel.Trace, $"Watcher resumed for {ApiTargetDescriptor()}.");
                }
                _watchTimer = new Timer(new TimerCallback(_watchStep), null, 0, Timeout.Infinite);
            }
            else
                _logger?.Log(LogLevel.Trace, $"Attemption to start active watcher for {ApiTargetDescriptor()}.");
        }

        public Task StartWatchAsync() => StartWatchAsync(new StartWatchModel());

        public void StopWatch()
        {
            if (Active)
            {
                Active = false;
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger?.Log(LogLevel.Trace, $"Watcher paused for {ApiTargetDescriptor()}." +
                        $"{(Ts.HasValue ? $" Ts: {Ts.Value}." : "")}{(Pts.HasValue ? $" Pts: {Pts.Value}." : "")}");
                }
                else
                    _logger?.Log(LogLevel.Information, $"Watcher paused{(_api.UserId.HasValue ? $" for user {_api.UserId.Value}." : ".")}");
                
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