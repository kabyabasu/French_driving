using UnityEditor;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace PermisVR.Editor
{
    /// Enables the Android-build interaction profile needed for Driving.inputactions'
    /// <XRController>{LeftHand|RightHand}/trigger,grip,devicePosition,deviceRotation bindings
    /// to actually bind to Quest Touch controllers on-device. Done via script, not by
    /// hand-editing the OpenXR Package Settings asset.
    public static class OpenXRProfileSetup
    {
        [MenuItem("PermisVR/Build/Enable Oculus Touch Controller Profile (Android)")]
        public static void EnableOculusTouchProfileForAndroid()
        {
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings == null)
            {
                Debug.LogError("OpenXRProfileSetup: no OpenXR settings found for Android. " +
                                "Is the OpenXR package/provider installed for the Android platform?");
                return;
            }

            var feature = settings.GetFeature<OculusTouchControllerProfile>();
            if (feature == null)
            {
                Debug.LogError("OpenXRProfileSetup: OculusTouchControllerProfile feature not found " +
                                "in the Android OpenXR settings.");
                return;
            }

            if (feature.enabled)
            {
                Debug.Log("OpenXRProfileSetup: Oculus Touch Controller Profile is already enabled for Android.");
                return;
            }

            feature.enabled = true;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("OpenXRProfileSetup: enabled Oculus Touch Controller Profile for Android.");
        }
    }
}
