using System;
using System.Collections;
using Kott.Utilities.Progress;
using UnityEngine;
using UnityEngine.UI;

namespace Kott.Scopes.Demo
{
    /// <summary>
    ///     Fullscreen progress overlay, spawned under the Core scope's root so it survives every scene change. Also
    ///     owns the EventSystem, since every scope below shows UI.
    /// </summary>
    [Serializable]
    public sealed class LoadingScreen : Manager, ILoadingScreen
    {
        [SerializeField]
        [Tooltip("Keeps the screen up at least this long, so fast loads don't flicker.")]
        private float _minimumDuration = 0.4f;

        private RectTransform _canvas;
        private Text _title;
        private Text _info;
        private Image _bar;
        private ProgressSink _progress;
        private int _pending;
        private float _shownAt;

        /// <inheritdoc />
        public IProgress<ProgressReport> Progress => _progress;

        /// <inheritdoc />
        public override IEnumerator Initialize(IProgress<ProgressReport> progress)
        {
            DemoUI.CreateEventSystem(Scope.Root.transform);

            _canvas = DemoUI.CreateCanvas("Loading Screen", Scope.Root.transform, 100);
            RectTransform background = DemoUI.Fullscreen(_canvas, new Color(0.04f, 0.05f, 0.07f, 1f));
            RectTransform column = DemoUI.Column(background, 900f);
            _title = DemoUI.Label(column, string.Empty, 48);
            _bar = DemoUI.ProgressBar(column);
            _info = DemoUI.Label(column, string.Empty, 24);
            _info.color = new Color(1f, 1f, 1f, 0.6f);

            _progress = new ProgressSink(this);
            _canvas.gameObject.SetActive(false);
            yield break;
        }

        /// <inheritdoc />
        public void ShowUntilDone(string title, ScopeOperation operation)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (_pending++ == 0)
            {
                _shownAt = Time.unscaledTime;
                _bar.fillAmount = 0f;
                _info.text = string.Empty;
            }

            _title.text = title;
            _canvas.gameObject.SetActive(true);
            operation.Completed += _ => Scope.StartCoroutine(HideAfterMinimumDuration());
        }

        private IEnumerator HideAfterMinimumDuration()
        {
            while (Time.unscaledTime - _shownAt < _minimumDuration)
            {
                yield return null;
            }

            if (--_pending == 0)
            {
                _canvas.gameObject.SetActive(false);
            }
        }

        private void Report(ProgressReport report)
        {
            _bar.fillAmount = report.Progress;
            if (!string.IsNullOrEmpty(report.Info))
            {
                _info.text = report.Info;
            }
        }

        // keeps IProgress off the manager's public surface
        private sealed class ProgressSink : IProgress<ProgressReport>
        {
            private readonly LoadingScreen _screen;

            public ProgressSink(LoadingScreen screen)
            {
                _screen = screen;
            }

            public void Report(ProgressReport value)
            {
                _screen.Report(value);
            }
        }
    }
}
