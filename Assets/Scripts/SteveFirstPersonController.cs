using UnityEngine;
using UnityEngine.XR;
using UnityEngine.AI;

[RequireComponent(typeof(CharacterController))]

public sealed class SteveFirstPersonController : MonoBehaviour
{
    [SerializeField] private Transform viewCamera;
    [SerializeField, Min(0f)] private float moveSpeed = 3.5f;
    [SerializeField, Min(0f)] private float mouseSensitivity = 2f;
    [SerializeField, Range(1f, 89f)] private float maxLookAngle = 80f;
    [SerializeField, Min(0f)] private float knockbackSpeed = 6f;
    [SerializeField, Min(0f)] private float knockbackLiftSpeed = 3.5f;

    private CharacterController characterController;
    private Animator animator;
    private float verticalSpeed;
    private float pitch;
    private float cameraLocalYaw;
    private Vector3 knockbackVelocity;
    private SteveHealth health;
    private bool deathView;
    private PlayerEconomy economy;
    private PicoPlayerTracking tracking;
    private readonly Vector3 deathCameraOffset = new Vector3(0f, 2.25f, -3.25f);

    public float MoveSpeed => moveSpeed;
    public bool IsMovementSuspended { get; private set; }

    public void SetMovementSuspended(bool suspended)
    {
        IsMovementSuspended = suspended;
        verticalSpeed = 0f;
        knockbackVelocity = Vector3.zero;
        if (animator != null) animator.SetBool("Moving", false);
        if (suspended) SetCursorLocked(false);
    }

    public void TeleportTo(Vector3 position, float yaw)
    {
        if (characterController == null) characterController = GetComponent<CharacterController>();
        bool wasEnabled = characterController.enabled;
        characterController.enabled = false;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        verticalSpeed = 0f;
        knockbackVelocity = Vector3.zero;
        if ((tracking == null || !tracking.IsActive) && viewCamera != null && !deathView)
        {
            pitch = 0f;
            viewCamera.localRotation = Quaternion.Euler(0f, cameraLocalYaw, 0f);
        }
        characterController.enabled = wasEnabled;
        Physics.SyncTransforms();
    }

    public void IncreaseMoveSpeed(float amount)
    {
        if (amount > 0f)
            moveSpeed += amount;
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        health = GetComponent<SteveHealth>();
        economy = GetComponent<PlayerEconomy>();
        tracking = GetComponent<PicoPlayerTracking>();

        if (viewCamera == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
                viewCamera = mainCamera.transform;
        }

        if (viewCamera == null)
        {
            Debug.LogError("Steve needs a view camera.", this);
            enabled = false;
            return;
        }

        pitch = Mathf.DeltaAngle(0f, viewCamera.localEulerAngles.x);
        cameraLocalYaw = viewCamera.localEulerAngles.y;
    }

    private void Update()
    {
        if (IsMovementSuspended) return;
        if ((tracking == null || !tracking.IsActive) && !deathView && (economy == null || !economy.BlocksGameplayInput))
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                SetCursorLocked(false);
            else if (Input.GetMouseButtonDown(0))
                SetCursorLocked(true);

            if (Cursor.lockState == CursorLockMode.Locked)
                LookAround();
        }

