using System.Collections.Generic;
using System.IO;
using PermisVR.Editor.Map;
using PermisVR.ExamUnity;
using PermisVR.Input;
using PermisVR.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PermisVR.Editor.Car
{
    /// Turns the placeholder box car into a small hatchback: visible body (hood, pillars, roof), a
    /// Yaris-style cabin, a three-spoke wheel at a realistic height and tilt, hands that grip it,
    /// and realistic acceleration/brake/pedal tuning. Applies to the M2 and M1 scenes; re-runnable.
    public static class CarUpgradeSetup
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/_Project/Scenes/M2_Argenteuil.unity",
            "Assets/_Project/Scenes/M1_TestPlane.unity",
        };

        private const string MeshDir = "Assets/_Project/Data/Car/Generated";
        private const string MaterialDir = "Assets/_Project/Data/Materials/Car";
        private const string VehicleConfigPath = "Assets/_Project/Data/VehicleConfig.asset";
        private const string TriggerConfigPath = "Assets/_Project/Data/TriggerResponseConfig.asset";
        private const string WheelConfigPath = "Assets/_Project/Data/SteeringWheelConfig.asset";

        // Steering wheel: lower than before and tilted like a real column (top further away).
        private static readonly Vector3 WheelCenterLocal = new(0f, -0.27f, 0.42f);
        private static readonly Quaternion WheelTilt = Quaternion.Euler(20f, 0f, 0f);

        // Cockpit-local spots for the driver's display text and the OSM credit (cockpit at car (-0.45, 0.6, 0.3)).
        private static readonly Vector3 SpeedometerLocal = new(0f, -0.245f, 0.61f);
        private static readonly Vector3 AttributionLocal = new(0.45f, -0.325f, 0.56f);

        private static readonly Dictionary<string, (Color color, float gloss, float metallic)> Palette = new()
        {
            [HatchbackBuilder.TrimBlack] = (new Color(0.05f, 0.05f, 0.055f), 0.3f, 0f),
            [HatchbackBuilder.Charcoal] = (new Color(0.12f, 0.12f, 0.13f), 0.2f, 0f),
            [HatchbackBuilder.DarkGrey] = (new Color(0.22f, 0.22f, 0.23f), 0.15f, 0f),
            [HatchbackBuilder.MidGrey] = (new Color(0.42f, 0.42f, 0.43f), 0.15f, 0f),
            [HatchbackBuilder.Headliner] = (new Color(0.72f, 0.71f, 0.68f), 0.05f, 0f),
            [HatchbackBuilder.Fabric] = (new Color(0.17f, 0.17f, 0.19f), 0.05f, 0f),
            [HatchbackBuilder.Silver] = (new Color(0.62f, 0.62f, 0.64f), 0.6f, 0.6f),
            [HatchbackBuilder.ScreenOff] = (new Color(0.02f, 0.02f, 0.025f), 0.8f, 0f),
            [HatchbackBuilder.ScreenOn] = (new Color(0.07f, 0.11f, 0.17f), 0.8f, 0f),
            [HatchbackBuilder.ScreenUi] = (new Color(0.25f, 0.45f, 0.65f), 0.5f, 0f),
            [HatchbackBuilder.Carpet] = (new Color(0.07f, 0.07f, 0.075f), 0f, 0f),
            [HatchbackBuilder.Leather] = (new Color(0.07f, 0.07f, 0.075f), 0.35f, 0f),
            ["Glove"] = (new Color(0.55f, 0.55f, 0.58f), 0.1f, 0f),
        };

        [MenuItem("PermisVR/Car/Build Hatchback Interior, Hands And Tuning")]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            ApplyTuning();
            var handMeshes = BuildHandMeshes();
            foreach (var path in ScenePaths)
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var car = GameObject.Find("Car");
                if (car == null)
                {
                    Debug.LogError($"CarUpgradeSetup: no 'Car' in {path}; skipped.");
                    continue;
                }

                Apply(car, handMeshes);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"CarUpgradeSetup: hatchback body, interior, wheel and hands applied to {path}.");
            }

            AssetDatabase.SaveAssets();
            if (File.Exists(ScenePaths[0]))
            {
                EditorSceneManager.OpenScene(ScenePaths[0], OpenSceneMode.Single);
            }
        }

        /// Small-hatchback feel: brakes at ~9 m/s² max, progressive (squared) pedal response.
        /// New powertrain and grab fields take their code defaults on existing assets.
        private static void ApplyTuning()
        {
            var vehicle = AssetDatabase.LoadAssetAtPath<VehicleConfig>(VehicleConfigPath);
            if (vehicle != null)
            {
                vehicle.brakeTorque = 1350f;
                vehicle.maxSteerAngleDeg = 35f;
                vehicle.highSpeedSteerAngleDeg = 12f; // less steering fade at speed
                EditorUtility.SetDirty(vehicle);
            }

            var trigger = AssetDatabase.LoadAssetAtPath<TriggerResponseConfig>(TriggerConfigPath);
            if (trigger != null)
            {
                // Hermite with slopes 0 and 2 is exactly x²: gentle at the start of the pull, full at the end.
                trigger.responseCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 2f));
                EditorUtility.SetDirty(trigger);
            }

            var wheel = AssetDatabase.LoadAssetAtPath<SteeringWheelConfig>(WheelConfigPath);
            if (wheel != null)
            {
                wheel.maxWheelAngleDeg = 225f; // quicker steering (450° lock-to-lock)
                EditorUtility.SetDirty(wheel);
            }
        }

        private static Dictionary<string, Mesh> BuildHandMeshes()
        {
            var meshes = new Dictionary<string, Mesh>();
            foreach (var left in new[] { true, false })
            {
                foreach (var grip in new[] { true, false })
                {
                    var name = $"Hand_{(left ? "Left" : "Right")}_{(grip ? "Grip" : "Open")}";
                    meshes[name] = SaveMesh(HandMeshBuilder.Build(name, left, grip));
                }
            }

            return meshes;
        }

        private static void Apply(GameObject car, Dictionary<string, Mesh> handMeshes)
        {
            var cockpit = car.transform.Find("Cockpit");
            var wheelCenter = cockpit != null ? cockpit.Find("SteeringWheelCenter") : null;
            var wheel = cockpit != null ? cockpit.GetComponent<VirtualSteeringWheel>() : null;
            if (wheelCenter == null || wheel == null)
            {
                Debug.LogError("CarUpgradeSetup: need Car/Cockpit with a VirtualSteeringWheel and SteeringWheelCenter.");
                return;
            }

            // Body: the old box keeps its collider (physics/inertia) but is no longer drawn.
            var body = car.transform.Find("Body");
            var paint = body != null ? body.GetComponent<MeshRenderer>().sharedMaterial : null;
            if (body != null)
            {
                body.GetComponent<MeshRenderer>().enabled = false;
            }

            var bodyParts = new Dictionary<string, MeshBuilder>();
            HatchbackBuilder.BuildBody(bodyParts);
            HatchbackBuilder.BuildInterior(bodyParts);
            ReplaceGroup(car.transform, "Hatchback", bodyParts, paint, "Car");

            wheelCenter.localPosition = WheelCenterLocal;
            wheelCenter.localRotation = WheelTilt;
            var column = new Dictionary<string, MeshBuilder>();
            HatchbackBuilder.BuildSteeringColumn(column);
            ReplaceGroup(wheelCenter, "SteeringColumn", column, paint, "Column");

            var wheelConfig = AssetDatabase.LoadAssetAtPath<SteeringWheelConfig>(WheelConfigPath);
            var rimRadius = wheelConfig != null ? wheelConfig.rimRadiusM : 0.18f;
            var wheelParts = new Dictionary<string, MeshBuilder>();
            HatchbackBuilder.BuildSteeringWheel(wheelParts, rimRadius);
            var wheelMesh = ReplaceGroup(wheelCenter, "WheelMesh", wheelParts, paint, "Wheel");
            var visual = wheelCenter.GetComponent<SteeringWheelVisual>();
            if (visual == null)
            {
                visual = wheelCenter.gameObject.AddComponent<SteeringWheelVisual>();
            }

            var visualSo = new SerializedObject(visual);
            visualSo.FindProperty("_wheel").objectReferenceValue = wheel;
            visualSo.FindProperty("_mesh").objectReferenceValue = wheelMesh;
            visualSo.ApplyModifiedPropertiesWithoutUndo();

            AddHand(cockpit, wheel, true, handMeshes);
            AddHand(cockpit, wheel, false, handMeshes);

            Move(cockpit, "Speedometer", SpeedometerLocal, Quaternion.identity);
            Move(cockpit, "OsmAttribution", AttributionLocal, Quaternion.Euler(0f, 12f, 0f));

            var session = Object.FindFirstObjectByType<ExamSession>();
            if (session != null)
            {
                MirrorSetup.Ensure(session);
            }
        }

        /// Recreates `name` under `parent` with one child per material (merged mesh, no collider).
        private static Transform ReplaceGroup(Transform parent, string name, Dictionary<string, MeshBuilder> parts,
            Material paint, string meshPrefix)
        {
            var existing = parent.Find(name);
            while (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
                existing = parent.Find(name);
            }

            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            foreach (var part in parts)
            {
                if (part.Value.IsEmpty)
                {
                    continue;
                }

                var go = new GameObject(part.Key);
                go.transform.SetParent(group, false);
                go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(part.Value.Build($"{meshPrefix}_{part.Key}"));
                go.AddComponent<MeshRenderer>().sharedMaterial =
                    part.Key == HatchbackBuilder.Paint && paint != null ? paint : GetMaterial(part.Key);
            }

            return group;
        }

        private static void AddHand(Transform cockpit, VirtualSteeringWheel wheel, bool left, Dictionary<string, Mesh> meshes)
        {
            var side = left ? "Left" : "Right";
            var name = $"{side}HandVisual";
            var existing = cockpit.Find(name);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var go = new GameObject(name);
            go.transform.SetParent(cockpit, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = meshes[$"Hand_{side}_Open"];
            go.AddComponent<MeshRenderer>().sharedMaterial = GetMaterial("Glove");

            var hand = go.AddComponent<SteeringHandVisual>();
            var so = new SerializedObject(hand);
            so.FindProperty("_wheel").objectReferenceValue = wheel;
            so.FindProperty("_controller").objectReferenceValue = FindDeep(cockpit, $"{side}HandTracked");
            so.FindProperty("_isLeft").boolValue = left;
            so.FindProperty("_meshFilter").objectReferenceValue = filter;
            so.FindProperty("_gripMesh").objectReferenceValue = meshes[$"Hand_{side}_Grip"];
            so.FindProperty("_openMesh").objectReferenceValue = meshes[$"Hand_{side}_Open"];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Move(Transform cockpit, string childName, Vector3 localPosition, Quaternion localRotation)
        {
            var child = cockpit.Find(childName);
            if (child != null)
            {
                child.localPosition = localPosition;
                child.localRotation = localRotation;
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            Debug.LogWarning($"CarUpgradeSetup: no '{name}' under {root.name}; that hand only shows when gripping the wheel.");
            return null;
        }

        /// Saves (or overwrites in place, keeping the asset's GUID) a generated car mesh.
        private static Mesh SaveMesh(Mesh mesh)
        {
            Directory.CreateDirectory(MeshDir);
            var path = $"{MeshDir}/{mesh.name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static Material GetMaterial(string name)
        {
            Directory.CreateDirectory(MaterialDir);
            var path = $"{MaterialDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }

            var (color, gloss, metallic) = Palette.TryGetValue(name, out var entry) ? entry : (Color.magenta, 0f, 0f);
            material.color = color;
            material.SetFloat("_Glossiness", gloss);
            material.SetFloat("_Metallic", metallic);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
