namespace Kott.Scopes
{
    /// <summary>
    ///     Marks a manager as a singleton: while its scope is loaded, it can be resolved by its concrete
    ///     type through <see cref="Singleton{T}" /> or <see cref="SingletonRegistry" />.
    /// </summary>
    /// <remarks>
    ///     Implement <see cref="ISingletonContract{T}" /> instead to also make the manager resolvable by an
    ///     interface, which is the recommended way to consume managers from other scopes.
    /// </remarks>
    public interface ISingleton
    { }
}
