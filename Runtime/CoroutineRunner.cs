using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

namespace Kott.Scopes
{
    internal class CoroutineRunner : MonoBehaviour
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
            yield return _routine;
            _pool.Release(this);
        }
    }
}
