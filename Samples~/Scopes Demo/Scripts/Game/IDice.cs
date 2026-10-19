namespace Kott.Scopes.Demo
{
    /// <summary>A six-sided die.</summary>
    public interface IDice : IManagerContract
    {
        /// <summary>Returns a number from 1 to 6.</summary>
        int Roll();
    }
}