        if (health == null || !health.IsDead || knockbackVelocity.sqrMagnitude > 0.01f ||
            !characterController.isGrounded)
            Move();
    }

    public void EnterDeathView()
    {
        deathView = true;
        SetCursorLocked(false);
        if (tracking != null && tracking.IsActive) return;
        if (viewCamera == null)
            return;

        viewCamera.localPosition = deathCameraOffset;
        Vector3 focus = transform.position + Vector3.up * 0.9f;
        viewCamera.rotation = Quaternion.LookRotation(focus - viewCamera.position, Vector3.up);
    }

    public void ApplyKnockback(Vector3 sourcePosition)
    {
        if (IsMovementSuspended) return;
        Vector3 away = Vector3.ProjectOnPlane(transform.position - sourcePosition, Vector3.up);
        if (away.sqrMagnitude < 0.0001f)
            away = -transform.forward;
        knockbackVelocity = away.normalized * knockbackSpeed;
        verticalSpeed = knockbackLiftSpeed;
    }

    private void LookAround()
    {
        // 这里提供了两种输入方式

        float yawChange;
        float pitchChange;
        InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightHand.isValid && rightHand.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
        {
            yawChange = axis.x * mouseSensitivity;
            pitchChange = axis.y * mouseSensitivity;
        }
        else
        {
            yawChange = Input.GetAxis("Mouse X") * mouseSensitivity;
            pitchChange = Input.GetAxis("Mouse Y") * mouseSensitivity;
        }
        transform.Rotate(Vector3.up, yawChange, Space.World);
        pitch = Mathf.Clamp(pitch - pitchChange, -maxLookAngle, maxLookAngle);
        viewCamera.localRotation = Quaternion.Euler(pitch, cameraLocalYaw, 0f);
    }

    private void Move()
    {
        InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        float horizontal;
        float vertical;

        // 这里提供了两种输入方式
        if (leftHand.isValid && leftHand.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
        {
            horizontal = axis.x;
            vertical = axis.y;
        }
        else
        {
            horizontal = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            vertical = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
        }

        Vector3 forward = Vector3.ProjectOnPlane(viewCamera.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(viewCamera.right, Vector3.up).normalized;
        Vector3 movement = Vector3.ClampMagnitude(forward * vertical + right * horizontal, 1f);
        if (knockbackVelocity.sqrMagnitude > 0.01f || (health != null && health.IsDead) ||
            (economy != null && economy.BlocksGameplayInput))
            movement = Vector3.zero;

        if (characterController.isGrounded && verticalSpeed < 0f)
            verticalSpeed = -2f;

        verticalSpeed += Physics.gravity.y * Time.deltaTime;
        Vector3 positionBeforeMove = transform.position;
        bool walking = characterController.isGrounded && movement.sqrMagnitude > 0.01f &&
            knockbackVelocity.sqrMagnitude <= 0.01f;
        Vector3 horizontalStep = LimitToNavMesh((movement * moveSpeed + knockbackVelocity) * Time.deltaTime);
        characterController.Move(horizontalStep + Vector3.up * verticalSpeed * Time.deltaTime);
        PlayerAudio playerAudio = GetComponent<PlayerAudio>();
        if (playerAudio != null)
            playerAudio.RecordMovement(Vector3.ProjectOnPlane(transform.position - positionBeforeMove, Vector3.up).magnitude,
                walking && characterController.isGrounded);
        knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, 12f * Time.deltaTime);
        if (animator != null)
            animator.SetBool("Moving", characterController.isGrounded &&
                new Vector2(characterController.velocity.x, characterController.velocity.z).sqrMagnitude > 0.04f);
    }

    private Vector3 LimitToNavMesh(Vector3 desiredStep)
    {
        if (desiredStep.sqrMagnitude < 0.000001f)
            return Vector3.zero;

        float searchRadius = characterController.isGrounded ? 0.6f : 1.5f;
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit current, searchRadius, NavMesh.AllAreas))
            return Vector3.zero;

        if (CanTravel(current.position, desiredStep))
            return desiredStep;

        Vector3 stepX = new Vector3(desiredStep.x, 0f, 0f);
        if (CanTravel(current.position, stepX))
            return stepX;

        Vector3 stepZ = new Vector3(0f, 0f, desiredStep.z);
        return CanTravel(current.position, stepZ) ? stepZ : Vector3.zero;
    }

    private static bool CanTravel(Vector3 start, Vector3 step)
    {
        if (step.sqrMagnitude < 0.000001f)
            return false;

        Vector3 end = start + step;
        if (!NavMesh.SamplePosition(end, out NavMeshHit nearest, 0.2f, NavMesh.AllAreas))
            return false;

        Vector3 difference = nearest.position - end;
        difference.y = 0f;
        if (difference.sqrMagnitude > 0.0001f)
            return false;

        return !NavMesh.Raycast(start, end, out _, NavMesh.AllAreas);
    }

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void OnDisable()
    {
        SetCursorLocked(false);
    }
}
