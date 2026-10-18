using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kott.Scopes
{
    /// <summary>
    ///     Logs through the scope system's <see cref="ILogger" /> with a fixed tag naming the source, e.g. a
    ///     manager's full type name. Follows when <see cref="ScopeHandler.Logger" /> is replaced.
    /// </summary>
    /// <example>
    ///     <code>
    /// Logger.Log(LogType.Warning, "No save found, starting fresh.");
    /// Logger.LogFormat(LogType.Log, "Loaded {0} levels in {1:F1}s", count, seconds);
    /// </code>
    /// </example>
    public sealed class TaggedLogger
    {
        private readonly Func<ILogger> _logger;

        internal TaggedLogger(string tag, Func<ILogger> logger)
        {
            if (string.IsNullOrEmpty(tag))
            {
                throw new ArgumentException("The tag must not be empty.", nameof(tag));
            }

            Tag = tag;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>The source of the logs, passed as tag to the <see cref="ILogger" />.</summary>
        public string Tag { get; }

        /// <summary>Logs <paramref name="message" />; <paramref name="context" /> is pinged when the log is clicked.</summary>
        public void Log(LogType type, object message, Object context = null)
        {
            ILogger logger = _logger();
            if (logger.IsLogTypeAllowed(type))
            {
                logger.Log(type, Tag, message, context);
            }
        }

        /// <summary>Logs a <see cref="string.Format(string, object[])" /> message; it is only formatted if the log type is enabled.</summary>
        public void LogFormat(LogType type, string format, params object[] args)
        {
            ILogger logger = _logger();
            if (logger.IsLogTypeAllowed(type))
            {
                logger.Log(type, Tag, string.Format(format, args));
            }
        }

        /// <summary>Exceptions keep their own stack trace; Unity's API takes no tag for them.</summary>
        internal void LogException(Exception exception)
        {
            _logger().LogException(exception);
        }
    }
}
