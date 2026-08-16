namespace Kott.Scopes
{
    public struct Singleton<T>
        where T : ISingleton
    {
        // TODO cache instance
        public T Instance => ScopeHandler.Instance.GetSingleton<T>();
    }
}