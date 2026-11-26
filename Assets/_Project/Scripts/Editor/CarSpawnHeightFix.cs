using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PermisVR.Editor
{
    /// One-off fix for the M1 scene's Car spawning with its wheels already embedded in the
    /// ground (WheelCollider radius 0.5 + wheels at local Y -0.3, with Car at world Y 0 means
    /// the wheel bottoms start 0.8m underground). That causes an explosive first-frame
    /// suspension collision that flings the Rigidbody (and the camera parented under it)
    /// into a wild spin that only settles after a few seconds — exactly the "spinning light"
    /// symptom reported in the headset. Raises Car so the wheels rest just above the ground.
    public static class CarSpawnHeightFix
    {
        [MenuItem("PermisVR/Scene/Fix Car Spawn Height")]
        public static void FixSpawnHeight()
        {
            var car = GameObject.Find("Car");
            if (car == null)
            {
                Debug.LogError("CarSpawnHeightFix: no 'Car' GameObject in the open scene.");
                return;
            }

            car.transform.position = new Vector3(car.transform.position.x, 0.9f, car.transform.position.z);

            var rigidbody = car.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.centerOfMass = new Vector3(0f, -0.3f, 0f);
            }

            EditorSceneManager.MarkSceneDirty(car.scene);
            EditorSceneManager.SaveScene(car.scene);
            Debug.Log("CarSpawnHeightFix: Car raised to a safe spawn height, center of mass lowered, scene saved.");
        }
    }
}
