using UnityEngine;

namespace Kott.Scopes.Tests
{
    /// <summary>Contract injected into <see cref="AwakeProbe" />.</summary>
    public interface IProbeService : IManagerContract
    { }

    /// <summary>
    ///     Component with an injected contract; records whether it was set in <c>Awake</c>. Lives in the runtime
    ///     test assembly because Unity cannot attach MonoBehaviours from editor assemblies, and is shared with the
    ///     EditMode tests.
    /// </summary>
    public sealed class AwakeProbe : MonoBehaviour
    {
        [Inject]
        public IProbeService Probe;

        public bool InjectedInAwake { get; private set; }
        public bool Awoken { get; private set; }

        private void Awake()
        {
            Awoken = true;
            InjectedInAwake = Probe != null;
        }
    }
}
