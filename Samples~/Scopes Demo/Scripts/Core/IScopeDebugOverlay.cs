namespace Kott.Scopes.Demo
{
    /// <summary>Shows the live scope tree and a lifecycle log (toggle with F1), plus error toasts.</summary>
    public interface IScopeDebugOverlay : IManagerContract
    {
        /// <summary>Adds a line to the lifecycle log.</summary>
        void Log(string message);

        /// <summary>Logs <paramref name="message" /> and shows it as a toast for a few seconds.</summary>
        void ShowError(string message);
    }
}
