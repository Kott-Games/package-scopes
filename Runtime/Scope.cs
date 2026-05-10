using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Kott.Scopes
{
    [CreateAssetMenu(fileName = "NewScope", menuName = "Scope", order = 9001)]
    public class Scope : ScriptableObject
    {
        [SerializeField] private AssetReference _sceneRef;
        [SerializeReference] private GameObject _managerContainer;

        public AssetReference SceneRef => _sceneRef;
        public GameObject ManagerContainer => _managerContainer;
    }
}
