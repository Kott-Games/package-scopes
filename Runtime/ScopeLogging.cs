using System;
using UnityEngine;

namespace Kott.Scopes
{
    /// <summary>
    ///     Logging of the scope system. Everything logs through one <see cref="ILogger" />,
    ///     <see cref="ScopeHandler.Logger" />, with a tag naming the source: <see cref="Tag" /> for the scope system,
    ///     the full type name for each manager (<see cref="Manager.Logger" />).
    /// </summary>
    /// <remarks>
    ///     To filter per source, assign an own <see cref="ILogger" /> implementation: it receives the tag in
    ///     <see cref="ILogger.Log(LogType, string, object, UnityEngine.Object)" />. A plain
    ///     <see cref="ILogHandler" /> only sees the formatted text (<c>"tag: message"</c>).
    /// </remarks>
    public static class ScopeLogging
    {
        /// <summary>Tag of the scope system's own logs.</summary>
        public const string Tag = "Kott.Scopes";

        /// <summary>The handler's logger, or <see cref="Debug.unityLogger" /> if no handler exists.</summary>
        internal static ILogger Current => ScopeHandler.HasInstance ? ScopeHandler.Instance.Logger : Debug.unityLogger;

        internal static string TagOf(Type type)
        {
            return type.FullName ?? type.Name;
        }
    }
}
