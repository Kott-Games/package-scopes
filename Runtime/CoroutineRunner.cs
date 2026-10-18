using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

namespace Kott.Scopes
{
    [AddComponentMenu("")]
    internal sealed class CoroutineRunner : MonoBehaviour
    {
        private IObjectPool<CoroutineRunner> _pool;
        private IEnumerator _routine;
        private TaggedLogger _logger;

        public void Run(IObjectPool<CoroutineRunner> pool, IEnumerator routine, TaggedLogger logger)
        {
            _pool = pool;
            _logger = logger;
            _routine = routine;
            StartCoroutine(RunRoutine());
        }

        private IEnumerator RunRoutine()
        {
            // SafeRoutine: a throwing routine must not keep the runner out of the pool forever
            yield return SafeRoutine.Run(_routine, _logger.LogException);
            _routine = null;
            _pool.Release(this);
        }
    }
}
