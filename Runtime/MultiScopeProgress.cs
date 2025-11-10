using System;

namespace Kott.Scopes
{
    public class MultiScopeProgress : IProgress<ScopeProgress>
    {
        private IProgress<ScopeProgress> _progress;

        public ScopeProgress Start;
        public float Scale;

        public MultiScopeProgress(IProgress<ScopeProgress> progress)
        {
            _progress = progress;
        }

        public void Report(ScopeProgress value)
        {
            value.Info = value.Info != null ? $"{Start.Info} - {value.Info}" : Start.Info;
            value.Progress = Start.Progress + value.Progress * Scale;
            _progress.Report(value);
        }
    }
}
