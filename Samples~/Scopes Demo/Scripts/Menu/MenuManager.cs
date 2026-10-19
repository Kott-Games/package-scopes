using System;
using UnityEngine;
using UnityEngine.UI;

namespace Kott.Scopes.Demo
{
    /// <summary>
    ///     The Menu scope (scene from the build profile's scene list). Builds its UI under the scene's <c>ui</c>
    ///     SceneLink and opens Stats as a child scope.
    /// </summary>
    [Serializable]
    public sealed class MenuManager : Manager
    {
        [SerializeField]
        private ScopeDefinition _stats;

        [Inject]
        private IAppFlow _app;

        [Inject]
        private ISaveManager _save;

        [InjectScene("ui")]
        private Transform _ui;

        private Text _summary;

        /// <inheritdoc />
        public override void OnSceneAwake()
        {
            RectTransform canvas = DemoUI.CreateCanvas("Menu", _ui, 0);
            RectTransform column = DemoUI.Column(canvas, 640f, DemoUI.PanelColor);
            DemoUI.Label(column, "<b>Scopes Demo</b>", 72).color = DemoUI.AccentColor;
            _summary = DemoUI.Label(column, string.Empty, 28);
            DemoUI.Button(column, "Start Game", _app.StartGame);
            DemoUI.Button(column, "Stats", ToggleStats);
            DemoUI.Button(column, "Quit", _app.Quit);
            DemoUI.Label(column, "<size=20>F1: scope tree · Esc: pause (in game)</size>", 20);
        }

        /// <inheritdoc />
        public override void Update()
        {
            PlayerProfile profile = _save.Profile;
            _summary.text = $"Games played: {profile.GamesPlayed}   Coins: {profile.TotalCoins}";
        }

        private void ToggleStats()
        {
            ScopeHandler handler = ScopeHandler.Instance;
            if (handler.TryGetScope(_stats, out Scope stats))
            {
                handler.UnloadScope(stats);
            }
            else
            {
                // a child of Menu: unloading Menu (Start Game) closes it too
                handler.LoadScope(_stats, Scope);
            }
        }
    }
}
