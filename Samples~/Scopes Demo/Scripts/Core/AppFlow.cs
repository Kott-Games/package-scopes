using System;
using System.Collections;
using Kott.Utilities.Progress;
using UnityEngine;

namespace Kott.Scopes.Demo
{
    /// <summary>
    ///     Swaps the Core scope's children: Menu ↔ Game. Menu and Game are siblings, so neither can see the other's
    ///     contracts; the swap is done here, one level above both.
    /// </summary>
    [Serializable]
    public sealed class AppFlow : Manager, IAppFlow
    {
        [SerializeField]
        private ScopeDefinition _menu;

        [SerializeField]
        private ScopeDefinition _game;

        [Inject]
        private ILoadingScreen _loadingScreen;

        [Inject]
        private IScopeDebugOverlay _overlay;

        /// <inheritdoc />
        public bool IsBusy { get; private set; }

        /// <inheritdoc />
        public override IEnumerator Initialize(IProgress<ProgressReport> progress)
        {
            // Operations run one after another: this load starts once Core itself is loaded. Waiting for it here
            // would deadlock, queuing it is fine.
            Swap(null, _menu, "Loading menu");
            yield break;
        }

        /// <inheritdoc />
        public void StartGame()
        {
            Swap(_menu, _game, "Starting game");
        }

        /// <inheritdoc />
        public void ShowMenu()
        {
            Swap(_game, _menu, "Back to menu");
        }

        /// <inheritdoc />
        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void Swap(ScopeDefinition from, ScopeDefinition to, string title)
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            ScopeHandler handler = ScopeHandler.Instance;

            // unloading a scope unloads its children first (Game: Pause, Board or a mini game)
            if (from != null && handler.TryGetScope(from, out Scope current))
            {
                handler.UnloadScope(current, _loadingScreen.Progress);
            }

            ScopeOperation load = handler.LoadScope(to, Scope, _loadingScreen.Progress);
            _loadingScreen.ShowUntilDone(title, load);
            load.Completed += operation =>
            {
                IsBusy = false;
                if (!operation.Succeeded)
                {
                    _overlay.ShowError($"Loading {to.name} failed: {operation.Exception.Message}");
                }
            };
        }
    }
}
