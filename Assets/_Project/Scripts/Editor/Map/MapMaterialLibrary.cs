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
            material.mainTexture = null;
            material.enableInstancing = true;
            material.SetFloat("_Glossiness", 0.1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// Textured variant: the meshes carry world-space UVs in metres, so the texture repeats
        /// every repeatM metres. White tint, so the texture shows its own colours.
        public static Material GetTextured(string name, Texture2D texture, float repeatM)
        {
            var material = Get(name, Color.white);
            material.mainTexture = texture;
            material.mainTextureScale = Vector2.one / repeatM;
            material.mainTextureOffset = Vector2.zero;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
