using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

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

        private ObjectPool<CoroutineRunner> _pool;
        private ILogger _logger;

        /// <summary>
        ///     Receives exceptions of failed routines. Defaults to <see cref="ScopeLogging.CreateDefaultLogger" /> with
        ///     category <see cref="ScopeLogging.Category" />; independent of <see cref="ScopeHandler.LoggerFactory" />.
        /// </summary>
        public ILogger Logger
        {
            get => _logger ??= ScopeLogging.CreateDefaultLogger(ScopeLogging.Category);
            set => _logger = value ?? throw new System.ArgumentNullException(nameof(value));
        }

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
                _instance.Logger.Log(LogType.Warning, (object)
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

        /// <summary>Runs <paramref name="routine" />. An exception is logged and ends only this routine.</summary>
        public void Run(IEnumerator routine)
        {
            CoroutineRunner runner = _pool.Get();
            runner.Run(_pool, routine, Logger);
        }

        private void Initialize()
        {
            if (_pool != null)
            {
                return;
            }

            _instance = this;
            DontDestroyOnLoad(transform.root.gameObject);
            _pool = new ObjectPool<CoroutineRunner>(PoolCreate, PoolGet, PoolRelease, PoolDestroy);
        }

        private CoroutineRunner PoolCreate()
        {
            return gameObject.AddComponent<CoroutineRunner>();
        }

        private static void PoolGet(CoroutineRunner runner)
        {
            runner.enabled = true;
        }

        private static void PoolRelease(CoroutineRunner runner)
        {
            runner.enabled = false;
        }

        private static void PoolDestroy(CoroutineRunner runner)
        {
            Destroy(runner);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }
    }
}
