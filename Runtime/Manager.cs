using System;
using System.Collections;
using UnityEngine;

namespace Kott.Scopes
{
    public abstract class Manager : ScriptableObject
    {
        [Obsolete("Use Initialize instead.")]
        public virtual System.Threading.Tasks.Task InitializeAsync(IProgress<ScopeProgress> progress)
        {
            return System.Threading.Tasks.Task.Delay(0);
        }

        public virtual IEnumerator Initialize(IProgress<ScopeProgress> progress)
        {
            yield break;
        }

        public virtual void OnSceneLoaded() { }
        public virtual void Update() { }
    }

    public abstract class Manager<T> : Manager where T : Manager
    {
        private static T _instance;

        public static T Instance => _instance;

        public override IEnumerator Initialize(IProgress<ScopeProgress> progress)
        {
            _instance = (T)(object)this;
            yield break;
        }
    }
}