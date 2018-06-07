using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using VkNet;
using VkNet.Model;

namespace VkNetExtend.LongPollService
{
    using Models;

    public delegate void LongPollNewMessagesDelegate(ILongPollWatcher watcher, IEnumerable<Message> messages);

    public interface ILongPollWatcher
    {
        /// <summary>
        /// Current amount of passed steps where nothing happened
        /// </summary>
        int CurrentSleepSteps { get; }

        /// <summary>
        /// Last value of "ts" property retrieved from Long Poll server
        /// </summary>
        ulong? Ts { get; }

        /// <summary>
        /// Last value of "new_pts" property retrieved from Long Poll server.
        /// Used for retrieving actions, that stores forever
        /// </summary>
        ulong? Pts { get; }

        /// <summary>
        /// Event watching was started successfuly
        /// </summary>
        bool Enabled { get; }
        /// <summary>
        /// Event watching is currently working
        /// </summary>
        bool Active { get; }

        /// <summary>
        /// Messages retrieved by Long Poll watcher
        /// </summary>
        event LongPollNewMessagesDelegate NewMessages;

        /// <summary>
        /// Start watching for new events async
        /// </summary>
        Task StartWatchAsync(StartWatchModel model);

        /// <summary>
        /// Suspend watching
        /// </summary>
        void StopWatch();
    }
}
