namespace Kott.Scopes
{
    /// <summary>
    ///     Marker for manager contracts. A contract is an interface that extends this one; a manager implementing
    ///     it provides it to its own scope and to every scope loaded below it, where it is injected into
    ///     <see cref="InjectAttribute" /> fields.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Contracts resolve along the scope's parent chain only: own scope → parent → … → root. Scopes in other
    ///         branches of the tree never see each other's contracts.
    ///     </para>
    ///     <para>
    ///         A contract can have only one provider per chain: loading a scope that provides a contract one of its
    ///         ancestors already provides fails. Sibling scopes may provide the same contract.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    /// public interface IScoreManager : IManagerContract
    /// {
    ///     int Score { get; }
    /// }
    ///
    /// [Serializable]
    /// public class ScoreManager : Manager, IScoreManager { … }
    ///
    /// [Serializable]
    /// public class HudManager : Manager
    /// {
    ///     [Inject] private IScoreManager _scores;
    /// }
    /// </code>
    /// </example>
    public interface IManagerContract
    { }
}
