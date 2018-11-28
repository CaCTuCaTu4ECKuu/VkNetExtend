using System;
using System.Collections.Generic;
using System.Text;

namespace VkNetExtend.MessageLongPoll.Models
{
    /// <summary>
    /// Options for starting LongPool watcher
    /// </summary>
    public class StartWatchModel
    {
        /// <summary>
        /// Last retrieved Ts
        /// </summary>
        public ulong? Ts { get; set; } = null;
        /// <summary>
        /// Last retrieved Pts
        /// </summary>
        public ulong? Pts { get; set; } = null;
    }
}
