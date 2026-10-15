namespace Kott.Scopes
{
    /// <summary>
    ///     Accessor for the singleton registered as <typeparamref name="T" />, typically declared as a field
    ///     of a manager or component: <c>private Singleton&lt;IScoreManager&gt; _scores;</c>.
    /// </summary>
    /// <remarks>
    ///     Every access resolves through <see cref="SingletonRegistry.Current" />. The instance is
    ///     intentionally not cached: scopes unload and reload, and a cached instance would outlive its scope.
    ///     Tests can substitute instances with <c>SingletonTestScope</c> (Kott.Scopes.Testing).
    /// </remarks>
    /// <typeparam name="T">A manager type or singleton contract.</typeparam>
    public struct Singleton<T>
        where T : ISingleton
    {
        /// <summary>The registered instance.</summary>
        /// <exception cref="System.InvalidOperationException">Nothing is registered as <typeparamref name="T" />.</exception>
        public T Instance => SingletonRegistry.Current.Get<T>();

        /// <summary>Whether an instance is currently registered as <typeparamref name="T" />.</summary>
        public bool IsAvailable => SingletonRegistry.Current.IsRegistered(typeof(T));

        /// <summary>Gets the registered instance if there is one.</summary>
        public bool TryGet(out T instance)
        {
            return SingletonRegistry.Current.TryGet(out instance);
        }
    }
}
