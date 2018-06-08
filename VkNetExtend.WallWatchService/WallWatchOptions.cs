using System;
using System.Collections.Generic;
using System.Text;

namespace VkNetExtend.WallWatchService
{
    public class WallWatchOptions
    {
        /// <summary>
        /// Initial posts count to load per request (amount of last posts to load on probe request)
        /// </summary>
        public int ProbeLoadPostsCount { get; set; } = 10;

        /// <summary>
        /// Maximum posts count to load per request
        /// </summary>
        public int MaxLoadPostsCount { get; set; } = 100;

        /// <summary>
        /// Indicates whether to inform about new posts on loading after retrieve or after gathering all new posts
        /// </summary>
        public bool InformImmediately { get; set; } = true;

        /// <summary>
        /// No nw posts in a row max sleep steps multiplier
        /// </summary>
        public byte MaxSleepSteps { get; set; } = 3;

        /// <summary>
        /// Watcher wait time after step in miliseconds if there was no new posts
        /// </summary>
        public int StepSleepTimeMsec { get; set; } = 5000;
    }
}
