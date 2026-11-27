using UnityEditor;
using UnityEngine;

namespace PermisVR.Editor.Map
{
    /// Saves generated map meshes as asset files (stored in Git LFS) instead of embedding them in
    /// the scene, which keeps the scene file small. The folder is wiped and rewritten on each import.
    internal static class GeneratedMeshStore
    {
        private const string ParentDir = "Assets/_Project/Data/Maps/Generated";
        private const string Dir = ParentDir + "/Argenteuil";

        public static void Reset()
        {
            if (AssetDatabase.IsValidFolder(Dir))
            {
                AssetDatabase.DeleteAsset(Dir);
            }

            if (!AssetDatabase.IsValidFolder(ParentDir))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Data/Maps", "Generated");
            }

            AssetDatabase.CreateFolder(ParentDir, "Argenteuil");
        }

        /// Mesh names are unique per import (layer + tile), so they double as file names.
        public static Mesh Save(Mesh mesh)
        {
            AssetDatabase.CreateAsset(mesh, $"{Dir}/{mesh.name}.asset");
            return mesh;
        }
    }
}
