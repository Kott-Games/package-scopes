using System;
using System.Collections.Generic;

namespace Kott.Scopes.Demo
{
    /// <summary>Persistent player data, stored as JSON in <c>PlayerPrefs</c>.</summary>
    [Serializable]
    public sealed class PlayerProfile
    {
        /// <summary>Finished board games.</summary>
        public int GamesPlayed;

        /// <summary>Coins collected over all finished games.</summary>
        public int TotalCoins;

        /// <summary>Best score per mini game.</summary>
        public List<BestScore> BestScores = new();

        /// <summary>The best score of <paramref name="miniGame" />, or <c>null</c> if it was never played.</summary>
        public int? GetBestScore(string miniGame)
        {
            return BestScores.Find(score => score.MiniGame == miniGame)?.Score;
        }
    }

    /// <summary>Best score of one mini game.</summary>
    [Serializable]
    public sealed class BestScore
    {
        /// <summary>Name of the mini game.</summary>
        public string MiniGame;

        /// <summary>The best score reached.</summary>
        public int Score;
    }
}
