using System;
using System.Collections;
using Kott.Utilities.Progress;
using UnityEngine;

namespace Kott.Scopes
{
    public sealed class DebugDelayManager : Manager
    {
        [SerializeField] private float _initializeDelay = 1f;
        [SerializeField] private float _uninitializeDelay = 1f;
        
        public override IEnumerator Initialize(IProgress<ProgressReport> progress)
        {
            return DelayRoutine(_initializeDelay, progress);
        }
        
        public override IEnumerator Uninitialize(IProgress<ProgressReport> progress)
        {
            return DelayRoutine(_uninitializeDelay, progress);
        }

        private static IEnumerator DelayRoutine(float delay, IProgress<ProgressReport> progress)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            float passedTime, startTime = Time.unscaledTime;
            while ((passedTime = Time.unscaledTime - startTime) < delay)
            {
                progress?.Report(new ProgressReport(passedTime / delay, info: null));
                yield return null;
            }
            progress?.Report(new ProgressReport(1f, info: null));
#else
            yield break;
#endif
        }
    }
}