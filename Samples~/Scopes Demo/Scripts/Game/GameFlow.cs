using System;
using System.Collections;
using System.Collections.Generic;
using Kott.Utilities.Progress;
using UnityEngine;

namespace Kott.Scopes.Demo
{
    /// <summary>
    ///     Board and the mini games are siblings under Game. Switching unloads one and loads the other; the game
    ///     state stays in <see cref="GameSession" />, one level up, so Board rebuilds itself from it on every load.
    /// </summary>
    [Serializable]
    public sealed class GameFlow : Manager, IGameFlow
    {
        [SerializeField]
        private ScopeDefinition _board;

        [SerializeField]
        private ScopeDefinition _pause;

        [SerializeField]
        private List<ScopeDefinition> _miniGames = new();

        [SerializeField]
        private KeyCode _pauseKey = KeyCode.Escape;

        [Inject]
        private IGameSession _session;

        [Inject]
        private IAppFlow _app;

        [Inject]
        private ILoadingScreen _loadingScreen;

        [Inject]
        private IScopeDebugOverlay _overlay;

        // the definition of the loaded (or loading) heavy child: Board or a mini game
        private ScopeDefinition _current;

        /// <inheritdoc />
        public bool IsBusy { get; private set; }

        /// <inheritdoc />
        public bool IsPaused { get; private set; }

        /// <inheritdoc />
        public IReadOnlyList<ScopeDefinition> MiniGames => _miniGames;

        /// <inheritdoc />
        public IMiniGame CurrentMiniGame { get; private set; }

        /// <inheritdoc />
        public override IEnumerator Initialize(IProgress<ProgressReport> progress)
        {
            // queued: runs right after Game finished loading (see AppFlow.Initialize)
            ReturnToBoard();
            yield break;
        }

        /// <inheritdoc />
        public override void Update()
        {
            if (Input.GetKeyDown(_pauseKey))
            {
                TogglePause();
            }
        }

        /// <inheritdoc />
        public void PlayMiniGame(ScopeDefinition miniGame)
        {
            if (miniGame == null)
            {
                throw new ArgumentNullException(nameof(miniGame));
            }

            Switch(miniGame, $"Loading {miniGame.name}", operation =>
            {
                if (operation.Succeeded)
                {
                    // IMiniGame is provided by whichever mini game is loaded: resolve it through the child scope
                    CurrentMiniGame = operation.Scope.Resolve<IMiniGame>();
                    _overlay.Log($"playing {CurrentMiniGame.DisplayName} (resolved IMiniGame from {operation.Scope.Name})");
                    return;
                }

                // the failed scope was already cleaned up by the scope system: no unload needed
                _overlay.ShowError($"{miniGame.name} failed to load: {operation.Exception.Message}");
                _current = null;
                if (_session.AwaitsMiniGame)
                {
                    _session.CompleteMiniGame(0);
                }

                ReturnToBoard();
            });
        }

        /// <inheritdoc />
        public void ReturnToBoard()
        {
            Switch(_board, "Loading board", operation =>
            {
                if (!operation.Succeeded)
                {
                    _overlay.ShowError($"Board failed to load: {operation.Exception.Message}");
                }
            });
        }

        /// <inheritdoc />
        public void TogglePause()
        {
            if (IsBusy)
            {
                return;
            }

            ScopeHandler handler = ScopeHandler.Instance;
            if (IsPaused)
            {
                if (handler.TryGetScope(_pause, out Scope pause))
                {
                    IsPaused = false;
                    handler.UnloadScope(pause);
                }
            }
            else
            {
                IsPaused = true;
                handler.LoadScope(_pause, Scope).Completed += operation => IsPaused = operation.Succeeded;
            }
        }

        /// <inheritdoc />
        public void ExitToMenu()
        {
            _app.ShowMenu();
        }

        private void Switch(ScopeDefinition target, string title, Action<ScopeOperation> completed)
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            CurrentMiniGame = null;
            ScopeHandler handler = ScopeHandler.Instance;
            if (_current != null && handler.TryGetScope(_current, out Scope current))
            {
                handler.UnloadScope(current, _loadingScreen.Progress);
            }

            _current = target;
            ScopeOperation load = handler.LoadScope(target, Scope, _loadingScreen.Progress);
            _loadingScreen.ShowUntilDone(title, load);
            load.Completed += operation =>
            {
                IsBusy = false;
                completed(operation);
            };
        }
    }
}
