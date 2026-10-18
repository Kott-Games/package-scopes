using UnityEngine;

namespace Kott.Scopes
{
    /// <summary>
    ///     Creates the <see cref="ILogger" /> of a category (the source of the logs, usually a full type name), like
    ///     .NET's <c>ILoggerFactory</c>. The factory decides output and filtering per category; the loggers it returns
    ///     are used as they are. Built in: <see cref="UnityLoggerFactory" />.
    /// </summary>
    public interface ILoggerFactory
    {
        /// <summary>Creates the logger for <paramref name="category" />; never returns <c>null</c>.</summary>
        ILogger CreateLogger(string category);
    }
}
