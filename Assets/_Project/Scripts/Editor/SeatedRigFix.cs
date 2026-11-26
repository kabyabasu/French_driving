using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PermisVR.Editor
{
    /// Fixes the two causes of "spinning, and sitting on the roof" in the M1 scene:
    /// the car Rigidbody had only WheelColliders (which don't count toward inertia, so Unity
    /// fell back to a 1,1,1 tensor and the car spun like a top), and the legacy XR CameraOffset
    /// lifted the camera by a 1.36 m standing eye height above the seated cockpit.
    public static class SeatedRigFix
    {
        private const int DeviceTrackingMode = 1;

        [MenuItem("PermisVR/Scene/Fix Spin And Seat Height")]
        public static void Apply()
        {
            var car = GameObject.Find("Car");
            if (car == null)
            {
                Debug.LogError("SeatedRigFix: no 'Car' GameObject in the open scene.");
                return;
            }

            EnsureBodyCollider(car.transform);

            var cockpit = car.transform.Find("Cockpit");
            if (cockpit != null)
            {
                ZeroLegacyCameraOffset(cockpit);
            }

            EditorSceneManager.MarkSceneDirty(car.scene);
            EditorSceneManager.SaveScene(car.scene);
            Debug.Log("SeatedRigFix: body collider restored, camera eye-height offset zeroed, scene saved.");
        }

        public static void EnsureBodyCollider(Transform car)
        {
            var body = car.Find("Body");
            if (body != null && body.GetComponent<BoxCollider>() == null)
            {
                body.gameObject.AddComponent<BoxCollider>();
            }
        }

        /// The legacy CameraOffset type lives in an editor-side assembly we don't reference,
        /// so it's matched by name and edited through SerializedObject.
        public static void ZeroLegacyCameraOffset(Transform root)
        {
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour.GetType().Name != "CameraOffset")
                {
                    continue;
                }

                var so = new SerializedObject(behaviour);
                var yOffset = so.FindProperty("m_CameraYOffset");
                var requestedMode = so.FindProperty("m_RequestedTrackingMode");
                if (yOffset != null)
                {
                    yOffset.floatValue = 0f;
                }

                if (requestedMode != null)
                {
                    requestedMode.intValue = DeviceTrackingMode;
                }

                so.ApplyModifiedProperties();
            }
        }
    }
}
