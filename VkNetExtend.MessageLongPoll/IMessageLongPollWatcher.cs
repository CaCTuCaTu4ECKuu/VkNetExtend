using System.Collections.Generic;
using System.Threading.Tasks;

using VkNet.Model;

namespace VkNetExtend.MessageLongPoll
{
    using Models;

    public delegate void LongPollNewMessagesDelegate(IMessageLongPollWatcher watcher, IEnumerable<Message> messages);
    public delegate void LongPollNewEventsDelegate(IMessageLongPollWatcher watcher, LongPollHistoryResponse history);

    public interface IMessageLongPollWatcher
    {
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
        /// Occures when Long Poll watcher retrieve new messages
        /// </summary>
        event LongPollNewMessagesDelegate NewMessages;

        /// <summary>
        /// Occures when Long Poll watcher retrieve new events
        /// </summary>
        event LongPollNewEventsDelegate NewEvents;

        /// <summary>
        /// Start watching for new events
        /// </summary>
        Task StartWatchAsync(StartWatchModel model);
        /// <summary>
        /// Start watching for new events from this moment
        /// </summary>
        /// <returns></returns>
        Task StartWatchAsync();
        /// <summary>
        /// Suspend watching
        /// </summary>
        void StopWatch();
    }
}
