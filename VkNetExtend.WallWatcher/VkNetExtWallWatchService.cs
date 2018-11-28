using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

using VkNet;

namespace VkNetExtend.WallWatchService
{
    public class VkNetExtWallWatchService : IWallWatchService
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

        public VkNetExtWallWatchService(ILogger logger,
            WallWatchOptions options,
            VkApi api)
        {
            _logger = logger;
            _options = options;
            _api = api;
        }

        public VkNetExtWallWatchService(WallWatchOptions options,
            VkApi api)
        {
            _logger = null;
            _options = options;
            _api = api;
        }
    }
}
