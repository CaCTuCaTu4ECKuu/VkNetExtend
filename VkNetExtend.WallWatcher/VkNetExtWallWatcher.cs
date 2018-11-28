using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

using VkNet;

namespace VkNetExtend.WallWatcher
{
    public class VkNetExtWallWatcher: IWallWatcher
    {
        protected readonly ILogger _logger;
        protected readonly WallWatcherOptions _options;
        protected readonly VkApi _api;

        #region Properties

        public bool Enabled { get; protected set; }
        public bool Active { get; protected set; }

        public long? LastPostId { get; protected set; }
        public long? FixedPostId { get; protected set; }

        #endregion

        public VkNetExtWallWatcher(ILogger logger,
            WallWatcherOptions options,
            VkApi api)
        {
            _logger = logger;
            _options = options;
            _api = api;
        }

        public VkNetExtWallWatcher(WallWatcherOptions options,
            VkApi api)
        {
            _logger = null;
            _options = options;
            _api = api;
        }
    }
}
