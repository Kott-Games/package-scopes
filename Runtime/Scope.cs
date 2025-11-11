using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Kott.Scopes
{
    [CreateAssetMenu(fileName = "NewScope", menuName = "Scope", order = 9001)]
    public class Scope : ScriptableObject
    {
        public AssetReference SceneRef;
        public Manager[] Managers;
    }
}
