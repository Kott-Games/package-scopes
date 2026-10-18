using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kott.Scopes
{
    /// <summary>
    ///     Logging of the scope system. Every manager gets its own <see cref="ILogger" /> (<see cref="Manager.Logger" />)
    ///     whose category is the manager's full type name; the scope system itself logs as <see cref="Category" />.
    /// </summary>
    /// <remarks>
    ///     Loggers are created by <see cref="ScopeHandler.LoggerFactory" />, which receives the category. Filter per
    ///     source there, e.g. by returning a logger with a higher <see cref="ILogger.filterLogType" /> for some
    ///     category prefixes. The default factory is <see cref="CreateDefaultLogger" />.
    /// </remarks>
    /// <example>
    ///     <code>
    /// [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    /// private static void ConfigureLogging()
    /// {
    ///     ScopeHandler.Instance.LoggerFactory = category =>
    ///     {
    ///         ILogger logger = ScopeLogging.CreateDefaultLogger(category);
    ///         if (category.StartsWith("Game.Audio"))
    ///             logger.filterLogType = LogType.Warning;
    ///         return logger;
    ///     };
    /// }
    /// </code>
    /// </example>
    public static class ScopeLogging
    {
        /// <summary>Category of the scope system's own logs.</summary>
        public const string Category = "Kott.Scopes";

        /// <summary>
        ///     Creates a logger that writes to <see cref="Debug.unityLogger" /> (respecting its filter) and prefixes
        ///     messages with <c>[category]</c>. Exceptions are passed on unchanged.
        /// </summary>
        public static ILogger CreateDefaultLogger(string category)
        {
            return new Logger(new CategoryLogHandler(category, Debug.unityLogger));
        }

        internal static string CategoryOf(Type type)
        {
            return type.FullName ?? type.Name;
        }
    }

    /// <summary>Prefixes formatted messages with <c>[category]</c> and forwards them.</summary>
    internal sealed class CategoryLogHandler : ILogHandler
    {
        private readonly string _prefix;
        private readonly ILogHandler _inner;

        public CategoryLogHandler(string category, ILogHandler inner)
        {
            if (string.IsNullOrEmpty(category))
            {
                throw new ArgumentException("The category must not be empty.", nameof(category));
            }

            _prefix = $"[{category.Replace("{", "{{").Replace("}", "}}")}] ";
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            _inner.LogFormat(logType, context, _prefix + format, args);
        }

        public void LogException(Exception exception, Object context)
        {
            _inner.LogException(exception, context);
        }
    }
}
