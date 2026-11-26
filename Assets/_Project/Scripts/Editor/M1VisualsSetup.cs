using PermisVR.Input;
using PermisVR.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PermisVR.Editor
{
    /// Adds placeholder geometry for the M1 scene: a visible steering wheel disc and rolling
    /// wheel meshes synced to their WheelColliders. M1SceneBuilder left these as invisible
    /// colliders / empty transforms, which makes the scene unreadable in the headset.
    public static class M1VisualsSetup
    {
        [MenuItem("PermisVR/Scene/Add M1 Placeholder Visuals")]
        public static void AddPlaceholderVisuals()
        {
            var car = GameObject.Find("Car");
            if (car == null)
            {
                Debug.LogError("M1VisualsSetup: no 'Car' GameObject in the open scene.");
                return;
            }

            var cockpit = car.transform.Find("Cockpit");
            var wheelCenter = cockpit != null ? cockpit.Find("SteeringWheelCenter") : null;
            var wheelComponent = cockpit != null ? cockpit.GetComponent<VirtualSteeringWheel>() : null;

            if (wheelCenter == null || wheelComponent == null)
            {
                Debug.LogError("M1VisualsSetup: no SteeringWheelCenter / VirtualSteeringWheel under Cockpit.");
                return;
            }

            AddSteeringWheelMesh(wheelCenter, wheelComponent);

            AddWheelMeshIfMissing(car.transform, "WheelFL");
            AddWheelMeshIfMissing(car.transform, "WheelFR");
            AddWheelMeshIfMissing(car.transform, "WheelRL");
            AddWheelMeshIfMissing(car.transform, "WheelRR");

            EditorSceneManager.MarkSceneDirty(car.scene);
            EditorSceneManager.SaveScene(car.scene);
            Debug.Log("M1VisualsSetup: added steering wheel + wheel placeholder meshes, scene saved.");
        }

        private static void AddSteeringWheelMesh(Transform wheelCenter, VirtualSteeringWheel wheelComponent)
        {
            if (wheelCenter.Find("WheelMesh") != null)
            {
                return;
            }

            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mesh.name = "WheelMesh";
            mesh.transform.SetParent(wheelCenter, false);
            mesh.transform.localPosition = Vector3.zero;
            mesh.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            mesh.transform.localScale = new Vector3(0.18f, 0.015f, 0.18f);
            Object.DestroyImmediate(mesh.GetComponent<Collider>());

            var visualSo = new SerializedObject(wheelCenter.gameObject.AddComponent<SteeringWheelVisual>());
            visualSo.FindProperty("_wheel").objectReferenceValue = wheelComponent;
            visualSo.FindProperty("_mesh").objectReferenceValue = mesh.transform;
            visualSo.ApplyModifiedProperties();
        }

        private static void AddWheelMeshIfMissing(Transform car, string wheelName)
        {
            var wheel = car.Find(wheelName);
            if (wheel == null)
            {
                Debug.LogError($"M1VisualsSetup: no '{wheelName}' under Car.");
                return;
            }

            var wheelCollider = wheel.GetComponent<WheelCollider>();
            if (wheelCollider == null)
            {
                Debug.LogError($"M1VisualsSetup: '{wheelName}' has no WheelCollider.");
                return;
            }

            if (wheel.Find("Visual") != null)
            {
                return;
            }

            var visualMesh = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visualMesh.name = "Visual";
            visualMesh.transform.SetParent(wheel, false);
            visualMesh.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var diameter = wheelCollider.radius * 2f;
            visualMesh.transform.localScale = new Vector3(diameter, 0.2f, diameter);
            Object.DestroyImmediate(visualMesh.GetComponent<Collider>());

            var visualSo = new SerializedObject(wheel.gameObject.AddComponent<WheelVisual>());
            visualSo.FindProperty("_mesh").objectReferenceValue = visualMesh.transform;
            visualSo.ApplyModifiedProperties();
        }
    }
}
