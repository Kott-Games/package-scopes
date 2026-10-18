namespace Kott.Scopes.Demo
{
    /// <summary>Top-level flow, owned by the root scope: swaps between the Menu and Game scopes.</summary>
    public interface IAppFlow : IManagerContract
    {
        /// <summary>Whether a swap is in progress; new requests are ignored until it is done.</summary>
        bool IsBusy { get; }

        /// <summary>Unloads Menu and loads Game.</summary>
        void StartGame();

        /// <summary>Unloads Game (and everything below it) and loads Menu.</summary>
        void ShowMenu();

        /// <summary>Quits the application (leaves play mode in the editor).</summary>
        void Quit();
    }
}
