#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

/// <summary>Editor-only integration checks using the same simulated XR device layouts as XR Device Simulator.</summary>
public sealed class CraftXRPlayModeValidation : MonoBehaviour
{
    public static bool Finished { get; private set; }
    public static string Stage { get; private set; }
    private readonly List<string> report = new List<string>();
    private readonly List<GameObject> temporary = new List<GameObject>();
    private XRDeviceSimulator simulator;
    private XRSimulatedHMD headDevice;
    private XRSimulatedController leftDevice;
    private XRSimulatedController rightDevice;
    private XRSimulatedHMDState headState;
    private XRSimulatedControllerState leftState;
    private XRSimulatedControllerState rightState;
    private CraftXRPlayer player;
    private GameFlowTimer flow;
    private bool cleaned;
    private bool restoreSimulator;
    private CraftXRSimulatorGate gate;
    private CraftXRSimulatorGate.InputMode originalMode;
    private UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager inputManager;
    private readonly List<UnityEngine.InputSystem.InputDevice> disabledPhysicalDevices = new List<UnityEngine.InputSystem.InputDevice>();
    private readonly List<UnityEngine.InputSystem.InputDevice> temporaryDevices = new List<UnityEngine.InputSystem.InputDevice>();

    [UnityEditor.MenuItem("Tools/CraftFight/Run XR simulator integration checks")]
    public static void Begin()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        if (FindObjectOfType<CraftXRPlayModeValidation>() != null) return;
        Finished = false;
        new GameObject("XR Integration Validation").AddComponent<CraftXRPlayModeValidation>();
    }

    private IEnumerator Start()
    {
        player = CraftXRPlayer.Active;
        flow = FindObjectOfType<GameFlowTimer>();
        gate = CraftXRSimulatorGate.Active;
        originalMode = gate != null ? gate.Mode : CraftXRSimulatorGate.InputMode.Auto;
        inputManager = player.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager>();
        if (inputManager != null) inputManager.enabled = false;
        foreach (var device in InputSystem.devices.ToArray())
        {
            if ((device is UnityEngine.InputSystem.XR.XRHMD && !(device is XRSimulatedHMD)) ||
                (device is UnityEngine.InputSystem.XR.XRController && !(device is XRSimulatedController)))
            {
                if (device.enabled) { disabledPhysicalDevices.Add(device); InputSystem.DisableDevice(device); }
            }
        }
        if (gate != null) gate.Mode = CraftXRSimulatorGate.InputMode.Simulator;
        simulator = Resources.FindObjectsOfTypeAll<XRDeviceSimulator>().FirstOrDefault(s => s.gameObject.scene.IsValid());
        restoreSimulator = simulator != null && simulator.enabled;
        if (simulator != null) simulator.enabled = false;
        headDevice = InputSystem.AddDevice<XRSimulatedHMD>();
        leftDevice = InputSystem.AddDevice<XRSimulatedController>();
        rightDevice = InputSystem.AddDevice<XRSimulatedController>();
        InputSystem.SetDeviceUsage(leftDevice, UnityEngine.InputSystem.CommonUsages.LeftHand);
        InputSystem.SetDeviceUsage(rightDevice, UnityEngine.InputSystem.CommonUsages.RightHand);
        headState = new XRSimulatedHMDState { isTracked = true, trackingState = 3,
            centerEyeRotation = Quaternion.identity, deviceRotation = Quaternion.identity,
            leftEyeRotation = Quaternion.identity, rightEyeRotation = Quaternion.identity };
        leftState = new XRSimulatedControllerState { isTracked = true, trackingState = 3,
            devicePosition = new Vector3(-0.25f, -0.3f, 0.5f), deviceRotation = Quaternion.identity };
        rightState = leftState;
        rightState.devicePosition = new Vector3(0.25f, -0.3f, 0.5f);
        if (inputManager != null) inputManager.enabled = true;
        InputSystem.onBeforeUpdate += Feed;
        var sequence = Checks();
        while (true)
        {
            object current = null;
            bool next = false;
            try { next = sequence.MoveNext(); if (next) current = sequence.Current; }
            catch (Exception exception) { report.Add("FAIL: " + Stage + ": " + exception); break; }
            if (!next) break;
            yield return current;
        }
        Cleanup();
        report.Add("Physical headset, hardware haptics and Android performance: NOT TESTED.");
        Directory.CreateDirectory("Docs");
        File.WriteAllLines("Docs/XRPlayModeValidation.txt", report);
        Finished = true;
        Stage = "Finished";
        Debug.Log("[XR Integration]\n" + string.Join("\n", report));
    }

    private void Feed()
    {
        if (headDevice != null && headDevice.added) InputSystem.QueueStateEvent(headDevice, headState);
        if (leftDevice != null && leftDevice.added) InputSystem.QueueStateEvent(leftDevice, leftState);
        if (rightDevice != null && rightDevice.added) InputSystem.QueueStateEvent(rightDevice, rightState);
    }
    private void Check(bool ok, string description)
    {
        report.Add((ok ? "PASS: " : "FAIL: ") + description);
        if (!ok) {
            foreach (var hand in player.GetComponentsInChildren<CraftXRHand>()) {
                var ray = hand.GetComponentInChildren<XRRayInteractor>();
                UnityEngine.EventSystems.RaycastResult hit;
                string ui = ray != null && ray.TryGetCurrentUIRaycastResult(out hit) && hit.gameObject != null ? hit.gameObject.name : "none";
                report.Add("DETAIL: " + hand.name + " target=" + (hand.Detector.selectedObject != null ? hand.Detector.selectedObject.name : "none") + " ui=" + ui + " item=" + hand.Inventory.GetSelectedItemName());
            }
        }
        if (!ok) Debug.LogWarning("[XR Integration] " + description);
    }
    private IEnumerator Pulse(bool left, ControllerButton button)
    {
        if (left) leftState = leftState.WithButton(button); else rightState = rightState.WithButton(button);
        if (button == ControllerButton.TriggerButton) { if (left) leftState.trigger = 1f; else rightState.trigger = 1f; }
        yield return new WaitForSeconds(0.12f);
        if (left) leftState = leftState.WithButton(button, false); else rightState = rightState.WithButton(button, false);
        if (button == ControllerButton.TriggerButton) { if (left) leftState.trigger = 0f; else rightState.trigger = 0f; }
        yield return new WaitForSeconds(0.12f);
    }
    private void Aim(bool left, Vector3 target, float distance = 0.35f)
    {
        Vector3 position = target - player.HeadCamera.transform.forward * distance + Vector3.up * 0.25f;
        Transform offset = player.Origin.CameraFloorOffsetObject.transform;
        Quaternion rotation = Quaternion.Inverse(offset.rotation) * Quaternion.LookRotation(target - position);
        if (left) { leftState.devicePosition = offset.InverseTransformPoint(position); leftState.deviceRotation = rotation; }
        else { rightState.devicePosition = offset.InverseTransformPoint(position); rightState.deviceRotation = rotation; }
    }
    private IEnumerator TriggerAt(bool left, Vector3 target)
    {
        Aim(left, target);
        yield return new WaitForSeconds(0.4f);
        yield return Pulse(left, ControllerButton.TriggerButton);
    }
    private static Vector3 Center(GameObject target)
    {
        var collider = target.GetComponentInChildren<Collider>();
        return collider != null ? collider.bounds.center : target.transform.position;
    }
    private DroppedItem SpawnDrop(string name, Vector3 position)
    {
        var instance = Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DroppedItem.prefab"));
        temporary.Add(instance);
        var item = instance.GetComponent<DroppedItem>();
        item.Init(position, Vector3.zero, name);
        item.GetComponent<Rigidbody>().isKinematic = true;
        return item;
    }
    private IEnumerator Checks()
    {
        Stage = "Tracking";
        yield return new WaitForSeconds(0.2f);
        Vector3 rightBefore = player.RightController.position;
        leftState.devicePosition += Vector3.left * 0.15f;
        yield return new WaitForSeconds(0.15f);
        Check(Vector3.Distance(rightBefore, player.RightController.position) < 0.005f, "Left tracking does not move right controller");
        Check(Vector3.Distance(player.LeftInventory.transform.position, player.LeftController.position) < 0.005f &&
            Vector3.Distance(player.RightInventory.transform.position, player.RightController.position) < 0.005f,
            "Both Steve palms follow simulated controllers");
        Check(Vector3.Distance(player.HeadCamera.transform.position,
            player.Origin.CameraFloorOffsetObject.transform.TransformPoint(headState.centerEyePosition)) < 0.005f,
            "Tracked head follows simulated HMD");
        yield return TrackingSpaceChecks();

        Stage = "Start";
        yield return Pulse(false, ControllerButton.TriggerButton);
        Check(flow.CurrentPhase == GameFlowTimer.Phase.Preparation, "Simulated trigger starts the existing round flow");

        Stage = "Locomotion";
        Vector3 before = player.transform.position;
        leftState.primary2DAxis = new Vector2(0f, 1f);
        yield return new WaitForSeconds(0.3f);
        leftState.primary2DAxis = Vector2.zero;
        yield return new WaitForSeconds(0.1f);
        Check(Vector3.Distance(before, player.transform.position) > 0.05f, "Left simulated stick drives standard XRI movement");
        float yaw = player.transform.eulerAngles.y;
        rightState.primary2DAxis = new Vector2(1f, 0f);
        yield return new WaitForSeconds(.1f);
        float earlyTurn = Mathf.Abs(Mathf.DeltaAngle(yaw, player.transform.eulerAngles.y));
        yield return new WaitForSeconds(.2f);
        float laterTurn = Mathf.Abs(Mathf.DeltaAngle(yaw, player.transform.eulerAngles.y));
        rightState.primary2DAxis = Vector2.zero;
        yield return new WaitForSeconds(.1f);
        Check(earlyTurn > 1f && laterTurn > earlyTurn + 3f && laterTurn < 28f,
            "Right stick rotates continuously over time instead of jumping in 30 degree steps");
        float releasedYaw = player.transform.eulerAngles.y;
        yield return new WaitForSeconds(.15f);
        Check(Mathf.Abs(Mathf.DeltaAngle(releasedYaw, player.transform.eulerAngles.y)) < .1f,
            "Continuous turning stops when the stick is released");
        var movement = player.GetComponent<SteveFirstPersonController>();
        movement.SetMovementSuspended(true);
        yield return new WaitForSeconds(0.1f);
        before = player.transform.position;
        leftState.primary2DAxis = Vector2.up;
        yield return new WaitForSeconds(0.2f);
        leftState.primary2DAxis = Vector2.zero;
        Check(Vector3.Distance(before, player.transform.position) < 0.01f, "Round movement suspension blocks XRI locomotion");
        movement.SetMovementSuspended(false);

        Stage = "Inventory";
        var leftInventory = player.LeftInventory;
        var rightInventory = player.RightInventory;
        int leftIndex = leftInventory.SelectedIndex, rightIndex = rightInventory.SelectedIndex;
        yield return Pulse(true, ControllerButton.PrimaryButton);
        Check(leftInventory.SelectedIndex == (leftIndex + 1) % 3 && rightInventory.SelectedIndex == rightIndex,
            "Left face button cycles only the left three-slot inventory");
        string droppedName = rightInventory.GetSelectedItemName();
        yield return Pulse(false, ControllerButton.SecondaryButton);
        Check(string.IsNullOrEmpty(rightInventory.GetSelectedItemName()) &&
            FindObjectsOfType<DroppedItem>().Any(d => d.itemName == droppedName), "Secondary button drops the selected item");
        var pickup = SpawnDrop("oak_plank", player.transform.position + Vector3.up * 1.1f + player.transform.forward);
        yield return new WaitForSeconds(0.1f);
        yield return TriggerAt(false, Center(pickup.gameObject));
        Check(rightInventory.GetSelectedItemName() == "oak_plank" && pickup == null, "Trigger ray transfers a pickup into the hand");
        var rejected = SpawnDrop("iron_ingot", player.transform.position + Vector3.up * 1.1f + player.transform.forward);
        yield return new WaitForSeconds(0.1f);
        yield return TriggerAt(false, Center(rejected.gameObject));
        Check(rejected != null && rejected.gameObject.activeInHierarchy && rightInventory.GetSelectedItemName() == "oak_plank",
            "Full selected slot rejects pickup without destroying it");
        if (rejected != null) rejected.gameObject.SetActive(false);

        yield return RayInteractionChecks();

        Stage = "Crafting";
        var table = FindObjectOfType<CraftTable>();
        var slots = table.GetComponentsInChildren<CraftSlot>();
        foreach (var slot in slots) slot.SetSlotItem(string.Empty);
        leftInventory.RemoveItem(); leftInventory.AddItem("oak_plank");
        yield return TriggerAt(true, Center(slots.Single(s => s.index == 0).gameObject));
        Check(slots.Single(s => s.index == 0).capturedItemName == "oak_plank" &&
            leftInventory.GetSelectedItemName() == "", "Trigger places the original item into an empty craft slot");
        leftInventory.AddItem("oak_plank");
        yield return TriggerAt(true, Center(slots.Single(s => s.index == 3).gameObject));
        var craftButton = FindObjectOfType<global::Button>();
        yield return TriggerAt(true, Center(craftButton.gameObject));
        var output = slots.Single(s => s.isOutputSlot);
        Check(output.capturedItemName == "stick", "Trigger presses the workbench button and produces a real recipe");
        yield return TriggerAt(true, Center(output.gameObject));
        Check(leftInventory.GetSelectedItemName() == "stick" && output.capturedItemName == "",
            "Output can be collected without writing invalid input slot 9");

        Stage = "Anvil";
        leftInventory.RemoveItem(); leftInventory.AddItem("iron_ingot");
        var anvil = FindObjectOfType<AnvilManager>();
        yield return TriggerAt(true, Center(anvil.gameObject));
        Check(anvil.captured && leftInventory.GetSelectedItemName() == "", "Trigger places iron into the anvil");
        var hammer = FindObjectOfType<Hammer>();
        var grab = hammer.GetComponent<XRGrabInteractable>();
        yield return HammerClickChecks(hammer, anvil);
        Transform grip = grab.attachTransform != null ? grab.attachTransform : hammer.transform;
        Transform offset = player.Origin.CameraFloorOffsetObject.transform;
        rightState.devicePosition = offset.InverseTransformPoint(grip.position);
        rightState.deviceRotation = Quaternion.Inverse(offset.rotation) * grip.rotation;
        rightState.grip = 1f;
        rightState = rightState.WithButton(ControllerButton.GripButton);
        yield return new WaitForSeconds(0.3f);
        Check(grab.isSelected, "Simulated grip selects the physical XR hammer");
        Check(!rightInventory.IsInventoryEnabled, "Physical hammer grip locks the same hand's inventory");
        string storedWhileGripping = rightInventory.GetSelectedItemName();
        yield return Pulse(false, ControllerButton.SecondaryButton);
        Check(rightInventory.GetSelectedItemName() == storedWhileGripping,
            "Drop button cannot remove a stored item while the hand grips a physical tool");
        var point = hammer.transform.Find("point") ?? hammer.transform;
        Vector3 pointFromGrip = point.position - grip.position;
        Vector3 anvilCenter = Center(anvil.gameObject);
        for (int swing = 0; swing < 3; swing++)
        {
            Vector3 high = anvilCenter + Vector3.up * 0.65f;
            rightState.devicePosition = offset.InverseTransformPoint(high - pointFromGrip);
            yield return new WaitForSeconds(0.12f);
            for (int step = 1; step <= 4; step++)
            {
                Vector3 hitPoint = Vector3.Lerp(high, anvilCenter, step / 4f);
                rightState.devicePosition = offset.InverseTransformPoint(hitPoint - pointFromGrip);
                yield return new WaitForFixedUpdate();
            }
            yield return new WaitForSeconds(0.08f);
        }
        Check(anvil.process > 0f, "Moving the gripped XR hammer physically advances anvil processing");
        rightState.grip = 0f;
        rightState = rightState.WithButton(ControllerButton.GripButton, false);
        yield return new WaitForSeconds(0.2f);
        Check(!grab.isSelected && rightInventory.IsInventoryEnabled, "Releasing grip restores that hand's inventory");
        for (int i = 0; i < 12 && anvil.process < 1f; i++) anvil.Process(20f);
        Check(anvil.process >= 1f, "The original anvil process reaches the completed iron plate state");
        yield return TriggerAt(true, Center(anvil.gameObject));
        Check(leftInventory.GetSelectedItemName() == "iron_plate" && !anvil.captured,
            "Processed iron plate can be retrieved through the same XR interaction");

        Stage = "Trading UI";
        var economy = player.GetComponent<PlayerEconomy>();
        var trader = FindObjectOfType<VillagerTrader>();
        movement.TeleportTo(trader.transform.position - Vector3.forward * 1.8f + Vector3.up * 0.1f, 0f);
        economy.AddCoins(3);
        yield return new WaitForSeconds(0.2f);
        yield return TriggerAt(false, Center(trader.gameObject));
        Check(economy.CurrentTrader == trader, "Controller ray opens a nearby trader");
        if (economy.CurrentTrader != null)
        {
            var upgrade = GameObject.Find("XR Trade/Upgrade 0");
            int coins = economy.Coins;
            float speed = movement.MoveSpeed;
            yield return TriggerAt(false, upgrade.transform.position);
            Check(economy.Coins < coins && movement.MoveSpeed > speed,
                "XR UI ray click purchases an upgrade and updates gameplay stats");
            var close = GameObject.Find("XR Trade/Close");
            yield return TriggerAt(false, close.transform.position);
            Check(economy.CurrentTrader == null, "XR UI close button returns control to gameplay");
        }

        economy.CloseTrade();
        yield return ContactCombatChecks();

        yield return FirearmChecks();

        Stage = "Round lifecycle";
        foreach (var slot in slots) slot.SetSlotItem(string.Empty);
        for (int i = 0; i < 3; i++)
        {
            typeof(GameFlowTimer).GetField("deadline", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(flow, Time.time - 1f);
            yield return new WaitForSeconds(0.2f);
            Check(flow.CurrentPhase == GameFlowTimer.Phase.Combat, "Round " + (i + 1) + " enters combat with XR player");
            if (i == 0 || i == 2) yield return ClearLootChecks(i == 2);
            else
            {
                typeof(GameFlowTimer).GetField("deadline", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(flow, Time.time - 1f);
                yield return new WaitForSeconds(0.2f);
                Check(flow.CurrentPhase == GameFlowTimer.Phase.Preparation, "Combat timeout retains the existing transition to preparation");
            }
        }
        Check(flow.CurrentPhase == GameFlowTimer.Phase.Completed, "Three-round lifecycle completes and teleports the XR player");
    }




    private IEnumerator TrackingSpaceChecks()
    {
        Stage = "Headset tracking space";
        var gate = CraftXRSimulatorGate.Active;
        Check(gate != null && gate.UsingSimulation, "Offline simulation starts in simulator mode");
        Check(simulator != null && !simulator.removeOtherHMDDevices,
            "Simulator is configured to preserve physical headset devices");
        var oldHead = headState;
        var oldLeft = leftState;
        var oldRight = rightState;
        Vector3 oldPosition = player.transform.position;
        float oldYaw = player.HeadCamera.transform.eulerAngles.y;
        var physical = InputSystem.AddDevice<UnityEngine.InputSystem.XR.XRHMD>();
        temporaryDevices.Add(physical);
        gate.Mode = CraftXRSimulatorGate.InputMode.Auto;
        gate.RefreshMode();
        headState.centerEyePosition = headState.devicePosition = new Vector3(.2f, 1.65f, .1f);
        leftState.devicePosition = new Vector3(-.25f, 1.2f, .4f);
        rightState.devicePosition = new Vector3(.25f, 1.25f, .4f);
        yield return new WaitForSeconds(.35f);
        Check(!gate.UsingSimulation && !simulator.gameObject.activeInHierarchy && physical.added,
            "A physical-headset layout disables simulation without deleting that headset");
        Check(player.Origin.RequestedTrackingOriginMode == Unity.XR.CoreUtils.XROrigin.TrackingOriginMode.Floor &&
            Mathf.Abs(player.Origin.CameraFloorOffsetObject.transform.localPosition.y) < .001f &&
            Mathf.Abs(player.Origin.CameraInOriginSpaceHeight - 1.65f) < .01f,
            "Floor-space headset height is used directly without adding another eye-height offset");
        Check(player.HeadCamera.transform.position.y > player.LeftController.position.y &&
            player.HeadCamera.transform.position.y > player.RightController.position.y &&
            Vector3.Distance(player.LeftInventory.transform.position, player.LeftController.position) < .005f,
            "Floor-space controllers stay below the head and visible palms follow those same poses");
        headState.centerEyePosition = headState.devicePosition = new Vector3(.2f, 1.1f, .1f);
        yield return new WaitForSeconds(.15f);
        Check(Mathf.Abs(player.Origin.CameraInOriginSpaceHeight - 1.1f) < .01f,
            "Physical crouching changes eye height instead of being overridden by a fixed camera height");
        player.TeleportTo(new Vector3(91f, 20f, 12f), 75f);
        Check(Mathf.Abs(player.transform.position.y - 20f) < .001f &&
            Mathf.Abs(player.Origin.CameraInOriginSpaceHeight - 1.1f) < .01f,
            "Round teleport preserves physical height and places the rig floor on the destination");
        player.HeadCamera.transform.localPosition = Vector3.zero;
        player.TeleportTo(new Vector3(92f, 21f, 13f), 0f);
        Check(Mathf.Abs(player.transform.position.y - 21f) < .001f,
            "Teleport before the first tracked pose does not invent a half-metre height offset");
        InputSystem.RemoveDevice(physical);
        gate.Mode = CraftXRSimulatorGate.InputMode.Simulator;
        headState = oldHead; leftState = oldLeft; rightState = oldRight;
        yield return new WaitForSeconds(.35f);
        player.TeleportTo(oldPosition, oldYaw);
        Check(Mathf.Abs(player.Origin.CameraFloorOffsetObject.transform.localPosition.y - 1.67f) < .01f,
            "Returning to simulator mode restores one shared 1.67 metre fallback offset");
    }

    private IEnumerator RayInteractionChecks()
    {
        Stage = "Remote ray interaction";
        var movement = player.GetComponent<SteveFirstPersonController>();
        Vector3 oldPosition = player.transform.position;
        float oldYaw = player.HeadCamera.transform.eulerAngles.y;
        var oldLeft = leftState;
        var oldRight = rightState;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        temporary.Add(floor);
        floor.name = "XR Remote Interaction Floor";
        floor.transform.position = new Vector3(200f, 49.5f, 200f);
        floor.transform.localScale = new Vector3(30f, 1f, 30f);
        movement.TeleportTo(new Vector3(200f, 50.1f, 200f), 0f);
        rightState.devicePosition = new Vector3(.25f, -.3f, .5f);
        rightState.deviceRotation = Quaternion.identity;
        leftState.devicePosition = new Vector3(-.25f, -.3f, .5f);
        leftState.deviceRotation = Quaternion.identity;
        var inventory = player.RightInventory;
        inventory.RemoveItem();
        yield return new WaitForSeconds(.3f);
        var hand = player.GetComponentsInChildren<CraftXRHand>().Single(h => h.Inventory == inventory);
        var ray = hand.GetComponentInChildren<XRRayInteractor>();
        Vector3 origin, direction;
        ray.GetLineOriginAndDirection(out origin, out direction);
        var distant = SpawnDrop("oak_plank", origin + direction * 3.8f);
        var near = SpawnDrop("iron_ingot", origin + player.RightController.right * .3f);
        yield return new WaitForSeconds(.1f);
        var nearCollider = near.GetComponent<Collider>();
        near.transform.position = origin + player.RightController.right * (nearCollider.bounds.extents.x + .1f);
        yield return new WaitForSeconds(.3f);
        Check(hand.Detector.selectedObject == distant.gameObject && hand.CanUseRayTarget,
            "The visible ray selects a pickup at 3.8 metres instead of a nearby off-ray item");
        var marker = GameObject.Find("Interaction Ray Hit RIGHT");
        Check(marker != null && marker.activeInHierarchy && !string.IsNullOrEmpty(hand.Hint),
            "Ray-selected items display a hit marker and an interaction hint");
        yield return Pulse(false, ControllerButton.TriggerButton);
        Check(distant == null && inventory.GetSelectedItemName() == "oak_plank",
            "Simulated trigger collects a distant pickup into the same hand's selected slot");
        var rejected = SpawnDrop("flint", origin + direction * 3.8f);
        yield return new WaitForSeconds(.3f);
        Check(hand.Detector.selectedObject == rejected.gameObject && !hand.CanUseRayTarget &&
            hand.Hint.Contains("满"), "A full slot explains why the ray-targeted item cannot be collected");
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        temporary.Add(wall);
        wall.transform.position = origin + direction * 1.8f;
        wall.transform.localScale = new Vector3(1f, 2f, .2f);
        yield return new WaitForSeconds(.3f);
        Check(!hand.HasRayTarget, "Solid walls block ray interaction with pickups behind them");
        wall.SetActive(false);
        inventory.RemoveItem();
        rejected.transform.position = origin + direction * 5.6f;
        yield return new WaitForSeconds(.3f);
        yield return Pulse(false, ControllerButton.TriggerButton);
        Check(!hand.HasRayTarget && rejected != null && inventory.GetSelectedItemName() == "",
            "Items beyond five metres cannot be selected or collected");
        Check(marker == null || !marker.activeInHierarchy, "The interaction marker hides when the ray has no target");
        rejected.gameObject.SetActive(false);
        near.gameObject.SetActive(false);
        inventory.AddItem("oak_plank");
        leftState = oldLeft;
        rightState = oldRight;
        movement.TeleportTo(oldPosition, oldYaw);
        yield return new WaitForSeconds(.15f);
        floor.SetActive(false);
    }



    private IEnumerator HammerClickChecks(Hammer hammer, AnvilManager anvil)
    {
        Stage = "Click-to-hold hammer";
        var left = player.LeftInventory;
        var right = player.RightInventory;
        var oldLeft = leftState;
        var oldRight = rightState;
        var grab = hammer.GetComponent<XRGrabInteractable>();
        var rightHand = player.RightController.GetComponent<CraftXRHand>();
        var leftHand = player.LeftController.GetComponent<CraftXRHand>();
        var ray = player.RightController.GetComponentInChildren<XRRayInteractor>();
        bool previousForceGrab = ray.useForceGrab;
        string stored = right.GetSelectedItemName();
        int selected = right.SelectedIndex;
        yield return TriggerAt(false, Center(hammer.gameObject));
        yield return new WaitForSeconds(.25f);
        Transform grip = grab.attachTransform;
        Check(grab.isSelected && rightHand.IsHoldingHammer && ray.isPerformingManualInteraction &&
            Vector3.Distance(grip.position, player.RightController.position) < .1f,
            "A single trigger ray interaction brings the hammer grip directly to the right hand and keeps holding after release");
        Check(!right.IsInventoryEnabled && left.IsInventoryEnabled &&
            !right.transform.Find("XR Fist Hitbox").GetComponent<Collider>().enabled,
            "Click-holding a hammer disables only that hand's inventory and fist hitbox");
        yield return Pulse(false, ControllerButton.PrimaryButton);
        Check(right.SelectedIndex == selected && right.GetSelectedItemName() == stored,
            "A click-held hammer locks inventory switching while preserving the stored selected item");
        yield return TriggerAt(true, Center(hammer.gameObject));
        Check(rightHand.IsHoldingHammer && !leftHand.IsHoldingHammer,
            "The other hand cannot steal an already held hammer with a trigger interaction");
        var point = hammer.transform.Find("point") ?? hammer.transform;
        Vector3 fromGrip = point.position - grip.position;
        Vector3 center = Center(anvil.gameObject);
        Transform offset = player.Origin.CameraFloorOffsetObject.transform;
        float before = anvil.process;
        for (int swing = 0; swing < 3; swing++)
        {
            rightState.devicePosition = offset.InverseTransformPoint(center + Vector3.up * .65f - fromGrip);
            yield return new WaitForSeconds(.12f);
            for (int step = 1; step <= 4; step++)
            {
                rightState.devicePosition = offset.InverseTransformPoint(
                    Vector3.Lerp(center + Vector3.up * .65f, center, step / 4f) - fromGrip);
                yield return new WaitForFixedUpdate();
            }
            yield return new WaitForSeconds(.08f);
        }
        Check(anvil.process > before,
            "The click-held hammer physically processes iron through the original Hammer and AnvilManager logic");
        yield return Pulse(false, ControllerButton.SecondaryButton);
        Check(!grab.isSelected && !rightHand.IsHoldingHammer && right.IsInventoryEnabled &&
            right.GetSelectedItemName() == stored && ray.useForceGrab == previousForceGrab,
            "The secondary button releases a click-held hammer and restores inventory without dropping a stored item");

        var body = hammer.GetComponent<Rigidbody>();
        bool oldKinematic = body.isKinematic;
        body.isKinematic = true;
        hammer.transform.position = center + Vector3.up * .8f + Vector3.right * .5f;
        Physics.SyncTransforms();
        yield return TriggerAt(true, Center(hammer.gameObject));
        yield return new WaitForSeconds(.2f);
        Check(leftHand.IsHoldingHammer && !left.IsInventoryEnabled && right.IsInventoryEnabled,
            "The left hand can also pick up and latch the hammer through a single trigger interaction");
        yield return Pulse(true, ControllerButton.SecondaryButton);
        Check(!leftHand.IsHoldingHammer && left.IsInventoryEnabled && !grab.isSelected,
            "Releasing a left-hand hammer restores the left inventory");
        body.isKinematic = oldKinematic;
        leftState = oldLeft;
        rightState = oldRight;
        // The original grip/processing checks below start with an unprocessed ingot.
        anvil.SlotUpdate(true);
        yield return new WaitForSeconds(.2f);
    }

    private IEnumerator ClearLootChecks(bool finalRound)
    {
        Stage = "Post-clear coin collection";
        var spawned = (List<MonsterHealth>)typeof(MonsterSpawner)
            .GetField("spawned", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(flow.Monsters);
        typeof(MonsterSpawner).GetField("interval", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(flow.Monsters, 0f);
        typeof(MonsterSpawner).GetField("nextSpawnAt", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(flow.Monsters, Time.time - 1f);
        yield return new WaitForSeconds(.2f);
        Vector3 combatPosition = player.transform.position;
        int round = flow.RoundNumber;
        foreach (var monster in spawned.ToArray())
            if (monster != null && !monster.IsDead) monster.TakeDamage(999, player.transform.position);
        yield return new WaitForSeconds(.1f);
        Check(flow.CurrentPhase == GameFlowTimer.Phase.Loot && flow.RoundNumber == round &&
            flow.RemainingSeconds > 4f && Vector3.Distance(player.transform.position, combatPosition) < .3f,
            (finalRound ? "Final-round" : "Round-one") + " monster clearance starts a five-second loot countdown without immediately teleporting");
        yield return new WaitForSeconds(.4f);
        var economy = player.GetComponent<PlayerEconomy>();
        var movement = player.GetComponent<SteveFirstPersonController>();
        Vector3 beforeMove = player.transform.position;
        leftState.primary2DAxis = Vector2.up;
        yield return new WaitForSeconds(.2f);
        leftState.primary2DAxis = Vector2.zero;
        Check(flow.CurrentPhase == GameFlowTimer.Phase.Loot && !movement.IsMovementSuspended &&
            Vector3.Distance(player.transform.position, beforeMove) > .05f,
            "XR locomotion stays available while collecting post-clear coins");
        var coin = FindObjectsOfType<CoinPickup>().FirstOrDefault(c => c.gameObject.activeInHierarchy);
        Check(coin != null, "Monster drops remain in the scene after the monster hierarchy is cleared");
        if (coin != null)
        {
            int coins = economy.Coins;
            movement.TeleportTo(coin.transform.position - Vector3.up * .5f, 0f);
            yield return new WaitForSeconds(.2f);
            Check(economy.Coins > coins,
                "Walking the player collider into a dropped coin during Loot actually adds it to the wallet");
        }
        float remaining = flow.RemainingSeconds;
        yield return new WaitForSeconds(remaining + .15f);
        Check(finalRound ? flow.CurrentPhase == GameFlowTimer.Phase.Completed :
            flow.CurrentPhase == GameFlowTimer.Phase.Preparation && flow.RoundNumber == round + 1,
            finalRound ? "The last round waits for loot collection before completing in the crafting area" :
            "The loot countdown advances exactly once to the next crafting preparation");
    }

    private IEnumerator ContactCombatChecks()
    {
        Stage = "Contact melee";
        var movement = player.GetComponent<SteveFirstPersonController>();
        var attack = player.GetComponent<SteveMeleeAttack>();
        var oldLeft = leftState;
        var oldRight = rightState;
        var oldPosition = player.transform.position;
        float oldYaw = player.HeadCamera.transform.eulerAngles.y;
        var left = player.LeftInventory;
        var right = player.RightInventory;
        string oldLeftItem = left.GetSelectedItemName(), oldRightItem = right.GetSelectedItemName();
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        temporary.Add(floor);
        floor.transform.position = new Vector3(500f, 49.5f, 500f);
        floor.transform.localScale = new Vector3(20f, 1f, 20f);
        movement.TeleportTo(new Vector3(500f, 50f, 500f), 0f);
        leftState.devicePosition = new Vector3(-.3f, -.3f, .5f);
        rightState.devicePosition = new Vector3(.3f, -.3f, .5f);
        leftState.deviceRotation = rightState.deviceRotation = Quaternion.identity;
        left.RemoveItem();
        right.RemoveItem();
        yield return new WaitForSeconds(.2f);
        var leftFist = left.transform.Find("XR Fist Hitbox").GetComponent<BoxCollider>();
        var rightFist = right.transform.Find("XR Fist Hitbox").GetComponent<BoxCollider>();
        Check(leftFist.enabled && rightFist.enabled && leftFist.isTrigger && rightFist.isTrigger,
            "Both empty hands have active trigger hitboxes at the end of the arms");

        var enemy = new GameObject("XR Contact Target", typeof(SphereCollider), typeof(MonsterHealth));
        temporary.Add(enemy);
        var target = enemy.GetComponent<MonsterHealth>();
        typeof(MonsterHealth).GetField("currentHealth", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, 200);
        enemy.GetComponent<SphereCollider>().radius = .035f;
        Vector3 away = new Vector3(505f, 52f, 505f);
        enemy.transform.position = player.RightController.position + player.RightController.forward * 1.5f;
        yield return new WaitForSeconds(.7f);
        int hp = target.CurrentHealth;
        yield return Pulse(false, ControllerButton.TriggerButton);
        yield return Pulse(true, ControllerButton.TriggerButton);
        Check(target.CurrentHealth == hp && !attack.TryAttackFrom(player.RightController),
            "Empty-hand triggers and the legacy ray-attack entry cannot remotely damage a monster in XR");

        enemy.transform.position = leftFist.bounds.center;
        Physics.SyncTransforms();
        yield return new WaitForSeconds(.15f);
        Check(target.CurrentHealth == hp - attack.AttackDamage,
            "A stationary left fist touching a monster damages it without a trigger or minimum swing speed");
        hp = target.CurrentHealth;
        yield return new WaitForSeconds(.8f);
        var extra = enemy.AddComponent<SphereCollider>();
        extra.radius = .03f;
        yield return new WaitForSeconds(.15f);
        Check(target.CurrentHealth == hp,
            "Holding contact and touching a second collider on the same monster do not repeat damage");
        extra.enabled = false;
        enemy.transform.position = away;
        yield return new WaitForSeconds(.7f);
        enemy.transform.position = leftFist.bounds.center;
        Physics.SyncTransforms();
        yield return new WaitForSeconds(.15f);
        Check(target.CurrentHealth == hp - attack.AttackDamage,
            "Separating and contacting again damages the monster after cooldown, including after a collider is disabled");

        enemy.transform.position = away;
        yield return new WaitForSeconds(.7f);
        hp = target.CurrentHealth;
        enemy.transform.position = rightFist.bounds.center;
        Physics.SyncTransforms();
        yield return new WaitForSeconds(.15f);
        Check(target.CurrentHealth == hp - attack.AttackDamage, "The right empty fist uses the same contact damage rules");
        enemy.transform.position = away;
        yield return new WaitForSeconds(.7f);

        left.AddItem("oak_plank");
        right.AddItem("iron_ingot");
        yield return new WaitForSeconds(.15f);
        hp = target.CurrentHealth;
        enemy.transform.position = leftFist.transform.position;
        Physics.SyncTransforms();
        yield return new WaitForSeconds(.15f);
        Check(!leftFist.enabled && !rightFist.enabled && target.CurrentHealth == hp,
            "Selecting a material disables fist damage instead of attacking through a held item");
        enemy.transform.position = away;
        left.RemoveItem(); left.AddItem("wood_sword");
        right.RemoveItem(); right.AddItem("iron_sword");
        yield return new WaitForSeconds(.2f);
        var wood = left.HeldItemTransform.Find("sword_collider").GetComponent<BoxCollider>();
        var iron = right.HeldItemTransform.Find("sword_collider").GetComponent<BoxCollider>();
        Check(wood.enabled && iron.enabled && wood.isTrigger && iron.isTrigger &&
            wood.GetComponent<CraftXRContactDamage>() != null && iron.GetComponent<CraftXRContactDamage>() != null &&
            !leftFist.enabled && !rightFist.enabled,
            "Wood and iron swords reuse their authored blade hitboxes and the same contact component as fists");

        hp = target.CurrentHealth;
        enemy.transform.position = player.RightController.position + player.RightController.forward * 2f;
        yield return Pulse(false, ControllerButton.TriggerButton);
        Check(target.CurrentHealth == hp, "A sword trigger no longer performs a ranged melee attack");
        enemy.transform.position = away;
        yield return new WaitForSeconds(.7f);
        enemy.transform.position = wood.bounds.center;
        Physics.SyncTransforms();
        yield return new WaitForSeconds(.15f);
        Check(target.CurrentHealth == hp - attack.AttackDamage, "A wood sword blade collision deals contact damage");
        hp = target.CurrentHealth;
        enemy.transform.position = away;
        yield return new WaitForSeconds(.7f);
        enemy.transform.position = iron.bounds.center;
        Physics.SyncTransforms();
        yield return new WaitForSeconds(.15f);
        Check(target.CurrentHealth == hp - attack.AttackDamage, "An iron sword blade collision deals contact damage");

        enemy.transform.position = away;
        yield return new WaitForSeconds(.7f);
        movement.SetMovementSuspended(true);
        yield return new WaitForSeconds(.15f);
        hp = target.CurrentHealth;
        enemy.transform.position = iron.transform.TransformPoint(iron.center);
        Physics.SyncTransforms();
        yield return new WaitForSeconds(.15f);
        Check(!iron.enabled && !wood.enabled && target.CurrentHealth == hp,
            "Suspending gameplay disables sword and fist contact damage");
        enemy.transform.position = away;
        movement.SetMovementSuspended(false);
        yield return new WaitForSeconds(.7f);
        int originalDamage = attack.AttackDamage;
        attack.IncreaseAttackDamage(2);
        enemy.transform.position = wood.bounds.center;
        Physics.SyncTransforms();
        yield return new WaitForSeconds(.15f);
        Check(target.CurrentHealth == hp - originalDamage - 2, "Contact damage retains the existing attack upgrade bonus");
        typeof(SteveMeleeAttack).GetField("attackDamage", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(attack, originalDamage);
        enemy.SetActive(false);

        left.RemoveItem(); left.AddItem("m1911");
        right.RemoveItem(); right.AddItem("m1911");
        yield return new WaitForSeconds(.15f);
        Check(!leftFist.enabled && !rightFist.enabled, "Equipping a gun disables the fist hitbox on that hand");

        Stage = "Contact knockback";
        var bindings = (MonsterSpawner.PrefabBinding[])typeof(MonsterSpawner)
            .GetField("monsterPrefabs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(flow.Monsters);
        var points = (Transform[])typeof(MonsterSpawner)
            .GetField("spawnPoints", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(flow.Monsters);
        UnityEngine.AI.NavMeshHit nav;
        bool found = UnityEngine.AI.NavMesh.SamplePosition(points[0].position, out nav, 3f, UnityEngine.AI.NavMesh.AllAreas);
        Check(found, "The knockback test uses the scene's real navigable monster spawn area");
        if (found)
        {
            var real = Instantiate(bindings[0].prefab, nav.position, bindings[0].prefab.transform.rotation);
            temporary.Add(real);
            real.SetActive(true);
            var agent = real.GetComponent<UnityEngine.AI.NavMeshAgent>();
            agent.Warp(nav.position);
            var chase = real.GetComponent<MonsterChaseTarget>();
            chase.enabled = false;
            agent.isStopped = true;
            var realHealth = real.GetComponent<MonsterHealth>();
            movement.TeleportTo(nav.position - Vector3.forward * 1.8f, 0f);
            left.RemoveItem(); left.AddItem("oak_plank");
            right.RemoveItem();
            yield return new WaitForSeconds(.7f);
            int realHp = realHealth.CurrentHealth;
            Vector3 positionBefore = real.transform.position;
            var capsule = real.GetComponent<CapsuleCollider>();
            var offset = player.Origin.CameraFloorOffsetObject.transform;
            Vector3 fistOffset = rightFist.transform.position - player.RightController.position;
            rightState.devicePosition = offset.InverseTransformPoint(capsule.bounds.center - fistOffset);
            yield return new WaitForSeconds(.15f);
            Check(realHealth.CurrentHealth == realHp - attack.AttackDamage && chase.IsKnockedBack,
                "Fist contact reuses MonsterHealth damage feedback and starts the real monster knockback");
            chase.enabled = true;
            yield return new WaitForSeconds(.15f);
            Check(Vector3.Dot(Vector3.ProjectOnPlane(real.transform.position - positionBefore, Vector3.up), Vector3.forward) > .05f,
                "The struck monster actually moves away from the player through the existing knockback controller");
            real.SetActive(false);
        }
        left.RemoveItem();
        right.RemoveItem();
        if (!string.IsNullOrEmpty(oldLeftItem)) left.AddItem(oldLeftItem);
        if (!string.IsNullOrEmpty(oldRightItem)) right.AddItem(oldRightItem);
        leftState = oldLeft;
        rightState = oldRight;
        movement.TeleportTo(oldPosition, oldYaw);
        floor.SetActive(false);
        yield return new WaitForSeconds(.2f);
    }

    private IEnumerator FirearmChecks()
    {
        Stage = "Firearms";
        var movement = player.GetComponent<SteveFirstPersonController>();
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        temporary.Add(floor);
        floor.name = "XR Firearm Test Floor";
        floor.transform.position = new Vector3(300f, 49.5f, 300f);
        floor.transform.localScale = new Vector3(120f, 1f, 120f);
        movement.TeleportTo(new Vector3(300f, 50.1f, 300f), 0f);
        leftState.devicePosition = new Vector3(-.25f, -.3f, .5f);
        rightState.devicePosition = new Vector3(.25f, -.3f, .5f);
        leftState.deviceRotation = rightState.deviceRotation = Quaternion.identity;
        var right = player.RightInventory;
        while (right.SelectedIndex != 2) right.Cycle(1);
        if (right.GetSelectedItemName() != "m1911") { right.RemoveItem(); right.AddItem("m1911"); }
        yield return new WaitForSeconds(.3f);
        Transform muzzle = right.HeldItemTransform.Find("muzzle");
        Check(right.IsFirearmSelected && muzzle != null &&
            Vector3.Angle(muzzle.forward, player.RightController.forward) < 1f,
            "Equipped M1911 muzzle points along the simulated hand aim");
        var enemy = new GameObject("XR Firearm Target", typeof(CapsuleCollider), typeof(MonsterHealth));
        temporary.Add(enemy);
        var target = enemy.GetComponent<MonsterHealth>();
        typeof(MonsterHealth).GetField("currentHealth", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, 200);
        var capsule = enemy.GetComponent<CapsuleCollider>();
        capsule.center = Vector3.up * .9f; capsule.height = 1.8f; capsule.radius = .35f;
        enemy.transform.position = muzzle.position + muzzle.forward * 8f - capsule.center;
        yield return new WaitForSeconds(.1f);
        int before = target.CurrentHealth;
        yield return Pulse(false, ControllerButton.TriggerButton);
        var shooter = right.GetComponent<CraftXRFirearm>();
        Check(shooter != null && target.CurrentHealth == before - 10,
            "Right simulated trigger fires from the muzzle and damages a target beyond melee range");
        int shots = shooter.ShotsFired;
        yield return new WaitForSeconds(.3f);
        rightState = rightState.WithButton(ControllerButton.TriggerButton);
        rightState.trigger = 1f;
        yield return new WaitForSeconds(.65f);
        rightState = rightState.WithButton(ControllerButton.TriggerButton, false);
        rightState.trigger = 0f;
        yield return new WaitForSeconds(.1f);
        Check(shooter.ShotsFired == shots + 1, "Holding the trigger fires exactly one semi-automatic shot");
        yield return new WaitForSeconds(.3f);
        before = target.CurrentHealth;
        bool first = right.TryFireSelectedGun(), second = right.TryFireSelectedGun();
        Check(first && !second && target.CurrentHealth == before - 10,
            "Fire interval rejects repeated shots within the same frame");
        var fx = GameObject.Find("Gun Shot FX RIGHT");
        Check(fx != null && fx.GetComponent<LineRenderer>().enabled &&
            fx.transform.Find("Muzzle Flash").gameObject.activeSelf,
            "A shot displays a muzzle flash and visible tracer");
        Check(right.SelectedItemData.firearmShotSound != null,
            "M1911 has a configured gunshot audio clip");
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        temporary.Add(wall);
        wall.name = "XR Bullet Blocking Wall";
        wall.transform.position = muzzle.position + muzzle.forward * 3f;
        wall.transform.localScale = new Vector3(3f, 3f, .2f);
        yield return new WaitForSeconds(.3f);
        before = target.CurrentHealth;
        yield return Pulse(false, ControllerButton.TriggerButton);
        Check(target.CurrentHealth == before, "A solid wall blocks bullets from reaching a target behind it");
        wall.SetActive(false);
        yield return new WaitForSeconds(.3f);
        enemy.transform.position = muzzle.position + muzzle.forward * 45f - capsule.center;
        yield return new WaitForSeconds(.1f);
        before = target.CurrentHealth;
        yield return Pulse(false, ControllerButton.TriggerButton);
        Check(target.CurrentHealth == before, "Targets beyond the configured range take no shot damage");
        enemy.transform.position = muzzle.position + muzzle.forward * 8f - capsule.center;
        yield return new WaitForSeconds(.3f);
        var attack = player.GetComponent<SteveMeleeAttack>();
        int oldDamage = attack.AttackDamage;
        attack.IncreaseAttackDamage(2);
        before = target.CurrentHealth;
        yield return Pulse(false, ControllerButton.TriggerButton);
        Check(target.CurrentHealth == before - 12, "Existing attack upgrades also increase firearm damage");
        typeof(SteveMeleeAttack).GetField("attackDamage", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(attack, oldDamage);

        shots = shooter.ShotsFired;
        before = target.CurrentHealth;
        yield return Pulse(true, ControllerButton.TriggerButton);
        Check(shooter.ShotsFired == shots && target.CurrentHealth == before,
            "The other hand's trigger cannot fire the right-hand gun");
        var left = player.LeftInventory;
        left.RemoveItem(); left.AddItem("m1911");
        yield return new WaitForSeconds(.2f);
        Transform leftMuzzle = left.HeldItemTransform.Find("muzzle");
        enemy.transform.position = leftMuzzle.position + leftMuzzle.forward * 8f - capsule.center;
        yield return new WaitForSeconds(.1f);
        before = target.CurrentHealth;
        yield return Pulse(true, ControllerButton.TriggerButton);
        Check(left.GetComponent<CraftXRFirearm>() != null && target.CurrentHealth == before - 10,
            "The left simulated trigger fires an independently equipped left-hand gun");

        shots = shooter.ShotsFired;
        right.Cycle(1);
        yield return new WaitForSeconds(.3f);
        yield return Pulse(false, ControllerButton.TriggerButton);
        Check(!right.IsFirearmSelected && shooter.ShotsFired == shots,
            "Switching away from the gun prevents further firearm shots");
        while (right.SelectedIndex != 2) right.Cycle(1);
        yield return new WaitForSeconds(.3f);

        var canvasObject = new GameObject("XR Firearm UI Test", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster));
        temporary.Add(canvasObject);
        canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 200);
        canvasObject.transform.position = player.RightController.position + player.RightController.forward * 1.5f;
        canvasObject.transform.rotation = player.RightController.rotation;
        canvasObject.transform.localScale = Vector3.one * .002f;
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = player.HeadCamera;
        var button = new GameObject("Target Button", typeof(RectTransform), typeof(UnityEngine.UI.Image),
            typeof(UnityEngine.UI.Button));
        button.transform.SetParent(canvasObject.transform, false);
        button.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 200);
        var uiButton = button.GetComponent<UnityEngine.UI.Button>();
        uiButton.targetGraphic = button.GetComponent<UnityEngine.UI.Image>();
        bool clicked = false;
        uiButton.onClick.AddListener(() => clicked = true);
        yield return new WaitForSeconds(.4f);
        shots = shooter.ShotsFired;
        yield return Pulse(false, ControllerButton.TriggerButton);
        Check(clicked && shooter.ShotsFired == shots,
            "Pointing a loaded gun's controller at XR UI clicks the button without shooting");
        canvasObject.SetActive(false);
        movement.SetMovementSuspended(true);
        yield return new WaitForSeconds(.1f);
        shots = shooter.ShotsFired;
        yield return Pulse(false, ControllerButton.TriggerButton);
        Check(shooter.ShotsFired == shots, "Gameplay suspension blocks firearm input");
        movement.SetMovementSuspended(false);
        enemy.SetActive(false);
        movement.TeleportTo(new Vector3(76.5f, 15.15f, 7f), 0f);
        floor.SetActive(false);
    }

    private void Cleanup()
    {
        if (cleaned) return;
        cleaned = true;
        InputSystem.onBeforeUpdate -= Feed;
        if (inputManager != null) inputManager.enabled = false;
        foreach (var device in temporaryDevices) if (device != null && device.added) InputSystem.RemoveDevice(device);
        if (headDevice != null && headDevice.added) InputSystem.RemoveDevice(headDevice);
        if (leftDevice != null && leftDevice.added) InputSystem.RemoveDevice(leftDevice);
        if (rightDevice != null && rightDevice.added) InputSystem.RemoveDevice(rightDevice);
        foreach (var instance in temporary) if (instance != null) Destroy(instance);
        foreach (var device in disabledPhysicalDevices) if (device != null && device.added) InputSystem.EnableDevice(device);
        if (gate != null) gate.Mode = originalMode;
        if (simulator != null && restoreSimulator) simulator.enabled = true;
        if (inputManager != null) inputManager.enabled = true;
    }
    private void OnDisable() => Cleanup();
}

#endif
