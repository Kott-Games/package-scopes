using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Kott.Scopes.Demo
{
    /// <summary>
    ///     Shared frame of the mini games: UI, input and reporting the result. Injects from three levels: its own
    ///     scene (<c>ui</c>), the Game scope (<see cref="IGameSession" />, <see cref="IGameFlow" />) and the root
    ///     scope (<see cref="ISaveManager" />). Inherited private fields are injected too.
    /// </summary>
    [Serializable]
    public abstract class MiniGameBase : Manager, IMiniGame
    {
        [SerializeField]
        private float _returnDelay = 2f;

        [SerializeField]
        private KeyCode _actionKey = KeyCode.Space;

        [Inject]
        private IGameSession _session;

        [Inject]
        private IGameFlow _flow;

        [Inject]
        private ISaveManager _save;

        [InjectScene("ui")]
        private Transform _ui;

        /// <inheritdoc />
        public abstract string DisplayName { get; }

        /// <inheritdoc />
        public bool IsFinished { get; private set; }

        /// <inheritdoc />
        public int Score { get; private set; }

        /// <summary>One line explaining the controls.</summary>
        protected abstract string Instructions { get; }

        /// <summary>The big status line in the middle of the screen.</summary>
        protected Text Status { get; private set; }

        /// <inheritdoc />
        public override void OnSceneAwake()
        {
            RectTransform canvas = DemoUI.CreateCanvas(DisplayName, _ui, 0);
            RectTransform column = DemoUI.Column(canvas, 900f, DemoUI.PanelColor);
            DemoUI.Label(column, $"<b>{DisplayName}</b>", 64).color = DemoUI.AccentColor;
            DemoUI.Label(column, $"{_session.CurrentPlayer.Name}: {Instructions}", 28);
            BuildContent(column);
            Status = DemoUI.Label(column, string.Empty, 44);
            DemoUI.Button(column, $"Go ({_actionKey})", HandleAction, 36);
            Begin();
        }

        /// <inheritdoc />
        public override void Update()
        {
            // the Pause scope sets the time scale to 0, but key presses still arrive
            if (IsFinished || _flow.IsPaused)
            {
                return;
            }

            if (Input.GetKeyDown(_actionKey))
            {
                HandleAction();
            }

            Tick(Time.deltaTime);
        }

        /// <summary>Adds game-specific UI between the instructions and the status line.</summary>
        protected virtual void BuildContent(RectTransform column)
        { }

        /// <summary>Called once the UI exists.</summary>
        protected abstract void Begin();

        /// <summary>Called every unpaused frame until the game is finished.</summary>
        protected abstract void Tick(float deltaTime);

        /// <summary>Called when the player presses the action key or button.</summary>
        protected abstract void OnAction();

        /// <summary>Reports the result to the session and the profile, then returns to the board.</summary>
        protected void Finish(int score, int coins, string summary)
        {
            IsFinished = true;
            Score = score;
            bool best = _save.SubmitScore(DisplayName, score);
            if (_session.AwaitsMiniGame)
            {
                _session.CompleteMiniGame(coins);
            }

            Status.text =
                $"{summary}\n<size=32>Score {score}{(best ? " - new best!" : string.Empty)} · +{coins} coins</size>";
            Scope.StartCoroutine(ReturnToBoard());
        }

        private void HandleAction()
        {
            if (!IsFinished && !_flow.IsPaused)
            {
                OnAction();
            }
        }

        private IEnumerator ReturnToBoard()
        {
            yield return new WaitForSeconds(_returnDelay);
            _flow.ReturnToBoard();
        }
    }
}
