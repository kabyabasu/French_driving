using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PermisVR.Editor
{
    /// Placeholder colors for M1: yellow exterior body, white interior/wheel, so the scene
    /// reads as a car instead of a plain gray box. Also nudges the wheel within easy reach.
    public static class M1MaterialsSetup
    {
        private const string MaterialsDir = "Assets/_Project/Data/Materials";

        [MenuItem("PermisVR/Scene/Set M1 Placeholder Colors")]
        public static void SetPlaceholderColors()
        {
            var car = GameObject.Find("Car");
            if (car == null)
            {
                Debug.LogError("M1MaterialsSetup: no 'Car' GameObject in the open scene.");
                return;
            }

            var body = car.transform.Find("Body");
            if (body != null)
            {
                ApplyMaterial(body, "CarYellow", new Color(0.95f, 0.78f, 0.05f));
            }

            var cockpit = car.transform.Find("Cockpit");
            var wheelCenter = cockpit != null ? cockpit.Find("SteeringWheelCenter") : null;
            var wheelMesh = wheelCenter != null ? wheelCenter.Find("WheelMesh") : null;
            if (wheelMesh != null)
            {
                ApplyMaterial(wheelMesh, "InteriorWhite", Color.white);
            }

            if (wheelCenter != null)
            {
                // Bring the wheel a bit closer, within easy arm's reach.
                wheelCenter.localPosition = new Vector3(0f, -0.15f, 0.4f);
            }

            EditorSceneManager.MarkSceneDirty(car.scene);
            EditorSceneManager.SaveScene(car.scene);
            Debug.Log("M1MaterialsSetup: applied placeholder colors, nudged wheel position, scene saved.");
        }

        private static void ApplyMaterial(Transform target, string materialName, Color color)
        {
            var renderer = target.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            var assetPath = $"{MaterialsDir}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                // NOTE: the project is currently on the Built-in Render Pipeline, not URP as
                // CLAUDE.md specifies (URP was never installed during initial setup) — using
                // "Standard" here to match what's actually active. Switch this once URP is set up.
                var shader = Shader.Find("Standard");
                material = new Material(shader) { color = color };
                Directory.CreateDirectory(MaterialsDir);
                AssetDatabase.CreateAsset(material, assetPath);
            }
            else
            {
                material.color = color;
            }

            renderer.sharedMaterial = material;
        }
    }
}
