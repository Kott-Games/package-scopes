using UnityEngine;

namespace Kott.Scopes
{
    /// <summary>Lives on <see cref="Scope.Root" />: drives update callbacks and hosts scope coroutines.</summary>
    [AddComponentMenu("")]
    internal sealed class ScopeRootBehaviour : MonoBehaviour
    {
        internal Scope Scope;

        private void Update()
        {
            if (Scope is { State: ScopeState.Loaded })
            {
                Scope.InvokeUpdate();
            }
        }

        private void FixedUpdate()
        {
            if (Scope is { State: ScopeState.Loaded })
            {
                Scope.InvokeFixedUpdate();
            }
        }
    }
}
