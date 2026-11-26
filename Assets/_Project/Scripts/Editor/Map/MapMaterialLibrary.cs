using System.IO;
using UnityEditor;
using UnityEngine;

namespace PermisVR.Editor.Map
{
    /// Shared, GPU-instancing-enabled materials for generated map content.
    internal static class MapMaterialLibrary
    {
        private const string MaterialsDir = "Assets/_Project/Data/Materials/Map";

        public static Material Get(string name, Color color)
        {
            var path = $"{MaterialsDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                // Built-in render pipeline is what's active (see M1MaterialsSetup).
                material = new Material(Shader.Find("Standard"));
                Directory.CreateDirectory(MaterialsDir);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            material.enableInstancing = true;
            material.SetFloat("_Glossiness", 0.1f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
