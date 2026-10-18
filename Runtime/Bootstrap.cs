using Kott.Utilities.Progress;
using UnityEngine;

namespace Kott.Scopes
{
    /// <summary>
    ///     Loads the root scope on <c>Start</c>. Put it in the first scene of the build and assign the root scope's definition.
    /// </summary>
    [AddComponentMenu("Scopes/Bootstrap")]
    public sealed class Bootstrap : MonoBehaviour
    {
        [SerializeField]
        private ScopeDefinition _rootScope;

        [Tooltip("Log loading progress to the console (editor and development builds).")]
        [SerializeField]
        private bool _logProgress;

        /// <summary>The definition loaded on start. Changing it after <c>Start</c> has no effect.</summary>
        public ScopeDefinition RootScope
        {
            get => _rootScope;
            set => _rootScope = value;
        }

        /// <summary>The load operation, once started.</summary>
        public ScopeOperation Operation { get; private set; }

        private void Start()
        {
            if (_rootScope == null)
            {
                ScopeHandler.Instance.Logger.Log(LogType.Error, (object)"Bootstrap has no root scope assigned.", this);
                return;
            }

            Operation = ScopeHandler.Instance.LoadScope(_rootScope,
                progress: _logProgress ? DebugProgressReporter.Instance : null);
        }
    }
}
