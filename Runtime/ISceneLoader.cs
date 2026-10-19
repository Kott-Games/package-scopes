using System;
using System.Collections;
using Kott.Utilities.Progress;
using UnityEngine.SceneManagement;

namespace Kott.Scopes
{
    /// <summary>
    ///     Loads and unloads the scene of one loaded scope. Created per load by
    ///     <see cref="SceneReference.CreateLoader" />, so it may hold state such as handles.
    /// </summary>
    public interface ISceneLoader
    {
        /// <summary>The loaded scene. Invalid before loading finished (and for loaders without a real scene).</summary>
        Scene Scene { get; }

        /// <summary>
        ///     Loads the scene additively, activates it and makes it the active scene.
        ///     Signal failure by throwing.
        /// </summary>
        /// <param name="progress">Progress from 0 to 1. May be <c>null</c>.</param>
        IEnumerator Load(IProgress<ProgressReport> progress);

        /// <summary>
        ///     Unloads the scene. Also called after a failed <see cref="Load" />, so it must cope with a
        ///     scene that is only partially or not at all loaded.
        /// </summary>
        /// <param name="progress">Progress from 0 to 1. May be <c>null</c>.</param>
        IEnumerator Unload(IProgress<ProgressReport> progress);
    }
}
