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
        public void CategoryLogHandler_EmptyCategory_Throws()
        {
            Assert.Throws<ArgumentException>(() => new CategoryLogHandler("", _log));
        }
    }
}
