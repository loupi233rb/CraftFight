using UnityEngine;
using UnityEngine.XR;

/// <summary>Tracks the headset and the two controller palms in a shared, metre-based space.</summary>
[DefaultExecutionOrder(150)]
[DisallowMultipleComponent]
public sealed class PicoPlayerTracking : MonoBehaviour
{
    [SerializeField] private Camera headCamera;
    [SerializeField] private Transform leftArm;
    [SerializeField] private Transform rightArm;
    [SerializeField] private Transform leftPalm;
    [SerializeField] private Transform rightPalm;
    [Tooltip("The Steve arm points along local -Y; the controller points along local +Z.")]
    [SerializeField] private Vector3 leftArmRotationOffset = new Vector3(-90f, 0f, 0f);
    [SerializeField] private Vector3 rightArmRotationOffset = new Vector3(-90f, 0f, 0f);

    private bool originInitialized;
    private Vector3 originOffset;
    private Quaternion originHeading;
    private Vector3 cameraRestPosition;
    private Quaternion cameraRestRotation;
    private Vector3 leftRestPosition;
    private Quaternion leftRestRotation;
    private Vector3 rightRestPosition;
    private Quaternion rightRestRotation;
    private SteveHealth health;

    // This also blocks desktop pose writers while a headset is starting or temporarily untracked.
    public bool IsActive => isActiveAndEnabled && XRSettings.isDeviceActive;
    public bool LeftTracked { get; private set; }
    public bool RightTracked { get; private set; }

    private void Awake()
    {
        health = GetComponent<SteveHealth>();
        if (headCamera == null || leftArm == null || rightArm == null ||
            leftPalm == null || rightPalm == null)
        {
            Debug.LogError("Assign the headset camera, both Steve arms, and both inventory palm anchors.", this);
            enabled = false;
            return;
        }
        cameraRestPosition = headCamera.transform.localPosition;
        cameraRestRotation = headCamera.transform.localRotation;
        leftRestPosition = leftArm.localPosition;
        leftRestRotation = leftArm.localRotation;
        rightRestPosition = rightArm.localPosition;
        rightRestRotation = rightArm.localRotation;
    }

    private void OnEnable() => Application.onBeforeRender += ApplyTracking;
    private void OnDisable()
    {
        Application.onBeforeRender -= ApplyTracking;
        RestoreDesktopPose();
    }

    // After the Animator and the desktop hand controller; before the wrist inventory view.
    private void LateUpdate() => ApplyTracking();

    private void ApplyTracking()
    {
        if (!IsActive)
        {
            RestoreDesktopPose();
            return;
        }

        InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (!head.isValid) head = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
        if (!TryReadPose(head, out Vector3 headPosition, out Quaternion headRotation))
        {
            LeftTracked = RightTracked = false;
            return;
        }

        if (!originInitialized)
            InitializeOrigin(headPosition);

        Quaternion spaceRotation = transform.rotation * originHeading;
        Vector3 spacePosition = transform.position + transform.rotation * originOffset;
        headCamera.transform.SetPositionAndRotation(spacePosition + spaceRotation * headPosition,
            spaceRotation * headRotation);

        // Let the authored death animation own the arms, while head tracking remains continuous.
        if (health != null && health.IsDead)
        {
            LeftTracked = RightTracked = false;
            return;
        }
        LeftTracked = TrackHand(XRNode.LeftHand, leftArm, leftPalm, leftArmRotationOffset,
            spacePosition, spaceRotation);
        RightTracked = TrackHand(XRNode.RightHand, rightArm, rightPalm, rightArmRotationOffset,
            spacePosition, spaceRotation);
    }

    private void InitializeOrigin(Vector3 headPosition)
    {
        Transform parent = headCamera.transform.parent;
        Vector3 spawnPosition = parent != null ? parent.TransformPoint(cameraRestPosition) : cameraRestPosition;
        Quaternion spawnRotation = parent != null ? parent.rotation * cameraRestRotation : cameraRestRotation;
        Vector3 forward = Vector3.ProjectOnPlane(spawnRotation * Vector3.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;
        Quaternion heading = Quaternion.LookRotation(forward.normalized, Vector3.up);
        originHeading = Quaternion.Inverse(transform.rotation) * heading;
        // Preserve the spawn point and eye height; no assumed player height or model scale.
        originOffset = Quaternion.Inverse(transform.rotation) *
            (spawnPosition - transform.position - heading * headPosition);
        XRDevice.DisableAutoXRCameraTracking(headCamera, true);
        originInitialized = true;
        Debug.Log("PICO tracking active: headset, left controller, right controller.", this);
    }

    private static bool TryReadPose(InputDevice device, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        if (!device.isValid) return false;
        if (device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && !tracked)
            return false;
        return device.TryGetFeatureValue(CommonUsages.devicePosition, out position) &&
            device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation);
    }

    private static bool TrackHand(XRNode node, Transform arm, Transform palm, Vector3 rotationOffset,
        Vector3 spacePosition, Quaternion spaceRotation)
    {
        if (!TryReadPose(InputDevices.GetDeviceAtXRNode(node), out Vector3 position, out Quaternion rotation))
            return false;
        ApplyHandPose(arm, palm, spacePosition + spaceRotation * position,
            spaceRotation * rotation * Quaternion.Euler(rotationOffset));
        return true;
    }

    internal static void ApplyHandPose(Transform arm, Transform palm, Vector3 position, Quaternion rotation)
    {
        arm.rotation = rotation;
        // Move the palm, rather than the shoulder, onto the controller. World-space subtraction
        // also works with the imported model's scaled and rotated parent transforms.
        arm.position += position - palm.position;
    }

    private void RestoreDesktopPose()
    {
        LeftTracked = RightTracked = false;
        if (!originInitialized) return;
        originInitialized = false;
        if (headCamera != null)
        {
            XRDevice.DisableAutoXRCameraTracking(headCamera, false);
            headCamera.transform.localPosition = cameraRestPosition;
            headCamera.transform.localRotation = cameraRestRotation;
        }
        if (leftArm != null)
        {
            leftArm.localPosition = leftRestPosition;
            leftArm.localRotation = leftRestRotation;
        }
        if (rightArm != null)
        {
            rightArm.localPosition = rightRestPosition;
            rightArm.localRotation = rightRestRotation;
        }
    }
}
