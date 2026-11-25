using System.IO;
using PermisVR.Input;
using PermisVR.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PermisVR.Editor
{
    /// Builds the M1 milestone scene (flat test plane + drivable car + cockpit skeleton)
    /// entirely from code, so nothing here is a hand-edited .unity/.prefab file.
    public static class M1SceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/M1_TestPlane.unity";
        private const string DataDir = "Assets/_Project/Data";
        private const string DrivingActionsPath = "Assets/_Project/Scripts/Input/Driving.inputactions";

        [MenuItem("PermisVR/Scene/Build M1 Test Plane")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            CreateGround();
            var car = CreateCar();
            CreateCockpit(car);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"M1SceneBuilder: saved {ScenePath}. Next: add the Meta XR camera rig under " +
                      "Car/Cockpit and re-point LeftHandAnchor/RightHandAnchor at its real controller " +
                      "transforms (see console warning above if the rig wasn't auto-detected).");
        }

        private static void CreateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(2f, 1f, 2f); // 20m x 20m
        }

        private static GameObject CreateCar()
        {
            var car = new GameObject("Car");
            car.AddComponent<Rigidbody>().mass = 1200f;
            var controller = car.AddComponent<CarController>();

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(car.transform, false);
            body.transform.localScale = new Vector3(1.8f, 1f, 4f);
            body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            Object.DestroyImmediate(body.GetComponent<BoxCollider>());

            var frontLeft = CreateWheel("WheelFL", new Vector3(-0.8f, -0.3f, 1.3f), car.transform);
            var frontRight = CreateWheel("WheelFR", new Vector3(0.8f, -0.3f, 1.3f), car.transform);
            var rearLeft = CreateWheel("WheelRL", new Vector3(-0.8f, -0.3f, -1.3f), car.transform);
            var rearRight = CreateWheel("WheelRR", new Vector3(0.8f, -0.3f, -1.3f), car.transform);

            var vehicleConfig = LoadOrCreateConfig<VehicleConfig>($"{DataDir}/VehicleConfig.asset");

            var so = new SerializedObject(controller);
            so.FindProperty("_config").objectReferenceValue = vehicleConfig;
            so.FindProperty("_frontLeft").objectReferenceValue = frontLeft;
            so.FindProperty("_frontRight").objectReferenceValue = frontRight;
            so.FindProperty("_rearLeft").objectReferenceValue = rearLeft;
            so.FindProperty("_rearRight").objectReferenceValue = rearRight;
            so.ApplyModifiedProperties();

            return car;
        }

        private static WheelCollider CreateWheel(string name, Vector3 localPosition, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.AddComponent<WheelCollider>();
        }

        private static void CreateCockpit(GameObject car)
        {
            var cockpit = new GameObject("Cockpit");
            cockpit.transform.SetParent(car.transform, false);
            cockpit.transform.localPosition = new Vector3(0f, 0.6f, 0.3f);

            // Placeholders: once the Meta XR camera rig is added under Cockpit, re-point these
            // two at its real left/right controller transforms. Auto-detecting the rig's exact
            // hierarchy isn't safe to guess blind across Meta XR SDK versions.
            var leftHand = new GameObject("LeftHandAnchor");
            leftHand.transform.SetParent(cockpit.transform, false);
            leftHand.transform.localPosition = new Vector3(-0.3f, 0f, 0.4f);

            var rightHand = new GameObject("RightHandAnchor");
            rightHand.transform.SetParent(cockpit.transform, false);
            rightHand.transform.localPosition = new Vector3(0.3f, 0f, 0.4f);

            var wheelCenter = new GameObject("SteeringWheelCenter");
            wheelCenter.transform.SetParent(cockpit.transform, false);
            wheelCenter.transform.localPosition = new Vector3(0f, -0.1f, 0.5f);

            var wheelConfig = LoadOrCreateConfig<SteeringWheelConfig>($"{DataDir}/SteeringWheelConfig.asset");
            var triggerConfig = LoadOrCreateConfig<TriggerResponseConfig>($"{DataDir}/TriggerResponseConfig.asset");

            var wheel = cockpit.AddComponent<VirtualSteeringWheel>();
            var wheelSo = new SerializedObject(wheel);
            wheelSo.FindProperty("_config").objectReferenceValue = wheelConfig;
            wheelSo.FindProperty("_wheelCenter").objectReferenceValue = wheelCenter.transform;
            wheelSo.FindProperty("_leftHand").objectReferenceValue = leftHand.transform;
            wheelSo.FindProperty("_rightHand").objectReferenceValue = rightHand.transform;
            wheelSo.FindProperty("_leftGrip").objectReferenceValue = FindActionReference("GripLeft");
            wheelSo.FindProperty("_rightGrip").objectReferenceValue = FindActionReference("GripRight");
            wheelSo.ApplyModifiedProperties();

            var input = cockpit.AddComponent<PlayerDrivingInput>();
            var inputSo = new SerializedObject(input);
            inputSo.FindProperty("_car").objectReferenceValue = car.GetComponent<CarController>();
            inputSo.FindProperty("_wheel").objectReferenceValue = wheel;
            inputSo.FindProperty("_throttle").objectReferenceValue = FindActionReference("Throttle");
            inputSo.FindProperty("_brake").objectReferenceValue = FindActionReference("Brake");
            inputSo.FindProperty("_triggerResponse").objectReferenceValue = triggerConfig;
            inputSo.ApplyModifiedProperties();

            if (FindActionReference("GripLeft") == null)
            {
                Debug.LogWarning("M1SceneBuilder: couldn't find InputActionReference sub-assets on " +
                                  DrivingActionsPath + " yet — re-run this menu command after the first " +
                                  "import, or assign them by hand on PlayerDrivingInput/VirtualSteeringWheel.");
            }
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

        private static T LoadOrCreateConfig<T>(string assetPath) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            var instance = ScriptableObject.CreateInstance<T>();
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
            AssetDatabase.CreateAsset(instance, assetPath);
            return instance;
        }
    }
}
