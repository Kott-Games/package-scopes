using Kott.Utilities.Progress;
using System;
using System.Collections;

namespace Kott.Scopes
{
    public interface IManager
    { }

    public interface IManagerContract<T> : IManager
        where T : IManager
    { }

    [System.Serializable]
    public abstract class Manager : IManager
    {
        public virtual IEnumerator Initialize(IProgress<ProgressReport> progress)
        {
            yield break;
        }

        public virtual IEnumerator Uninitialize(IProgress<ProgressReport> progress)
        {
            yield break;
        }

        public virtual void SceneAwake() { }
        public virtual void SceneStart() { }
        public virtual void SceneExit() { }

        public virtual void Update() { }
    }
}