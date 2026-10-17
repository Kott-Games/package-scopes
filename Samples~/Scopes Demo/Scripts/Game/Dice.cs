using System;
using System.Collections;
using Kott.Utilities.Progress;
using UnityEngine;
using Random = System.Random;

namespace Kott.Scopes.Demo
{
    /// <summary>Rolls with a random generator created in <see cref="Initialize" />.</summary>
    [Serializable]
    public sealed class Dice : Manager, IDice
    {
        [SerializeField]
        [Tooltip("0 = different rolls every game.")]
        private int _seed;

        private Random _random;

        /// <inheritdoc />
        public override IEnumerator Initialize(IProgress<ProgressReport> progress)
        {
            _random = _seed != 0 ? new Random(_seed) : new Random();
            yield break;
        }

        /// <inheritdoc />
        public int Roll()
        {
            if (_random == null)
            {
                throw new InvalidOperationException("The dice are not initialized yet.");
            }

            return _random.Next(1, 7);
        }
    }
}
