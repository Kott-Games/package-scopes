using UnityEngine;

namespace Kott.Scopes.Demo
{
    /// <summary>One player of the running game.</summary>
    public sealed class PlayerState
    {
        /// <summary>Creates a player on the start tile.</summary>
        public PlayerState(string name, Color color)
        {
            Name = name;
            Color = color;
        }

        /// <summary>Display name.</summary>
        public string Name { get; }

        /// <summary>Token color.</summary>
        public Color Color { get; }

        /// <summary>Collected coins (never negative).</summary>
        public int Coins { get; internal set; }

        /// <summary>Index of the tile the player stands on.</summary>
        public int Position { get; internal set; }
    }
}
