using UnityEngine;

namespace Kott.Scopes
{
    [DefaultExecutionOrder(-9001)]
    public sealed class SceneRoot : MonoBehaviour
    {
        public RuntimeScope RuntimeScope { get; set; }

        private void Awake()
        {
            ScopeHandler.Instance.SetupSceneRoot(this);

            foreach (Manager manager in RuntimeScope.Managers)
            {
                manager.SceneAwake();
            }
        }

        private void Start()
        {
            foreach (Manager manager in RuntimeScope.Managers)
            {
                manager.SceneStart();
            }
        }

        private void Update()
        {
            foreach (Manager manager in RuntimeScope.Managers)
            {
                manager.SceneUpdate();
            }
        }
    }
}