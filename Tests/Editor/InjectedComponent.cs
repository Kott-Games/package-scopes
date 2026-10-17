using UnityEngine;

namespace Kott.Scopes.Tests
{
    /// <summary>Component with an injected contract, for scene and prefab injection tests.</summary>
    internal sealed class InjectedComponent : MonoBehaviour
    {
        [Inject]
        public IFooService Foo;
    }
}
