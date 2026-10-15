namespace Kott.Scopes
{
    /// <summary>Lifecycle state of a <see cref="RuntimeScope" />.</summary>
    public enum ScopeState
    {
        /// <summary>Managers are initializing or the scene is loading.</summary>
        Loading,

        /// <summary>Fully loaded; managers receive update callbacks.</summary>
        Loaded,

        /// <summary>The scene is unloading or managers are uninitializing.</summary>
        Unloading,

        /// <summary>Unloaded; the instance is no longer part of the scope tree.</summary>
        Unloaded,

        /// <summary>Loading failed and the partially loaded scope was cleaned up.</summary>
        Failed,
    }
}
