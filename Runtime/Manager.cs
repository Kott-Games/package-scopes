using System;
using System.Collections;
using Kott.Utilities.Progress;

namespace Kott.Scopes
{
    /// <summary>
    ///     Base class for the managers of a <see cref="Scope" />. Managers are plain C# classes stored in the
    ///     Scope asset; each load works on its own copy, so runtime state never leaks into the asset.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Subclasses must be <c>[Serializable]</c>. Serialized fields (including references to prefabs and
    ///         other assets) are authored in the Scope inspector. Scene objects are reached through
    ///         <see cref="RuntimeScope.GetSceneComponent{T}" /> (<see cref="SceneLink" />).
    ///     </para>
    ///     <para>
    ///         Lifecycle while loading: <see cref="Initialize" /> (in list order, before the scene loads) →
    ///         <see cref="SceneAwake" /> → <see cref="SceneStart" /> → every frame <see cref="SceneUpdate" /> /
    ///         <see cref="SceneFixedUpdate" />. While unloading: <see cref="SceneExit" /> → scene unload →
    ///         <see cref="Uninitialize" /> (in reverse list order).
    ///     </para>
    ///     <para>
    ///         Implement <see cref="ISingleton" /> or <see cref="ISingletonContract{T}" /> to make the manager
    ///         resolvable through <see cref="Singleton{T}" />. Singletons are registered before the first
    ///         <see cref="Initialize" /> of the scope and removed after its last <see cref="Uninitialize" />.
    ///     </para>
    ///     <para>
    ///         Renaming a manager class breaks the stored type. Keep it loadable with
    ///         <c>[UnityEngine.Scripting.APIUpdating.MovedFrom(false, sourceClassName: "OldName")]</c>.
    ///     </para>
    /// </remarks>
    [Serializable]
    public abstract class Manager
    {
        /// <summary>The loaded scope this manager belongs to. Set before <see cref="Initialize" />.</summary>
        public RuntimeScope RuntimeScope { get; internal set; }

        /// <summary>
        ///     Initialization before the scope's scene is loaded. May span multiple frames.
        ///     Throwing fails the scope load; already initialized managers are uninitialized again.
        /// </summary>
        /// <remarks>Do not wait for other scope operations here: they are queued behind this one.</remarks>
        /// <param name="progress">Progress of this manager's initialization (0–1). May be <c>null</c>.</param>
        public virtual IEnumerator Initialize(IProgress<ProgressReport> progress)
        {
            yield break;
        }

        /// <summary>Uninitialization after the scope's scene is unloaded. May span multiple frames.</summary>
        /// <param name="progress">Progress of this manager's uninitialization (0–1). May be <c>null</c>.</param>
        public virtual IEnumerator Uninitialize(IProgress<ProgressReport> progress)
        {
            yield break;
        }

        /// <summary>Called when the scope's scene activates, before the <c>Awake</c> of other scene objects.</summary>
        public virtual void SceneAwake()
        { }

        /// <summary>Called before the <c>Start</c> of other scene objects.</summary>
        public virtual void SceneStart()
        { }

        /// <summary>Called every frame while the scope is loaded, also for scopes without a scene.</summary>
        public virtual void SceneUpdate()
        { }

        /// <summary>Called every fixed update while the scope is loaded, also for scopes without a scene.</summary>
        public virtual void SceneFixedUpdate()
        { }

        /// <summary>Called before the scope's scene unloads (and on application quit).</summary>
        public virtual void SceneExit()
        { }
    }
}
