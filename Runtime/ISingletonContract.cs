namespace Kott.Scopes
{
    /// <summary>
    ///     Declares that a manager provides the singleton contract <typeparamref name="T" />. While the
    ///     manager's scope is loaded, <c>Singleton&lt;T&gt;</c> resolves to it.
    /// </summary>
    /// <typeparam name="T">The contract type, usually the interface that declares this one.</typeparam>
    /// <example>
    ///     <code>
    /// public interface IScoreManager : ISingletonContract&lt;IScoreManager&gt;
    /// {
    ///     int Score { get; }
    /// }
    /// </code>
    /// </example>
    public interface ISingletonContract<T> : ISingleton
        where T : ISingleton
    { }
}
