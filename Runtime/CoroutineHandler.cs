using System.Collections;
using UnityEngine;

namespace Kott.Scopes
{
    /// <summary>
    ///     Runs coroutines independent of any scope (they survive scope unloads). For coroutines that belong
    ///     to a scope use <see cref="Scope.StartCoroutine" />.
    /// </summary>
    /// <remarks>Created on first access like <see cref="ScopeHandler" />; a hand-placed instance is reused.</remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("Scopes/Coroutine Handler")]
    public sealed class CoroutineHandler : MonoBehaviour
    {
        private static CoroutineHandler _instance;

        /// <summary>The coroutine handler; created on first access.</summary>
        /// <exception cref="System.InvalidOperationException">Accessed outside play mode.</exception>
        public static CoroutineHandler Instance
        {
            get
            {
                if (_instance == null)
                {
                    ObjectUtility.FindOrCreateHost<CoroutineHandler>("Coroutines").Initialize();
                }

                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                ScopeLogging.Current.Log(LogType.Warning, (object)
                    $"A CoroutineHandler already exists on '{_instance.name}'; removing the one on '{name}'.", this);
                Destroy(this);
                return;
            }

            Initialize();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        ///     Runs <paramref name="routine" />; stop it with <c>StopCoroutine</c>. An exception ends only this routine
        ///     and is logged through the scope system's logger (see <see cref="ScopeHandler.LoggerFactory" />).
        /// </summary>
        public Coroutine Run(IEnumerator routine)
        {
            return StartCoroutine(SafeRoutine.Run(routine, ScopeLogging.Current.LogException));
        }

        private void Initialize()
        {
            if (_instance == this)
            {
                return;
            }

            _instance = this;
            DontDestroyOnLoad(transform.root.gameObject);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }
    }
}
