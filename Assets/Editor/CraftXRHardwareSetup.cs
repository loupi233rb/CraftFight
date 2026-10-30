using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using Unity.XR.CoreUtils;

public static class CraftXRHardwareSetup
{
    [MenuItem("Tools/CraftFight/Repair headset tracking and continuous turn")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        var player = UnityEngine.Object.FindObjectOfType<CraftXRPlayer>();
        if (player == null) throw new InvalidOperationException("XR player is missing.");
        ConfigurePlayer(player);
        var prefab = PrefabUtility.LoadPrefabContents("Assets/Prefabs/models/xr_steve.prefab");
        try
        {
            ConfigurePlayer(prefab.GetComponent<CraftXRPlayer>());
            PrefabUtility.SaveAsPrefabAsset(prefab, "Assets/Prefabs/models/xr_steve.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        var simulator = Resources.FindObjectsOfTypeAll<XRDeviceSimulator>().FirstOrDefault(s =>
            s.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(s));
        if (simulator != null)
        {
            var gate = simulator.GetComponentInParent<CraftXRSimulatorGate>(true);
            if (gate == null)
            {
                var root = new GameObject("XR Simulation Mode (Editor only)");
                root.tag = "EditorOnly";
                simulator.transform.SetParent(root.transform, true);
                gate = root.AddComponent<CraftXRSimulatorGate>();
            }
            Set(gate, "simulator", simulator);
            simulator.removeOtherHMDDevices = false;
            simulator.gameObject.SetActive(true);
            PrefabUtility.SaveAsPrefabAssetAndConnect(gate.gameObject,
                "Assets/Prefabs/XRSimulation.prefab", InteractionMode.AutomatedAction);
        }
        var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
        if (settings != null)
        {
            // Editor Play and PC Link both use the Standalone OpenXR settings.
            settings.InitManagerOnStart = true;
            EditorUtility.SetDirty(settings);
        }
        var openXR = UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
        var profiles = new[] { "OculusTouchControllerProfile", "HTCViveControllerProfile", "ValveIndexControllerProfile", "MicrosoftMotionControllerProfile", "KHRSimpleControllerProfile" };
        if (openXR != null)
            foreach (var feature in openXR.GetFeatures<UnityEngine.XR.OpenXR.Features.OpenXRFeature>())
                if (profiles.Contains(feature.GetType().Name)) { feature.enabled = true; EditorUtility.SetDirty(feature); }
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[CraftXR] Physical headset auto-detection, shared floor tracking and 60 deg/s continuous turn saved.");
    }

    private static void ConfigurePlayer(CraftXRPlayer player)
    {
        var continuous = player.GetComponentInChildren<ActionBasedContinuousTurnProvider>(true);
        if (continuous == null) throw new InvalidOperationException("Continuous turn provider is missing.");
        continuous.turnSpeed = 60f;
        continuous.leftHandTurnAction = new InputActionProperty((InputActionReference)null);
        var move = player.GetComponentInChildren<ActionBasedContinuousMoveProvider>(true);
        if (move != null) move.rightHandMoveAction = new InputActionProperty((InputActionReference)null);
        foreach (var snap in player.GetComponentsInChildren<ActionBasedSnapTurnProvider>(true)) snap.enabled = false;
        continuous.enabled = true;
        Set(player, "turnProvider", continuous);
        player.Origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
        player.Origin.CameraYOffset = 1.67f;
        foreach (var manager in player.GetComponentsInChildren<ActionBasedControllerManager>(true))
        {
            manager.smoothMotionEnabled = manager.transform == player.LeftController;
            manager.smoothTurnEnabled = manager.transform == player.RightController;
        }
        foreach (var inventory in new[] { player.LeftInventory, player.RightInventory })
        {
            if (inventory == null) continue;
            var pose = new SerializedObject(inventory);
            // The Steve palm points along local -Y. Flip the old underside pose
            // around that axis so tools stand up while gun muzzles keep aiming forward.
            pose.FindProperty("heldItemRotation").vector3Value = new Vector3(0f, 180f, 0f);
            pose.FindProperty("heldItemOffset").vector3Value =
                new Vector3(inventory == player.LeftInventory ? -.01f : .01f, -.1f, .06f);
            // Local +Z is the top of the XR wrist; the panel's visible face is -Z.
            pose.FindProperty("panelOffset").vector3Value = new Vector3(0f, .2f, .14f);
            pose.FindProperty("panelRotation").vector3Value = new Vector3(180f, 0f, 90f);
            pose.ApplyModifiedPropertiesWithoutUndo();
            var fist = inventory.transform.Find("XR Fist Hitbox");
            if (fist == null)
            {
                fist = new GameObject("XR Fist Hitbox").transform;
                fist.SetParent(inventory.transform, false);
            }
            fist.localPosition = new Vector3(0f, -.04f, 0f);
            fist.localRotation = Quaternion.identity;
            fist.localScale = Vector3.one;
            fist.gameObject.layer = 2; // Ignore Raycast; still participates in monster contacts.
            var hitbox = fist.GetComponent<BoxCollider>();
            if (hitbox == null) hitbox = fist.gameObject.AddComponent<BoxCollider>();
            hitbox.center = Vector3.zero;
            hitbox.size = Vector3.one * .18f;
            hitbox.isTrigger = true;
            var body = fist.GetComponent<Rigidbody>();
            if (body == null) body = fist.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var contact = fist.GetComponent<CraftXRContactDamage>();
            if (contact == null) contact = fist.gameObject.AddComponent<CraftXRContactDamage>();
            contact.Configure(player.GetComponent<SteveMeleeAttack>(), inventory, true);
            EditorUtility.SetDirty(contact);

        }
        var legacy = player.GetComponent<PicoPlayerTracking>();
        if (legacy != null) legacy.enabled = false;
    }

    private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
