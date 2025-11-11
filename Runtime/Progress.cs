using System;

namespace ToG.Scopes
{
    public struct ScopeProgress
    {
        public float Progress;
        public string Info;

        public ScopeProgress(float progress, string info)
        {
            Progress = progress;
            Info = info;
        }
    }

    public class SubProgress : IProgress<ScopeProgress>
    {
        private IProgress<ScopeProgress> _progress;

        public ScopeProgress Start;
        public float Scale;

        public SubProgress(IProgress<ScopeProgress> progress)
        {
            _progress = progress;
        }

        public void Report(ScopeProgress value)
        {
            value.Info = value.Info != null ? $"{Start.Info}\n{value.Info}" : Start.Info;
            value.Progress = Start.Progress + value.Progress * Scale;
            _progress.Report(value);
        }
    }
}
