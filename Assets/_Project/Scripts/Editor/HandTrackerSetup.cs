using PermisVR.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace PermisVR.Editor
{
    /// Adds Input System TrackedPoseDriver hand trackers under the XR rig's Camera Offset
    /// (since Unity's "Convert Main Camera to XR Rig" doesn't add hand anchors itself) and
    /// wires them into VirtualSteeringWheel. Operates on the currently open scene; does not
    /// rebuild it.
    public static class HandTrackerSetup
    {
        private const string DrivingActionsPath = "Assets/_Project/Scripts/Input/Driving.inputactions";

        [MenuItem("PermisVR/Scene/Add Hand Trackers To Cockpit")]
        public static void AddHandTrackers()
        {
            var car = GameObject.Find("Car");
            if (car == null)
            {
                Debug.LogError("HandTrackerSetup: no 'Car' GameObject in the open scene.");
                return;
            }

            var cockpit = car.transform.Find("Cockpit");
            if (cockpit == null)
            {
                Debug.LogError("HandTrackerSetup: no 'Cockpit' under Car.");
                return;
            }

            var cameraOffset = FindDeepChild(cockpit, "Camera Offset");
            if (cameraOffset == null)
            {
                Debug.LogError("HandTrackerSetup: no 'Camera Offset' found under Cockpit. " +
                                "Make sure the XR rig is parented under Cockpit first.");
                return;
            }

            var leftHand = GetOrCreateHandTracker(cameraOffset, "LeftHandTracked", "LeftHandPosition", "LeftHandRotation");
            var rightHand = GetOrCreateHandTracker(cameraOffset, "RightHandTracked", "RightHandPosition", "RightHandRotation");

            var wheel = cockpit.GetComponent<VirtualSteeringWheel>();
            if (wheel == null)
            {
                Debug.LogError("HandTrackerSetup: Cockpit has no VirtualSteeringWheel component.");
                return;
            }

            var so = new SerializedObject(wheel);
            so.FindProperty("_leftHand").objectReferenceValue = leftHand.transform;
            so.FindProperty("_rightHand").objectReferenceValue = rightHand.transform;
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(car.scene);
            Debug.Log("HandTrackerSetup: added LeftHandTracked/RightHandTracked under Camera Offset " +
                      "and wired them into VirtualSteeringWheel's Left Hand/Right Hand. You can now " +
                      "delete the old LeftHandAnchor/RightHandAnchor placeholders under Cockpit.");
        }

        private static GameObject GetOrCreateHandTracker(Transform parent, string objectName, string positionAction, string rotationAction)
        {
            var existing = parent.Find(objectName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            var driver = go.AddComponent<TrackedPoseDriver>();

            var so = new SerializedObject(driver);
            AssignActionReference(so, "m_PositionInput", FindActionReference(positionAction));
            AssignActionReference(so, "m_RotationInput", FindActionReference(rotationAction));
            so.ApplyModifiedProperties();

            return go;
        }

        private static void AssignActionReference(SerializedObject so, string inputPropertyName, InputActionReference reference)
        {
            var property = so.FindProperty(inputPropertyName);
            if (property == null)
            {
                Debug.LogError($"HandTrackerSetup: TrackedPoseDriver has no field '{inputPropertyName}' in " +
                                "this Input System version — assign it by hand in the Inspector instead.");
                return;
            }

            var useReference = property.FindPropertyRelative("m_UseReference");
            var referenceProp = property.FindPropertyRelative("m_Reference");
            if (useReference == null || referenceProp == null)
            {
                Debug.LogError($"HandTrackerSetup: couldn't find InputActionProperty's internal fields on " +
                                $"'{inputPropertyName}' — assign '{reference?.name}' by hand in the Inspector.");
                return;
            }

            useReference.boolValue = true;
            referenceProp.objectReferenceValue = reference;
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

        private static InputActionReference FindActionReference(string actionName)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(DrivingActionsPath))
            {
                if (asset is InputActionReference reference && reference.action?.name == actionName)
                {
                    return reference;
                }
            }

            return null;
        }
    }
}
