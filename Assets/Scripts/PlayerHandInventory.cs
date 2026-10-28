using UnityEngine;

/// <summary>Desktop Q/E poses and wheel selection; tracked hands are owned by PicoPlayerTracking.</summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class PlayerHandInventory : MonoBehaviour
{
    public enum Hand { Left, Right }

    [SerializeField] private HandInventory leftInventory;
    [SerializeField] private HandInventory rightInventory;
    [SerializeField] private Transform leftArm;
    [SerializeField] private Transform rightArm;
    [SerializeField] private Transform viewCamera;
    [SerializeField, Min(0.01f)] private float raiseDuration = 0.2f;
    [SerializeField] private Hand activeHand = Hand.Right;

    private SteveHealth health;
    private float leftWeight;
    private float rightWeight;
    private bool inspectLeft;
    private bool inspectRight;
    private PlayerEconomy economy;
    private PicoPlayerTracking tracking;

    private void Awake()
    {
        health = GetComponent<SteveHealth>();
        economy = GetComponent<PlayerEconomy>();
        tracking = GetComponent<PicoPlayerTracking>();
        if (viewCamera == null && Camera.main != null) viewCamera = Camera.main.transform;
    }

    private void Update()
    {
        if (tracking != null && tracking.IsActive) { inspectLeft = inspectRight = false; return; }
        if (health != null && health.IsDead) { inspectLeft = inspectRight = false; return; }
        if (economy != null && economy.BlocksGameplayInput) { inspectLeft = inspectRight = false; return; }
        if (Input.GetKeyDown(KeyCode.Q)) activeHand = Hand.Left;
        if (Input.GetKeyDown(KeyCode.E)) activeHand = Hand.Right;
        inspectLeft = Input.GetKey(KeyCode.Q);
        inspectRight = Input.GetKey(KeyCode.E);
        float wheel = Input.mouseScrollDelta.y;
        HandInventory inventory = activeHand == Hand.Left ? leftInventory : rightInventory;
        if (inventory != null && Mathf.Abs(wheel) > 0.01f) inventory.Cycle(wheel > 0f ? 1 : -1);
    }

    private void LateUpdate()
    {
        if (tracking != null && tracking.IsActive) return;
        if (health != null && health.IsDead) return;
        leftWeight = Mathf.MoveTowards(leftWeight, inspectLeft ? 1f : 0f, Time.deltaTime / raiseDuration);
        rightWeight = Mathf.MoveTowards(rightWeight, inspectRight ? 1f : 0f, Time.deltaTime / raiseDuration);
        PoseArm(leftArm, leftWeight, -1f);
        PoseArm(rightArm, rightWeight, 1f);
    }

    private void PoseArm(Transform arm, float weight, float side)
    {
        if (arm == null || viewCamera == null || weight <= 0f) return;
        Vector3 target = viewCamera.position + viewCamera.forward * 0.65f +
            viewCamera.right * (side * 0.24f) - viewCamera.up * 0.18f;
        Vector3 direction = target - arm.position;
        if (direction.sqrMagnitude < 0.0001f) return;
        // The model is a rigid cuboid with local -Y pointing from shoulder to wrist.
        Vector3 localDirection = arm.parent.InverseTransformDirection(direction.normalized);
        Quaternion raised = Quaternion.FromToRotation(Vector3.down, localDirection);
        arm.localRotation = Quaternion.Slerp(arm.localRotation, raised, Mathf.SmoothStep(0f, 1f, weight));
    }

}
