using System;

namespace Kott.Scopes
{
    /// <summary>
    ///     Marks a field to receive a manager contract (an interface extending <see cref="IManagerContract" />),
    ///     resolved along the scope's parent chain.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Managers are injected before any manager of their scope initializes; scene components when the scope's
    ///         scene activates (before their <c>Awake</c>); other objects through <see cref="Scope.Inject" /> or
    ///         <see cref="Scope.Instantiate(UnityEngine.GameObject, UnityEngine.Transform)" />.
    ///     </para>
    ///     <para>
    ///         A missing contract fails the scope load (or throws for <see cref="Scope.Inject" />) unless the field
    ///         is <see cref="Optional" />. When the scope unloads, the fields of its managers are set to <c>null</c>.
    ///     </para>
    ///     <para>Tests fill these fields with <c>TestInjector</c> (Kott.Scopes.Testing).</para>
    /// </remarks>
    /// <example>
    ///     <code>
    /// [Inject] private IConfigManager _configManager;
    /// [Inject(Optional = true)] private IAnalyticsManager _analytics;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class InjectAttribute : Attribute
    {
        /// <summary>Leave the field <c>null</c> instead of failing if no scope in the chain provides the contract.</summary>
        public bool Optional { get; set; }
    }
}
