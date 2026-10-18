using System;
using UnityEngine;

namespace Kott.Scopes
{
    /// <summary>Kind of a <see cref="ScopeOperation" />.</summary>
    public enum ScopeOperationType
    {
        /// <summary>Loading a scope.</summary>
        Load,

        /// <summary>Unloading a scope and its children.</summary>
        Unload,
    }

    /// <summary>
    ///     A queued or running scope load/unload. Yield it from a coroutine to wait for it, or subscribe to
    ///     <see cref="Completed" />. Operations run one after another in the order they were requested.
    /// </summary>
    /// <example>
    ///     <code>
    /// ScopeOperation load = ScopeHandler.Instance.LoadScope(levelDefinition, parent: Scope);
    /// yield return load;
    /// if (!load.Succeeded)
    ///     ShowError(load.Exception.Message);
    /// </code>
    /// </example>
    public sealed class ScopeOperation : CustomYieldInstruction
    {
        private readonly ILogger _logger;
        private Action<ScopeOperation> _completed;

        internal ScopeOperation(ScopeOperationType type, ScopeDefinition definition, Scope scope, ILogger logger)
        {
            _logger = logger;
            Type = type;
            Definition = definition;
            Scope = scope;
        }

        /// <summary>Load or unload.</summary>
        public ScopeOperationType Type { get; }

        /// <summary>The definition being loaded or unloaded.</summary>
        public ScopeDefinition Definition { get; }

        /// <summary>
        ///     The loaded scope (load: set once loading starts; state <see cref="ScopeState.Failed" /> on failure)
        ///     or the scope being unloaded.
        /// </summary>
        public Scope Scope { get; internal set; }

        /// <summary>Whether the operation finished, successfully or not.</summary>
        public bool IsDone { get; private set; }

        /// <summary>Whether the operation finished without error.</summary>
        public bool Succeeded => IsDone && Exception == null;

        /// <summary>
        ///     Why the operation failed, or <c>null</c>. An unload always completes; it reports the exceptions
        ///     thrown while uninitializing (an <see cref="AggregateException" /> if there were several).
        /// </summary>
        public Exception Exception { get; private set; }

        /// <inheritdoc />
        public override bool keepWaiting => !IsDone;

        /// <summary>Invoked when the operation finishes. Subscribing to a finished operation invokes the handler immediately.</summary>
        public event Action<ScopeOperation> Completed
        {
            add
            {
                if (IsDone)
                {
                    value?.Invoke(this);
                }
                else
                {
                    _completed += value;
                }
            }
            remove => _completed -= value;
        }

        internal void Complete(Exception exception)
        {
            if (IsDone)
            {
                return;
            }

            Exception = exception;
            IsDone = true;

            Action<ScopeOperation> completed = _completed;
            _completed = null;
            if (completed == null)
            {
                return;
            }

            foreach (Delegate handler in completed.GetInvocationList())
            {
                try
                {
                    ((Action<ScopeOperation>)handler).Invoke(this);
                }
                catch (Exception handlerException)
                {
                    _logger.LogException(handlerException);
                }
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            string state = !IsDone ? "pending" : Succeeded ? "succeeded" : "failed";
            return $"{Type} scope '{(Definition != null ? Definition.name : "?")}' ({state})";
        }
    }
}
