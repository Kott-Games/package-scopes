using UnityEngine;

namespace Kott.Scopes.Tests
{
    internal interface IProbeService : IManagerContract
    { }

    /// <summary>Records whether its injected field was already set in <c>Awake</c>.</summary>
    internal sealed class AwakeProbe : MonoBehaviour
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
