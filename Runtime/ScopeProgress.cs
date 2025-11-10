namespace Kott.Scopes
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
}
