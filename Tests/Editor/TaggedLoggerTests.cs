using System;
using NUnit.Framework;
using UnityEngine;

namespace Kott.Scopes.Tests
{
    public class TaggedLoggerTests
    {
        private RecordingLogHandler _log;

        [SetUp]
        public void SetUp()
        {
            _log = new RecordingLogHandler();
        }

        [TearDown]
        public void TearDown()
        {
            _log.Verify();
        }

        [Test]
        public void Log_Message_PassesTagToLogger()
        {
            _log.Expect(LogType.Warning, @"^Game\.Combat: low health$");

            new TaggedLogger("Game.Combat", () => _log.Logger).Log(LogType.Warning, "low health");
        }

        [Test]
        public void LogFormat_Args_Formatted()
        {
            _log.Expect(LogType.Log, @"^Game\.Combat: 3 of 5 \{x\}$");

            new TaggedLogger("Game.Combat", () => _log.Logger).LogFormat(LogType.Log, "{0} of {1} {2}", 3, 5, "{x}");
        }

        [Test]
        public void LogFormat_TypeFiltered_NotFormatted()
        {
            _log.Logger.filterLogType = LogType.Warning;
            CountingFormattable argument = new();

            new TaggedLogger("Game.Combat", () => _log.Logger).LogFormat(LogType.Log, "{0}", argument);

            Assert.That(argument.Formatted, Is.EqualTo(0));
        }

        [Test]
        public void Log_LoggerReplaced_UsesCurrentLogger()
        {
            RecordingLogHandler first = new();
            ILogger current = first.Logger;
            TaggedLogger logger = new("Game.Combat", () => current);
            _log.Expect(LogType.Log, "^Game.Combat: second$");

            current = _log.Logger;
            logger.Log(LogType.Log, "second");

            first.AssertNotLogged(LogType.Log, "second");
        }

        [Test]
        public void Constructor_EmptyTag_Throws()
        {
            Assert.Throws<ArgumentException>(() => new TaggedLogger("", () => _log.Logger));
        }

        private sealed class CountingFormattable
        {
            public int Formatted;

            public override string ToString()
            {
                Formatted++;
                return "counted";
            }
        }
    }
}
