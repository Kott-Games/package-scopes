using UnityEngine;

namespace Kott.Scopes
{
    /// <summary>Lives on <see cref="RuntimeScope.Root" />: drives update callbacks and hosts scope coroutines.</summary>
    [AddComponentMenu("")]
    internal sealed class ScopeRootBehaviour : MonoBehaviour
    {
        internal RuntimeScope RuntimeScope;

        private void Update()
        {
            if (RuntimeScope is { State: ScopeState.Loaded })
            {
                RuntimeScope.InvokeSceneUpdate();
            }
        }

        private void FixedUpdate()
        {
            if (RuntimeScope is { State: ScopeState.Loaded })
            {
                RuntimeScope.InvokeSceneFixedUpdate();
            }
        }
    }
}
