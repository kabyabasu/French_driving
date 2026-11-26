using System.Collections.Generic;
using PermisVR.Exam;
using PermisVR.ExamUnity;
using PermisVR.ExamUnity.Config;
using PermisVR.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PermisVR.Editor
{
    /// Adds the CEPC results sheet (world-space canvas in the cockpit) and the grid asset it reads.
    public static class ExamResultsSetup
    {
        private const string GridPath = "Assets/_Project/Data/CepcGrid.asset";
        private const string DrivingActionsPath = "Assets/_Project/Scripts/Input/Driving.inputactions";
        private const string ScreenName = "CepcResults";
        private const float CanvasWidthPx = 800f;
        private const float CanvasHeightPx = 1000f;
        // 0.0009 m per px => 0.72 m x 0.9 m, about 0.9 m in front of the driver.
        private const float MetresPerPixel = 0.0009f;

        [MenuItem("PermisVR/Scene/Add CEPC Results Screen")]
        public static void AddToOpenScene()
        {
            var session = Object.FindFirstObjectByType<ExamSession>();
            if (session == null)
            {
                Debug.LogError("ExamResultsSetup: no ExamSession in the open scene; run PermisVR > Map > Import Argenteuil first.");
                return;
            }

            Ensure(session);
            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("ExamResultsSetup: CEPC results screen added; press the left menu button in-game to open it.");
        }

        public static void Ensure(ExamSession session)
        {
            var car = GameObject.Find("Car");
            var cockpit = car != null ? car.transform.Find("Cockpit") : null;
            if (cockpit == null)
            {
                Debug.LogError("ExamResultsSetup: no Car/Cockpit in the open scene.");
                return;
            }

            var existing = cockpit.Find(ScreenName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var go = new GameObject(ScreenName, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(cockpit, false);
            rect.sizeDelta = new Vector2(CanvasWidthPx, CanvasHeightPx);
            rect.localPosition = new Vector3(0f, 0.3f, 0.9f);
            rect.localScale = Vector3.one * MetresPerPixel;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var background = go.AddComponent<Image>();
            background.color = new Color(0.97f, 0.97f, 0.94f);
            background.raycastTarget = false;

            var screen = go.AddComponent<CepcResultsScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("_session").objectReferenceValue = session;
            so.FindProperty("_gridAsset").objectReferenceValue = LoadOrCreateGrid();
            so.FindProperty("_toggleAction").objectReferenceValue = FindActionReference("Pause");
            so.FindProperty("_canvas").objectReferenceValue = canvas;
            so.FindProperty("_content").objectReferenceValue = rect;
            so.FindProperty("_font").objectReferenceValue = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static InputActionReference FindActionReference(string actionName)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(DrivingActionsPath))
            {
                if (asset is InputActionReference reference && reference.action?.name == actionName)
                {
                    return reference;
                }
            }

            Debug.LogWarning($"ExamResultsSetup: no '{actionName}' action in {DrivingActionsPath}.");
            return null;
        }

        // Grid lines that currently have a rule judging them.
        private static readonly string[] EvaluatedCriteria =
        {
            ExamCriteria.ApplyRegulations,
            ExamCriteria.TakeInformation,
        };

        private static CepcGridAsset LoadOrCreateGrid()
        {
            var grid = AssetDatabase.LoadAssetAtPath<CepcGridAsset>(GridPath);
            if (grid == null)
            {
                grid = ScriptableObject.CreateInstance<CepcGridAsset>();
                grid.criteria = DefaultCriteria();
                AssetDatabase.CreateAsset(grid, GridPath);
            }

            foreach (var entry in grid.criteria)
            {
                if (System.Array.IndexOf(EvaluatedCriteria, entry.id) >= 0)
                {
                    entry.isEvaluated = true;
                }
            }

            EditorUtility.SetDirty(grid);
            AssetDatabase.SaveAssets();
            return grid;
        }

        /// Starting values from memory of the official grid, NOT verified: they sum to 29, while
        /// CLAUDE.md says 31. Check against the Sécurité routière "Guide B" and edit the asset.
        private static List<CepcCriterionEntry> DefaultCriteria()
        {
            const string vehicle = "Connaître et maîtriser son véhicule";
            const string road = "Appréhender la route";
            const string share = "Circuler dans des conditions difficiles et partager la route";
            const string autonomous = "Pratiquer une conduite autonome, sûre et économique";

            return new List<CepcCriterionEntry>
            {
                Entry(ExamCriteria.Verifications, "Vérifications et questions (sécurité, premiers secours)", vehicle, 3),
                Entry(ExamCriteria.Commands, "Connaître et utiliser les commandes", vehicle, 4),
                Entry(ExamCriteria.TakeInformation, "Prendre l'information", road, 3),
                Entry(ExamCriteria.AdaptSpeed, "Adapter son allure aux situations", road, 3),
                Entry(ExamCriteria.ApplyRegulations, "Appliquer la réglementation", road, 3, evaluated: true),
                Entry(ExamCriteria.Communicate, "Communiquer avec les autres usagers", share, 3),
                Entry(ExamCriteria.ShareRoadway, "Partager la chaussée", share, 3),
                Entry(ExamCriteria.SafetyGaps, "Maintenir des espaces de sécurité", share, 3),
                Entry(ExamCriteria.AutonomousDriving, "Conduite autonome", autonomous, 2),
                Entry(ExamCriteria.EcoDriving, "Conduite économique et respectueuse de l'environnement (bonus)", autonomous, 1, bonus: true),
                Entry(ExamCriteria.Courtesy, "Courtoisie (bonus)", autonomous, 1, bonus: true),
            };
        }

        private static CepcCriterionEntry Entry(string id, string label, string family, int maxPoints,
            bool bonus = false, bool evaluated = false)
        {
            return new CepcCriterionEntry
            {
                id = id,
                labelFr = label,
                familyFr = family,
                maxPoints = maxPoints,
                isBonus = bonus,
                isEvaluated = evaluated,
            };
        }
    }
}
