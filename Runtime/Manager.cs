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
        public virtual IEnumerator InitializeAsync(IProgress<ProgressReport> progress)
        {
            yield break;
        }

        public virtual void OnSceneLoaded() { }
        public virtual void OnSceneActivated() { }
        public virtual void Update() { }
    }
}