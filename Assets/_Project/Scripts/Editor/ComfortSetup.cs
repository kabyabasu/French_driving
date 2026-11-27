using System.Collections.Generic;
using PermisVR.Comfort;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PermisVR.Editor
{
    /// Adds the motion-sickness measures to the open scene: CockpitStabilizer on the car (its
    /// visual-only children stop following suspension pitch, roll and bounce) and the turning
    /// vignette in front of the XR camera.
    public static class ComfortSetup
    {
        private const string ConfigPath = "Assets/_Project/Data/ComfortConfig.asset";
        private const string ShaderName = "PermisVR/ComfortVignette";
        private const string MaterialPath = "Assets/_Project/Data/Materials/ComfortVignette.mat";
        private const string VignetteName = "ComfortVignette";

        [MenuItem("PermisVR/Scene/Add Comfort (Stabilised Cockpit + Turn Vignette)")]
        public static void AddToOpenScene()
        {
            var car = GameObject.Find("Car");
            var body = car != null ? car.GetComponent<Rigidbody>() : null;
            var camera = Camera.main;
            if (body == null || camera == null)
            {
                Debug.LogError("ComfortSetup: needs a 'Car' with a Rigidbody and a Main Camera in the open scene.");
                return;
            }

            var config = LoadOrCreateConfig();
            AddStabilizer(car, config);
            AddVignette(camera, body, config);

            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("ComfortSetup: stabilised cockpit and turn vignette added; scene saved.");
        }

        /// Every direct child of the car without physics (colliders, wheels) is visual and gets stabilised.
        private static void AddStabilizer(GameObject car, ComfortConfig config)
        {
            var visuals = new List<Transform>();
            foreach (Transform child in car.transform)
            {
                if (child.GetComponentInChildren<Collider>(true) == null)
                {
                    visuals.Add(child);
                }
            }

            var stabilizer = car.GetComponent<CockpitStabilizer>();
            if (stabilizer == null)
            {
                stabilizer = car.AddComponent<CockpitStabilizer>();
            }

            var so = new SerializedObject(stabilizer);
            so.FindProperty("_config").objectReferenceValue = config;
            var list = so.FindProperty("_visuals");
            list.arraySize = visuals.Count;
            for (var i = 0; i < visuals.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = visuals[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"ComfortSetup: stabilising {string.Join(", ", visuals.ConvertAll(v => v.name))}.");
        }

        private static void AddVignette(Camera camera, Rigidbody body, ComfortConfig config)
        {
            var existing = camera.transform.Find(VignetteName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var go = new GameObject(VignetteName, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(camera.transform, false);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = LoadOrCreateMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;

            var vignette = go.AddComponent<ComfortVignette>();
            var so = new SerializedObject(vignette);
            so.FindProperty("_config").objectReferenceValue = config;
            so.FindProperty("_car").objectReferenceValue = body;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ComfortConfig LoadOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<ComfortConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<ComfortConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                AssetDatabase.SaveAssets();
            }

            return config;
        }

        private static Material LoadOrCreateMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find(ShaderName)) { enableInstancing = true };
                AssetDatabase.CreateAsset(material, MaterialPath);
                AssetDatabase.SaveAssets();
            }

            return material;
        }
    }
}
