using UnityEngine;

namespace Kott.Scopes
{
    [DefaultExecutionOrder(-9001)]
    public class SceneRoot : MonoBehaviour
    {
        private void Awake()
        {
            ScopeHandler.Instance.SceneAwake(this);
        }

        private void Start()
        {
            ScopeHandler.Instance.SceneStart(this);
        }
    }
}
