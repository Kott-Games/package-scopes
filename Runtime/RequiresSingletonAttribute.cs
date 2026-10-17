using System;

namespace Kott.Scopes
{
    /// <summary>
    ///     Declares that a manager needs the singleton <see cref="Type" /> while it is loaded, for dependencies
    ///     that are not visible as a <see cref="Singleton{T}" /> field (e.g. resolved through
    ///     <see cref="SingletonRegistry" /> directly). Scope validation checks that the scope or one of its declared
    ///     parents provides it.
    /// </summary>
    /// <example>
    ///     <code>
    /// [Serializable, RequiresSingleton(typeof(IScoreManager))]
    /// public class HudManager : Manager { … }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public sealed class RequiresSingletonAttribute : Attribute
    {
        /// <param name="type">A manager type implementing <see cref="ISingleton" />, or a singleton contract.</param>
        public RequiresSingletonAttribute(Type type)
        {
            Type = type;
        }

        /// <summary>The required singleton type.</summary>
        public Type Type { get; }
    }
}
