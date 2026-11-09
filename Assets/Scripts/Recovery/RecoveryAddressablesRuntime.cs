using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Nageel.Recovery
{
    /// <summary>
    /// Loads the generated SUV from a real Addressables catalog at runtime.
    /// The scene's primary driveable car remains embedded as an offline fallback.
    /// </summary>
    public sealed class RecoveryAddressablesRuntime : MonoBehaviour
    {
        public bool catalogAssetLoaded;
        public string status = "Catalog not yet tested";
        private AsyncOperationHandle<GameObject> handle;
        private bool hasHandle;

        private IEnumerator Start()
        {
            status = "Loading local recovery SUV from Addressables...";
            handle = Addressables.LoadAssetAsync<GameObject>("nageel/vehicles/recovery_suv");
            hasHandle = true;
            yield return handle;
            catalogAssetLoaded = handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null;
            status = catalogAssetLoaded ? "Addressables prefab loaded and verified" :
                "Addressables catalog missing or build failed. Embedded recovery car remains playable.";
            if (!catalogAssetLoaded) Debug.LogWarning("[Nageel Addressables] " + status);
        }

        private void OnDestroy()
        {
            if (hasHandle && handle.IsValid()) Addressables.Release(handle);
        }
    }
}
