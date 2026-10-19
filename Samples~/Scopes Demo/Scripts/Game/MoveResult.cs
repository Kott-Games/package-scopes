namespace Kott.Scopes.Demo
{
    /// <summary>The outcome of <see cref="IGameSession.Move" />.</summary>
    public readonly struct MoveResult
    {
        /// <summary>Creates a result.</summary>
        public MoveResult(int player, int roll, int from, TileKind tile, int coins)
        {
            Player = player;
            Roll = roll;
            From = from;
            Tile = tile;
            Coins = coins;
        }

        /// <summary>Index of the player who moved.</summary>
        public int Player { get; }

        /// <summary>The rolled number.</summary>
        public int Roll { get; }

        /// <summary>The tile the player started on.</summary>
        public int From { get; }

        /// <summary>The kind of the landing tile.</summary>
        public TileKind Tile { get; }

        /// <summary>Coins gained (positive) or lost (negative) on the landing tile.</summary>
        public int Coins { get; }
    }
}
