using Kott.Utilities.Progress;
using System;
using System.Collections;
using UnityEngine;

namespace Kott.Scopes
{
    public class DelayManager : Manager
    {
        [SerializeField] private float _delaySeconds = 1f;

        public override IEnumerator InitializeAsync(IProgress<ProgressReport> progress)
        {
            yield return base.InitializeAsync(progress);
            float timer = 0f;
            while (timer < _delaySeconds)
            {
                timer += Time.deltaTime;
                progress.Report(new ProgressReport(timer / _delaySeconds, "Delaying..."));
                yield return null;
            }
        }
    }
}