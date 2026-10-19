namespace Kott.Scopes.Demo
{
    /// <summary>The player profile, kept for the whole session by the root scope (Core).</summary>
    public interface ISaveManager : IManagerContract
    {
        /// <summary>The loaded profile. Change it through the methods below so it gets saved.</summary>
        PlayerProfile Profile { get; }

        /// <summary>Counts a finished board game and adds the coins of all players to the total.</summary>
        void RecordGame(int coins);

        /// <summary>Stores <paramref name="score" /> if it beats the best score of <paramref name="miniGame" />.</summary>
        /// <returns>Whether it is a new best score.</returns>
        bool SubmitScore(string miniGame, int score);

        /// <summary>Resets the profile to its defaults.</summary>
        void ResetProfile();
    }
}
