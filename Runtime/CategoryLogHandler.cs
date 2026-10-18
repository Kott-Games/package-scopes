using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kott.Scopes
{
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
