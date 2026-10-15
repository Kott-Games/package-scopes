using System;

namespace Kott.Scopes.Testing
{
    /// <summary>
    ///     Replaces the singletons <see cref="Singleton{T}" /> resolves for the duration of a test, so managers
    ///     and other code can be tested with fakes and without loading scopes.
    /// </summary>
    /// <remarks>
    ///     While the scope is alive, <see cref="SingletonRegistry.Current" /> is a fresh registry containing only
    ///     what was added here. Disposing restores the previous registry. Nested scopes must be disposed in
    ///     reverse order of creation (<c>using</c> does that).
    /// </remarks>
    /// <example>
    ///     <code>
    /// using var singletons = SingletonTestScope.Create()
    ///     .Add&lt;IConfigManager&gt;(new FakeConfigManager())
    ///     .Add&lt;IScoreManager&gt;(new FakeScoreManager());
    /// </code>
    /// </example>
    public sealed class SingletonTestScope : IDisposable
    {
        private bool _disposed;

        private SingletonTestScope()
        {
            SingletonRegistry.PushOverride(Registry);
        }

        /// <summary>The registry active while this scope is alive.</summary>
        public SingletonRegistry Registry { get; } = new();

        /// <summary>Creates a scope and makes it current.</summary>
        public static SingletonTestScope Create()
        {
            return new SingletonTestScope();
        }

        /// <summary>Registers <paramref name="instance" /> as <typeparamref name="T" /> only.</summary>
        /// <exception cref="InvalidOperationException"><typeparamref name="T" /> was already added.</exception>
        public SingletonTestScope Add<T>(T instance)
            where T : ISingleton
        {
            ThrowIfDisposed();
            Registry.RegisterAs(this, typeof(T), instance);
            return this;
        }

        /// <summary>
        ///     Registers <paramref name="instance" /> the way a loaded scope would: under its concrete type and all
        ///     of its singleton contracts.
        /// </summary>
        public SingletonTestScope AddWithContracts(ISingleton instance)
        {
            ThrowIfDisposed();
            Registry.Register(this, instance);
            return this;
        }

        /// <summary>Restores the previously active registry.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            SingletonRegistry.PopOverride(Registry);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return "SingletonTestScope";
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SingletonTestScope));
            }
        }
    }
}
