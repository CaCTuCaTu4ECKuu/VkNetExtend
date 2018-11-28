using System;
using Microsoft.Extensions.Logging;

namespace VkNetExtend.MessageLongPoll.Logger
{
    public class ConsoleLogger : ILogger
    {
        public LogLevel LogLevel { get; set; } = LogLevel.Information;

        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None && logLevel >= this.LogLevel;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (IsEnabled(logLevel))
                Console.WriteLine($"{logLevel}: {eventId} - {formatter(state, exception)}");
        }
    }
}
