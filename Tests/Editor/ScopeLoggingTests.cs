using System;
using NUnit.Framework;
using UnityEngine;

namespace Kott.Scopes.Tests
{
    public class ScopeLoggingTests
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
        public void CategoryLogHandler_Message_PrefixedWithCategory()
        {
            _log.Expect(LogType.Warning, @"^\[Game\.Combat\] low health: 3$");
            ILogger logger = new Logger(new CategoryLogHandler("Game.Combat", _log));

            logger.Log(LogType.Warning, "low health: 3");
        }

        [Test]
        public void CategoryLogHandler_CategoryWithBraces_NotTreatedAsFormat()
        {
            _log.Expect(LogType.Log, @"^\[Odd\{0\}\] hi$");
            ILogger logger = new Logger(new CategoryLogHandler("Odd{0}", _log));

            logger.Log("hi");
        }

        [Test]
        public void CategoryLogHandler_Exception_PassedOnUnchanged()
        {
            _log.Expect(LogType.Exception, "^System.InvalidOperationException: boom$");
            ILogger logger = new Logger(new CategoryLogHandler("Game.Combat", _log));

            logger.LogException(new InvalidOperationException("boom"));
        }

        [Test]
        public void UnityLoggerFactory_CreateLogger_WritesWithCategoryPrefix()
        {
            _log.Expect(LogType.Warning, @"^\[Game\.Combat\] low health$");

            new UnityLoggerFactory(_log).CreateLogger("Game.Combat").Log(LogType.Warning, "low health");
        }

        [Test]
        public void UnityLoggerFactory_CreateLoggerOfType_UsesFullTypeName()
        {
            _log.Expect(LogType.Log, $@"^\[{typeof(ScopeLoggingTests).FullName.Replace(".", @"\.")}\] hi$");

            new UnityLoggerFactory(_log).CreateLogger<ScopeLoggingTests>().Log("hi");
        }

        [Test]
        public void UnityLoggerFactory_Filters_LongestPrefixWins()
        {
            UnityLoggerFactory factory = new UnityLoggerFactory(_log) { DefaultFilter = LogType.Warning }
                .SetFilter("Game", LogType.Error)
                .SetFilter("Game.Audio", LogType.Log);

            Assert.That(factory.CreateLogger("Game.Audio.Music").filterLogType, Is.EqualTo(LogType.Log));
            Assert.That(factory.CreateLogger("Game.Combat").filterLogType, Is.EqualTo(LogType.Error));
            Assert.That(factory.CreateLogger("Kott.Scopes").filterLogType, Is.EqualTo(LogType.Warning));
        }

        [Test]
        public void UnityLoggerFactory_SetFilterTwice_LastOneApplies()
        {
            UnityLoggerFactory factory = new UnityLoggerFactory(_log)
                .SetFilter("Game", LogType.Error)
                .SetFilter("Game", LogType.Warning);

            Assert.That(factory.CreateLogger("Game.Combat").filterLogType, Is.EqualTo(LogType.Warning));
        }

        [Test]
        public void UnityLoggerFactory_FilteredLog_NotWritten()
        {
            ILogger logger = new UnityLoggerFactory(_log).SetFilter("Game", LogType.Warning).CreateLogger("Game.X");

            logger.Log(LogType.Log, "hidden");

            _log.AssertNotLogged(LogType.Log, "hidden");
        }

        [Test]
        public void CategoryLogHandler_EmptyCategory_Throws()
        {
            Assert.Throws<ArgumentException>(() => new CategoryLogHandler("", _log));
        }
    }
}
