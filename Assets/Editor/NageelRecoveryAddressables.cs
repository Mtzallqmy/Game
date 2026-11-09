#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Build;
using UnityEngine;

namespace Nageel.EditorTools
{
    /// <summary>
    /// Creates a genuine Unity Addressables catalog for generated local recovery
    /// assets. Remote DLC requires separate group profile, upload and checksums.
    /// Do not publish an asset_manifests row until remote objects exist.
    /// </summary>
    public static class NageelRecoveryAddressables
    {
        [MenuItem("NAGEEL/Recovery/3 - Build Real Addressables Catalog")]
        public static void Build()
        {
            const string prefab = "Assets/Prefabs/NageelRecoverySUV.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefab) == null)
                throw new InvalidOperationException(
                    "First run NAGEEL > Recovery > Generate Playable 3D Scenes.");

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                settings = AddressableAssetSettings.Create(
                    "Assets/AddressableAssetsData", "AddressableAssetSettings", true, true);
                AddressableAssetSettingsDefaultObject.Settings = settings;
            }
            var group = settings.DefaultGroup;
            if (group == null)
                throw new InvalidOperationException("Addressables default group is missing.");

            string guid = AssetDatabase.AssetPathToGUID(prefab);
            if (String.IsNullOrEmpty(guid))
                throw new InvalidOperationException("Unity AssetDatabase has no valid prefab GUID.");

            var entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = "nageel/vehicles/recovery_suv";
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (result == null || !String.IsNullOrEmpty(result.Error))
                throw new InvalidOperationException("Addressables build failed: " + result?.Error);

            Debug.Log("NAGEEL: Real LOCAL Addressables content built. " +
                "Configure remote group + publish verified bundles before enabling first-boot DLC.");
        }
    }
}
#endif
