using System;
using UnityEngine;

namespace Kott.Scopes
{
    /// <summary>Helpers for <see cref="ILoggerFactory" />.</summary>
    public static class LoggerFactoryExtensions
    {
        /// <summary>Creates the logger whose category is the full name of <typeparamref name="T" />.</summary>
        public static ILogger CreateLogger<T>(this ILoggerFactory factory)
        {
            return factory.CreateLogger(typeof(T));
        }

        /// <summary>Creates the logger whose category is the full name of <paramref name="type" />.</summary>
        public static ILogger CreateLogger(this ILoggerFactory factory, Type type)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            return factory.CreateLogger(ScopeLogging.CategoryOf(type));
        }
    }
}
