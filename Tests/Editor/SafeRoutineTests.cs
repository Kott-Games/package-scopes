using System;
using System.Collections;
using System.Collections.Generic;
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

        private static void Drain(IEnumerator routine)
        {
            while (routine.MoveNext())
            { }
        }
    }
}
