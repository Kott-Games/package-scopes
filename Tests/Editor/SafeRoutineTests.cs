using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kott.Scopes.Tests
{
    public class SafeRoutineTests
    {
        [Test]
        public void Run_NestedRoutineThrows_ReportsExceptionAndEnds()
        {
            List<string> log = new();
            Exception caught = null;

            Drain(SafeRoutine.Run(Outer(log), e => caught = e));

            Assert.That(caught, Is.InstanceOf<InvalidOperationException>());
            Assert.That(log, Is.EqualTo(new[] { "outer", "inner", "inner finally", "outer finally" }));
        }

        [Test]
        public void Run_CleanupThrowsAfterFailure_ReportsBothAsAggregate()
        {
            Exception caught = null;

            Drain(SafeRoutine.Run(ThrowingCleanup(Throwing("inner")), e => caught = e));

            Assert.That(caught, Is.InstanceOf<AggregateException>());
            Assert.That(((AggregateException)caught).InnerExceptions.Select(e => e.Message),
                Is.EqualTo(new[] { "inner", "cleanup" }));
        }

        [Test]
        public void Run_CleanupThrowsAfterSuccess_ReportsException()
        {
            // regression: dispose exceptions went straight to Debug.LogException instead of the caller
            Exception caught = null;

            Drain(SafeRoutine.Run(ThrowingDispose(), e => caught = e));

            Assert.That(caught?.Message, Is.EqualTo("dispose"));
        }

        [Test]
        public void Run_FactoryThrows_ReportsException()
        {
            Exception caught = null;

            Drain(SafeRoutine.Run(() => throw new InvalidOperationException("factory"), e => caught = e));

            Assert.That(caught?.Message, Is.EqualTo("factory"));
        }

        [Test]
        public void Run_NullRoutine_CompletesWithoutException()
        {
            bool failed = false;

            Drain(SafeRoutine.Run(() => null, _ => failed = true));

            Assert.That(failed, Is.False);
        }

        [Test]
        public void Run_YieldInstructions_PassedThrough()
        {
            WaitForEndOfFrame instruction = new();
            List<object> yielded = new();

            IEnumerator routine = SafeRoutine.Run(Yielding(instruction), null);
            while (routine.MoveNext())
            {
                yielded.Add(routine.Current);
            }

            Assert.That(yielded, Is.EqualTo(new object[] { null, instruction, null }));
        }

        private static IEnumerator Outer(List<string> log)
        {
            try
            {
                log.Add("outer");
                yield return Inner(log);
                log.Add("outer after inner");
            }
            finally
            {
                log.Add("outer finally");
            }
        }

        private static IEnumerator Inner(List<string> log)
        {
            try
            {
                log.Add("inner");
                yield return null;
                throw new InvalidOperationException("inner");
            }
            finally
            {
                log.Add("inner finally");
            }
        }

        private static IEnumerator Yielding(object instruction)
        {
            yield return null;
            yield return NestedYielding(instruction);
            yield return null;
        }

        private static IEnumerator NestedYielding(object instruction)
        {
            yield return instruction;
        }

        private static IEnumerator ThrowingCleanup(IEnumerator nested)
        {
            try
            {
                yield return nested;
            }
            finally
            {
                throw new InvalidOperationException("cleanup");
            }
        }

        private static IEnumerator Throwing(string message)
        {
            yield return null;
            throw new InvalidOperationException(message);
        }

        private static IEnumerator ThrowingDispose()
        {
            return new DisposeThrowingRoutine();
        }

        private sealed class DisposeThrowingRoutine : IEnumerator, IDisposable
        {
            public object Current => null;

            public bool MoveNext()
            {
                return false;
            }

            public void Reset()
            { }

            public void Dispose()
            {
                throw new InvalidOperationException("dispose");
            }
        }

        private static void Drain(IEnumerator routine)
        {
            while (routine.MoveNext())
            { }
        }
    }
}
