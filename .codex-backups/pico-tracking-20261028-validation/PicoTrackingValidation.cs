using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PicoTrackingValidation
{
    private const string PrefabPath = "Assets/Prefabs/models/vr_steve.prefab";
    private const string ReportPath = "Docs/PicoTrackingValidation.txt";
    private const string SessionKey = "CraftFight.PicoTracking.Validation.20261028.v1";

    static PicoTrackingValidation() => EditorApplication.delayCall += ValidateOnce;

    private static void ValidateOnce()
    {
        if (SessionState.GetBool(SessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ValidateOnce;
            return;
        }
        SessionState.SetBool(SessionKey, true);
        Validate();
    }

    [MenuItem("Tools/CraftFight/Validate PICO hand tracking")]
    public static void Validate()
    {
        var results = new List<string>();
        Scene preview = default;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before running this editor validation.");
            Type trackingType = typeof(PlayerHandInventory).Assembly.GetType("PicoPlayerTracking", true);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Check(prefab != null, "Player prefab loads", results);
            preview = EditorSceneManager.NewPreviewScene();
            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            Component tracking = player.GetComponent(trackingType);
            Check(tracking != null, "Player prefab contains PICO tracking", results);
            var serialized = new SerializedObject(tracking);
            Transform left = Reference<Transform>(serialized, "leftArm");
            Transform right = Reference<Transform>(serialized, "rightArm");
            Transform leftPalm = Reference<Transform>(serialized, "leftPalm");
            Transform rightPalm = Reference<Transform>(serialized, "rightPalm");
            Camera camera = Reference<Camera>(serialized, "headCamera");
            Check(camera != null && camera.CompareTag("MainCamera"), "Head camera bound", results);
            Check(left != null && right != null && left != right, "Two independent arms bound", results);
            Check(leftPalm != null && leftPalm.IsChildOf(left), "Left inventory palm belongs to left arm", results);
            Check(rightPalm != null && rightPalm.IsChildOf(right), "Right inventory palm belongs to right arm", results);
            MethodInfo apply = trackingType.GetMethod("ApplyHandPose", BindingFlags.Static | BindingFlags.NonPublic);
            Check(apply != null, "Tracked pose method available", results);
            Vector3 rightBefore = rightPalm.position;
            Quaternion rightRotationBefore = right.rotation;
            Vector3 target = camera.transform.position + new Vector3(-0.3f, -0.25f, 0.4f);
            Quaternion rotation = Quaternion.Euler(-35f, 65f, 12f);
            apply.Invoke(null, new object[] { left, leftPalm, target, rotation });
            Check(Vector3.Distance(leftPalm.position, target) < 0.0001f,
                "Left palm reaches controller position", results);
            Check(Quaternion.Angle(left.rotation, rotation) < 0.01f,
                "Left arm follows controller rotation", results);
            Check(Vector3.Distance(rightPalm.position, rightBefore) < 0.0001f &&
                Quaternion.Angle(right.rotation, rightRotationBefore) < 0.01f,
                "Moving left controller does not move right hand", results);
            player.transform.SetPositionAndRotation(new Vector3(7f, 3f, -4f), Quaternion.Euler(0f, 123f, 0f));
            player.transform.localScale = Vector3.one * 1.7f;
            target = new Vector3(6.5f, 4.3f, -3.2f);
            rotation = Quaternion.Euler(23f, -62f, 35f);
            apply.Invoke(null, new object[] { right, rightPalm, target, rotation });
            Check(Vector3.Distance(rightPalm.position, target) < 0.0001f,
                "Right palm remains accurate under a scaled and rotated player", results);
            Check(Quaternion.Angle(right.rotation, rotation) < 0.01f,
                "Right rotation remains accurate under a rotated player", results);
            Check(player.GetComponentsInChildren<HandInventory>(true).Length == 2,
                "Both existing wrist inventories retained", results);
            results.Add("Active scene: " + SceneManager.GetActiveScene().path);
            results.Add("Active scene dirty: " + SceneManager.GetActiveScene().isDirty);
            results.Add("Play Mode: " + EditorApplication.isPlaying);
            results.Add("PASS: Editor compilation and transform checks complete.");
            results.Add("NOT TESTED: physical PICO headset/controller tracking, hand alignment and rendering.");
        }
        catch (Exception exception)
        {
            results.Add("FAIL: " + exception);
            Debug.LogError("PICO tracking validation failed: " + exception.Message);
        }
        finally
        {
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            Directory.CreateDirectory("Docs");
            File.WriteAllLines(ReportPath, results);
            Debug.Log("PICO tracking validation report: " + Path.GetFullPath(ReportPath));
        }
    }

    private static T Reference<T>(SerializedObject serialized, string name) where T : UnityEngine.Object =>
        serialized.FindProperty(name)?.objectReferenceValue as T;

    private static void Check(bool condition, string description, List<string> results)
    {
        if (!condition) throw new InvalidOperationException(description);
        results.Add("PASS: " + description);
    }
}
