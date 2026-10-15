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

        public void Run(IObjectPool<CoroutineRunner> pool, IEnumerator routine)
        {
            _pool = pool;
            _routine = routine;
            StartCoroutine(RunRoutine());
        }

        private IEnumerator RunRoutine()
        {
            // SafeRoutine: a throwing routine must not keep the runner out of the pool forever
            yield return SafeRoutine.Run(_routine, Debug.LogException);
            _routine = null;
            _pool.Release(this);
        }
    }
}
