using UnityEngine;

namespace Kott.Scopes.Demo
{
    /// <summary>
    ///     A player's piece. Spawned from a prefab with <see cref="Scope.Instantiate{T}" />, which injects it before
    ///     its <c>Awake</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerToken : MonoBehaviour
    {
        [Inject]
        private IGameSession _session;

        private Renderer _renderer;

        /// <summary>The player this token belongs to.</summary>
        public int PlayerIndex { get; private set; }

        /// <summary>Assigns the token to a player and takes the player's color.</summary>
        public void Bind(int playerIndex)
        {
            PlayerIndex = playerIndex;
            PlayerState player = _session.Players[playerIndex];
            name = $"Token {player.Name}";
            Primitives.Tint(_renderer, player.Color);
        }

        private void Awake()
        {
            _renderer = Primitives.Create(PrimitiveType.Capsule, transform, new Vector3(0f, 0.6f, 0f),
                new Vector3(0.45f, 0.5f, 0.45f), Color.white);
        }
    }
}
