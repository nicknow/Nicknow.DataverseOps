using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataverseOps
{
    internal class DVOILogger : ILogger
    {
        private readonly ILogger? _loggerInstance;
        private readonly bool _activelyLogData;

        internal DVOILogger(ILogger loggerInstance)
        {
            _loggerInstance = loggerInstance;
            _activelyLogData = true;
        }

        internal DVOILogger()
        {
            _loggerInstance = null;
            _activelyLogData = false;
        }

        public bool IsLoggingEnabled => _activelyLogData;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            if (!_activelyLogData) return null;

            return _loggerInstance?.BeginScope(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            if (!_activelyLogData) return false;

            return _loggerInstance == null ? false : _loggerInstance.IsEnabled(logLevel);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!_activelyLogData) return;

            _loggerInstance?.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}
