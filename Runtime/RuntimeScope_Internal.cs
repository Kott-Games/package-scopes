using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Kott.Scopes
{
    public sealed partial class RuntimeScope
    {
        internal Scope Scope { get; }
        internal List<RuntimeScope> ChildrenInternal { get; } = new();

        internal GameObject ManagerContainer { get; }
        internal IReadOnlyList<Manager> Managers { get; }
        internal HashSet<Type> SingleManagerTypes { get; } = new();

        internal SceneRoot SceneRoot { get; set; }
        internal SceneInstance SceneInstance { get; set; }

        private readonly Dictionary<string, SceneLink> _sceneLinks = new();

        internal RuntimeScope(Scope scope, RuntimeScope parent, GameObject managerContainer)
        {
            Scope = scope;
            Parent = parent;
            Children = ChildrenInternal;
            ManagerContainer = managerContainer;

            List<Manager> managers = new();
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
    }
}