using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Kott.Scopes.Demo
{
    /// <summary>
    ///     The Board scope: the heavy 3D world of the board game. Holds no game state itself: it is unloaded for every
    ///     mini game and rebuilt from <see cref="IGameSession" /> (Game scope) when it loads again.
    /// </summary>
    [Serializable]
    public sealed class BoardManager : Manager
    {
        private static readonly Vector3 _cameraOffset = new(0f, 9f, -8.5f);

        [SerializeField]
        private PlayerToken _tokenPrefab;

        [SerializeField]
        private float _stepDuration = 0.2f;

        [SerializeField]
        private KeyCode _rollKey = KeyCode.Space;

        [Inject]
        private IGameSession _session;

        [Inject]
        private IGameFlow _flow;

        [Inject]
        private ISaveManager _save;

        // filled from the scene's SceneLinks once the scene is loaded, null during Initialize
        [InjectScene("board_camera")]
        private Camera _camera;

        [InjectScene("tiles")]
        private Transform _tilesRoot;

        [InjectScene("tokens")]
        private Transform _tokensRoot;

        [InjectScene("hud")]
        private Transform _hudRoot;

        private readonly List<BoardTile> _tiles = new();
        private readonly List<PlayerToken> _tokens = new();
        private readonly StringBuilder _builder = new();
        private RectTransform _canvas;
        private Text _hud;
        private Button _rollButton;
        private RectTransform _popup;
        private bool _moving;

        /// <inheritdoc />
        public override void OnSceneAwake()
        {
            _tilesRoot.GetComponentsInChildren(_tiles);
            _tiles.Sort((a, b) => a.Index.CompareTo(b.Index));
            if (_tiles.Count != _session.TileCount)
            {
                Logger.Log(LogType.Warning,
                    $"The Board scene has {_tiles.Count} tiles, but the session expects {_session.TileCount}.");
            }

            for (int i = 0; i < _session.Players.Count; i++)
            {
                // Scope.Instantiate injects the copy's components before their Awake
                PlayerToken token = Scope.Instantiate(_tokenPrefab, _tokensRoot);
                token.Bind(i);
                token.transform.position = TokenPosition(_session.Players[i].Position, i);
                _tokens.Add(token);
            }

            BuildHud();
            _camera.transform.position = CameraTarget();
        }

        /// <inheritdoc />
        public override void OnSceneStart()
        {
            // rebuilt from the session: a game that ended in a mini game shows its result right away
            if (_session.IsOver)
            {
                ShowGameOver();
            }
            else if (_session.AwaitsMiniGame)
            {
                ShowMiniGameChoice();
            }
        }

        /// <inheritdoc />
        public override void Update()
        {
            if (Input.GetKeyDown(_rollKey))
            {
                Roll();
            }

            _rollButton.interactable = CanRoll();
            _hud.text = DescribeState();

            float blend = 1f - Mathf.Exp(-3f * Time.deltaTime);
            _camera.transform.position = Vector3.Lerp(_camera.transform.position, CameraTarget(), blend);
        }

        /// <inheritdoc />
        public override void OnSceneExit()
        {
            // the scene objects are destroyed with the scene; only drop the references
            _tiles.Clear();
            _tokens.Clear();
        }

        private bool CanRoll()
        {
            return !_moving && _popup == null && !_session.IsOver && !_session.AwaitsMiniGame && !_flow.IsBusy &&
                   !_flow.IsPaused;
        }

        private void Roll()
        {
            if (!CanRoll())
            {
                return;
            }

            MoveResult move = _session.Move();
            Scope.StartCoroutine(Animate(move));
        }

        private IEnumerator Animate(MoveResult move)
        {
            _moving = true;
            Transform token = _tokens[move.Player].transform;
            for (int step = 1; step <= move.Roll; step++)
            {
                int tile = (move.From + step) % _session.TileCount;
                Vector3 start = token.position;
                Vector3 end = TokenPosition(tile, move.Player);
                for (float time = 0f; time < _stepDuration; time += Time.deltaTime)
                {
                    float t = time / _stepDuration;
                    token.position = Vector3.Lerp(start, end, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.6f);
                    yield return null;
                }

                token.position = end;
            }

            _moving = false;
            if (move.Tile == TileKind.MiniGame)
            {
                ShowMiniGameChoice();
            }
            else if (_session.IsOver)
            {
                ShowGameOver();
            }
        }

        private void ShowMiniGameChoice()
        {
            RectTransform column = OpenPopup($"<b>{_session.CurrentPlayer.Name}: mini game!</b>");
            foreach (ScopeDefinition miniGame in _flow.MiniGames)
            {
                DemoUI.Button(column, miniGame.name, () => _flow.PlayMiniGame(miniGame));
            }
        }

        private void ShowGameOver()
        {
            PlayerState leader = _session.GetLeader();
            RectTransform column = OpenPopup($"<b>Game over</b>\n{leader.Name} wins with {leader.Coins} coins.");
            DemoUI.Button(column, "Back to Menu", () =>
            {
                int coins = 0;
                foreach (PlayerState player in _session.Players)
                {
                    coins += player.Coins;
                }

                _save.RecordGame(coins);
                _flow.ExitToMenu();
            });
        }

        private RectTransform OpenPopup(string title)
        {
            RectTransform column = DemoUI.Column(_canvas, 600f, DemoUI.PanelColor, "Popup");
            DemoUI.Label(column, title, 40);
            _popup = column;
            return column;
        }

        private void BuildHud()
        {
            _canvas = DemoUI.CreateCanvas("HUD", _hudRoot, 0);
            RectTransform panel = DemoUI.Column(_canvas, 560f, DemoUI.PanelColor, "Status");
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(32f, -32f);
            _hud = DemoUI.Label(panel, string.Empty, 28, TextAnchor.UpperLeft);
            _rollButton = DemoUI.Button(panel, $"Roll ({_rollKey})", Roll);
            DemoUI.Label(panel, "<size=20>Esc: pause · F1: scope tree</size>", 20, TextAnchor.UpperLeft);
        }

        private string DescribeState()
        {
            _builder.Clear();
            _builder.Append("<b>Round ").Append(Mathf.Min(_session.Round, _session.RoundCount)).Append('/')
                .Append(_session.RoundCount).Append("</b>\n");
            for (int i = 0; i < _session.Players.Count; i++)
            {
                PlayerState player = _session.Players[i];
                _builder.Append(i == _session.CurrentPlayerIndex && !_session.IsOver ? "> " : "   ");
                _builder.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(player.Color)).Append(">")
                    .Append(player.Name).Append("</color>  ").Append(player.Coins).Append(" coins\n");
            }

            _builder.Append("<size=22>").Append(_session.LastEvent).Append("</size>");
            return _builder.ToString();
        }

        private Vector3 TokenPosition(int tile, int player)
        {
            Vector3 center = tile < _tiles.Count ? _tiles[tile].transform.position : Vector3.zero;
            float offset = (player - (_session.Players.Count - 1) * 0.5f) * 0.45f;
            return center + new Vector3(offset, 0.1f, 0f);
        }

        // looks at the board center, drifting towards the current player's token
        private Vector3 CameraTarget()
        {
            Vector3 focus = Vector3.zero;
            if (!_session.IsOver && _session.CurrentPlayerIndex < _tokens.Count)
            {
                focus = Vector3.Lerp(Vector3.zero, _tokens[_session.CurrentPlayerIndex].transform.position, 0.35f);
            }

            return focus + _cameraOffset;
        }
    }
}
