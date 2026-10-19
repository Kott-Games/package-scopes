using System;

namespace Kott.Scopes
{
    /// <summary>
    ///     Marks a manager field to receive an object of the scope's own scene: the <see cref="SceneLink" /> with
    ///     <see cref="Key" />. Field type <c>GameObject</c> gets the linked object, a component or interface type
    ///     gets that component of it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Filled when the scene activates, before <see cref="Manager.OnSceneAwake" />; <c>null</c> during
    ///         <see cref="Manager.Initialize" /> and again after <see cref="Manager.OnSceneExit" />. A missing link or
    ///         component fails the scope load unless the field is <see cref="Optional" />.
    ///     </para>
    ///     <para>
    ///         Only supported on managers; scene components reference scene objects with ordinary serialized fields.
    ///         Scope validation checks that every key exists in the scene with the right component.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    /// [InjectScene("actor_container")] private Transform _actorContainer;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class InjectSceneAttribute : Attribute
    {
        /// <param name="key">The key of the <see cref="SceneLink" /> in the scope's scene.</param>
        public InjectSceneAttribute(string key)
        {
            Key = key;
        }

        /// <summary>The key of the <see cref="SceneLink" />.</summary>
        public string Key { get; }

        /// <summary>Leave the field <c>null</c> instead of failing if the link or component is missing.</summary>
        public bool Optional { get; set; }
    }
}
