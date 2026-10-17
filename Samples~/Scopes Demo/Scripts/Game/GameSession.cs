using System;
using System.Collections;
using System.Collections.Generic;
using Kott.Utilities.Progress;
using UnityEngine;

namespace Kott.Scopes.Demo
{
    /// <summary>
    ///     The board game's rules and state. Listed before <see cref="Dice" /> in the Game definition, but initializes
    ///     after it: it injects <see cref="IDice" />, and same-scope dependencies initialize first.
    /// </summary>
    [Serializable]
    public sealed class GameSession : Manager, IGameSession
    {
        private static readonly Color[] PlayerColors =
        {
            new(0.95f, 0.35f, 0.3f), new(0.3f, 0.6f, 1f), new(0.35f, 0.85f, 0.4f), new(1f, 0.8f, 0.25f),
        };

        [SerializeField]
        [Range(1, 4)]
        private int _playerCount = 2;

        [SerializeField]
        [Min(1)]
        private int _roundCount = 5;

        [SerializeField]
        [Min(4)]
        [Tooltip("Must match the BoardTiles in the Board scene.")]
        private int _tileCount = 12;

        [SerializeField]
        private int _coinTileReward = 3;

        [SerializeField]
        private int _penaltyTileCost = 2;

        [Inject]
        private IDice _dice;

        private readonly List<PlayerState> _players = new();
        private int _startingPlayer;

        /// <inheritdoc />
        public IReadOnlyList<PlayerState> Players => _players;

        /// <inheritdoc />
        public int CurrentPlayerIndex { get; private set; }

        /// <inheritdoc />
        public PlayerState CurrentPlayer => _players[CurrentPlayerIndex];

        /// <inheritdoc />
        public int Round { get; private set; }

        /// <inheritdoc />
        public int RoundCount => _roundCount;

        /// <inheritdoc />
        public int TileCount => _tileCount;

        /// <inheritdoc />
        public bool IsOver => Round > _roundCount;

        /// <inheritdoc />
        public bool AwaitsMiniGame { get; private set; }

        /// <inheritdoc />
        public string LastEvent { get; private set; }

        /// <inheritdoc />
        public override IEnumerator Initialize(IProgress<ProgressReport> progress)
        {
            _players.Clear();
            for (int i = 0; i < _playerCount; i++)
            {
                _players.Add(new PlayerState($"Player {i + 1}", PlayerColors[i % PlayerColors.Length]));
            }

            // uses IDice while initializing: only works because Dice is initialized first
            int bestRoll = 0;
            for (int i = 0; i < _players.Count; i++)
            {
                int roll = _dice.Roll();
                if (roll > bestRoll)
                {
                    bestRoll = roll;
                    _startingPlayer = i;
                }
            }

            CurrentPlayerIndex = _startingPlayer;
            Round = 1;
            AwaitsMiniGame = false;
            LastEvent = $"{CurrentPlayer.Name} starts (rolled {bestRoll}).";
            yield break;
        }

        /// <inheritdoc />
        public TileKind GetTileKind(int tile)
        {
            if (tile == 0)
            {
                return TileKind.Start;
            }

            switch (tile % 3)
            {
                case 1:
                    return TileKind.Coins;
                case 2:
                    return TileKind.MiniGame;
                default:
                    return TileKind.Penalty;
            }
        }

        /// <inheritdoc />
        public MoveResult Move()
        {
            if (IsOver)
            {
                throw new InvalidOperationException("The game is over.");
            }

            if (AwaitsMiniGame)
            {
                throw new InvalidOperationException($"{CurrentPlayer.Name} has to play a mini game first.");
            }

            PlayerState player = CurrentPlayer;
            int roll = _dice.Roll();
            int from = player.Position;
            int to = (from + roll) % _tileCount;
            player.Position = to;

            TileKind tile = GetTileKind(to);
            int coins = 0;
            switch (tile)
            {
                case TileKind.Coins:
                    coins = _coinTileReward;
                    break;
                case TileKind.Penalty:
                    coins = -Mathf.Min(_penaltyTileCost, player.Coins);
                    break;
            }

            player.Coins += coins;
            MoveResult result = new(CurrentPlayerIndex, roll, from, to, tile, coins);

            if (tile == TileKind.MiniGame)
            {
                AwaitsMiniGame = true;
                LastEvent = $"{player.Name} rolled {roll}: mini game!";
            }
            else
            {
                LastEvent = coins switch
                {
                    > 0 => $"{player.Name} rolled {roll}: +{coins} coins.",
                    < 0 => $"{player.Name} rolled {roll}: {coins} coins.",
                    _ => $"{player.Name} rolled {roll}.",
                };
                EndTurn();
            }

            return result;
        }

        /// <inheritdoc />
        public void CompleteMiniGame(int coins)
        {
            if (!AwaitsMiniGame)
            {
                throw new InvalidOperationException("No mini game is awaited.");
            }

            coins = Mathf.Max(0, coins);
            CurrentPlayer.Coins += coins;
            LastEvent = $"{CurrentPlayer.Name} won {coins} coins in the mini game.";
            AwaitsMiniGame = false;
            EndTurn();
        }

        /// <inheritdoc />
        public PlayerState GetLeader()
        {
            PlayerState leader = _players[0];
            foreach (PlayerState player in _players)
            {
                if (player.Coins > leader.Coins)
                {
                    leader = player;
                }
            }

            return leader;
        }

        private void EndTurn()
        {
            CurrentPlayerIndex = (CurrentPlayerIndex + 1) % _players.Count;
            if (CurrentPlayerIndex == _startingPlayer)
            {
                Round++;
            }
        }
    }
}
