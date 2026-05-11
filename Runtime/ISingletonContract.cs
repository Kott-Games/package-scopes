namespace Kott.Scopes
{
    public interface ISingletonContract<T> : ISingleton
        where T : ISingleton
    { }
}