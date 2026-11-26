using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace PermisVR.Editor
{
    public static class BuildTools
    {
        private const string ApkOutputPath = "Builds/PermisVR.apk";
        private const string M1ScenePath = "Assets/_Project/Scenes/M1_TestPlane.unity";

        /// Builds the Quest APK from the scenes currently enabled in Build Settings.
        [MenuItem("PermisVR/Build/Quest 3 APK")]
        public static void BuildQuest()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            EnsureM1SceneRegistered();

            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("BuildTools.BuildQuest: no scenes enabled in Build Settings.");
                EditorApplication.Exit(1);
                return;
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = ApkOutputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });

            var success = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
            Debug.Log($"BuildTools.BuildQuest: {report.summary.result}, {report.summary.totalErrors} error(s)");

            if (!success && Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }

        private static void EnsureM1SceneRegistered()
        {
            if (EditorBuildSettings.scenes.Any(s => s.path == M1ScenePath))
            {
                return;
            }

            Debug.Log($"BuildTools: no scenes in Build Settings yet, adding {M1ScenePath}.");
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(M1ScenePath, true),
            };
        }
    }
}
