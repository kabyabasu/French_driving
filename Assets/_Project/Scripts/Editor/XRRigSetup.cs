using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PermisVR.Editor
{
    /// Redoes, in one scripted step, what was previously done by hand in the Editor UI:
    /// runs Unity's own "Convert Main Camera To XR Rig" command, moves the resulting rig
    /// under Car/Cockpit, adds the hand trackers (HandTrackerSetup), removes the old
    /// LeftHandAnchor/RightHandAnchor placeholders, and saves the scene.
    public static class XRRigSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/M1_TestPlane.unity";

        [MenuItem("PermisVR/Scene/Setup XR Rig + Hand Trackers (Full)")]
        public static void SetupXRRigAndHandTrackers()
        {
            var car = GameObject.Find("Car");
            if (car == null)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                car = GameObject.Find("Car");
                if (car == null)
                {
                    Debug.LogError("XRRigSetup: no 'Car' GameObject found, even after opening " + ScenePath);
                    return;
                }
            }

            var cockpit = car.transform.Find("Cockpit");
            if (cockpit == null)
            {
                Debug.LogError("XRRigSetup: no 'Cockpit' under Car.");
                return;
            }

            if (FindDeepChild(cockpit, "Camera Offset") != null)
            {
                Debug.LogWarning("XRRigSetup: Cockpit already has a 'Camera Offset' rig — skipping the " +
                                  "conversion step and only (re)adding hand trackers.");
            }
            else if (!ConvertAndReparentCamera(cockpit))
            {
                return;
            }

            if (FindDeepChild(cockpit, "Camera Offset") == null)
            {
                Debug.LogError("XRRigSetup: no 'Camera Offset' found under Cockpit after conversion.");
                return;
            }

            HandTrackerSetup.AddHandTrackers();

            RemoveIfPresent(cockpit, "LeftHandAnchor");
            RemoveIfPresent(cockpit, "RightHandAnchor");
            RemoveStrayRootRig(car.scene);

            EditorSceneManager.MarkSceneDirty(car.scene);
            EditorSceneManager.SaveScene(car.scene);
            Debug.Log("XRRigSetup: XR rig moved under Car/Cockpit, hand trackers wired, old placeholders " +
                      "removed, scene saved to " + car.scene.path);
        }

        private static bool ConvertAndReparentCamera(Transform cockpit)
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("XRRigSetup: no Main Camera found in the scene.");
                return false;
            }

            Selection.activeGameObject = mainCamera.gameObject;
            EditorApplication.ExecuteMenuItem("GameObject/XR/Convert Main Camera To XR Rig");

            var rigRoot = mainCamera.transform;
            while (rigRoot.parent != null)
            {
                rigRoot = rigRoot.parent;
            }

            if (rigRoot == mainCamera.transform)
            {
                Debug.LogError("XRRigSetup: 'Convert Main Camera To XR Rig' didn't create a parent rig — " +
                                "check that menu path still matches the installed XR package version.");
                return false;
            }

            rigRoot.SetParent(cockpit, false);
            rigRoot.localPosition = Vector3.zero;
            rigRoot.localRotation = Quaternion.identity;
            SeatedRigFix.ZeroLegacyCameraOffset(rigRoot);
            return true;
        }

        private static void RemoveStrayRootRig(UnityEngine.SceneManagement.Scene scene)
        {
            // "Convert Main Camera To XR Rig" can leave a second, empty rig object at the
            // scene root in addition to the real one we just moved under Cockpit. The real
            // rig only ever belongs under Car/Cockpit, so anything named the same at root
            // level is the stray leftover.
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "XRRig" || root.name == "XR Rig")
                {
                    Debug.LogWarning("XRRigSetup: removing stray root-level '" + root.name + "' left over " +
                                      "from the conversion command (the real rig now lives under Car/Cockpit).");
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static void RemoveIfPresent(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            if (child != null)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    return child;
                }

                var found = FindDeepChild(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
