using System.Collections.Generic;

namespace Kott.Scopes.Demo
{
    /// <summary>
    ///     Switches the Game scope's heavy child between Board and the mini games (only one is loaded at a time) and
    ///     opens the Pause scope.
    /// </summary>
    public interface IGameFlow : IManagerContract
    {
        /// <summary>Whether a switch is in progress; new requests are ignored until it is done.</summary>
        bool IsBusy { get; }

        /// <summary>Whether the Pause scope is open (or opening).</summary>
        bool IsPaused { get; }

        /// <summary>The mini game definitions to choose from.</summary>
        IReadOnlyList<ScopeDefinition> MiniGames { get; }

        /// <summary>The running mini game, resolved through its scope; <c>null</c> on the board.</summary>
        IMiniGame CurrentMiniGame { get; }

        /// <summary>Unloads Board and loads <paramref name="miniGame" />. If it fails, returns to Board.</summary>
        void PlayMiniGame(ScopeDefinition miniGame);

        /// <summary>Unloads the mini game and loads Board again.</summary>
        void ReturnToBoard();

        /// <summary>Opens or closes the Pause scope.</summary>
        void TogglePause();

        /// <summary>Ends the game and goes back to the menu.</summary>
        void ExitToMenu();
    }
}
