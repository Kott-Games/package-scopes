using System.Collections.Generic;
using UnityEngine;

namespace Kott.Scopes
{
    public sealed partial class RuntimeScope
    {
        public RuntimeScope Parent { get; }
        public IReadOnlyList<RuntimeScope> Children { get; }

        public GameObject GetSceneGameObject(string key)
        {
            return _sceneLinks[key].gameObject;
        }

        public T GetSceneComponent<T>(string key)
        {
            return _sceneLinks[key].GetComponent<T>();
        }
    }
}