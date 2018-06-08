using System;
using System.Collections.Generic;
using System.Text;

using VkNet.Enums.Filters;

namespace VkNetExtend.LongPollService
{
    public class LongPollWatcherOptions
    {
        /// <summary>
        /// Version for connection to Long Pool.
        /// <see href="https://vk.com/dev/using_longpoll"/>
        /// </summary>
        public uint LongPollVersion { get; set; } = 2;

        /// <summary>
        /// Waiting time
        /// </summary>
        public int Wait { get; set; } = 25;

        /// <summary>
        /// No activity in a row max sleep steps multiplier
        /// </summary>
        public byte MaxSleepSteps { get; set; } = 3;

        /// <summary>
        /// Watcher thread sleep time after no activity step in miliseconds
        /// </summary>
        public int StepSleepTimeMsec { get; set; } = 333;

        /// <summary>
        /// Count of symbols to trim messages. 0 - dont trim messages
        /// </summary>
        public int HistoryPreviewLength { get; set; } = 0;

        /// <summary>
        /// Return actions indicating that user become online/offline
        /// </summary>
        public bool HistoryOnlines { get; set; } = true;

        /// <summary>
        /// List of additional fields to return.
        /// <see href="https://vk.com/dev/objects/user"/>
        /// </summary>
        public UsersFields HistoryFields { get; set; } = null;
    }
}
