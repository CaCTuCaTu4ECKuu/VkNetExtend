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
        /// Last retrieved event id to get unmanaged actions
        /// </summary>
        public ulong? Pts { get; set; } = null;

        /// <summary>
        /// If <see cref="Pts"/> less that actual action Id missing actions will be loaded on start
        /// </summary>
        public bool LoadUnretrievedEvents { get; set; }

        /// <summary>
        /// Limit count of event that will be loaded within <see cref="LoadUnretrievedEvents"/>
        /// </summary>
        public int UnretrievedEventsLoadLimit { get; set; } = int.MaxValue;
    }
}
