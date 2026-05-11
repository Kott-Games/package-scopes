using UnityEngine;

namespace Kott.Scopes
{
    public sealed class SceneLink : MonoBehaviour
    {
        [SerializeField] private string _key;

        public string Key => _key;
    }
}