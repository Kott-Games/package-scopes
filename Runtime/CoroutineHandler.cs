using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

namespace Kott.Scopes
{
    /// <summary>
    ///     Handles running of coroutines for other objects.
    /// </summary>
    public sealed class CoroutineHandler : MonoBehaviour
    {
        public static CoroutineHandler Instance { get; private set; }

        private ObjectPool<CoroutineRunner> _pool;

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _pool = new ObjectPool<CoroutineRunner>(PoolCreate, PoolGet, PoolRelease, PoolDestroy);
        }

        private CoroutineRunner PoolCreate()
        {
            CoroutineRunner runner = gameObject.AddComponent<CoroutineRunner>();
            return runner;
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

        public void Run(IEnumerator routine)
        {
            CoroutineRunner runner = _pool.Get();
            runner.Run(_pool, routine);
        }
    }
}