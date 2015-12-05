namespace VkNetExtend
{
    using System;
    using System.Collections.ObjectModel;
    using System.Threading;
    using System.Threading.Tasks;

    using VkNet;
    using VkNet.Model;
    using VkNet.Exception;
    using VkNet.Model.RequestParams.Messages;

    public delegate void MessagesRecievedDelegate(VkApi owner, ReadOnlyCollection<Message> messages);

    public class LongPoolWatcher
    {
        private VkApi _account;

        private ulong? Ts { get; set; }
        private ulong? Pts { get; set; }

        public bool Active { get; private set; }

        #region Управление слежением
        private Timer _watchTimer;

        public byte MaxSleepSteps = 3;
        public int SteepSleepTime = 333;
        private byte _currentSleepSteps = 1;
        #endregion

        public event MessagesRecievedDelegate NewMessages;

        public LongPoolWatcher(VkApi api)
        {
            _account = api;
        }

        private LongPollServerResponse GetLongPoolServer(ulong? lastPts = null)
        {
            var response = _account.Messages.GetLongPollServer(false, lastPts == null);

            Ts = response.Ts;
            Pts = Pts == null ? response.Pts : lastPts;

            return response;
        }
        private Task<LongPollServerResponse> GetLongPoolServerAsync(ulong? lastPts = null)
        {
            return Task.Run(() => { return GetLongPoolServer(lastPts); });
        }

        private LongPollHistoryResponse GetLongPoolHistory()
        {
            if (!Ts.HasValue)
                GetLongPoolServer(null);
            GetLongPollHistoryParams rp = new GetLongPollHistoryParams();
            rp.Ts = Ts.Value;
            rp.Pts = Pts;

            int c = 0;
            LongPollHistoryResponse history = null;
            string errorLog = "";

            while (c < 5 && history == null)
            {
                c++;
                try
                {
                    history = _account.Messages.GetLongPollHistory(rp);
                }
                catch (TooManyRequestsException)
                {
                    Thread.Sleep(150);
                    c--;
                }
                catch (Exception ex)
                {
                    errorLog += string.Format("{0} - {1}{2}", c, ex.Message, Environment.NewLine);
                }
            }

            if (history != null)
            {
                Pts = history.NewPts;
                foreach (var m in history.Messages)
                    m.FromId = m.Type == VkNet.Enums.MessageType.Sended ? _account.UserId : m.UserId;
            }
            else
                throw new NotImplementedException(errorLog);

            return history;
        }
        private Task<LongPollHistoryResponse> GetLongPoolHistoryAsync()
        {
            return Task.Run(() => { return GetLongPoolHistory(); });
        }

        private async void _watchAsync(object state)
        {
            var history = await GetLongPoolHistoryAsync();
            if (history.Messages.Count > 0)
            {
                _currentSleepSteps = 1;
                if (NewMessages != null)
                    NewMessages(_account, history.Messages);
            }
            else if (_currentSleepSteps < MaxSleepSteps)
                _currentSleepSteps++;

            _watchTimer.Change(_currentSleepSteps * SteepSleepTime, Timeout.Infinite);
        }

        public async void StartAsync(ulong? lastTs = null, ulong? lastPts = null)
        {
            if (Active)
                throw new NotImplementedException("Messages for {0} already watching");

            Active = true;
            await GetLongPoolServerAsync(lastPts);

            _watchTimer = new Timer(new TimerCallback(_watchAsync), null, 0, Timeout.Infinite);
        }
        public void Stop()
        {
            if (_watchTimer != null)
                _watchTimer.Dispose();
            Active = false;
            _watchTimer = null;
        }
    }
}
