using System;
using UnityEngine;

namespace Kott.Scopes.Demo
{
    /// <summary>Press as often as possible before the time runs out.</summary>
    [Serializable]
    public sealed class MashGame : MiniGameBase
    {
        [SerializeField]
        private float _duration = 5f;

        [SerializeField]
        [Tooltip("Presses per coin.")]
        private int _pressesPerCoin = 4;

        private bool _running;
        private float _remaining;
        private int _presses;

        /// <inheritdoc />
        public override string DisplayName => "Mash";

        /// <inheritdoc />
        protected override string Instructions => $"press as often as you can in {_duration:0} seconds.";

        /// <inheritdoc />
        protected override void Begin()
        {
            Status.text = "Press to start";
        }

        /// <inheritdoc />
        protected override void Tick(float deltaTime)
        {
            if (!_running)
            {
                return;
            }

            _remaining -= deltaTime;
            if (_remaining <= 0f)
            {
                _running = false;
                Finish(_presses, _presses / Mathf.Max(1, _pressesPerCoin), $"{_presses} presses");
                return;
            }

            Status.text = $"{_presses}  <size=32>({_remaining:0.0} s)</size>";
        }

        /// <inheritdoc />
        protected override void OnAction()
        {
            if (!_running)
            {
                _running = true;
                _remaining = _duration;
            }

            _presses++;
        }
    }
}
