using System;
using Kott.Utilities.Progress;

namespace Kott.Scopes.Demo
{
    /// <summary>A fullscreen overlay showing the progress of scope operations.</summary>
    public interface ILoadingScreen : IManagerContract
    {
        /// <summary>Pass this to <c>LoadScope</c>/<c>UnloadScope</c> to show the operation's progress.</summary>
        IProgress<ProgressReport> Progress { get; }

        /// <summary>
        ///     Shows the screen with <paramref name="title" /> until <paramref name="operation" /> is done. Overlapping
        ///     calls keep it visible until the last of their operations is done.
        /// </summary>
        void ShowUntilDone(string title, ScopeOperation operation);
    }
}
