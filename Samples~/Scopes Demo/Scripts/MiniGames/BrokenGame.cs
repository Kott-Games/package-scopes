using System;
using System.Collections;
using Kott.Utilities.Progress;

namespace Kott.Scopes.Demo
{
    /// <summary>
    ///     Fails on purpose while initializing. The scope system uninitializes the managers that already initialized
    ///     (the DebugDelayManager listed before it), removes the scope, and fails the load operation; GameFlow shows
    ///     the error and returns to the board.
    /// </summary>
    [Serializable]
    public sealed class BrokenGame : Manager, IMiniGame
    {
        /// <inheritdoc />
        public string DisplayName => "Broken";

        /// <inheritdoc />
        public override IEnumerator Initialize(IProgress<ProgressReport> progress)
        {
            yield return null;
            throw new InvalidOperationException("Broken on purpose, to show how a failed load is cleaned up.");
        }
    }
}
