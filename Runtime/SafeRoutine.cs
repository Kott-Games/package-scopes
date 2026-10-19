using System;
using System.Collections;
using System.Collections.Generic;

namespace Kott.Scopes
{
    /// <summary>
    ///     Runs a coroutine and its nested <see cref="IEnumerator" />s while catching exceptions.
    /// </summary>
    /// <remarks>
    ///     Unity stops a coroutine chain silently when a nested routine throws: callers never resume and
    ///     their <c>finally</c> blocks never run. This runner steps nested enumerators itself, so an exception
    ///     is reported through a callback and the caller continues. Other yield values (<c>null</c>,
    ///     <see cref="UnityEngine.YieldInstruction" />s, <see cref="UnityEngine.Coroutine" />s) are passed
    ///     through to Unity. An exception thrown while disposing a nested routine (its <c>finally</c> blocks) fails
    ///     the routine as well; several exceptions are reported as one <see cref="AggregateException" />.
    /// </remarks>
    internal static class SafeRoutine
    {
        /// <summary>Runs <paramref name="routine" />; an exception ends it and is passed to <paramref name="onException" />.</summary>
        public static IEnumerator Run(IEnumerator routine, Action<Exception> onException)
        {
            return Run(() => routine, onException);
        }

        /// <summary>
        ///     Creates the routine lazily and runs it, so a synchronous exception thrown by the factory
        ///     (e.g. a non-iterator override) is caught as well. A <c>null</c> routine counts as empty.
        /// </summary>
        public static IEnumerator Run(Func<IEnumerator> createRoutine, Action<Exception> onException)
        {
            Stack<IEnumerator> stack = new();
            Exception failure = null;

            try
            {
                IEnumerator routine = createRoutine();
                if (routine != null)
                {
                    stack.Push(routine);
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            while (failure == null && stack.Count > 0)
            {
                IEnumerator current = stack.Peek();
                object yielded = null;
                bool moved = false;

                try
                {
                    moved = current.MoveNext();
                    if (moved)
                    {
                        yielded = current.Current;
                    }
                }
                catch (Exception exception)
                {
                    failure = exception;
                }

                if (failure != null)
                {
                    break;
                }

                if (!moved)
                {
                    stack.Pop();
                    failure = Dispose(current);
                    continue;
                }

                if (yielded is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }

                yield return yielded;
            }

            if (failure == null)
            {
                yield break;
            }

            List<Exception> failures = null;
            while (stack.Count > 0)
            {
                Exception disposeFailure = Dispose(stack.Pop());
                if (disposeFailure != null)
                {
                    failures ??= new List<Exception> { failure };
                    failures.Add(disposeFailure);
                }
            }

            onException?.Invoke(failures == null
                ? failure
                : new AggregateException("A routine threw, and so did its cleanup.", failures));
        }

        private static Exception Dispose(IEnumerator enumerator)
        {
            try
            {
                (enumerator as IDisposable)?.Dispose();
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }
    }
}
