using System;
using System.Collections.Generic;
using System.Text;

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
    }
}
