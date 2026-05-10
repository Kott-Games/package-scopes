using System;
using System.Collections;
using Kott.Utilities.Progress;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Kott.Scopes
{
    public class Bootstrap : MonoBehaviour
    {
        [SerializeField] private AssetReference _scopeRef;

        private IEnumerator Start()
        {
            AsyncOperationHandle<Scope> loadScopeHandle = _scopeRef.LoadAssetAsync<Scope>();
            yield return new WaitForAsyncOperationHandleWithProgress(loadScopeHandle, DebugProgressReporter.Instance);

            Scope scope = loadScopeHandle.Result;
            yield return ScopeHandler.Instance.LoadScope(scope, null, DebugProgressReporter.Instance);
        }
    }
}