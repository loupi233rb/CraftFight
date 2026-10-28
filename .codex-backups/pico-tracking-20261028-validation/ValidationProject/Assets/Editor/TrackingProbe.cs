using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class TrackingProbe
{
    public static void Run()
    {
        var results = new List<string>();
        GameObject root = null;
        try
        {
            string assemblyPath = Path.GetFullPath("../TrackingCheck-1300b0aE.dll");
            Assembly assembly = Assembly.LoadFile(assemblyPath);
            Type type = assembly.GetType("PicoPlayerTracking", true);
            MethodInfo apply = type.GetMethod("ApplyHandPose", BindingFlags.Static | BindingFlags.NonPublic);
            root = new GameObject("Probe Player");
            root.SetActive(false);
            Transform model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            model.localRotation = Quaternion.Euler(7f, 32f, -5f);
            Transform left = MakeArm(model, "Left", new Vector3(-0.3f, 1.4f, 0f), out Transform leftPalm);
            Transform right = MakeArm(model, "Right", new Vector3(0.3f, 1.4f, 0f), out Transform rightPalm);
            Vector3 rightBefore = rightPalm.position;
            Vector3 target = new Vector3(-0.25f, 1.1f, 0.5f);
            Quaternion rotation = Quaternion.Euler(-35f, 62f, 19f);
            apply.Invoke(null, new object[] { left, leftPalm, target, rotation });
            Check(Vector3.Distance(leftPalm.position, target) < 0.0001f, "Left palm reaches tracked position", results);
            Check(Quaternion.Angle(left.rotation, rotation) < 0.01f, "Left rotation follows controller", results);
            Check(Vector3.Distance(rightPalm.position, rightBefore) < 0.0001f, "Left controller leaves right palm unchanged", results);
            root.transform.SetPositionAndRotation(new Vector3(9f, 3f, -7f), Quaternion.Euler(0f, 143f, 0f));
            root.transform.localScale = Vector3.one * 1.7f;
            Vector3 leftBefore = leftPalm.position;
            target = new Vector3(8.7f, 4.2f, -6.3f);
            rotation = Quaternion.Euler(25f, -35f, 48f);
            apply.Invoke(null, new object[] { right, rightPalm, target, rotation });
            Check(Vector3.Distance(rightPalm.position, target) < 0.0001f, "Right palm matches tracking under rotated and scaled ancestors", results);
            Check(Quaternion.Angle(right.rotation, rotation) < 0.01f, "Right rotation matches under rotated ancestors", results);
            Check(Vector3.Distance(leftPalm.position, leftBefore) < 0.0001f, "Right controller leaves left palm unchanged", results);
            var cameraObject = new GameObject("Head");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.668f, -0.162f);
            cameraObject.transform.localRotation = Quaternion.Euler(12f, 175f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            Component tracking = root.AddComponent(type);
            Set(type, tracking, "headCamera", camera);
            Set(type, tracking, "leftArm", left);
            Set(type, tracking, "rightArm", right);
            Set(type, tracking, "leftPalm", leftPalm);
            Set(type, tracking, "rightPalm", rightPalm);
            type.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tracking, null);
            Vector3 spawn = camera.transform.position;
            Vector3 headPosition = new Vector3(0.3f, 1.72f, -0.4f);
            type.GetMethod("InitializeOrigin", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tracking, new object[] { headPosition });
            Vector3 offset = (Vector3)Get(type, tracking, "originOffset");
            Quaternion heading = (Quaternion)Get(type, tracking, "originHeading");
            Quaternion spaceRotation = root.transform.rotation * heading;
            Vector3 spacePosition = root.transform.position + root.transform.rotation * offset;
            Check(Vector3.Distance(spacePosition + spaceRotation * headPosition, spawn) < 0.0001f,
                "Head calibration preserves the authored camera spawn", results);
            Vector3 delta = new Vector3(0.2f, -0.1f, 0.05f);
            Vector3 moved = spacePosition + spaceRotation * (headPosition + delta);
            Check(Mathf.Abs(Vector3.Distance(moved, spawn) - delta.magnitude) < 0.0001f,
                "Head translation stays in metres despite model scale", results);
            Check(!(bool)type.GetProperty("IsActive").GetValue(tracking), "No-headset mode leaves desktop input available", results);
            results.Add("ALL 9 CHECKS PASSED");
            results.Add("Synthetic poses only; physical PICO rendering and tracking have not been tested.");
            File.WriteAllLines("../TrackingProbeResults.txt", results);
            Debug.Log(string.Join("\n", results));
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            results.Add("FAIL: " + error);
            File.WriteAllLines("../TrackingProbeResults.txt", results);
            Debug.LogError(error);
            EditorApplication.Exit(1);
        }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static Transform MakeArm(Transform parent, string name, Vector3 position, out Transform palm)
    {
        Transform arm = new GameObject(name).transform;
        arm.SetParent(parent, false);
        arm.localPosition = position;
        palm = new GameObject(name + " Inventory").transform;
        palm.SetParent(arm, false);
        palm.localPosition = new Vector3(0f, -0.6f, 0f);
        return arm;
    }
    private static object Get(Type type, Component component, string name) =>
        type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(component);
    private static void Set(Type type, Component component, string name, object value) =>
        type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(component, value);
    private static void Check(bool condition, string description, List<string> results)
    {
        if (!condition) throw new Exception(description);
        results.Add("PASS: " + description);
    }
}
