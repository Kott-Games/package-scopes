using System;
using System.Collections;
using System.Collections.Generic;
using Kott.Scopes.Testing;
using NUnit.Framework;

namespace Kott.Scopes.Demo.Tests
{
    /// <summary>
    ///     Game-side unit tests: a manager is a plain class, so it is created with <c>new</c> and its contracts are
    ///     filled with fakes through the <see cref="TestInjector" />. No scope, scene or play mode needed.
    /// </summary>
    public class GameSessionTests
    {
        private sealed class FakeDice : IDice
        {
            private readonly Queue<int> _rolls;

            public FakeDice(params int[] rolls)
            {
                _rolls = new Queue<int>(rolls);
            }

            public int Roll()
            {
                return _rolls.Dequeue();
            }
        }

        // the first two rolls decide the starting player (two players by default)
        private static GameSession CreateSession(params int[] rolls)
        {
            GameSession session = TestInjector.Create().Add<IDice>(new FakeDice(rolls)).Inject(new GameSession());
            IEnumerator routine = session.Initialize(null);
            while (routine.MoveNext())
            { }

            return session;
        }

        [Test]
        public void Initialize_SecondPlayerRollsHigher_SecondPlayerStarts()
        {
            GameSession session = CreateSession(2, 5);

            Assert.That(session.CurrentPlayerIndex, Is.EqualTo(1));
            Assert.That(session.Round, Is.EqualTo(1));
        }

        [Test]
        public void Move_OntoCoinTile_AddsCoinsAndEndsTurn()
        {
            GameSession session = CreateSession(6, 1, 1);

            MoveResult move = session.Move();

            Assert.That(move.Tile, Is.EqualTo(TileKind.Coins));
            Assert.That(session.Players[0].Coins, Is.EqualTo(3));
            Assert.That(session.Players[0].Position, Is.EqualTo(1));
            Assert.That(session.CurrentPlayerIndex, Is.EqualTo(1));
        }

        [Test]
        public void Move_OntoPenaltyTileWithoutCoins_CoinsStayAtZero()
        {
            GameSession session = CreateSession(6, 1, 3);

            MoveResult move = session.Move();

            Assert.That(move.Tile, Is.EqualTo(TileKind.Penalty));
            Assert.That(move.Coins, Is.EqualTo(0));
            Assert.That(session.Players[0].Coins, Is.EqualTo(0));
        }

        [Test]
        public void Move_OntoMiniGameTile_AwaitsMiniGameAndKeepsTurn()
        {
            GameSession session = CreateSession(6, 1, 2, 4);

            session.Move();

            Assert.That(session.AwaitsMiniGame, Is.True);
            Assert.That(session.CurrentPlayerIndex, Is.EqualTo(0));
            Assert.Throws<InvalidOperationException>(() => session.Move());
        }

        [Test]
        public void CompleteMiniGame_AfterMiniGameTile_AwardsCoinsAndEndsTurn()
        {
            GameSession session = CreateSession(6, 1, 2);
            session.Move();

            session.CompleteMiniGame(7);

            Assert.That(session.AwaitsMiniGame, Is.False);
            Assert.That(session.Players[0].Coins, Is.EqualTo(7));
            Assert.That(session.CurrentPlayerIndex, Is.EqualTo(1));
        }

        [Test]
        public void CompleteMiniGame_NoMiniGameAwaited_Throws()
        {
            GameSession session = CreateSession(6, 1);

            Assert.Throws<InvalidOperationException>(() => session.CompleteMiniGame(1));
        }

        [Test]
        public void Move_AllPlayersMoved_NextRound()
        {
            GameSession session = CreateSession(1, 6, 3, 3);

            session.Move();
            session.Move();

            Assert.That(session.Round, Is.EqualTo(2));
            Assert.That(session.CurrentPlayerIndex, Is.EqualTo(1));
        }

        [Test]
        public void Move_LastRoundPlayed_GameIsOver()
        {
            // rolls of 3 only reach penalty and start tiles: no mini game interrupts the turns
            int[] rolls = new int[2 + 2 * 5];
            Array.Fill(rolls, 3);
            rolls[0] = 6;
            rolls[1] = 1;
            GameSession session = CreateSession(rolls);

            for (int i = 0; i < 2 * 5; i++)
            {
                session.Move();
            }

            Assert.That(session.IsOver, Is.True);
            Assert.Throws<InvalidOperationException>(() => session.Move());
        }
    }
}
