using System;
using System.Collections;
using Kott.Utilities.Progress;

namespace Kott.Scopes
{
    /// <summary>
    ///     Base class for the managers of a <see cref="ScopeDefinition" />. Managers are plain C# classes stored in
    ///     the definition asset; each load works on its own copy, so runtime state never leaks into the asset.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Subclasses must be <c>[Serializable]</c>. Serialized fields (including references to prefabs and
    ///         other assets) are authored in the inspector.
    ///     </para>
    ///     <para>
    ///         Dependencies: implement manager contracts (interfaces extending <see cref="IManagerContract" />) to
    ///         provide them, and depend on contracts through <see cref="InjectAttribute" /> fields. Scene objects
    ///         come through <see cref="InjectSceneAttribute" /> fields (or <see cref="Scope.GetSceneComponent{T}" />).
    ///     </para>
    ///     <para>
    ///         Lifecycle while loading: contracts registered and <c>[Inject]</c> fields filled for all managers of the
    ///         scope → <see cref="Initialize" /> in dependency order (a manager initializes after the same-scope
    ///         managers it injects; list order otherwise) → scene loads, <c>[InjectScene]</c> fields filled →
    ///         <see cref="OnSceneAwake" /> → <see cref="OnSceneStart" /> → every frame <see cref="Update" /> /
    ///         <see cref="FixedUpdate" />. While unloading: <see cref="OnSceneExit" /> → scene unload →
    ///         <see cref="Uninitialize" /> in reverse initialization order → injected fields set to <c>null</c>.
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
        public Scope Scope { get; internal set; }

        /// <summary>
        ///     Initialization before the scope's scene is loaded. May span multiple frames. Injected managers are
        ///     already initialized. Throwing fails the scope load; already initialized managers are uninitialized again.
        /// </summary>
        /// <remarks>Do not wait for other scope operations here: they are queued behind this one.</remarks>
        /// <param name="progress">Progress of this manager's initialization (0–1). May be <c>null</c>.</param>
        public virtual IEnumerator Initialize(IProgress<ProgressReport> progress)
        {
            yield break;
        }

        /// <summary>
        ///     Uninitialization after the scope's scene is unloaded. May span multiple frames (e.g. to save state).
        ///     Injected managers are still available.
        /// </summary>
        /// <param name="progress">Progress of this manager's uninitialization (0–1). May be <c>null</c>.</param>
        public virtual IEnumerator Uninitialize(IProgress<ProgressReport> progress)
        {
            yield break;
        }

        /// <summary>
        ///     Called when the scope's scene activates, before the <c>Awake</c> of other scene objects.
        ///     <see cref="InjectSceneAttribute" /> fields are filled.
        /// </summary>
        public virtual void OnSceneAwake()
        { }

        /// <summary>Called before the <c>Start</c> of other scene objects.</summary>
        public virtual void OnSceneStart()
        { }

        /// <summary>Called every frame while the scope is loaded, also for scopes without a scene.</summary>
        public virtual void Update()
        { }

        /// <summary>Called every fixed update while the scope is loaded, also for scopes without a scene.</summary>
        public virtual void FixedUpdate()
        { }

        /// <summary>Called before the scope's scene unloads (and on application quit).</summary>
        public virtual void OnSceneExit()
        { }
    }
}
