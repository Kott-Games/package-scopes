using System.Collections;
using Kott.Utilities.Progress;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Kott.Scopes
{
    public sealed class Bootstrap : MonoBehaviour
    {
        [SerializeField] private AssetReference _scopeRef;

        private IEnumerator Start()
        {
            SubProgress subProgress = new(DebugProgressReporter.Instance);

            subProgress.Start = new ProgressReport(0f, "Loading root scope asset.");
            subProgress.Scale = 0.5f;
            
            AsyncOperationHandle<Scope> loadScopeHandle = _scopeRef.LoadAssetAsync<Scope>();
            yield return new WaitForAsyncOperationHandleWithProgress(loadScopeHandle, subProgress);

            subProgress.Start = new ProgressReport(0.5f, "Loading root scope.");
            subProgress.Scale = 0.5f;
            
            Scope scope = loadScopeHandle.Result;
            yield return ScopeHandler.Instance.LoadScope(scope, subProgress);
        }
    }
}