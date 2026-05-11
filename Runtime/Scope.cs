using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Kott.Scopes
{
    [CreateAssetMenu(fileName = "NewScope", menuName = "Scope", order = 9001)]
    public sealed class Scope : ScriptableObject
    {
        [SerializeField] private AssetReference _sceneRef;
        [SerializeField] private GameObject _managerContainer;

        public AssetReference SceneRef => _sceneRef;
        public GameObject ManagerContainer => _managerContainer;
    }
}
