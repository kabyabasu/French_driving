using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PermisVR.Editor
{
    /// One-off fix for scenes built before M1SceneBuilder placed the cockpit on the driver's
    /// (left, for France LHD) side: repositions the existing Cockpit in place, without
    /// rebuilding the scene and losing the XR rig / hand trackers / visuals added since.
    public static class CockpitSeatFix
    {
        [MenuItem("PermisVR/Scene/Fix Cockpit To Left Seat (France LHD)")]
        public static void FixCockpitPosition()
        {
            var car = GameObject.Find("Car");
            if (car == null)
            {
                Debug.LogError("CockpitSeatFix: no 'Car' GameObject in the open scene.");
                return;
            }

            var cockpit = car.transform.Find("Cockpit");
            if (cockpit == null)
            {
                Debug.LogError("CockpitSeatFix: no 'Cockpit' under Car.");
                return;
            }

            cockpit.localPosition = new Vector3(-0.45f, cockpit.localPosition.y, cockpit.localPosition.z);

            EditorSceneManager.MarkSceneDirty(car.scene);
            EditorSceneManager.SaveScene(car.scene);
            Debug.Log("CockpitSeatFix: Cockpit moved to the left seat, scene saved.");
        }
    }
}
