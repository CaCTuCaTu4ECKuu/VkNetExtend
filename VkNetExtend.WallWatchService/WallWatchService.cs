using System;
using System.Collections.Generic;
using System.Text;
using NLog;

using VkNet;

namespace VkNetExtend.WallWatchService
{
    public class WallWatchService : IWallWatchService
    {
        protected readonly ILogger _logger;
        protected readonly WallWatchOptions _options;
        protected readonly VkApi _api;

        #region Properties

        public bool Enabled { get; protected set; }
        public bool Active { get; protected set; }

        public long? LastPostId { get; protected set; }
        public long? FixedPostId { get; protected set; }

        #endregion

        public WallWatchService(ILogger logger,
            WallWatchOptions options,
            VkApi api)
        {
            _logger = logger;
            _options = options;
            _api = api;
        }

        public WallWatchService(WallWatchOptions options,
            VkApi api)
        {
            _logger = null;
            _options = options;
            _api = api;
        }


    }
}
