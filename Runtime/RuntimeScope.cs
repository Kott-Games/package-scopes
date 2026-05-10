using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Kott.Scopes
{
    public sealed class RuntimeScope
    {
        public RuntimeScope Parent { get; }
        public IReadOnlyList<RuntimeScope> Children => ChildrenInternal;
        
        internal Scope Scope { get; }
        internal List<RuntimeScope> ChildrenInternal { get; } = new();
        
        internal GameObject ManagerContainer { get; }
        internal IReadOnlyList<IManager> Managers { get; }
        internal HashSet<Type> SingleManagerTypes { get; } = new();
        
        internal SceneInstance SceneInstance { get; set; }
        
        private readonly Dictionary<string, SceneLink> _sceneLinks = new();

        internal RuntimeScope(Scope scope, RuntimeScope parent, GameObject managerContainer)
        {
            Scope = scope;
            Parent = parent;
            ManagerContainer = managerContainer;

            List<IManager> managers = new();
            managerContainer.GetComponentsInChildren(managers);
            Managers = managers;
        }
        
        internal void CollectSceneLinks()
        {
            List<SceneLink> sceneLinks = new();
            GameObject[] sceneObjects = SceneInstance.Scene.GetRootGameObjects();
            foreach (GameObject sceneObject in sceneObjects)
            {
                sceneLinks.Clear();
                sceneObject.GetComponentsInChildren(true, sceneLinks);
                foreach (SceneLink sceneLink in sceneLinks)
                {
                    _sceneLinks.Add(sceneLink.Key, sceneLink);
                }
            }
        }
        
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