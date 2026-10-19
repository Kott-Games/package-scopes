using UnityEngine;

namespace Kott.Scopes.Demo
{
    /// <summary>
    ///     A tile of the Board scene. Scene components get <see cref="InjectAttribute" /> fields too: the scene's
    ///     SceneRoot injects them before their <c>Awake</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardTile : MonoBehaviour
    {
        [SerializeField]
        private int _index;

        [Inject]
        private IGameSession _session;

        /// <summary>Position of the tile on the track, starting at 0.</summary>
        public int Index => _index;

        /// <summary>Color of a tile kind on the board and in the HUD.</summary>
        public static Color ColorOf(TileKind kind)
        {
            switch (kind)
            {
                case TileKind.Coins:
                    return new Color(0.95f, 0.8f, 0.25f);
                case TileKind.Penalty:
                    return new Color(0.85f, 0.3f, 0.3f);
                case TileKind.MiniGame:
                    return new Color(0.55f, 0.4f, 0.95f);
                default:
                    return new Color(0.85f, 0.85f, 0.85f);
            }
        }

        private void Awake()
        {
            // _session is already set here
            Primitives.Create(PrimitiveType.Cube, transform, Vector3.zero, new Vector3(1.7f, 0.2f, 1.7f),
                ColorOf(_session.GetTileKind(_index)));
        }
    }
}
