using System.Collections.Generic;

namespace Kott.Scopes.Demo
{
    /// <summary>
    ///     State of the running board game: players, coins, positions, turn and round. Lives in the Game scope, so it
    ///     survives while Board and the mini games are unloaded and loaded again.
    /// </summary>
    public interface IGameSession : IManagerContract
    {
        /// <summary>All players in turn order.</summary>
        IReadOnlyList<PlayerState> Players { get; }

        /// <summary>Index of the player whose turn it is.</summary>
        int CurrentPlayerIndex { get; }

        /// <summary>The player whose turn it is.</summary>
        PlayerState CurrentPlayer { get; }

        /// <summary>Current round, starting at 1. Greater than <see cref="RoundCount" /> once the game is over.</summary>
        int Round { get; }

        /// <summary>Number of rounds in a game.</summary>
        int RoundCount { get; }

        /// <summary>Number of tiles on the board.</summary>
        int TileCount { get; }

        /// <summary>Whether all rounds are played.</summary>
        bool IsOver { get; }

        /// <summary>
        ///     Whether the current player landed on a mini game tile: the turn ends with
        ///     <see cref="CompleteMiniGame" />.
        /// </summary>
        bool AwaitsMiniGame { get; }

        /// <summary>A line describing the last thing that happened.</summary>
        string LastEvent { get; }

        /// <summary>The kind of tile <paramref name="tile" />.</summary>
        TileKind GetTileKind(int tile);

        /// <summary>
        ///     Rolls the dice and moves the current player. Coin tiles apply immediately and end the turn; a mini game
        ///     tile sets <see cref="AwaitsMiniGame" />.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">The game is over or awaits a mini game.</exception>
        MoveResult Move();

        /// <summary>Awards <paramref name="coins" /> to the current player and ends the turn.</summary>
        /// <exception cref="System.InvalidOperationException">No mini game is awaited.</exception>
        void CompleteMiniGame(int coins);

        /// <summary>The player with the most coins (the earlier one on a tie).</summary>
        PlayerState GetLeader();
    }
}
