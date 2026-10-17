namespace Kott.Scopes.Demo
{
    /// <summary>What happens when a player lands on a board tile.</summary>
    public enum TileKind
    {
        /// <summary>The first tile; nothing happens.</summary>
        Start,

        /// <summary>The player gains coins.</summary>
        Coins,

        /// <summary>The player loses coins.</summary>
        Penalty,

        /// <summary>The player plays a mini game for coins.</summary>
        MiniGame,
    }
}
