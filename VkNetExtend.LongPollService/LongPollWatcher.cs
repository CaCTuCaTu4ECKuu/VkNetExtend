using NLog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using VkNet;
using VkNet.Model;
using VkNet.Model.RequestParams;

namespace VkNetExtend.LongPollService
{
    using Models;

    public class LongPollWatcher : ILongPollWatcher
    {
        protected readonly ILogger _logger;
        protected readonly LongPollWatcherOptions _options;
        protected readonly VkApi _api;

        #region Properties

        public bool Enabled { get; protected set; } = false;
        public bool Active { get; protected set; } = false;

        public ulong? Ts { get; protected set; } = null;
        public ulong? Pts { get; protected set; } = null;

        public int CurrentSleepSteps { get; protected set; } = 1;

        #endregion

        private Timer _watchTimer;

        public event LongPollNewMessagesDelegate NewMessages;

        public LongPollWatcher(ILogger logger,
            LongPollWatcherOptions options,
            VkApi api)
        {
            _logger = logger;
            _options = options;
            _api = api;
        }
        public LongPollWatcher(LongPollWatcherOptions options,
            VkApi api)
        {
            _logger = null;
            _options = options;
            _api = api;
        }
        
        protected Task<LongPollServerResponse> GetLongPollServerAsync()
        {
            _logger?.Log(LogLevel.Trace, $"Start retrieving Long Poll Server for {ApiTargetDescriptor()}.");
            try
            {

                return _api.Messages.GetLongPollServerAsync(!Pts.HasValue, _options.LongPollVersion)
                    .ContinueWith(r =>
                    {
                        Ts = r.Result.Ts;
                        if (r.Result.Pts.HasValue)
                            Pts = r.Result.Pts;

                        return r.Result;
                    });
            }
            catch (Exception ex)
            {
                _logger?.Log(LogLevel.Error, $"Error retrieving Long Poll Server{(_api.UserId.HasValue ? $" for user {_api.UserId.Value}" : "")}.{Environment.NewLine}" +
                    $"{(ex.InnerException != null ? ex.InnerException.Message : ex.Message)}");
                throw;
            }
        }

        protected Task<LongPollHistoryResponse> GetLongPollHistoryAsync()
        {
            if (!Ts.HasValue)
                GetLongPollHistoryAsync();

            var req = new MessagesGetLongPollHistoryParams()
            {
                Ts = Ts.Value,
                Pts = Pts
            };

            return _api.Messages.GetLongPollHistoryAsync(req)
                .ContinueWith(r =>
                {
                    Pts = r.Result.NewPts;

                    return r.Result;
                });
        }


        protected Task WatchStepAsync(object state)
        {
            return GetLongPollHistoryAsync()
                .ContinueWith(r =>
                {
                    if (r.Result.Messages.Count > 0)
                    {
                        CurrentSleepSteps = 1;

                        NewMessages?.Invoke(this, r.Result.Messages);
                    }
                    else
                    {
                        if (CurrentSleepSteps < _options.MaxSleepSteps)
                            CurrentSleepSteps++;
                    }

                    _watchTimer?.Change(CurrentSleepSteps * _options.StepSleepTimeMsec, Timeout.Infinite);
                });
        }
        private async void _watchStep(object state) => await WatchStepAsync(state);

        public Task StartWatchAsync(StartWatchModel model)
        {
            if (!Active)
            {
                _logger?.Log(LogLevel.Trace, $"Starting watcher for {ApiTargetDescriptor()}{(model.Pts.HasValue ? $" with Pts: {model.Pts.Value}" : "")}.");

                Active = true;
                Pts = model.Pts;

                if (!Enabled)
                {
                    _logger?.Log(LogLevel.Trace, $"Watcher started for {ApiTargetDescriptor()}.");

                    return GetLongPollServerAsync()
                        .ContinueWith(r =>
                        {
                            _watchTimer = new Timer(new TimerCallback(_watchStep), null, 0, Timeout.Infinite);
                        });
                }
                else
                {
                    _logger?.Log(LogLevel.Trace, $"Watcher resumed for {ApiTargetDescriptor()}.");
                }
            }
            else
                _logger?.Log(LogLevel.Trace, $"Attemption to start active watcher for {ApiTargetDescriptor()}.");

            return Task.FromResult(true);
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