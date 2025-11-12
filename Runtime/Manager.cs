using Kott.Utilities.Progress;
using System;
using System.Collections;
using UnityEngine;

namespace Kott.Scopes
{
    public abstract class Manager : ScriptableObject
    {
        public virtual IEnumerator InitializeAsync(IProgress<ProgressReport> progress)
        {
            yield break;
        }

        public virtual void OnSceneLoaded() { }
        public virtual void OnSceneActivated() { }
        public virtual void Update() { }
    }

    public abstract class Manager<T> : Manager where T : Manager
    {
        private static T _instance;

        public static T Instance => _instance;

        public override IEnumerator InitializeAsync(IProgress<ProgressReport> progress)
        {
            _instance = (T)(object)this;
            yield return base.InitializeAsync(progress);
        }
    }
}