using Kott.Utilities.Progress;
using System;
using System.Collections;
using UnityEngine;

namespace Kott.Scopes
{
    public interface IManager
    {
        RuntimeScope RuntimeScope { get; }
        
        IEnumerator Initialize(IProgress<ProgressReport> progress)
        {
            yield break;
        }

        IEnumerator Uninitialize(IProgress<ProgressReport> progress)
        {
            yield break;
        }

        void SceneAwake() { }
        void SceneStart() { }
        void SceneExit() { }
    }
    
    public interface ISingletonManager : IManager
    { }

    public interface IManagerContract<T> : IManager
        where T : IManager
    { }

    [Serializable]
    public abstract class Manager : Component, IManager
    {
        public RuntimeScope RuntimeScope { get; internal set; }
    }
}