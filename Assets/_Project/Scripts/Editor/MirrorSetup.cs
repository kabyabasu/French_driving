using System.IO;
using PermisVR.Editor.Map;
using PermisVR.Exam;
using PermisVR.ExamUnity;
using PermisVR.ExamUnity.Config;
using PermisVR.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PermisVR.Editor
{
    /// Adds the interior and both exterior mirrors to the car, the gaze sensor that detects the
    /// driver checking them, and wires the mirror-check rules' config into the ExamSession.
    public static class MirrorSetup
    {
        private const string MirrorConfigPath = "Assets/_Project/Data/MirrorConfig.asset";
        private const string GazeConfigPath = "Assets/_Project/Data/MirrorGazeConfig.asset";
        private const string RulesConfigPath = "Assets/_Project/Data/MirrorRulesConfig.asset";
        private const string GlassMaterialPath = "Assets/_Project/Data/Materials/MirrorGlass.mat";
        private const string ContainerName = "Mirrors";

        [MenuItem("PermisVR/Scene/Add Mirrors")]
        public static void AddToOpenScene()
        {
            var session = Object.FindFirstObjectByType<ExamSession>();
            if (session == null)
            {
                Debug.LogError("MirrorSetup: no ExamSession in the open scene; run PermisVR > Map > Import Argenteuil first.");
                return;
            }

            Ensure(session);
            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("MirrorSetup: interior + left/right mirrors and mirror-check sensor added.");
        }

        public static void Ensure(ExamSession session)
        {
            var car = GameObject.Find("Car");
            var cockpit = car != null ? car.transform.Find("Cockpit") : null;
            var head = FindHead(cockpit);
            if (car == null || cockpit == null || head == null)
            {
                Debug.LogError("MirrorSetup: need Car/Cockpit with the XR Main Camera under it.");
                return;
            }

            var existing = car.transform.Find(ContainerName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var container = new GameObject(ContainerName).transform;
            container.SetParent(car.transform, false);
            var config = LoadOrCreate<MirrorConfig>(MirrorConfigPath);
            var glass = LoadOrCreateGlassMaterial();
            var housing = MapMaterialLibrary.Get("MirrorHousing", new Color(0.1f, 0.1f, 0.11f));

            // Car-local positions: body is x ±0.9, y 0..1, z ±2; the driver sits at the cockpit (left seat).
            var eye = car.transform.InverseTransformPoint(head.position);
            // Interior mirror just behind the top of the windscreen; door mirrors at the front of the
            // doors, a little below eye level (matches the hatchback body from CarUpgradeSetup).
            var interior = CreateMirror(container, "InteriorMirror", new Vector3(0f, 0.86f, 0.66f), new Vector2(0.24f, 0.07f),
                new Vector3(0f, -0.05f, -1f), eye, 256, 75, 15f, 0, config, glass, housing);
            var left = CreateMirror(container, "LeftMirror", new Vector3(-0.98f, 0.4f, 0.9f), new Vector2(0.16f, 0.11f),
                new Vector3(-0.25f, 0f, -1f), eye, 160, 110, 25f, 1, config, glass, housing);
            var right = CreateMirror(container, "RightMirror", new Vector3(0.98f, 0.4f, 0.9f), new Vector2(0.16f, 0.11f),
                new Vector3(0.25f, 0f, -1f), eye, 160, 110, 25f, 0, config, glass, housing);

            AddGazeSensor(cockpit, session, head, interior, left, right);

            var sessionSo = new SerializedObject(session);
            sessionSo.FindProperty("_mirrorRulesConfig").objectReferenceValue = LoadOrCreate<MirrorRulesConfigAsset>(RulesConfigPath);
            sessionSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform FindHead(Transform cockpit)
        {
            if (cockpit == null)
            {
                return null;
            }

            foreach (var camera in cockpit.GetComponentsInChildren<Camera>(true))
            {
                if (camera.CompareTag("MainCamera"))
                {
                    return camera.transform;
                }
            }

            return null;
        }

        /// The glass faces halfway between the driver's eye and the reflected view, like an adjusted mirror.
        private static Transform CreateMirror(Transform parent, string name, Vector3 localPosition, Vector2 sizeM,
            Vector3 viewDirectionLocal, Vector3 eyeLocal, int textureWidth, int textureHeight, float verticalFovDeg,
            int frameOffset, MirrorConfig config, Material glassMaterial, Material housingMaterial)
        {
            var view = viewDirectionLocal.normalized;
            var toEye = (eyeLocal - localPosition).normalized;
            var normal = (toEye + view).normalized;

            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localPosition;

            // Unity's Quad is visible from its -Z side, so -Z must point along the mirror normal.
            var glass = GameObject.CreatePrimitive(PrimitiveType.Quad);
            glass.name = "Glass";
            Object.DestroyImmediate(glass.GetComponent<Collider>());
            glass.transform.SetParent(root, false);
            glass.transform.localRotation = Quaternion.LookRotation(-normal, Vector3.up);
            glass.transform.localScale = new Vector3(sizeM.x, sizeM.y, 1f);
            glass.GetComponent<Renderer>().sharedMaterial = glassMaterial;

            var shell = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shell.name = "Housing";
            Object.DestroyImmediate(shell.GetComponent<Collider>());
            shell.transform.SetParent(root, false);
            shell.transform.localRotation = glass.transform.localRotation;
            shell.transform.localPosition = -normal * 0.025f;
            shell.transform.localScale = new Vector3(sizeM.x + 0.02f, sizeM.y + 0.02f, 0.04f);
            shell.GetComponent<Renderer>().sharedMaterial = housingMaterial;

            var cameraGo = new GameObject("ReflectionCamera");
            cameraGo.transform.SetParent(root, false);
            cameraGo.transform.localRotation = Quaternion.LookRotation(view, Vector3.up);
            var camera = cameraGo.AddComponent<Camera>();
            camera.enabled = false;

            var mirror = root.gameObject.AddComponent<RearViewMirror>();
            var so = new SerializedObject(mirror);
            so.FindProperty("_config").objectReferenceValue = config;
            so.FindProperty("_camera").objectReferenceValue = camera;
            so.FindProperty("_glass").objectReferenceValue = glass.GetComponent<Renderer>();
            so.FindProperty("_textureWidth").intValue = textureWidth;
            so.FindProperty("_textureHeight").intValue = textureHeight;
            so.FindProperty("_verticalFovDeg").floatValue = verticalFovDeg;
            so.FindProperty("_frameOffset").intValue = frameOffset;
            so.ApplyModifiedPropertiesWithoutUndo();
            return glass.transform;
        }

        private static void AddGazeSensor(Transform cockpit, ExamSession session, Transform head,
            Transform interior, Transform left, Transform right)
        {
            var sensor = cockpit.GetComponent<MirrorCheckSensor>();
            if (sensor == null)
            {
                sensor = cockpit.gameObject.AddComponent<MirrorCheckSensor>();
            }

            var so = new SerializedObject(sensor);
            so.FindProperty("_session").objectReferenceValue = session;
            so.FindProperty("_head").objectReferenceValue = head;
            so.FindProperty("_config").objectReferenceValue = LoadOrCreate<MirrorGazeConfigAsset>(GazeConfigPath);
            var mirrors = so.FindProperty("_mirrors");
            mirrors.arraySize = 3;
            SetMirror(mirrors.GetArrayElementAtIndex(0), MirrorIds.Interior, interior);
            SetMirror(mirrors.GetArrayElementAtIndex(1), MirrorIds.Left, left);
            SetMirror(mirrors.GetArrayElementAtIndex(2), MirrorIds.Right, right);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetMirror(SerializedProperty element, string id, Transform target)
        {
            element.FindPropertyRelative("id").stringValue = id;
            element.FindPropertyRelative("target").objectReferenceValue = target;
        }

        private static Material LoadOrCreateGlassMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
            if (material != null)
            {
                return material;
            }

            material = new Material(Shader.Find("Unlit/Texture"));
            Directory.CreateDirectory(Path.GetDirectoryName(GlassMaterialPath)!);
            AssetDatabase.CreateAsset(material, GlassMaterialPath);
            return material;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }
    }
}
