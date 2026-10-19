using System;
using System.Collections;
using Kott.Utilities.Progress;
using UnityEngine;

namespace Kott.Scopes.Demo
{
    /// <summary>
    ///     The Pause scope: a scene-less overlay that stops time while it is loaded. Its UI lives under the scope's
    ///     root, so unloading the scope removes it, also when the whole Game scope unloads.
    /// </summary>
    [Serializable]
    public sealed class PauseMenu : Manager
    {
        [Inject]
        private IGameFlow _flow;

        private float _timeScale;

        /// <inheritdoc />
        public override IEnumerator Initialize(IProgress<ProgressReport> progress)
        {
            RectTransform canvas = DemoUI.CreateCanvas("Pause", Scope.Root.transform, 50);
            RectTransform dim = DemoUI.Fullscreen(canvas, new Color(0f, 0f, 0f, 0.6f));
            RectTransform column = DemoUI.Column(dim, 520f, DemoUI.PanelColor);
            DemoUI.Label(column, "<b>Paused</b>", 56).color = DemoUI.AccentColor;
            DemoUI.Button(column, "Resume", _flow.TogglePause);
            DemoUI.Button(column, "Exit to Menu", _flow.ExitToMenu);

            _timeScale = Time.timeScale;
            Time.timeScale = 0f;
            yield break;
        }

        /// <inheritdoc />
        public override IEnumerator Uninitialize(IProgress<ProgressReport> progress)
        {
            Time.timeScale = _timeScale;
            yield break;
        }
    }
}
