using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace PermisVR.Editor
{
    /// Summer-afternoon lighting for the Quest (Built-in pipeline): one realtime sun without
    /// shadows (directional shadows would add a full depth pass), a sky-tinted trilight ambient so
    /// faces away from the sun aren't flat grey, a tuned procedural sky, and fog matched to its
    /// horizon. Nothing is baked, so re-importing the map needs no lighting rebuild.
    public static class LightingSetup
    {
        private const string SkyPath = "Assets/_Project/Data/Materials/Sky.mat";
        private const string SunName = "Sun";

        // Sun about 50° up, from the south-west (afternoon in Île-de-France, early summer).
        private const float SunElevationDeg = 50f;
        private const float SunAzimuthDeg = 225f; // compass bearing the sunlight comes from (+Z = north)
        private const float SunIntensity = 1.15f;
        private static readonly Color SunColor = new(1f, 0.95f, 0.86f);

        private static readonly Color AmbientSky = new(0.56f, 0.64f, 0.76f);
        private static readonly Color AmbientEquator = new(0.5f, 0.51f, 0.5f);
        private static readonly Color AmbientGround = new(0.3f, 0.28f, 0.24f);

        private static readonly Color FogColor = new(0.74f, 0.8f, 0.87f); // the sky near the horizon
        public const float FogStartM = 220f;
        public const float FogEndM = 550f;

        [MenuItem("PermisVR/Scene/Apply Lighting (Summer Afternoon)")]
        public static void ApplyToOpenScene()
        {
            Apply();
            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("LightingSetup: sun, ambient, sky and fog applied; scene saved.");
        }

        /// Called by the map importer too, so every import keeps the same lighting.
        public static void Apply()
        {
            var sun = EnsureSun();
            RenderSettings.sun = sun;
            RenderSettings.skybox = LoadOrCreateSky();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = FogStartM;
            RenderSettings.fogEndDistance = FogEndM;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        }

        private static Light EnsureSun()
        {
            Light sun = null;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (light.type == LightType.Directional)
                {
                    sun = light;
                    break;
                }
            }

            if (sun == null)
            {
                sun = new GameObject(SunName).AddComponent<Light>();
                sun.type = LightType.Directional;
            }

            // Light travels along +Z of its transform: pitch down by the elevation, yaw to point
            // away from where the sun is.
            sun.transform.rotation = Quaternion.Euler(SunElevationDeg, SunAzimuthDeg + 180f, 0f);
            sun.color = SunColor;
            sun.intensity = SunIntensity;
            sun.shadows = LightShadows.None;
            sun.renderMode = LightRenderMode.ForcePixel; // forward base pass: no extra pass for one directional light
            sun.lightmapBakeType = LightmapBakeType.Realtime;
            return sun;
        }

        private static Material LoadOrCreateSky()
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, SkyPath);
            }

            sky.SetFloat("_SunDisk", 1f);            // simple disc: cheapest
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_AtmosphereThickness", 0.8f);
            sky.SetColor("_SkyTint", new Color(0.47f, 0.55f, 0.66f));
            sky.SetColor("_GroundColor", new Color(0.4f, 0.39f, 0.37f));
            sky.SetFloat("_Exposure", 1.25f);
            EditorUtility.SetDirty(sky);
            return sky;
        }
    }
}
