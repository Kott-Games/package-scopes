using System;
using System.Collections;
using Kott.Utilities.Progress;
using UnityEngine;

namespace Kott.Scopes
{
    public abstract class Manager : MonoBehaviour
    {
        public RuntimeScope RuntimeScope { get; internal set; }

        /// <summary>
        ///     Initialization before the scene is loaded.
        /// </summary>
        public virtual IEnumerator Initialize(IProgress<ProgressReport> progress)
        {
            yield break;
        }

        /// <summary>
        ///     Uninitialization after the scene is unloaded.
        /// </summary>
        public virtual IEnumerator Uninitialize(IProgress<ProgressReport> progress)
        {
            yield break;
        }

        public virtual void SceneAwake()
        { }

        public virtual void SceneStart()
        { }

        public virtual void SceneUpdate()
        { }

        public virtual void SceneExit()
        { }
    }
}