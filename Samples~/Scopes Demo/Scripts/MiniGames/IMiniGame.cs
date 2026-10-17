namespace Kott.Scopes.Demo
{
    /// <summary>
    ///     Provided by every mini game scope. The mini games are siblings under Game, so several definitions can
    ///     provide the same contract: only one of them is in a chain at a time.
    /// </summary>
    public interface IMiniGame : IManagerContract
    {
        /// <summary>Name shown in the UI and used for best scores.</summary>
        string DisplayName { get; }

        /// <summary>Whether the round is over and the result was reported.</summary>
        bool IsFinished { get; }

        /// <summary>The final score, once <see cref="IsFinished" />.</summary>
        int Score { get; }
    }
}
