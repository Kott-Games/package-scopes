using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ToG.Scopes
{
    [CreateAssetMenu(fileName = "Scope", menuName = "Scope")]
    public class Scope : ScriptableObject
    {
        public AssetReference SceneRef;
        public Manager[] Managers;
    }
}
