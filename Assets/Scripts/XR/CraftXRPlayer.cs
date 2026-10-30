using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>Connects the standard XRI rig to the existing game's player and round lifecycle.</summary>
[DefaultExecutionOrder(-150)]
[DisallowMultipleComponent]
public sealed class CraftXRPlayer : MonoBehaviour
{
    public static CraftXRPlayer Active { get; private set; }
    [SerializeField] private XROrigin origin;
    [SerializeField] private ContinuousMoveProviderBase moveProvider;
    [SerializeField] private ContinuousTurnProviderBase turnProvider;
    [SerializeField] private Transform leftController;
    [SerializeField] private Transform rightController;
    [SerializeField] private HandInventory leftInventory;
    [SerializeField] private HandInventory rightInventory;
    [SerializeField] private Transform leftArm;
    [SerializeField] private Transform rightArm;
    [SerializeField, Min(.5f)] private float fallbackEyeHeight = 1.67f;
    private SteveFirstPersonController movement;
    private SteveHealth health;
    private PlayerEconomy economy;
    private Vector3 previousPosition;
    private float nextOriginCheck;
    private readonly List<XRInputSubsystem> inputSubsystems = new List<XRInputSubsystem>();
    private bool originConfigured;
    private bool configuredSimulation;
    private XROrigin.TrackingOriginMode configuredMode;

    public Camera HeadCamera => origin != null ? origin.Camera : null;
    public XROrigin Origin => origin;
    public Transform LeftController => leftController;
    public Transform RightController => rightController;
    public HandInventory LeftInventory => leftInventory;
    public HandInventory RightInventory => rightInventory;
    public bool CanPlay => health != null && !health.IsDead && movement != null &&
        !movement.IsMovementSuspended && (economy == null || !economy.BlocksGameplayInput);

    private void OnEnable() => Active = this;
    private void Awake()
    {
        Active = this;
        movement = GetComponent<SteveFirstPersonController>();
        health = GetComponent<SteveHealth>();
        economy = GetComponent<PlayerEconomy>();
        previousPosition = transform.position;
        ConfigureTrackingOrigin();
    }

    private void ConfigureTrackingOrigin()
    {
        if (origin == null || origin.CameraFloorOffsetObject == null) return;
        bool simulation = CraftXRSimulatorGate.Active != null
            ? CraftXRSimulatorGate.Active.UsingSimulation : !CraftXRSimulatorGate.HasPhysicalHeadset();
        var desired = simulation ? XROrigin.TrackingOriginMode.Device : XROrigin.TrackingOriginMode.Floor;
        inputSubsystems.Clear();
        SubsystemManager.GetInstances(inputSubsystems);
        if (!simulation)
        {
            foreach (var subsystem in inputSubsystems)
            {
                if (!subsystem.running) continue;
                var supported = subsystem.GetSupportedTrackingOriginModes();
                if (supported != TrackingOriginModeFlags.Unknown &&
                    (supported & TrackingOriginModeFlags.Floor) == 0 &&
                    (supported & TrackingOriginModeFlags.Device) != 0)
                    desired = XROrigin.TrackingOriginMode.Device;
            }
        }
        origin.CameraYOffset = fallbackEyeHeight;
        if (!originConfigured || configuredSimulation != simulation || configuredMode != desired)
        {
            origin.RequestedTrackingOriginMode = desired;
            configuredMode = desired;
            configuredSimulation = simulation;
            originConfigured = true;
        }
        // Apply the height once to the shared parent of the head AND both hands.
        // Floor poses already contain real height. Device/simulator poses need the fallback height.
        float offset = desired == XROrigin.TrackingOriginMode.Floor ? 0f : fallbackEyeHeight;
        var floor = origin.CameraFloorOffsetObject.transform;
        Vector3 position = floor.localPosition;
        if (!Mathf.Approximately(position.y, offset))
        {
            position.y = offset;
            floor.localPosition = position;
        }
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextOriginCheck)
        {
            nextOriginCheck = Time.unscaledTime + .25f;
            ConfigureTrackingOrigin();
        }
        bool canMove = CanPlay;
        if (moveProvider != null)
        {
            moveProvider.moveSpeed = movement.MoveSpeed;
            moveProvider.enabled = canMove;
        }
        if (turnProvider != null) turnProvider.enabled = canMove;
        bool inventoryAvailable = health != null && !health.IsDead &&
            (economy == null || !economy.BlocksGameplayInput);
        if (leftInventory != null) leftInventory.SetInventoryEnabled(inventoryAvailable);
        if (rightInventory != null) rightInventory.SetInventoryEnabled(inventoryAvailable);
    }

    private void LateUpdate()
    {
        if (health != null && !health.IsDead)
        {
            PoseHand(leftArm, leftInventory, leftController);
            PoseHand(rightArm, rightInventory, rightController);
        }
        Vector3 step = Vector3.ProjectOnPlane(transform.position - previousPosition, Vector3.up);
        var audio = GetComponent<PlayerAudio>();
        if (audio != null) audio.RecordMovement(step.magnitude < 1f ? step.magnitude : 0f,
            CanPlay && step.sqrMagnitude > .000001f);
        previousPosition = transform.position;
    }

    private static void PoseHand(Transform arm, HandInventory palm, Transform controller)
    {
        if (arm == null || palm == null || controller == null) return;
        arm.rotation = controller.rotation * Quaternion.Euler(-90f, 0f, 0f);
        arm.position += controller.position - palm.transform.position;
    }

    public void TeleportTo(Vector3 floorPosition, float yaw)
    {
        if (origin == null || origin.Camera == null) return;
        var body = GetComponent<CharacterController>();
        bool wasEnabled = body != null && body.enabled;
        if (body != null) body.enabled = false;
        origin.RotateAroundCameraUsingOriginUp(Mathf.DeltaAngle(origin.Camera.transform.eulerAngles.y, yaw));
        // Move the rig's ground plane, preserving the tracked physical height.
        // This also works before the headset has delivered its first pose.
        Vector3 cameraFloor = origin.Camera.transform.position;
        cameraFloor.y = origin.Origin.transform.position.y;
        origin.Origin.transform.position += floorPosition - cameraFloor;
        if (body != null) body.enabled = wasEnabled;
        previousPosition = transform.position;
        Physics.SyncTransforms();
    }

    private void OnDisable()
    {
        if (Active == this) Active = null;
    }
}
