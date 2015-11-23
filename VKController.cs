using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VkNetExtend
{
    using VkNet;
    using VkNet.Model;

    public class VKController
    {
        private VkApi _api;

        private Hashtable WallWatchers;

        public LongPoolWatcher LongPool { get; private set; }

        public event MessagesRecievedDelegate NewMessages;
        public event PostsListDelegate NewPosts;
        public event FixedPostChangedDelegate FixedPostChanged;

        public VKController(VkApi api, int applicationId)
        {
            _api = api;

        }

        public void StartMessagesUpdateWatch(ulong? LastTs = null, ulong? LastPts = null)
        {
            if (LongPool == null)
            {
                LongPool = new LongPoolWatcher(_api);
                LongPool.NewMessages += LongPool_NewMessages;
            }
            LongPool.StartAsync(LastTs, LastPts);
        }
        public void StartWallWatch(long ownerId)
        {
            if (WallWatchers.ContainsKey(ownerId))
                ((WallWatcher)WallWatchers[ownerId]).Start();
            else
            {
                WallWatcher w = new WallWatcher(_api, ownerId);

                w.NewPosts += Wall_NewPosts;
                w.FixedPostChanged += Wall_FixedPostChanged;

                WallWatchers.Add(ownerId, w);
            }
        }
        public void StopWallWath(long ownerId)
        {
            if (WallWatchers.ContainsKey(ownerId))
                ((WallWatcher)WallWatchers[ownerId]).Stop();
        }

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
    }
}
