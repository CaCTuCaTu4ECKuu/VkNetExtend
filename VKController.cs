using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VkNetExtend
{
    using VkNet;
    using VkNet.Enums.Filters;
    using VkNet.Model;

    public class VKController
    {
        public VkApi API { get; private set; }

        private Hashtable WallWatchers = new Hashtable();

        public LongPoolWatcher LongPool { get; private set; }

        public event MessagesRecievedDelegate NewMessages;
        public event PostsListDelegate NewPosts;
        public event FixedPostChangedDelegate FixedPostChanged;

        public VKController(VkApi api = null)
        {
            if (api == null)
                API = new VkApi();
            else
                API = api;
        }
        public bool Authorize(int applicationId, string login, string password, Settings settings = null)
        {
            API.Authorize(applicationId, login, password, settings == null ? Settings.All : settings);
            return !string.IsNullOrEmpty(API.AccessToken);
        }
        public Task<bool> AuthorizeAsync(int applicationId, string login, string password, Settings settings = null)
        {
            return Task.Run(() => { return Authorize(applicationId, login, password, settings); });
        }

        public void StartLongPoolWatch(ulong? LastTs = null, ulong? LastPts = null)
        {
            if (LongPool == null)
            {
                LongPool = new LongPoolWatcher(API);
                LongPool.NewMessages += LongPool_NewMessages;
            }
            LongPool.StartAsync(LastTs, LastPts);
        }
        public void StartWallWatch(long ownerId, DateTime? lastDateToLoad = null, long lastPostToLoad = -1)
        {
            WallWatcher w = null;
            if (!WallWatchers.ContainsKey(ownerId))
            {
                w = new WallWatcher(API, ownerId);

                w.NewPosts += Wall_NewPosts;
                w.FixedPostChanged += Wall_FixedPostChanged;

                WallWatchers.Add(ownerId, w);
            }
            else
                w = (WallWatcher)WallWatchers[ownerId];

            // TODO: Внести эти махинации внутрь WallWatcher чтобы их можно было приостановить в случае Stop и продолжить при Start
            if (lastDateToLoad != null || lastPostToLoad >= 0)
                w.LoadWallPosts(lastDateToLoad, lastPostToLoad);
            w.Start(true);
        }
        public Task StartWallWatchAsync(long ownerId, DateTime? lastDateToLoad = null, long lastPostToLoad = -1)
        {
            return Task.Run(() => { StartWallWatch(ownerId, lastDateToLoad, lastPostToLoad); });
        }
        public void StopWallWath(long ownerId)
        {
            if (WallWatchers.ContainsKey(ownerId))
                ((WallWatcher)WallWatchers[ownerId]).Stop();
        }

        #region Events
        private void LongPool_NewMessages(IEnumerable<Message> messages, long accountID)
        {
            if (NewMessages != null)
                NewMessages(messages, accountID);
        }
        private void Wall_FixedPostChanged(long ownerId, long pid)
        {
            if (FixedPostChanged != null)
                FixedPostChanged(ownerId, pid);
        }
        private void Wall_NewPosts(long ownerId, IEnumerable<Post> posts)
        {
            if (NewPosts != null)
                NewPosts(ownerId, posts);
        }
        #endregion
    }
}
