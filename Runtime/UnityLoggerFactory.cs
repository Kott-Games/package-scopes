using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kott.Scopes
{
    /// <summary>
    ///     The default <see cref="ILoggerFactory" />: loggers write to <see cref="Debug.unityLogger" /> (or another
    ///     output) with messages prefixed <c>[category]</c>, filtered per category prefix.
    /// </summary>
    /// <remarks>
    ///     A filter is a <see cref="ILogger.filterLogType" />: that log type and more severe ones pass (Error, Assert,
    ///     Warning, Log; exceptions always pass). The filter of the longest matching category prefix applies
    ///     (ordinal <see cref="string.StartsWith(string, StringComparison)" />), otherwise <see cref="DefaultFilter" />.
    ///     Filters apply to loggers created afterwards; a logger's <see cref="ILogger.filterLogType" /> can still be
    ///     changed directly.
    /// </remarks>
    public sealed class UnityLoggerFactory : ILoggerFactory
    {
        private readonly ILogHandler _output;
        private readonly List<(string Prefix, LogType Filter)> _filters = new();

        /// <summary>Creates a factory writing to <see cref="Debug.unityLogger" /> (its global filter still applies).</summary>
        public UnityLoggerFactory()
            : this(Debug.unityLogger)
        { }

        /// <summary>Creates a factory writing to <paramref name="output" />.</summary>
        public UnityLoggerFactory(ILogHandler output)
        {
            _output = output ?? throw new ArgumentNullException(nameof(output));
        }

        /// <summary>Filter for categories without a matching prefix; <see cref="LogType.Log" /> (everything) by default.</summary>
        public LogType DefaultFilter { get; set; } = LogType.Log;

        /// <summary>Sets the filter of all categories starting with <paramref name="categoryPrefix" />.</summary>
        /// <returns>This factory, for chaining.</returns>
        public UnityLoggerFactory SetFilter(string categoryPrefix, LogType filterLogType)
        {
            if (string.IsNullOrEmpty(categoryPrefix))
            {
                throw new ArgumentException("The prefix must not be empty; use DefaultFilter instead.",
                    nameof(categoryPrefix));
            }

            _filters.RemoveAll(filter => filter.Prefix == categoryPrefix);
            _filters.Add((categoryPrefix, filterLogType));
            return this;
        }

        /// <inheritdoc />
        public ILogger CreateLogger(string category)
        {
            return new Logger(new CategoryLogHandler(category, _output)) { filterLogType = FilterOf(category) };
        }

        private LogType FilterOf(string category)
        {
            LogType result = DefaultFilter;
            int longest = 0;
            foreach ((string prefix, LogType filter) in _filters)
            {
                if (prefix.Length > longest && category.StartsWith(prefix, StringComparison.Ordinal))
                {
                    result = filter;
                    longest = prefix.Length;
                }
            }

            return result;
        }
    }
}
