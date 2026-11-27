using System;
using System.Diagnostics;
using System.IO;
using PermisVR.ExamUnity;
using PermisVR.ExamUnity.Config;
using PermisVR.ExamUnity.RouteData;
using PermisVR.Route;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace PermisVR.Editor.Route
{
    /// Puts the examiner in the open scene: voice clips (generated with the macOS French voice),
    /// the ExamRouteGuide on the passenger seat with a subtitle line, and the car at the route start.
    public static class ExamRouteSetup
    {
        private const string RoutePath = "Assets/_Project/Data/Routes/argenteuil_loop1_route.json";
        private const string ConfigPath = "Assets/_Project/Data/ExaminerConfig.asset";
        private const string ClipsDir = "Assets/_Project/Audio/Examiner";
        private const string ExaminerName = "Examiner";
        private const string SubtitleName = "ExaminerSubtitle";

        // macOS "Thomas" (fr_FR). Fine for a personal project; use a licensed voice before any public release.
        private const string Voice = "Thomas";
        private const int WordsPerMinute = 170;

        // Cockpit-local: the passenger seat (France: right of the driver), and a line of text in the
        // lower windscreen just above the fault display.
        private static readonly Vector3 PassengerLocal = new(0.8f, 0f, 0f);
        private static readonly Vector3 SubtitleLocal = new(0f, 0.26f, 0.72f);
        private const float RightLaneOffsetM = 1.6f;  // centre of the right-hand lane of a two-lane street

        [MenuItem("PermisVR/Route/Set Up Exam Route (Argenteuil Loop 1)")]
        public static void SetUpOpenScene()
        {
            // Clips first: importing assets can reload the scene, so scene objects are looked up afterwards.
            if (!GenerateMissingClips(out var clips))
            {
                return;
            }

            var car = GameObject.Find("Car");
            var cockpit = car != null ? car.transform.Find("Cockpit") : null;
            var body = car != null ? car.GetComponent<Rigidbody>() : null;
            var session = Object.FindAnyObjectByType<ExamSession>();
            var routeAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(RoutePath);
            if (cockpit == null || body == null || session == null || routeAsset == null)
            {
                Debug.LogError("ExamRouteSetup: needs Car (with Rigidbody and Cockpit), an ExamSession and " +
                               $"{RoutePath}. Open M2_Argenteuil and run PermisVR > Map > Import Argenteuil first.");
                return;
            }

            var examiner = ReplaceChild(cockpit, ExaminerName);
            examiner.transform.localPosition = PassengerLocal;
            var voice = examiner.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0.7f; // heard from the passenger side, but never faint
            voice.minDistance = 1f;
            voice.volume = 1f;

            var subtitle = CreateSubtitle(cockpit);
            var guide = examiner.AddComponent<ExamRouteGuide>();
            var so = new SerializedObject(guide);
            so.FindProperty("_routeJson").objectReferenceValue = routeAsset;
            so.FindProperty("_config").objectReferenceValue = LoadOrCreateConfig();
            so.FindProperty("_carBody").objectReferenceValue = body;
            so.FindProperty("_voice").objectReferenceValue = voice;
            so.FindProperty("_subtitle").objectReferenceValue = subtitle;
            var clipsProperty = so.FindProperty("_clips");
            clipsProperty.arraySize = clips.Length;
            for (var i = 0; i < clips.Length; i++)
            {
                clipsProperty.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            var sessionSo = new SerializedObject(session);
            sessionSo.FindProperty("_routeGuide").objectReferenceValue = guide;
            sessionSo.ApplyModifiedPropertiesWithoutUndo();

            PlaceCarAtRouteStart(car, routeAsset.text);

            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("ExamRouteSetup: examiner, voice and route set up; car placed at the route start.");
            EditorUtility.DisplayDialog("Exam route", "Examiner, voice and route are set up and the scene is saved. You can build now.", "OK");
        }

        [MenuItem("PermisVR/Route/Regenerate Examiner Voice Clips (macOS)")]
        public static void RegenerateClips()
        {
            if (Directory.Exists(ClipsDir))
            {
                foreach (var file in Directory.GetFiles(ClipsDir, "*.wav"))
                {
                    AssetDatabase.DeleteAsset(file.Replace('\\', '/'));
                }
            }

            if (GenerateMissingClips(out _))
            {
                Debug.Log("ExamRouteSetup: examiner clips regenerated. Re-run Set Up Exam Route to wire them.");
            }
        }

        /// One WAV per ExaminerPhrase, named after it; existing files are kept. All files are written
        /// first and imported in one batch, so the asset database refreshes once.
        private static bool GenerateMissingClips(out AudioClip[] clips)
        {
            var phrases = (ExaminerPhrase[])Enum.GetValues(typeof(ExaminerPhrase));
            clips = new AudioClip[phrases.Length];
            Directory.CreateDirectory(ClipsDir);
            var tempAiff = Path.Combine(Path.GetTempPath(), "permisvr_examiner.aiff");
            try
            {
                for (var i = 0; i < phrases.Length; i++)
                {
                    var phrase = phrases[i];
                    var path = $"{ClipsDir}/{phrase}.wav";
                    if (File.Exists(path))
                    {
                        continue;
                    }

                    if (Application.platform != RuntimePlatform.OSXEditor)
                    {
                        Debug.LogError($"ExamRouteSetup: {path} is missing and clips can only be generated on macOS.");
                        return false;
                    }

                    EditorUtility.DisplayProgressBar("Examiner voice", $"Recording \"{phrase}\"", (float)i / phrases.Length);
                    if (!Run("/usr/bin/say", $"-v {Voice} -r {WordsPerMinute} -o \"{tempAiff}\" \"{ExaminerPhrases.Text(phrase)}\"")
                        || !Run("/usr/bin/afconvert", $"-f WAVE -d LEI16@22050 -c 1 \"{tempAiff}\" \"{path}\""))
                    {
                        return false;
                    }
                }

                EditorUtility.DisplayProgressBar("Examiner voice", "Importing clips", 1f);
                AssetDatabase.Refresh();
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (var phrase in phrases)
                    {
                        ConfigureClip($"{ClipsDir}/{phrase}.wav");
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }

                foreach (var phrase in phrases)
                {
                    clips[(int)phrase] = AssetDatabase.LoadAssetAtPath<AudioClip>($"{ClipsDir}/{phrase}.wav");
                    if (clips[(int)phrase] == null)
                    {
                        Debug.LogError($"ExamRouteSetup: couldn't load {ClipsDir}/{phrase}.wav.");
                        return false;
                    }
                }

                return true;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static bool Run(string exe, string args)
        {
            using var process = Process.Start(new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });
            if (process == null)
            {
                Debug.LogError($"ExamRouteSetup: couldn't start {exe}.");
                return false;
            }

            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                Debug.LogError($"ExamRouteSetup: {exe} failed ({process.ExitCode}): {error}");
                return false;
            }

            return true;
        }

        /// Short mono speech: decompressed on load (no runtime decode cost), Vorbis in the APK.
        private static void ConfigureClip(string path)
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            if (importer.forceToMono && settings.loadType == AudioClipLoadType.DecompressOnLoad
                && settings.compressionFormat == AudioCompressionFormat.Vorbis)
            {
                return;
            }

            importer.forceToMono = true;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport(); // batched by StartAssetEditing
        }

        private static ExaminerConfigAsset LoadOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<ExaminerConfigAsset>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<ExaminerConfigAsset>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                AssetDatabase.SaveAssets();
            }

            return config;
        }

        private static GameObject ReplaceChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static TextMesh CreateSubtitle(Transform cockpit)
        {
            var go = ReplaceChild(cockpit, SubtitleName);
            go.transform.localPosition = SubtitleLocal;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var text = go.AddComponent<TextMesh>();
            text.font = font;
            text.fontSize = 100;
            text.characterSize = 0.0022f;
            text.anchor = TextAnchor.LowerCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(1f, 0.93f, 0.6f);
            text.text = "";
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return text;
        }

        /// On the route's first point, in the right-hand lane, facing along the route.
        private static void PlaceCarAtRouteStart(GameObject car, string json)
        {
            var route = RouteJsonLoader.Load(json, out _);
            route.Sample(0f, out var x, out var z, out var headingDeg);
            var rotation = Quaternion.Euler(0f, headingDeg, 0f);
            var position = new Vector3(x, car.transform.position.y, z) + rotation * Vector3.right * RightLaneOffsetM;
            car.transform.SetPositionAndRotation(position, rotation);
        }
    }
}
