using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

public static class CraftXRMigration
{
    private const string Sample = "Assets/Samples/XR Interaction Toolkit/2.6.5/";
    [MenuItem("Tools/CraftFight/Configure XR simulator scene")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before configuring XR.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var player = UnityEngine.Object.FindObjectOfType<SteveHealth>().gameObject;
        if (player.GetComponent<CraftXRPlayer>() != null)
            throw new InvalidOperationException("Scene already migrated. Use Validate XR migration.");
        Undo.RegisterFullObjectHierarchyUndo(player, "Migrate player to XR");
        var oldCamera = player.GetComponentInChildren<Camera>();
        var setup = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>(Sample + "Starter Assets/Prefabs/XR Interaction Setup.prefab"));
        setup.transform.SetParent(player.transform, false);
        var origin = setup.GetComponentInChildren<XROrigin>();
        origin.Origin = player;
        origin.CameraYOffset = 1.67f;
        origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
        origin.CameraFloorOffsetObject.transform.localPosition = Vector3.up * 1.67f;
        player.transform.rotation = Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f);
        // The old player root owns collision and all game references.
        UnityEngine.Object.DestroyImmediate(origin.GetComponent<CharacterController>());
        var camera = origin.Camera;
        camera.tag = "MainCamera";
        camera.nearClipPlane = 0.04f;
        camera.farClipPlane = oldCamera.farClipPlane;
        camera.fieldOfView = 75f;
        camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        oldCamera.tag = "Untagged";
        oldCamera.gameObject.SetActive(false);
        var tracking = player.GetComponent<PicoPlayerTracking>();
        if (tracking != null) tracking.enabled = false;
        player.GetComponent<PlayerHandInventory>().enabled = false;
        foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.name == "body_cube" || renderer.name == "head_cube" ||
                renderer.name.EndsWith("leg_cube")) renderer.enabled = false;
        }
        Transform left = origin.transform.Find("Camera Offset/Left Controller");
        Transform right = origin.transform.Find("Camera Offset/Right Controller");
        var inventories = player.GetComponentsInChildren<HandInventory>(true);
        var leftInventory = inventories.Single(i => i.GetHandLabel() == "LEFT");
        var rightInventory = inventories.Single(i => i.GetHandLabel() == "RIGHT");
        var move = setup.GetComponentInChildren<ContinuousMoveProviderBase>(true);
        var turn = setup.GetComponentInChildren<ActionBasedContinuousTurnProvider>(true);
        move.moveSpeed = player.GetComponent<SteveFirstPersonController>().MoveSpeed;
        ((ActionBasedContinuousMoveProvider)move).rightHandMoveAction = new InputActionProperty((InputActionReference)null);
        turn.turnSpeed = 60f;
        turn.leftHandTurnAction = new InputActionProperty((InputActionReference)null);
        foreach (var provider in setup.GetComponentsInChildren<LocomotionProvider>(true))
            provider.enabled = provider == move || provider == turn;
        foreach (var manager in setup.GetComponentsInChildren<ActionBasedControllerManager>(true))
        {
            manager.smoothMotionEnabled = manager.transform == left;
            manager.smoothTurnEnabled = manager.transform == right;
            manager.uiScrollingEnabled = false;
            Set(manager, "m_TeleportModeActivate", null);
            Set(manager, "m_TeleportModeCancel", null);
        }
        foreach (var transform in setup.GetComponentsInChildren<Transform>(true))
            if (transform.name == "Teleport Interactor" || transform.name == "Poke Interactor" || transform.name == "Gaze Interactor" ||
                transform.name == "Climb Teleport") transform.gameObject.SetActive(false);
        foreach (var ray in setup.GetComponentsInChildren<XRRayInteractor>(true))
        {
            ray.maxRaycastDistance = 5f;
            ray.raycastTriggerInteraction = QueryTriggerInteraction.Collide;
            ray.enableUIInteraction = true;
        }
        var bridge = player.AddComponent<CraftXRPlayer>();
        Set(bridge, "origin", origin); Set(bridge, "moveProvider", move); Set(bridge, "turnProvider", turn);
        Set(bridge, "leftController", left); Set(bridge, "rightController", right);
        Set(bridge, "leftInventory", leftInventory); Set(bridge, "rightInventory", rightInventory);
        Set(bridge, "leftArm", leftInventory.transform.parent); Set(bridge, "rightArm", rightInventory.transform.parent);
        Set(player.GetComponent<SteveFirstPersonController>(), "viewCamera", camera.transform);
        foreach (var inventory in inventories)
        {
            Set(inventory, "viewCamera", camera.transform);
            var hand = (inventory == leftInventory ? left : right).gameObject.AddComponent<CraftXRHand>();
            var serialized = new SerializedObject(hand);
            serialized.FindProperty("leftHand").boolValue = inventory == leftInventory;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Set(hand, "inventory", inventory);
            Set(hand, "detector", inventory.GetComponentInChildren<HandPlacementDetector>(true));
            Set(hand, "rayInteractor", hand.transform.Find("Ray Interactor").GetComponent<XRRayInteractor>());
        }
        foreach (var hud in UnityEngine.Object.FindObjectsOfType<GameFlowHud>())
        {
            Set(hud, "headCamera", camera);
            hud.enabled = false;
        }
        player.AddComponent<CraftXRHud>();
        foreach (var events in UnityEngine.Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>())
            if (!events.transform.IsChildOf(setup.transform)) events.gameObject.SetActive(false);
        foreach (var hammer in UnityEngine.Object.FindObjectsOfType<Hammer>())
        {
            var grab = hammer.GetComponent<XRGrabInteractable>();
            if (grab == null) grab = hammer.gameObject.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            grab.throwOnDetach = true; grab.retainTransformParent = true;
            var grip = hammer.transform.Find("hand");
            if (grip == null) { grip = new GameObject("XR Grip").transform; grip.SetParent(hammer.transform, false); }
            grab.attachTransform = grip;
        }
        foreach (var target in UnityEngine.Object.FindObjectsOfType<AnvilManager>())
            AddSimple(target.gameObject);
        foreach (var target in UnityEngine.Object.FindObjectsOfType<global::Button>())
            AddSimple(target.gameObject);
        foreach (var path in new[] { "Assets/Prefabs/DroppedItem.prefab", "Assets/Prefabs/Craft/craft_slot.prefab" })
        {
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try { AddSimple(prefab); PrefabUtility.SaveAsPrefabAsset(prefab, path); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
        var simulator = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>(Sample + "XR Device Simulator/XR Device Simulator.prefab"));
        simulator.name = "XR Device Simulator (Editor only)";
        simulator.tag = "EditorOnly";
        simulator.GetComponent<XRDeviceSimulator>().cameraTransform = camera.transform;
        var xrSettings = AssetDatabase.LoadMainAssetAtPath(
            "Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset");
        if (xrSettings != null)
        {
            var settings = new SerializedObject(xrSettings);
            settings.FindProperty("m_AutomaticallyInstantiateSimulatorPrefab").boolValue = false;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        player.name = "XR Steve";
        PrefabUtility.SaveAsPrefabAssetAndConnect(player, "Assets/Prefabs/models/xr_steve.prefab",
            InteractionMode.AutomatedAction);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        CraftXRHardwareSetup.Apply();
        Debug.Log("[CraftXR] Migration saved. Press Play to use the headset or simulator automatically.");
    }

    private static void Set(UnityEngine.Object target, string property, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        var field = serialized.FindProperty(property);
        if (field == null) throw new InvalidOperationException(target.GetType().Name + "." + property);
        field.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void AddSimple(GameObject target)
    {
        if (target.GetComponent<XRSimpleInteractable>() == null) target.AddComponent<XRSimpleInteractable>();
    }

    [MenuItem("Tools/CraftFight/Validate XR migration")]
    public static void Validate()
    {
        var player = UnityEngine.Object.FindObjectOfType<CraftXRPlayer>();
        if (player == null) throw new Exception("XR player is missing.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var report = new System.Collections.Generic.List<string>();
        Action<bool, string> check = (ok, description) => {
            report.Add((ok ? "PASS: " : "FAIL: ") + description);
            if (!ok) Debug.LogError("[CraftXR] " + description);
        };
        check(player.Origin != null && player.Origin.Origin == player.gameObject, "XR origin moves the existing game player");
        check(player.HeadCamera != null && player.HeadCamera.CompareTag("MainCamera"), "Tracked main camera is assigned");
        check(UnityEngine.Object.FindObjectsOfType<Camera>().Count(c => c.enabled && c.CompareTag("MainCamera")) == 1, "One active main camera");
        check(UnityEngine.Object.FindObjectsOfType<AudioListener>().Count(a => a.enabled) == 1, "One active audio listener");
        check(player.GetComponent<PicoPlayerTracking>() == null || !player.GetComponent<PicoPlayerTracking>().enabled, "Legacy tracking is disabled");
        check(player.GetComponentsInChildren<CraftXRHand>().Length == 2, "Both semantic hand controllers are assigned");
        check(player.LeftInventory != null && player.RightInventory != null &&
            player.LeftInventory != player.RightInventory, "Two independent wrist inventories");
        check(UnityEngine.Object.FindObjectsOfType<XRDeviceSimulator>().Length == 1, "One XR device simulator");
        check(UnityEngine.Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>().Length == 1, "One active event system");
        check(UnityEngine.Object.FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>().Length == 1, "XR UI input module is active");
        check(player.GetComponent<CraftXRHud>() != null, "World-space game and trading UI");
        check(UnityEngine.Object.FindObjectsOfType<Hammer>().All(h => h.GetComponent<XRGrabInteractable>() != null), "Hammers support XR grip selection");
        check(UnityEngine.Object.FindObjectsOfType<GameFlowTimer>().Length == 1, "Existing three-round game flow retained");
        check(scene.GetRootGameObjects().Sum(g => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(g)) == 0, "Scene root scripts resolve");
        report.Add("Scene: " + scene.path);
        report.Add("Automated scene checks only; physical alignment and comfort require headset verification.");
        Directory.CreateDirectory("Docs");
        File.WriteAllLines("Docs/XRMigrationValidation.txt", report);
        Debug.Log(string.Join("\n", report));
    }
}
