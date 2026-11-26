using System.IO;
using PermisVR.UI;
using PermisVR.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PermisVR.Editor
{
    /// Gives the M1 test plane a sense of motion: enlarges the ground to 200 m, scatters
    /// red/white posts every 10 m, and adds a km/h readout above the steering wheel.
    public static class M1ReferenceSetup
    {
        private const string MaterialsDir = "Assets/_Project/Data/Materials";
        private const float GroundSizeM = 200f;
        private const float PostSpacingM = 10f;
        private const float ClearRadiusAroundCarM = 6f;

        [MenuItem("PermisVR/Scene/Add Motion Reference Markers")]
        public static void Apply()
        {
            var car = GameObject.Find("Car");
            var ground = GameObject.Find("Ground");
            if (car == null || ground == null)
            {
                Debug.LogError("M1ReferenceSetup: needs 'Car' and 'Ground' in the open scene.");
                return;
            }

            // Unity's Plane primitive is 10 m wide at scale 1.
            ground.transform.localScale = new Vector3(GroundSizeM / 10f, 1f, GroundSizeM / 10f);

            AddPosts(car.scene);
            AddBoundaryWalls(car.scene);
            AddSpeedometer(car);

            EditorSceneManager.MarkSceneDirty(car.scene);
            EditorSceneManager.SaveScene(car.scene);
            Debug.Log("M1ReferenceSetup: 200 m ground, posts every 10 m, boundary walls, speedometer added, scene saved.");
        }

        private static void AddPosts(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Markers")
                {
                    return;
                }
            }

            var red = LoadOrCreateMaterial("MarkerRed", new Color(0.85f, 0.1f, 0.1f));
            var white = LoadOrCreateMaterial("MarkerWhite", Color.white);
            var markers = new GameObject("Markers");
            var half = GroundSizeM / 2f - PostSpacingM;

            for (var x = -half; x <= half; x += PostSpacingM)
            {
                for (var z = -half; z <= half; z += PostSpacingM)
                {
                    if (new Vector2(x, z).magnitude < ClearRadiusAroundCarM)
                    {
                        continue;
                    }

                    var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    post.name = "Post";
                    post.transform.SetParent(markers.transform, false);
                    post.transform.localPosition = new Vector3(x, 0.75f, z);
                    post.transform.localScale = new Vector3(0.3f, 1.5f, 0.3f);
                    Object.DestroyImmediate(post.GetComponent<Collider>());

                    var isRed = (Mathf.RoundToInt(x / PostSpacingM) + Mathf.RoundToInt(z / PostSpacingM)) % 2 == 0;
                    post.GetComponent<Renderer>().sharedMaterial = isRed ? red : white;
                    GameObjectUtility.SetStaticEditorFlags(post, StaticEditorFlags.BatchingStatic);
                }
            }
        }

        /// Visible barriers on all four edges so the car can't drive off the plane. Tall enough
        /// to catch the car body, since WheelColliders only raycast down and never hit walls.
        private static void AddBoundaryWalls(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Boundary")
                {
                    return;
                }
            }

            const float heightM = 2f;
            const float thicknessM = 1f;
            var red = LoadOrCreateMaterial("MarkerRed", new Color(0.85f, 0.1f, 0.1f));
            var boundary = new GameObject("Boundary");
            var edge = GroundSizeM / 2f;

            CreateWall(boundary.transform, red, new Vector3(0f, heightM / 2f, edge), new Vector3(GroundSizeM, heightM, thicknessM));
            CreateWall(boundary.transform, red, new Vector3(0f, heightM / 2f, -edge), new Vector3(GroundSizeM, heightM, thicknessM));
            CreateWall(boundary.transform, red, new Vector3(edge, heightM / 2f, 0f), new Vector3(thicknessM, heightM, GroundSizeM));
            CreateWall(boundary.transform, red, new Vector3(-edge, heightM / 2f, 0f), new Vector3(thicknessM, heightM, GroundSizeM));
        }

        private static void CreateWall(Transform parent, Material material, Vector3 position, Vector3 size)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = position;
            wall.transform.localScale = size;
            wall.GetComponent<Renderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(wall, StaticEditorFlags.BatchingStatic);
        }

        private static void AddSpeedometer(GameObject car)
        {
            var cockpit = car.transform.Find("Cockpit");
            if (cockpit == null || cockpit.Find("Speedometer") != null)
            {
                return;
            }

            var go = new GameObject("Speedometer");
            go.transform.SetParent(cockpit, false);
            // Just above and behind the wheel rim, readable from the seat.
            go.transform.localPosition = new Vector3(0f, 0.05f, 0.6f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var text = go.AddComponent<TextMesh>();
            text.font = font;
            text.text = "0 km/h";
            text.fontSize = 100;
            text.characterSize = 0.004f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(0.2f, 1f, 0.3f);
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;

            var so = new SerializedObject(go.AddComponent<Speedometer>());
            so.FindProperty("_car").objectReferenceValue = car.GetComponent<CarController>();
            so.FindProperty("_text").objectReferenceValue = text;
            so.ApplyModifiedProperties();
        }

        private static Material LoadOrCreateMaterial(string materialName, Color color)
        {
            var assetPath = $"{MaterialsDir}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material != null)
            {
                return material;
            }

            // Built-in pipeline is what's active (see M1MaterialsSetup).
            material = new Material(Shader.Find("Standard")) { color = color };
            Directory.CreateDirectory(MaterialsDir);
            AssetDatabase.CreateAsset(material, assetPath);
            return material;
        }
    }
}
