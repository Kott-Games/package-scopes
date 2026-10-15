using System;

namespace Kott.Scopes
{
    /// <summary>
    ///     Serializable reference to the scene of a <see cref="Scope" />, stored in the Scope asset.
    ///     Built in: <see cref="BuildSceneReference" /> (scene list of the build profile) and, with the
    ///     Addressables package installed, <c>AddressableSceneReference</c>.
    /// </summary>
    /// <remarks>Custom implementations must be <c>[Serializable]</c>.</remarks>
    [Serializable]
    public abstract class SceneReference
    {
        /// <summary>Whether a scene is assigned. A scope with an unassigned reference loads without a scene.</summary>
        public abstract bool IsAssigned { get; }

        /// <summary>Human-readable scene name for progress and log messages.</summary>
        public abstract string DisplayName { get; }

        /// <summary>Creates the loader for one load of the scene.</summary>
        public abstract ISceneLoader CreateLoader();
    }
}
