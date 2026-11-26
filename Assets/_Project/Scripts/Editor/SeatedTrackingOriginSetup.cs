using PermisVR.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PermisVR.Editor
{
    /// Adds SeatedTrackingOrigin to the XR rig under Car/Cockpit, so the seated tracking
    /// origin fix applies without hand-wiring it in the Inspector.
    public static class SeatedTrackingOriginSetup
    {
        [MenuItem("PermisVR/Scene/Force Seated Tracking Origin")]
        public static void AddSeatedTrackingOrigin()
        {
            var car = GameObject.Find("Car");
            if (car == null)
            {
                Debug.LogError("SeatedTrackingOriginSetup: no 'Car' GameObject in the open scene.");
                return;
            }

            var cockpit = car.transform.Find("Cockpit");
            if (cockpit == null)
            {
                Debug.LogError("SeatedTrackingOriginSetup: no 'Cockpit' under Car.");
                return;
            }

            var cameraOffset = FindDeepChild(cockpit, "Camera Offset");
            if (cameraOffset == null)
            {
                Debug.LogError("SeatedTrackingOriginSetup: no 'Camera Offset' found under Cockpit. " +
                                "Run the XR rig setup first.");
                return;
            }

            var rigRoot = cameraOffset.parent != null ? cameraOffset.parent : cameraOffset;
            if (rigRoot.GetComponent<SeatedTrackingOrigin>() == null)
            {
                rigRoot.gameObject.AddComponent<SeatedTrackingOrigin>();
            }

            EditorSceneManager.MarkSceneDirty(car.scene);
            EditorSceneManager.SaveScene(car.scene);
            Debug.Log("SeatedTrackingOriginSetup: SeatedTrackingOrigin added to " + rigRoot.name +
                      ", scene saved.");
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    return child;
                }

                var found = FindDeepChild(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
