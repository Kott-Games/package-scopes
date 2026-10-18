using System;
using UnityEngine;

namespace Kott.Scopes
{
    /// <summary>
    ///     Logging of the scope system, modeled on .NET logging: an <see cref="ILoggerFactory" />
    ///     (<see cref="ScopeHandler.LoggerFactory" />) creates one <see cref="ILogger" /> per category. Managers get
    ///     theirs as <see cref="Manager.Logger" /> (category = full type name); the scope system logs as
    ///     <see cref="Category" />.
    /// </summary>
    /// <example>
    ///     <code>
    /// [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    /// private static void ConfigureLogging()
    /// {
    ///     ScopeHandler.Instance.LoggerFactory = new UnityLoggerFactory()
    ///         .SetFilter("Kott.Scopes", LogType.Warning)
    ///         .SetFilter("Game.Audio", LogType.Error);
    /// }
    /// </code>
    /// </example>
    public static class ScopeLogging
    {
        /// <summary>Category of the scope system's own logs.</summary>
        public const string Category = "Kott.Scopes";

        /// <summary>The scope system's logger, or a default one if no handler exists.</summary>
        internal static ILogger Current =>
            ScopeHandler.HasInstance ? ScopeHandler.Instance.Logger : new UnityLoggerFactory().CreateLogger(Category);

        internal static string CategoryOf(Type type)
        {
            return type.FullName ?? type.Name;
        }
    }
}
