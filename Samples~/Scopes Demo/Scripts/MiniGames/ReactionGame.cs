using System;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Kott.Scopes.Demo
{
    /// <summary>Press as soon as the light turns green. Faster reactions score more.</summary>
    [Serializable]
    public sealed class ReactionGame : MiniGameBase
    {
        private static readonly Color _waitColor = new(0.85f, 0.25f, 0.25f);
        private static readonly Color _goColor = new(0.3f, 0.9f, 0.4f);

        [SerializeField]
        private Vector2 _waitRange = new(1.5f, 3.5f);

        [SerializeField]
        [Tooltip("Reaction time that scores 0.")]
        private float _slowestTime = 1f;

        private Image _light;
        private bool _go;
        private float _timer;

        /// <inheritdoc />
        public override string DisplayName => "Reaction";

        /// <inheritdoc />
        protected override string Instructions => "press as soon as the light turns green.";

        /// <inheritdoc />
        protected override void BuildContent(RectTransform column)
        {
            RectTransform row = DemoUI.Row(column, "Light");
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            Vector2 center = new(0.5f, 0.5f);
            _light = DemoUI.Panel(row, _waitColor, center, center).GetComponent<Image>();
            _light.rectTransform.sizeDelta = new Vector2(220f, 220f);
        }

        /// <inheritdoc />
        protected override void Begin()
        {
            Wait("Wait for green...");
        }

        /// <inheritdoc />
        protected override void Tick(float deltaTime)
        {
            if (_go)
            {
                _timer += deltaTime;
                return;
            }

            _timer -= deltaTime;
            if (_timer <= 0f)
            {
                _go = true;
                _timer = 0f;
                _light.color = _goColor;
                Status.text = "NOW!";
            }
        }

        /// <inheritdoc />
        protected override void OnAction()
        {
            if (!_go)
            {
                Wait("Too early! Wait for green...");
                return;
            }

            int milliseconds = Mathf.RoundToInt(_timer * 1000f);
            int score = Mathf.RoundToInt(Mathf.Clamp01(1f - _timer / _slowestTime) * 1000f);
            Finish(score, score / 100, $"{milliseconds} ms");
        }

        private void Wait(string status)
        {
            _go = false;
            _timer = Random.Range(_waitRange.x, _waitRange.y);
            _light.color = _waitColor;
            Status.text = status;
        }
    }
}
