using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

namespace Kott.Scopes
{
    public class CoroutineHandler : MonoBehaviour
    {
        public static CoroutineHandler Instance { get; private set; }

        private ObjectPool<CoroutineRunner> _pool;

        private void Awake()
        {
            Instance = this;
            _pool = new(PoolCreate, PoolGet, PoolRelease, PoolDestroy);
        }

        private CoroutineRunner PoolCreate()
        {
            CoroutineRunner runner = gameObject.AddComponent<CoroutineRunner>();
            return runner;
        }

        private void PoolGet(CoroutineRunner runner)
        {
            runner.enabled = true;
        }

        private void PoolRelease(CoroutineRunner runner)
        {
            runner.enabled = false;
        }

        private void PoolDestroy(CoroutineRunner runner)
        {
            UnityEngine.Object.Destroy(runner);
        }

        public void Run(IEnumerator routine)
        {
            CoroutineRunner runner = _pool.Get();
            runner.Run(_pool, routine);
        }
    }
}
